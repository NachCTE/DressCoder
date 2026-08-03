using DressCoder.Infrastructure.IoStore;

var path = args.Length > 0
    ? args[0]
    : @"D:\Development\PersonalProjects\DressCoder\example\dresscode\AerithNierEC\_extracted\MetaData\DA_ModMetaData.uasset";
var data = File.ReadAllBytes(path);
Console.WriteLine($"Archivo: {path}");
Console.WriteLine($"Tamaño total: {data.Length} bytes");

var pkg = new ZenPackage(data);
Console.WriteLine($"PackageFlags=0x{pkg.PackageFlags:X8}  CookedHeaderSize={pkg.CookedHeaderSize}");
Console.WriteLine($"ExportOffset={pkg.ExportOffset} ExportBundlesOffset={pkg.ExportBundlesOffset} GraphOffset={pkg.GraphOffset} GraphSize={pkg.GraphSize}");
Console.WriteLine($"ExportDataStart={pkg.ExportDataStart()}");
Console.WriteLine();
Console.WriteLine("== Names ==");
for (var i = 0; i < pkg.Names.Count; i++) Console.WriteLine($"  [{i}] {pkg.Names[i]}");

Console.WriteLine();
Console.WriteLine("== Imports ==");
foreach (var imp in pkg.Imports) Console.WriteLine($"  0x{imp:X16}");

Console.WriteLine();
Console.WriteLine("== Exports ==");
foreach (var e in pkg.Exports)
{
    Console.WriteLine($"  [{e.Index}] name='{e.Name}' cls=0x{e.Cls:X16} serialOffset={e.SerialOffset} serialSize={e.SerialSize} outer={e.Outer} super={e.Super}");
}

Console.WriteLine();
var start = pkg.ExportDataStart();
Console.WriteLine($"== Export payload hexdump (desde offset {start}) ==");
var payload = data.AsSpan(start).ToArray();
for (var i = 0; i < payload.Length; i += 16)
{
    var chunk = payload.AsSpan(i, Math.Min(16, payload.Length - i));
    var arr = chunk.ToArray();
    var hex = string.Join(" ", arr.Select(b => b.ToString("X2")));
    var ascii = new string(arr.Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
    Console.WriteLine($"  {start + i:X4}: {hex,-48} {ascii}");
}
