using System.Text;

namespace DressCoder.Infrastructure.IoStore;

/// <summary>One directory-index file entry: {chunk_index: internal path}. Built by <see cref="IoStoreToc"/>.</summary>
public sealed record IoStoreChunkInfo(int Index, long VirtualOffset, long UncompressedLength);

/// <summary>
/// A parsed IoStore container (.utoc + .ucas), opened read-only. Ported from
/// FFVII-Rebirth-Mesh-Patcher's lib/iostore.py `Toc` class (MIT license, see
/// docs/04-licencias-terceros.md) — verified there to reproduce an untouched
/// container byte-for-byte when round-tripped through the matching writer.
///
/// THE ONE BIG GOTCHA (kept from the source comment): a chunk's stored "offset"
/// is NOT a position in the .ucas file — it's a position in an imaginary
/// uncompressed layout. Converting it to a real file position requires walking
/// the compression block table (block = offset / block_size).
/// </summary>
public sealed class IoStoreToc : IDisposable
{
    private const uint Invalid = 0xFFFFFFFF;
    private static readonly byte[] MagicBytes = Encoding.ASCII.GetBytes("-==--==--==--==-");

    private readonly byte[] _data;
    private readonly FileStream _ucas;

    // Header fields (see build_toc_header in writer.py for the mirror image).
    public byte Version { get; }
    public int HeaderSize { get; }
    public int ChunkCount { get; }
    public int CompressionBlockCount { get; }
    public int CompressionBlockEntrySize { get; }
    public int CompressionMethodCount { get; }
    public int CompressionMethodNameLength { get; }
    public int BlockSize { get; }
    public int DirectoryIndexSize { get; }
    public ulong ContainerId { get; }

    /// <summary>Flags byte: 1=Compressed, 2=Encrypted, 4=Signed, 8=Indexed.</summary>
    public byte Flags { get; }

    /// <summary>12-byte raw chunk ID per chunk; last byte is the chunk type (2=package, 10=container header).</summary>
    public IReadOnlyList<byte[]> ChunkIds { get; }

    /// <summary>(virtual offset, uncompressed length) per chunk.</summary>
    public IReadOnlyList<(long Offset, long Length)> OffsetLengths { get; }

    /// <summary>(physical .ucas position, compressed size, uncompressed size, method index) per block.</summary>
    public IReadOnlyList<(long Position, int CompressedSize, int UncompressedSize, byte Method)> Blocks { get; }

    /// <summary>Compression method names; index 0 is always the implicit "None".</summary>
    public IReadOnlyList<string> Methods { get; }

    /// <summary>Raw directory-index bytes, kept for byte-identical passthrough when nothing changes.</summary>
    public byte[] DirectoryIndexRaw { get; }

    public int MetaOffset { get; }

    /// <summary>Mount point path prefix parsed out of the directory index, e.g. "../../../End/Mods/Foo/Content/".</summary>
    public string MountPoint { get; private set; } = string.Empty;

    /// <summary>{chunk index: internal path}, parsed by walking the directory-index folder tree.</summary>
    public IReadOnlyDictionary<int, string> Paths { get; private set; } = new Dictionary<int, string>();

    public IoStoreToc(string utocPath)
    {
        _data = File.ReadAllBytes(utocPath);

        if (!_data.AsSpan(0, 16).SequenceEqual(MagicBytes))
        {
            throw new InvalidDataException("No es un archivo IoStore .utoc válido (magic bytes incorrectos).");
        }

        Version = _data[16];

        HeaderSize = BitConverter.ToInt32(_data, 0x14);
        ChunkCount = BitConverter.ToInt32(_data, 0x18);
        CompressionBlockCount = BitConverter.ToInt32(_data, 0x1C);
        CompressionBlockEntrySize = BitConverter.ToInt32(_data, 0x20);
        CompressionMethodCount = BitConverter.ToInt32(_data, 0x24);
        CompressionMethodNameLength = BitConverter.ToInt32(_data, 0x28);
        BlockSize = BitConverter.ToInt32(_data, 0x2C);
        DirectoryIndexSize = BitConverter.ToInt32(_data, 0x30);

        ContainerId = BitConverter.ToUInt64(_data, 0x38);
        Flags = _data[0x50];

        var o = HeaderSize;

        var chunkIds = new List<byte[]>(ChunkCount);
        for (var i = 0; i < ChunkCount; i++)
        {
            chunkIds.Add(_data.AsSpan(o + i * 12, 12).ToArray());
        }
        ChunkIds = chunkIds;
        o += ChunkCount * 12;

        // Offsets and lengths: 10 bytes each, BIG-endian — the format's one inconsistency.
        var offlen = new List<(long, long)>(ChunkCount);
        for (var i = 0; i < ChunkCount; i++)
        {
            var raw = _data.AsSpan(o + i * 10, 10);
            var offset = ReadBigEndian(raw[..5]);
            var length = ReadBigEndian(raw.Slice(5, 5));
            offlen.Add((offset, length));
        }
        OffsetLengths = offlen;
        o += ChunkCount * 10;

        // Compression blocks: 12 bytes each, bit-packed little-endian.
        var blocks = new List<(long, int, int, byte)>(CompressionBlockCount);
        for (var i = 0; i < CompressionBlockCount; i++)
        {
            var raw = _data.AsSpan(o + i * 12, 12);
            var position = ReadLittleEndian(raw[..5]);
            var compressedSize = (int)ReadLittleEndian(raw.Slice(5, 3));
            var uncompressedSize = (int)ReadLittleEndian(raw.Slice(8, 3));
            var method = raw[11];
            blocks.Add((position, compressedSize, uncompressedSize, method));
        }
        Blocks = blocks;
        o += CompressionBlockCount * 12;

        var methods = new List<string> { "None" };
        for (var i = 0; i < CompressionMethodCount; i++)
        {
            var slice = _data.AsSpan(o, CompressionMethodNameLength);
            var nullIndex = slice.IndexOf((byte)0);
            var name = Encoding.ASCII.GetString(nullIndex >= 0 ? slice[..nullIndex] : slice);
            methods.Add(name);
            o += CompressionMethodNameLength;
        }
        Methods = methods;

        DirectoryIndexRaw = _data.AsSpan(o, DirectoryIndexSize).ToArray();
        o += DirectoryIndexSize;

        MetaOffset = o;

        _ucas = new FileStream(
            Path.ChangeExtension(utocPath, ".ucas"), FileMode.Open, FileAccess.Read, FileShare.Read);

        if (DirectoryIndexSize > 0)
        {
            ParseDirectoryIndex();
        }
    }

    private static long ReadBigEndian(ReadOnlySpan<byte> bytes)
    {
        long value = 0;
        foreach (var b in bytes) value = (value << 8) | b;
        return value;
    }

    private static long ReadLittleEndian(ReadOnlySpan<byte> bytes)
    {
        long value = 0;
        for (var i = bytes.Length - 1; i >= 0; i--) value = (value << 8) | bytes[i];
        return value;
    }

    /// <summary>
    /// Rebuilds {chunk_index: path} by walking the stored folder tree: linked lists
    /// (first_child/next_sibling/first_file), not nested arrays. 0xFFFFFFFF terminates a list.
    /// </summary>
    private void ParseDirectoryIndex()
    {
        var b = DirectoryIndexRaw;
        var o = 0;

        string ReadString()
        {
            var n = BitConverter.ToInt32(b, o);
            o += 4;
            if (n == 0) return string.Empty;
            if (n < 0)
            {
                var byteLen = -n * 2;
                var s = Encoding.Unicode.GetString(b, o, byteLen).TrimEnd('\0');
                o += byteLen;
                return s;
            }
            else
            {
                var s = Encoding.UTF8.GetString(b, o, n).TrimEnd('\0');
                o += n;
                return s;
            }
        }

        MountPoint = ReadString();

        var nDirs = BitConverter.ToInt32(b, o); o += 4;
        var dirs = new (uint NameId, uint FirstChild, uint NextSibling, uint FirstFile)[nDirs];
        for (var i = 0; i < nDirs; i++)
        {
            dirs[i] = (
                BitConverter.ToUInt32(b, o + i * 16),
                BitConverter.ToUInt32(b, o + i * 16 + 4),
                BitConverter.ToUInt32(b, o + i * 16 + 8),
                BitConverter.ToUInt32(b, o + i * 16 + 12));
        }
        o += nDirs * 16;

        var nFiles = BitConverter.ToInt32(b, o); o += 4;
        var files = new (uint NameId, uint NextFile, uint ChunkIndex)[nFiles];
        for (var i = 0; i < nFiles; i++)
        {
            files[i] = (
                BitConverter.ToUInt32(b, o + i * 12),
                BitConverter.ToUInt32(b, o + i * 12 + 4),
                BitConverter.ToUInt32(b, o + i * 12 + 8));
        }
        o += nFiles * 12;

        var nStrings = BitConverter.ToInt32(b, o); o += 4;
        var strings = new string[nStrings];
        for (var i = 0; i < nStrings; i++)
        {
            strings[i] = ReadString();
        }

        var result = new Dictionary<int, string>();

        void Walk(uint dirIndex, string prefix)
        {
            while (dirIndex != Invalid)
            {
                var (nameId, firstChild, nextSibling, firstFile) = dirs[dirIndex];
                var name = nameId != Invalid ? strings[nameId] : string.Empty;
                var here = prefix + (name.Length > 0 ? "/" + name : string.Empty);

                var f = firstFile;
                while (f != Invalid)
                {
                    var (fnameId, nextFile, chunkIndex) = files[f];
                    result[(int)chunkIndex] = (here + "/" + strings[fnameId]).TrimStart('/');
                    f = nextFile;
                }

                Walk(firstChild, here);
                dirIndex = nextSibling;
            }
        }

        Walk(0, string.Empty);
        Paths = result;
    }

    /// <summary>Returns the fully decompressed bytes of chunk <paramref name="index"/>.</summary>
    public byte[] Read(int index)
    {
        var (offset, length) = OffsetLengths[index];
        var block = (int)(offset / BlockSize);
        var output = new byte[length];
        var written = 0;
        var remaining = length;

        while (remaining > 0)
        {
            var (position, compressedSize, uncompressedSize, method) = Blocks[block];
            _ucas.Seek(position, SeekOrigin.Begin);
            var raw = new byte[compressedSize];
            var read = 0;
            while (read < compressedSize)
            {
                var n = _ucas.Read(raw, read, compressedSize - read);
                if (n == 0) throw new EndOfStreamException("Fin de .ucas inesperado leyendo un bloque.");
                read += n;
            }

            byte[] decoded;
            if (method == 0)
            {
                decoded = raw;
            }
            else
            {
                var name = method < Methods.Count ? Methods[method] : $"#{method}";
                decoded = name.Equals("Oodle", StringComparison.OrdinalIgnoreCase)
                    ? throw new InvalidOperationException(
                        "Este chunk usa compresión Oodle; llamar a ReadWithOodle en su lugar.")
                    : throw new NotSupportedException($"Este contenedor usa compresión '{name}', no soportada.");
            }

            Array.Copy(decoded, 0, output, written, uncompressedSize);
            written += uncompressedSize;
            remaining -= uncompressedSize;
            block++;
        }

        return output;
    }

    /// <summary>Like <see cref="Read"/>, but decompresses Oodle-compressed blocks via the given codec.</summary>
    public byte[] Read(int index, OodleCompression oodle)
    {
        var (offset, length) = OffsetLengths[index];
        var block = (int)(offset / BlockSize);
        var output = new byte[length];
        var written = 0;
        var remaining = length;

        while (remaining > 0)
        {
            var (position, compressedSize, uncompressedSize, method) = Blocks[block];
            _ucas.Seek(position, SeekOrigin.Begin);
            var raw = new byte[compressedSize];
            var read = 0;
            while (read < compressedSize)
            {
                var n = _ucas.Read(raw, read, compressedSize - read);
                if (n == 0) throw new EndOfStreamException("Fin de .ucas inesperado leyendo un bloque.");
                read += n;
            }

            byte[] decoded;
            if (method == 0)
            {
                decoded = raw;
            }
            else
            {
                var name = method < Methods.Count ? Methods[method] : $"#{method}";
                if (!name.Equals("Oodle", StringComparison.OrdinalIgnoreCase))
                {
                    throw new NotSupportedException($"Este contenedor usa compresión '{name}', no soportada.");
                }
                decoded = oodle.Decompress(raw, uncompressedSize);
            }

            Array.Copy(decoded, 0, output, written, uncompressedSize);
            written += uncompressedSize;
            remaining -= uncompressedSize;
            block++;
        }

        return output;
    }

    /// <summary>
    /// Returns this chunk's blocks exactly as stored on disk — still compressed, untouched.
    /// Used to reuse unchanged chunks verbatim when rebuilding a container (no decompress/recompress
    /// round trip, zero risk of introducing differences).
    /// </summary>
    public List<(byte[] Bytes, int UncompressedSize, byte Method)> ReadRawBlocks(int index)
    {
        var (offset, length) = OffsetLengths[index];
        var block = (int)(offset / BlockSize);
        var result = new List<(byte[], int, byte)>();
        long remaining = length;

        while (remaining > 0)
        {
            var (position, compressedSize, uncompressedSize, method) = Blocks[block];
            _ucas.Seek(position, SeekOrigin.Begin);
            var raw = new byte[compressedSize];
            var read = 0;
            while (read < compressedSize)
            {
                var n = _ucas.Read(raw, read, compressedSize - read);
                if (n == 0) throw new EndOfStreamException("Fin de .ucas inesperado leyendo un bloque.");
                read += n;
            }

            result.Add((raw, uncompressedSize, method));
            remaining -= uncompressedSize;
            block++;
        }

        return result;
    }

    /// <summary>The stored 32-byte checksum (SHA-1, zero-padded to 33 bytes) for a chunk.</summary>
    public byte[] MetaHash(int index) => _data.AsSpan(MetaOffset + index * 33, 32).ToArray();

    /// <summary>The full 33-byte meta row (SHA-1 + 12 zero bytes + flags byte) for a chunk, verbatim.</summary>
    public byte[] MetaRow(int index) => _data.AsSpan(MetaOffset + index * 33, 33).ToArray();

    /// <summary>2 = a package (.uasset), 10 = the container header.</summary>
    public byte ChunkType(int index) => ChunkIds[index][11];

    /// <summary>For package chunks, the first 8 bytes of the chunk ID are its Package ID.</summary>
    public ulong PackageId(int index) => BitConverter.ToUInt64(ChunkIds[index], 0);

    public void Dispose() => _ucas.Dispose();
}
