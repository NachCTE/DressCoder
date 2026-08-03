using System.Linq;
using DressCoder.Core.Converter;
using DressCoder.Core.Parser;
using DressCoder.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Prueba de fidelidad del round-trip retoc: unpack-raw -> pack-raw SIN modificar nada,
// sobre un mod Dresscode real (AerithNierEC), para confirmar si el contenedor reconstruido
// sigue siendo válido para el juego antes de invertir en el patcher de DataAssets (Modo Full).
// Ver docs/03-spike-tecnico-conclusiones.md sección 8.

var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning));
services.AddDressCoderInfrastructure();
var provider = services.BuildServiceProvider();

var reader = provider.GetRequiredService<IPakReader>();
var containerBuilder = provider.GetRequiredService<IContainerBuilder>();

var sourcePluginDir = @"D:\Development\PersonalProjects\DressCoder\example\dresscode\AerithNierEC";
var pluginName = "AerithNierEC";
var pakBaseName = $"{pluginName}End-WindowsNoEditor";
var sourcePaksDir = Path.Combine(sourcePluginDir, "Content", "Paks", "WindowsNoEditor");
var sourceUtoc = Path.Combine(sourcePaksDir, $"{pakBaseName}.utoc");
var sourcePak = Path.Combine(sourcePaksDir, $"{pakBaseName}.pak");
var sourceUplugin = Path.Combine(sourcePluginDir, $"{pluginName}.uplugin");
var sourceIconDir = Path.Combine(sourcePluginDir, "Resources");
var sourceIconFile = Directory.GetFiles(sourceIconDir, "Icon*.png").FirstOrDefault()
    ?? throw new FileNotFoundException($"No se encontró ningún Icon*.png en '{sourceIconDir}'");

var outputRoot = @"D:\Development\PersonalProjects\DressCoder\tools\roundtrip-test\output";
var outputPluginDir = Path.Combine(outputRoot, pluginName);
var outputPaksDir = Path.Combine(outputPluginDir, "Content", "Paks", "WindowsNoEditor");

if (Directory.Exists(outputPluginDir))
{
    Directory.Delete(outputPluginDir, recursive: true);
}

Console.WriteLine($"== 1/3: retoc unpack-raw sobre '{sourceUtoc}'...");
var extract = await reader.ExtractAsync(sourceUtoc);
if (!extract.IsSuccess)
{
    Console.WriteLine($"FALLÓ unpack-raw: {extract.Error}");
    return 1;
}

var project = extract.Value!;
Console.WriteLine($"OK: {project.Chunks.Count} chunks extraídos a '{project.StagingDirectory}'");

Directory.CreateDirectory(outputPaksDir);
var outputUtoc = Path.Combine(outputPaksDir, $"{pakBaseName}.utoc");

Console.WriteLine("== 2/3: retoc pack-raw (SIN modificar el manifest ni los chunks)...");
var build = await containerBuilder.BuildAsync(project.StagingDirectory, outputUtoc);
if (!build.IsSuccess)
{
    Console.WriteLine($"FALLÓ pack-raw: {build.Error}");
    return 1;
}

Console.WriteLine("OK: .utoc/.ucas reconstruidos.");

Console.WriteLine("== 3/3: copiando .pak/.uplugin/Icon sin cambios...");
File.Copy(sourcePak, Path.Combine(outputPaksDir, $"{pakBaseName}.pak"), overwrite: true);
File.Copy(sourceUplugin, Path.Combine(outputPluginDir, $"{pluginName}.uplugin"), overwrite: true);
Directory.CreateDirectory(Path.Combine(outputPluginDir, "Resources"));
File.Copy(sourceIconFile, Path.Combine(outputPluginDir, "Resources", Path.GetFileName(sourceIconFile)), overwrite: true);

var origUtocSize = new FileInfo(sourceUtoc).Length;
var origUcasSize = new FileInfo(Path.Combine(sourcePaksDir, $"{pakBaseName}.ucas")).Length;
var newUtocSize = new FileInfo(outputUtoc).Length;
var newUcasSize = new FileInfo(Path.Combine(outputPaksDir, $"{pakBaseName}.ucas")).Length;

Console.WriteLine();
Console.WriteLine($"Tamaño original: utoc={origUtocSize} ucas={origUcasSize}");
Console.WriteLine($"Tamaño reconstruido: utoc={newUtocSize} ucas={newUcasSize}");
Console.WriteLine();
Console.WriteLine($"LISTO. Plugin reconstruido en: {outputPluginDir}");
Console.WriteLine("Copiá esa carpeta a End/Mods/ (reemplazando la original) y probá en el juego.");

return 0;
