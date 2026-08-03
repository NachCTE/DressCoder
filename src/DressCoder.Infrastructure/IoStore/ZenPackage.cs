using System.Text;

namespace DressCoder.Infrastructure.IoStore;

/// <summary>One export table entry (72 bytes on disk). See <see cref="ZenPackage"/>.</summary>
public sealed class ZenExport
{
    public int Index { get; init; }
    public string Name { get; init; } = string.Empty;

    /// <summary>Offset in the ORIGINAL pre-container file — NOT usable to locate data within these bytes.</summary>
    public long SerialOffset { get; init; }

    public long SerialSize { get; init; }
    public ulong Outer { get; init; }
    public ulong Cls { get; init; }
    public ulong Super { get; init; }
    public ulong Template { get; init; }
    public ulong GlobalImport { get; init; }
    public uint ObjectFlags { get; init; }
    public byte Filter { get; init; }
}

/// <summary>
/// Parses a single Unreal package (.uasset) in Zen container format. Ported from
/// FFVII-Rebirth-Mesh-Patcher's lib/zen.py (MIT license, see docs/04-licencias-terceros.md).
///
/// A package has three parts that matter to us: the NAME TABLE (every string used,
/// referenced by index elsewhere), IMPORTS (things this package uses from other
/// packages), and EXPORTS (things this package defines — 72 bytes each).
/// </summary>
public sealed class ZenPackage
{
    public ulong PackageName { get; }
    public ulong SourceName { get; }
    public uint PackageFlags { get; }
    public uint CookedHeaderSize { get; }

    private readonly int _nameMapOffset;
    private readonly int _nameMapSize;
    private readonly int _nameHashOffset;
    private readonly int _nameHashSize;
    private readonly int _importOffset;

    public int ExportOffset { get; }
    public int ExportBundlesOffset { get; }
    public int GraphOffset { get; }
    public int GraphSize { get; }

    public IReadOnlyList<string> Names { get; }
    public IReadOnlyList<ulong> Imports { get; }
    public IReadOnlyList<ZenExport> Exports { get; }

    private readonly byte[] _data;

    public ZenPackage(byte[] data)
    {
        _data = data;

        // Package summary header: QQIIiiiiiiiiii (field order matters).
        PackageName = BitConverter.ToUInt64(data, 0);
        SourceName = BitConverter.ToUInt64(data, 8);
        PackageFlags = BitConverter.ToUInt32(data, 16);
        CookedHeaderSize = BitConverter.ToUInt32(data, 20);
        _nameMapOffset = BitConverter.ToInt32(data, 24);
        _nameMapSize = BitConverter.ToInt32(data, 28);
        _nameHashOffset = BitConverter.ToInt32(data, 32);
        _nameHashSize = BitConverter.ToInt32(data, 36);
        _importOffset = BitConverter.ToInt32(data, 40);
        ExportOffset = BitConverter.ToInt32(data, 44);
        ExportBundlesOffset = BitConverter.ToInt32(data, 48);
        GraphOffset = BitConverter.ToInt32(data, 52);
        GraphSize = BitConverter.ToInt32(data, 56);
        // offset 60: padding, unused.

        Names = LoadNameBatch(
            data.AsSpan(_nameMapOffset, _nameMapSize),
            data.AsSpan(_nameHashOffset, _nameHashSize));

        var nImports = (ExportOffset - _importOffset) / 8;
        var imports = new List<ulong>(Math.Max(nImports, 0));
        for (var i = 0; i < nImports; i++)
        {
            imports.Add(BitConverter.ToUInt64(data, _importOffset + i * 8));
        }
        Imports = imports;

        var nExports = (ExportBundlesOffset - ExportOffset) / 72;
        var exports = new List<ZenExport>(nExports);
        for (var i = 0; i < nExports; i++)
        {
            var o = ExportOffset + i * 72;
            var serialOffset = BitConverter.ToInt64(data, o);
            var serialSize = BitConverter.ToInt64(data, o + 8);
            var nameIndex = BitConverter.ToUInt32(data, o + 16);
            var nameNumber = BitConverter.ToUInt32(data, o + 20);
            var outer = BitConverter.ToUInt64(data, o + 24);
            var cls = BitConverter.ToUInt64(data, o + 32);
            var super = BitConverter.ToUInt64(data, o + 40);
            var template = BitConverter.ToUInt64(data, o + 48);
            var globalImport = BitConverter.ToUInt64(data, o + 56);
            var objFlags = BitConverter.ToUInt32(data, o + 64);
            var filt = data[o + 68];

            exports.Add(new ZenExport
            {
                Index = i,
                Name = NameAt(nameIndex, nameNumber),
                SerialOffset = serialOffset,
                SerialSize = serialSize,
                Outer = outer,
                Cls = cls,
                Super = super,
                Template = template,
                GlobalImport = globalImport,
                ObjectFlags = objFlags,
                Filter = filt,
            });
        }
        Exports = exports;
    }

    /// <summary>
    /// Decodes the name table: headers and strings are INTERLEAVED (header, string, header,
    /// string...), and the count of names is NOT stored directly — it's derived from the hash
    /// blob size: count = hashData.Length/8 - 1.
    /// </summary>
    private static List<string> LoadNameBatch(ReadOnlySpan<byte> nameData, ReadOnlySpan<byte> hashData)
    {
        var result = new List<string>();
        if (hashData.Length < 8) return result;

        var count = hashData.Length / 8 - 1;
        var o = 0;
        for (var i = 0; i < count; i++)
        {
            var b0 = nameData[o];
            var b1 = nameData[o + 1];
            o += 2;
            var isUtf16 = (b0 >> 7) != 0;
            var length = ((b0 & 0x7F) << 8) | b1;

            if (isUtf16)
            {
                // Wide names are big-endian and padded to a 2-byte boundary.
                var bytes = nameData.Slice(o, length * 2);
                var chars = new char[length];
                for (var c = 0; c < length; c++)
                {
                    chars[c] = (char)((bytes[c * 2] << 8) | bytes[c * 2 + 1]);
                }
                result.Add(new string(chars));
                o += length * 2;
                if ((o & 1) != 0) o += 1;
            }
            else
            {
                result.Add(Encoding.UTF8.GetString(nameData.Slice(o, length)));
                o += length;
            }
        }

        return result;
    }

    /// <summary>
    /// Resolves a name reference. Top 2 bits of the index are a type tag and must be masked
    /// off. number == 0 means use the name as-is; otherwise Unreal appends _(number-1).
    /// </summary>
    public string NameAt(uint index, uint number = 0)
    {
        index &= 0x3FFFFFFF;
        var s = index < Names.Count ? Names[(int)index] : $"<bad:{index}>";
        return number == 0 ? s : $"{s}_{number - 1}";
    }

    /// <summary>
    /// Where actual object data begins inside this package's bytes. NOT an export's
    /// SerialOffset (that refers to the original pre-container file) — the real data starts
    /// right after graph data, with exports laid out one after another in order.
    /// </summary>
    public int ExportDataStart() => GraphOffset + GraphSize;

    /// <summary>Locates the (start, end) byte range of the first export of the given class, or null.</summary>
    public (int Start, int End)? FindExportPayload(ulong classId)
    {
        var offset = ExportDataStart();
        foreach (var e in Exports)
        {
            if (e.Cls == classId) return (offset, offset + (int)e.SerialSize);
            offset += (int)e.SerialSize;
        }
        return null;
    }

    /// <summary>True if this package uses Unreal's compact "unversioned" property format.</summary>
    public bool UsesUnversionedProperties() => (PackageFlags & 0x00002000) != 0;
}
