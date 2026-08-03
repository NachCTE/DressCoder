using DressCoder.Infrastructure.IoStore;

// Prueba de fidelidad del round-trip usando NUESTRO propio reader/writer C# (portado de
// FFVII-Rebirth-Mesh-Patcher, MIT), en vez de retoc. Reconstruye el contenedor reusando cada
// chunk EXACTAMENTE como está en el .ucas original (sin descomprimir/recomprimir nada) y
// compara el .utoc resultante byte a byte contra el original.

var sourceUtoc = args.Length > 0
    ? args[0]
    : @"D:\Development\PersonalProjects\DressCoder\example\dresscode\AerithNierEC\Content\Paks\WindowsNoEditor\AerithNierECEnd-WindowsNoEditor.utoc";
var pakBaseName = Path.GetFileNameWithoutExtension(sourceUtoc);

Console.WriteLine($"Abriendo '{sourceUtoc}'...");
using var toc = new IoStoreToc(sourceUtoc);
Console.WriteLine($"OK: {toc.ChunkIds.Count} chunks, mount='{toc.MountPoint}', flags={toc.Flags:X2}");

// --- 1. Reconstruir lista de chunks reusando bloques crudos (sin tocar nada) ---
var chunksToWrite = new List<IoStoreChunkToWrite>();
for (var i = 0; i < toc.ChunkIds.Count; i++)
{
    var (_, length) = toc.OffsetLengths[i];
    var blocks = toc.ReadRawBlocks(i);
    chunksToWrite.Add(new IoStoreChunkToWrite
    {
        Id = toc.ChunkIds[i],
        Blocks = blocks,
        Size = length,
    });
}

// --- 2. Reconstruir directory index reusando los mismos paths, en orden de chunk index ---
var files = toc.Paths
    .OrderBy(kv => kv.Key)
    .Select(kv => (Path: kv.Value, ChunkIndex: kv.Key))
    .ToList();
var dirIndexBytes = DirectoryIndexBuilder.Build(toc.MountPoint, files);

// --- 3. Armar el contenedor ---
var built = IoStoreContainerWriter.BuildContainer(toc.Methods, toc.CompressionMethodNameLength, chunksToWrite);
var header = IoStoreContainerWriter.BuildTocHeader(toc, chunksToWrite.Count, built.BlockTable.Count, dirIndexBytes.Length);
var metas = IoStoreContainerWriter.BuildMetasFrom(toc, chunksToWrite.Count, new Dictionary<int, byte[]>());

var utocBytes = IoStoreContainerWriter.AssembleUtoc(
    header, built.ChunkIdsSection, built.OffsetLengthSection, built.CompressionBlockSection,
    built.CompressionMethodNamesSection, dirIndexBytes, metas);

// --- 4. Escribir salida y comparar ---
var outputDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "output");
Directory.CreateDirectory(outputDir);
var outUtoc = Path.Combine(outputDir, $"{pakBaseName}.utoc");
var outUcas = Path.Combine(outputDir, $"{pakBaseName}.ucas");
File.WriteAllBytes(outUtoc, utocBytes);
File.WriteAllBytes(outUcas, built.Ucas);

Console.WriteLine($"Escrito: {outUtoc} ({utocBytes.Length} bytes)");
Console.WriteLine($"Escrito: {outUcas} ({built.Ucas.Length} bytes)");

var origUtocBytes = File.ReadAllBytes(sourceUtoc);
var origUcasBytes = File.ReadAllBytes(Path.ChangeExtension(sourceUtoc, ".ucas"));

Console.WriteLine();
Console.WriteLine($"utoc: original={origUtocBytes.Length} reconstruido={utocBytes.Length} " +
                   $"{(origUtocBytes.Length == utocBytes.Length ? "(mismo tamaño)" : "(TAMAÑO DISTINTO)")}");
Console.WriteLine($"ucas: original={origUcasBytes.Length} reconstruido={built.Ucas.Length} " +
                   $"{(origUcasBytes.Length == built.Ucas.Length ? "(mismo tamaño)" : "(TAMAÑO DISTINTO)")}");

bool utocIdentical = origUtocBytes.AsSpan().SequenceEqual(utocBytes);
bool ucasIdentical = origUcasBytes.AsSpan().SequenceEqual(built.Ucas);
Console.WriteLine($".utoc idéntico byte a byte: {utocIdentical}");
Console.WriteLine($".ucas idéntico byte a byte: {ucasIdentical}");

if (!utocIdentical && origUtocBytes.Length == utocBytes.Length)
{
    for (var i = 0; i < origUtocBytes.Length; i++)
    {
        if (origUtocBytes[i] != utocBytes[i])
        {
            Console.WriteLine($"Primera diferencia en .utoc: offset {i} (0x{i:X}) orig={origUtocBytes[i]:X2} nuevo={utocBytes[i]:X2}");
            break;
        }
    }
}

if (!ucasIdentical && origUcasBytes.Length == built.Ucas.Length)
{
    var diffCount = 0;
    for (var i = 0; i < origUcasBytes.Length; i++)
    {
        if (origUcasBytes[i] != built.Ucas[i])
        {
            diffCount++;
            if (diffCount <= 10)
            {
                Console.WriteLine($"Diferencia en .ucas: offset {i} (0x{i:X}) orig={origUcasBytes[i]:X2} nuevo={built.Ucas[i]:X2}");
            }
        }
    }
    Console.WriteLine($"Total de bytes distintos en .ucas: {diffCount} / {origUcasBytes.Length}");
}

return 0;
