namespace DressCoder.Infrastructure.IoStore;

/// <summary>One chunk to lay out into a rebuilt container. Mirrors the chunk dicts in writer.py's build_container.</summary>
public sealed class IoStoreChunkToWrite
{
    /// <summary>12 raw bytes — the chunk ID, copied verbatim from the source container.</summary>
    public required byte[] Id { get; init; }

    /// <summary>Blocks ready to write: (bytes to write, uncompressed size, compression method index).</summary>
    public required IReadOnlyList<(byte[] Bytes, int UncompressedSize, byte Method)> Blocks { get; init; }

    /// <summary>Total uncompressed length of the chunk (sum of all blocks' uncompressed sizes).</summary>
    public required long Size { get; init; }
}

/// <summary>
/// Rebuilds the .ucas data file and the middle sections of a .utoc. Ported from
/// FFVII-Rebirth-Mesh-Patcher's lib/writer.py (MIT license, see docs/04-licencias-terceros.md)
/// — verified there to reproduce an untouched container byte-for-byte (51 chunks, all identical)
/// when fed the source container's own unmodified chunks.
///
/// THE TWO ADDRESS SPACES (kept from the source comment):
///   VIRTUAL:  what goes in the .utoc offset field. Each chunk starts on a fresh 64KB
///             boundary, so offsets step by BlockSize no matter how tiny the chunk.
///   PHYSICAL: where bytes actually sit in the .ucas. Blocks are packed back to back,
///             each starting at a 16-byte aligned position.
/// Mixing these up produces a container that looks valid and reads as garbage.
/// </summary>
public static class IoStoreContainerWriter
{
    public const int DefaultBlockSize = 65536;

    public sealed record BuiltContainer(
        byte[] ChunkIdsSection,
        byte[] OffsetLengthSection,
        byte[] CompressionBlockSection,
        byte[] CompressionMethodNamesSection,
        byte[] Ucas,
        IReadOnlyList<(long Offset, long Length)> OffsetLengths,
        IReadOnlyList<(long Position, int CompressedSize, int UncompressedSize, byte Method)> BlockTable);

    /// <summary>
    /// Lays out chunks into a .ucas and builds the matching .utoc middle sections.
    /// Unchanged chunks should reuse their ORIGINAL compressed blocks; rewritten chunks
    /// should already be Oodle-compressed (or stored raw as a fallback) by the caller.
    /// This method does no compression itself — it only lays bytes out.
    /// </summary>
    public static BuiltContainer BuildContainer(
        IReadOnlyList<string> compressionMethodNames,
        int compressionMethodNameLength,
        IReadOnlyList<IoStoreChunkToWrite> chunks,
        int blockSize = DefaultBlockSize)
    {
        var ucas = new List<byte>();
        var blockTable = new List<(long Position, int CompressedSize, int UncompressedSize, byte Method)>();
        var offlen = new List<(long Offset, long Length)>();
        var blockIndex = 0;

        foreach (var chunk in chunks)
        {
            // Each chunk begins on a fresh block, so its virtual offset is its starting
            // block number times the block size.
            offlen.Add(((long)blockIndex * blockSize, chunk.Size));

            foreach (var (bytes, uncompressedSize, method) in chunk.Blocks)
            {
                // Pad to a 16-byte boundary BEFORE recording the position.
                while (ucas.Count % 16 != 0) ucas.Add(0);
                blockTable.Add((ucas.Count, bytes.Length, uncompressedSize, method));
                ucas.AddRange(bytes);
                blockIndex++;
            }
        }

        // Trailing padding: the whole .ucas file is aligned to a 16-byte boundary, even past
        // the last block's own data (observed empirically — not every source file's last block
        // happens to already land on one).
        while (ucas.Count % 16 != 0) ucas.Add(0);

        using var chunkIdsStream = new MemoryStream();
        foreach (var chunk in chunks) chunkIdsStream.Write(chunk.Id);

        using var offlenStream = new MemoryStream();
        foreach (var (offset, length) in offlen)
        {
            offlenStream.Write(ToBigEndian(offset, 5));
            offlenStream.Write(ToBigEndian(length, 5));
        }

        using var blocksStream = new MemoryStream();
        foreach (var (position, compressedSize, uncompressedSize, method) in blockTable)
        {
            var entry = new byte[12];
            ToLittleEndian(position, 5).CopyTo(entry, 0);
            ToLittleEndian(compressedSize, 3).CopyTo(entry, 5);
            ToLittleEndian(uncompressedSize, 3).CopyTo(entry, 8);
            entry[11] = method;
            blocksStream.Write(entry);
        }

        using var methodNamesStream = new MemoryStream();
        // Index 0 is implicitly "None" and is not stored; start from methods[1].
        for (var i = 1; i < compressionMethodNames.Count; i++)
        {
            var name = System.Text.Encoding.ASCII.GetBytes(compressionMethodNames[i]);
            var padded = new byte[compressionMethodNameLength];
            name.CopyTo(padded, 0);
            methodNamesStream.Write(padded);
        }

        return new BuiltContainer(
            chunkIdsStream.ToArray(),
            offlenStream.ToArray(),
            blocksStream.ToArray(),
            methodNamesStream.ToArray(),
            ucas.ToArray(),
            offlen,
            blockTable);
    }

    private static byte[] ToBigEndian(long value, int byteCount)
    {
        var result = new byte[byteCount];
        for (var i = byteCount - 1; i >= 0; i--)
        {
            result[i] = (byte)(value & 0xFF);
            value >>= 8;
        }
        return result;
    }

    private static byte[] ToLittleEndian(long value, int byteCount)
    {
        var result = new byte[byteCount];
        for (var i = 0; i < byteCount; i++)
        {
            result[i] = (byte)(value & 0xFF);
            value >>= 8;
        }
        return result;
    }

    /// <summary>
    /// Builds the 144-byte .utoc header. Most fields are copied from the source container —
    /// we only ever change how many chunks/blocks there are, never the container's identity
    /// or its compression settings.
    /// </summary>
    public static byte[] BuildTocHeader(
        IoStoreToc source, int chunkCount, int blockCount, int directoryIndexSize, int blockSize = DefaultBlockSize)
    {
        var h = new byte[144];
        var magic = System.Text.Encoding.ASCII.GetBytes("-==--==--==--==-");
        magic.CopyTo(h, 0);
        h[16] = source.Version;

        void WriteInt32(int offset, int value) => BitConverter.GetBytes(value).CopyTo(h, offset);

        WriteInt32(0x14, 144);
        WriteInt32(0x18, chunkCount);
        WriteInt32(0x1C, blockCount);
        WriteInt32(0x20, 12);
        WriteInt32(0x24, source.CompressionMethodCount);
        WriteInt32(0x28, source.CompressionMethodNameLength);
        WriteInt32(0x2C, blockSize);
        WriteInt32(0x30, directoryIndexSize);

        BitConverter.GetBytes(source.ContainerId).CopyTo(h, 0x38);
        h[0x50] = source.Flags;

        return h;
    }

    /// <summary>
    /// Builds the chunk checksum table, reusing the source container's rows for unchanged
    /// chunks and recomputing SHA-1 only for <paramref name="modified"/> (index -> new payload).
    /// Each row is 33 bytes: SHA-1 (20 bytes) + 12 zero bytes + a flags byte.
    /// Valid only when chunk order/count is unchanged from the source.
    /// </summary>
    public static byte[] BuildMetasFrom(IoStoreToc source, int chunkCount, IReadOnlyDictionary<int, byte[]> modified)
    {
        var out_ = new byte[chunkCount * 33];
        // Copy the source's raw meta bytes for the shared prefix (chunk indices are stable);
        // chunkCount may exceed the source's own chunk count when new chunks were appended —
        // those extra rows are filled in below via `modified`.
        var sourceChunkCount = Math.Min(chunkCount, source.ChunkIds.Count);
        var sourceMetaBytes = ReadSourceMetas(source, sourceChunkCount);
        sourceMetaBytes.CopyTo(out_, 0);

        foreach (var (index, payload) in modified)
        {
            using var sha1 = System.Security.Cryptography.SHA1.Create();
            var hash = sha1.ComputeHash(payload);
            var rowOffset = index * 33;
            hash.CopyTo(out_, rowOffset);
            // bytes [20..32) are already zero from the `new byte[]` allocation.
            out_[rowOffset + 32] = 1;
        }

        return out_;
    }

    private static byte[] ReadSourceMetas(IoStoreToc source, int chunkCount)
    {
        var bytes = new byte[chunkCount * 33];
        for (var i = 0; i < chunkCount; i++)
        {
            source.MetaRow(i).CopyTo(bytes, i * 33);
        }
        return bytes;
    }

    /// <summary>Assembles the final .utoc bytes from all sections, in on-disk order.</summary>
    public static byte[] AssembleUtoc(
        byte[] header,
        byte[] chunkIdsSection,
        byte[] offsetLengthSection,
        byte[] compressionBlockSection,
        byte[] compressionMethodNamesSection,
        byte[] directoryIndexSection,
        byte[] metasSection)
    {
        using var ms = new MemoryStream();
        ms.Write(header);
        ms.Write(chunkIdsSection);
        ms.Write(offsetLengthSection);
        ms.Write(compressionBlockSection);
        ms.Write(compressionMethodNamesSection);
        ms.Write(directoryIndexSection);
        ms.Write(metasSection);
        return ms.ToArray();
    }
}
