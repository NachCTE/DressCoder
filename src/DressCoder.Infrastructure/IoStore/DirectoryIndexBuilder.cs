using System.Text;

namespace DressCoder.Infrastructure.IoStore;

/// <summary>
/// Builds the directory index (filename table) for a .utoc, ported from
/// FFVII-Rebirth-Mesh-Patcher's lib/dirindex.py (MIT license, see docs/04-licencias-terceros.md).
///
/// The container stores its file listing as a TREE of linked lists (each folder points to
/// its first child folder and its next sibling; each file points to the next file in the
/// same folder) rather than nested arrays. New entries are PREPENDED to their list (not
/// appended), so lists end up in reverse creation order — matching how Unreal itself builds
/// them, which is what makes byte-identical round-trips possible.
/// </summary>
public static class DirectoryIndexBuilder
{
    private const uint Invalid = 0xFFFFFFFF;

    /// <summary>
    /// Serializes a directory index.
    /// </summary>
    /// <param name="mount">Path prefix, e.g. "../../../End/Mods/SomeMod/Content/".</param>
    /// <param name="files">List of (path, chunk_index) pairs, in the order chunks were added.</param>
    public static byte[] Build(string mount, IReadOnlyList<(string Path, int ChunkIndex)> files)
    {
        // String table: every folder/file name is stored once, referenced by number.
        var strings = new List<string>();
        var stringIds = new Dictionary<string, int>();

        int StringId(string s)
        {
            if (stringIds.TryGetValue(s, out var id)) return id;
            id = strings.Count;
            strings.Add(s);
            stringIds[s] = id;
            return id;
        }

        // Folder node: [name_id, first_child, next_sibling, first_file]. Entry 0 is the root (no name).
        var dirs = new List<uint[]> { new[] { Invalid, Invalid, Invalid, Invalid } };
        var fileEntries = new List<uint[]>(); // [name_id, next_file, chunk_index]
        var children = new Dictionary<uint, Dictionary<string, uint>>();

        uint GetOrMakeDir(uint parent, string name)
        {
            var siblings = children.TryGetValue(parent, out var existing)
                ? existing
                : children[parent] = new Dictionary<string, uint>();

            if (siblings.TryGetValue(name, out var found)) return found;

            var index = (uint)dirs.Count;
            dirs.Add(new[] { (uint)StringId(name), Invalid, Invalid, Invalid });
            siblings[name] = index;

            // PREPEND: the new folder becomes the parent's first child, pointing at whoever held that slot.
            dirs[(int)index][2] = dirs[(int)parent][1];
            dirs[(int)parent][1] = index;
            return index;
        }

        foreach (var (path, chunkIndex) in files)
        {
            var parts = path.Trim('/').Split('/');
            uint folder = 0;
            for (var i = 0; i < parts.Length - 1; i++)
            {
                folder = GetOrMakeDir(folder, parts[i]);
            }

            var nameId = (uint)StringId(parts[^1]);
            // PREPEND, same as folders.
            fileEntries.Add(new[] { nameId, dirs[(int)folder][3], (uint)chunkIndex });
            dirs[(int)folder][3] = (uint)(fileEntries.Count - 1);
        }

        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);

        // Strings here are length-prefixed and INCLUDE their null terminator.
        void WriteString(string s)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            var withNull = new byte[bytes.Length + 1];
            bytes.CopyTo(withNull, 0);
            w.Write(withNull.Length);
            w.Write(withNull);
        }

        WriteString(mount);

        w.Write(dirs.Count);
        foreach (var d in dirs)
        {
            foreach (var v in d) w.Write(v);
        }

        w.Write(fileEntries.Count);
        foreach (var f in fileEntries)
        {
            foreach (var v in f) w.Write(v);
        }

        w.Write(strings.Count);
        foreach (var s in strings) WriteString(s);

        w.Flush();
        return ms.ToArray();
    }
}
