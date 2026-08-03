namespace DressCoder.Infrastructure.IoStore;

/// <summary>New chunk to add to an existing container, alongside all its original (untouched) chunks.</summary>
public sealed class NewContainerChunk
{
    /// <summary>12-byte raw chunk ID. Must not collide with any existing chunk ID in the target container.</summary>
    public required byte[] Id { get; init; }

    public required byte[] Payload { get; init; }

    /// <summary>Path to register in the directory index, e.g. "MetaData/DA_ModMetaData.uasset".</summary>
    public required string Path { get; init; }
}

/// <summary>
/// Appends new chunks to a copy of an existing IoStore container, reusing every original chunk's
/// raw (still-compressed) bytes verbatim — zero risk to the ~99% of the container that doesn't
/// change. Built on top of <see cref="IoStoreContainerWriter"/>, which was validated to reproduce
/// an untouched container byte-for-byte (see tools/roundtrip-test-v2).
///
/// NOTE: intentionally does NOT touch the container's `ContainerHeader` chunk (type 10) — even
/// retoc's own parser cannot read this game's ContainerHeader format (custom engine version), so
/// registering a brand-new package there is unsolved. This class is the vehicle for the empirical
/// experiment: does FF7RML discover a new `MetaData/DA_ModMetaData.uasset` via the directory
/// index/AssetRegistry alone, without a ContainerHeader entry? See docs/03 section 9.4.
/// </summary>
public static class ContainerChunkInjector
{
    public static void InjectAndWrite(
        IoStoreToc source,
        string outputUtocPath,
        IReadOnlyList<NewContainerChunk> newChunks,
        OodleCompression? oodle,
        int blockSize = IoStoreContainerWriter.DefaultBlockSize)
    {
        var chunksToWrite = new List<IoStoreChunkToWrite>();
        var files = new List<(string Path, int ChunkIndex)>();

        for (var i = 0; i < source.ChunkIds.Count; i++)
        {
            var (_, length) = source.OffsetLengths[i];
            chunksToWrite.Add(new IoStoreChunkToWrite
            {
                Id = source.ChunkIds[i],
                Blocks = source.ReadRawBlocks(i),
                Size = length,
            });
            if (source.Paths.TryGetValue(i, out var path))
            {
                files.Add((path, i));
            }
        }

        foreach (var newChunk in newChunks)
        {
            var newIndex = chunksToWrite.Count;
            var oodleMethodIndex = (byte)Math.Max(1, source.Methods.ToList().IndexOf("Oodle"));
            var blocks = SplitAndCompress(newChunk.Payload, blockSize, oodle, oodleMethodIndex);
            chunksToWrite.Add(new IoStoreChunkToWrite
            {
                Id = newChunk.Id,
                Blocks = blocks,
                Size = newChunk.Payload.Length,
            });
            files.Add((newChunk.Path, newIndex));
        }

        var dirIndexBytes = DirectoryIndexBuilder.Build(source.MountPoint, files);
        var built = IoStoreContainerWriter.BuildContainer(source.Methods, source.CompressionMethodNameLength, chunksToWrite);
        var header = IoStoreContainerWriter.BuildTocHeader(source, chunksToWrite.Count, built.BlockTable.Count, dirIndexBytes.Length, blockSize);

        // New chunks' meta rows need real SHA-1 hashes; existing ones are copied verbatim from source.
        var modifiedMetas = new Dictionary<int, byte[]>();
        for (var i = 0; i < newChunks.Count; i++)
        {
            modifiedMetas[chunksToWrite.Count - newChunks.Count + i] = newChunks[i].Payload;
        }
        var metas = IoStoreContainerWriter.BuildMetasFrom(source, chunksToWrite.Count, modifiedMetas);

        var utocBytes = IoStoreContainerWriter.AssembleUtoc(
            header, built.ChunkIdsSection, built.OffsetLengthSection, built.CompressionBlockSection,
            built.CompressionMethodNamesSection, dirIndexBytes, metas);

        var outputDir = Path.GetDirectoryName(outputUtocPath);
        if (!string.IsNullOrEmpty(outputDir)) Directory.CreateDirectory(outputDir);

        File.WriteAllBytes(outputUtocPath, utocBytes);
        File.WriteAllBytes(Path.ChangeExtension(outputUtocPath, ".ucas"), built.Ucas);
    }

    private static List<(byte[] Bytes, int UncompressedSize, byte Method)> SplitAndCompress(
        byte[] payload, int blockSize, OodleCompression? oodle, byte oodleMethodIndex)
    {
        var result = new List<(byte[], int, byte)>();
        for (var offset = 0; offset < payload.Length; offset += blockSize)
        {
            var length = Math.Min(blockSize, payload.Length - offset);
            var block = payload.AsSpan(offset, length).ToArray();

            var compressed = oodle?.TryCompressVerified(block);
            result.Add(compressed is not null
                ? (compressed, block.Length, oodleMethodIndex)
                : (block, block.Length, (byte)0));
        }
        return result;
    }
}
