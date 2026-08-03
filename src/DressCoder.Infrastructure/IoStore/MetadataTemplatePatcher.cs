using System.Text;

namespace DressCoder.Infrastructure.IoStore;

/// <summary>
/// Patches classic Unreal <c>FString</c> property values (int32 length + ASCII/UTF-8 bytes +
/// null terminator) inside a single-export Zen package, such as Dresscode's
/// <c>PDA_ModMetaData</c> DataAsset. Handles arbitrary length changes — safe because these
/// template packages have exactly one export, so a size change only requires updating that
/// export's own <c>SerialSize</c> field in the export table; no other export's offset shifts.
///
/// NOT a general-purpose Unreal property editor: it works by locating the exact byte pattern
/// of the OLD FString value (length prefix + text + null) and splicing in the NEW one. This is
/// the same "same-length-safe / general via byte shifting" approach validated in
/// docs/03-spike-tecnico-conclusiones.md section 8, now applied generically since this
/// template's export table has only one entry to fix up.
/// </summary>
public static class MetadataTemplatePatcher
{
    /// <summary>
    /// Replaces each entry's old FString value with a new one inside <paramref name="templateBytes"/>.
    /// Throws if the template does not have exactly one export (the assumption that makes this safe),
    /// or if an old value's exact byte pattern cannot be found.
    /// </summary>
    public static byte[] PatchStrings(byte[] templateBytes, IReadOnlyDictionary<string, string> replacements)
    {
        var pkg = new ZenPackage(templateBytes);
        if (pkg.Exports.Count != 1)
        {
            throw new InvalidOperationException(
                $"MetadataTemplatePatcher solo soporta templates de un único export (este tiene {pkg.Exports.Count}).");
        }

        var working = templateBytes.ToList();
        var totalDelta = 0;

        foreach (var (oldValue, newValue) in replacements)
        {
            if (oldValue == newValue) continue;

            var oldPattern = SerializeFString(oldValue);
            var newPattern = SerializeFString(newValue);

            var index = IndexOf(working, oldPattern);
            if (index < 0)
            {
                throw new InvalidOperationException(
                    $"No se encontró el valor '{oldValue}' dentro del template — no se puede parchear.");
            }

            working.RemoveRange(index, oldPattern.Length);
            working.InsertRange(index, newPattern);
            totalDelta += newPattern.Length - oldPattern.Length;
        }

        var result = working.ToArray();

        if (totalDelta != 0)
        {
            var export = pkg.Exports[0];
            var newSize = export.SerialSize + totalDelta;
            var entryOffset = pkg.ExportOffset; // single export: entry 0
            // SerialSize is the second Q (offset +8) of the 72-byte export entry.
            BitConverter.GetBytes(newSize).CopyTo(result, entryOffset + 8);
        }

        return result;
    }

    /// <summary>Serializes a string the way Unreal's classic FString does: int32 length (incl. null) + bytes + null.</summary>
    private static byte[] SerializeFString(string value)
    {
        var textBytes = Encoding.UTF8.GetBytes(value);
        var length = textBytes.Length + 1; // includes the null terminator
        var result = new byte[4 + textBytes.Length + 1];
        BitConverter.GetBytes(length).CopyTo(result, 0);
        textBytes.CopyTo(result, 4);
        result[^1] = 0;
        return result;
    }

    private static int IndexOf(List<byte> haystack, byte[] needle)
    {
        if (needle.Length == 0) return -1;
        for (var i = 0; i <= haystack.Count - needle.Length; i++)
        {
            var matched = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { matched = false; break; }
            }
            if (matched) return i;
        }
        return -1;
    }
}
