using DressCoder.Infrastructure.IoStore;

// Busca la tabla de package IDs dentro del chunk ContainerHeader (tipo 10) de AerithNierEC,
// aprovechando que YA SABEMOS el chunk ID exacto del DA_ModMetaData (4AFF37BE69FA230500000002,
// visto en tools/inject-metadata-test). Si el ContainerHeader lista sus paquetes por
// FPackageId (8 bytes), buscamos esos 8 bytes crudos dentro del chunk para ubicar el offset
// de la tabla y entender su estructura alrededor.

var utoc = @"D:\Development\PersonalProjects\DressCoder\example\dresscode\AerithNierEC\Content\Paks\WindowsNoEditor\AerithNierECEnd-WindowsNoEditor.utoc";
using var toc = new IoStoreToc(utoc);

var headerChunkIndex = -1;
for (var i = 0; i < toc.ChunkIds.Count; i++)
{
    if (toc.ChunkType(i) == 10) { headerChunkIndex = i; break; }
}
Console.WriteLine($"ContainerHeader chunk index = {headerChunkIndex}");

using var oodle = OodleCompression.Load(@"D:\Development\PersonalProjects\DressCoder\tools\bin\oo2core_9_win64.dll");
var headerBytes = toc.Read(headerChunkIndex, oodle);
Console.WriteLine($"ContainerHeader tamaño descomprimido: {headerBytes.Length} bytes");

var outPath = @"D:\Development\PersonalProjects\DressCoder\tools\diag-ucas\aerith_containerheader.bin";
File.WriteAllBytes(outPath, headerBytes);
Console.WriteLine($"Volcado a {outPath}");

Console.WriteLine();
Console.WriteLine("Primeros 128 bytes (hex):");
Console.WriteLine(Convert.ToHexString(headerBytes.AsSpan(0, Math.Min(128, headerBytes.Length))));

// Chunk id del DA_ModMetaData (12 bytes), primeros 8 = FPackageId probablemente.
var metaChunkIdHex = "4AFF37BE69FA230500000002";
var metaChunkIdBytes = Convert.FromHexString(metaChunkIdHex);
var packageIdBytes = metaChunkIdBytes.AsSpan(0, 8).ToArray();
Console.WriteLine();
Console.WriteLine($"Buscando package id (8 bytes) = {Convert.ToHexString(packageIdBytes)} dentro del ContainerHeader...");

var found = new List<int>();
for (var i = 0; i <= headerBytes.Length - 8; i++)
{
    var match = true;
    for (var j = 0; j < 8; j++)
    {
        if (headerBytes[i + j] != packageIdBytes[j]) { match = false; break; }
    }
    if (match) found.Add(i);
}
Console.WriteLine($"Ocurrencias encontradas: {found.Count}");
foreach (var off in found)
{
    Console.WriteLine($"  offset {off} (0x{off:X})");
}

// Encontrar dónde termina el contenido real (antes del padding de ceros hasta 64KB virtual)
var lastNonZero = headerBytes.Length - 1;
while (lastNonZero >= 0 && headerBytes[lastNonZero] == 0) lastNonZero--;
Console.WriteLine();
Console.WriteLine($"Último byte no-cero: offset {lastNonZero} (tamaño real aprox {lastNonZero + 1} bytes)");

// --- Intento de parseo asumiendo layout "version <= Initial" de FIoContainerHeader (retoc) ---
Console.WriteLine();
Console.WriteLine("=== Intento de parseo (version Initial / legacy) ===");
var pos = 0;
ulong ReadU64() { var v = BitConverter.ToUInt64(headerBytes, pos); pos += 8; return v; }
uint ReadU32() { var v = BitConverter.ToUInt32(headerBytes, pos); pos += 4; return v; }
byte[] ReadBytes(int n) { var v = headerBytes.AsSpan(pos, n).ToArray(); pos += n; return v; }

var containerId = ReadU64();
Console.WriteLine($"container_id = 0x{containerId:X16} (pos={pos})");
var packageCount = ReadU32();
Console.WriteLine($"package_count = {packageCount} (pos={pos})");
var namesBufLen = ReadU32();
Console.WriteLine($"names_buffer len = {namesBufLen} (pos={pos})");
ReadBytes((int)namesBufLen);
Console.WriteLine($"  (skip) pos={pos}");
var nameHashesLen = ReadU32();
Console.WriteLine($"name_hashes_buffer len = {nameHashesLen} (pos={pos})");
ReadBytes((int)nameHashesLen);
Console.WriteLine($"  (skip) pos={pos}");

var pkgIdCount = ReadU32();
Console.WriteLine($"StoreEntries.package_ids count = {pkgIdCount} (pos={pos})");
var packageIds = new List<ulong>();
for (var i = 0; i < pkgIdCount; i++) packageIds.Add(ReadU64());
Console.WriteLine($"  (leidos {packageIds.Count} package ids) pos={pos}");
for (var i = 0; i < Math.Min(5, packageIds.Count); i++) Console.WriteLine($"    [{i}] 0x{packageIds[i]:X16}");
var targetId = BitConverter.ToUInt64(packageIdBytes, 0);
var targetIndex = packageIds.IndexOf(targetId);
Console.WriteLine($"  indice de nuestro target (0x{targetId:X16}): {targetIndex}");

var entriesBufLen = ReadU32();
Console.WriteLine($"StoreEntries.buffer len = {entriesBufLen} (pos={pos})");
var entriesBuf = ReadBytes((int)entriesBufLen);
Console.WriteLine($"  (skip) pos={pos}");

Console.WriteLine($"Bytes restantes tras StoreEntries: {headerBytes.Length - pos} (deberian ser mayormente ceros de padding si esto es correcto; ultimo no-cero fue {lastNonZero})");
Console.WriteLine($"pos actual {pos} vs lastNonZero {lastNonZero}");

if (pkgIdCount > 0 && pkgIdCount < 10000 && entriesBufLen < 100000)
{
    Console.WriteLine();
    Console.WriteLine("Interpretacion PLAUSIBLE. Volcando entriesBuf (primeros 64 bytes):");
    Console.WriteLine(Convert.ToHexString(entriesBuf.AsSpan(0, Math.Min(64, entriesBuf.Length))));
}


// Dump con offsets alrededor de cada ocurrencia encontrada (40 bytes antes, 40 después)
foreach (var off in found)
{
    Console.WriteLine();
    Console.WriteLine($"--- contexto alrededor de offset {off} ---");
    var start = Math.Max(0, off - 48);
    var end = Math.Min(headerBytes.Length, off + 48);
    for (var i = start; i < end; i += 16)
    {
        var lineEnd = Math.Min(i + 16, end);
        var hex = Convert.ToHexString(headerBytes.AsSpan(i, lineEnd - i));
        var marker = (off >= i && off < lineEnd) ? "  <== package id aqui" : "";
        Console.WriteLine($"  0x{i:X4}: {hex}{marker}");
    }
}

