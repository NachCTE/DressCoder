using DressCoder.Core.Models;
using DressCoder.Infrastructure.Assembly;
using Microsoft.Extensions.Logging.Abstractions;

// Empaqueta el contenedor ya inyectado (utoc/ucas con DA_ModMetaData agregado) como un plugin
// completo (.uplugin + icono + Content/Paks/WindowsNoEditor) listo para copiar a End/Mods/.
// Reusa el PluginAssembler real (Modo Simple), apuntando al contenedor YA MODIFICADO en vez
// del replacer original, para probar si la metadata inyectada alcanza para que Dresscode/FF7RML
// lo detecte como mod (aunque el mount point todavía no esté reescrito — ver docs/03 sección 9).

var injectedUtoc = @"D:\Development\PersonalProjects\DressCoder\tools\inject-metadata-test\output\ZAerithBahamutRobeStandard_P.utoc";
var outputDir = @"D:\Development\PersonalProjects\DressCoder\tools\test-plugin-output";

var metadata = new ModMetadataInput
{
    PluginName = "DressCoderTest",
    FriendlyName = "Bahamut Robe (DressCoder Test)",
    Description = "Prueba de deteccion de metadata inyectada",
    CreatedBy = "DressCoder",
};

var assembler = new PluginAssembler(NullLogger<PluginAssembler>.Instance);
var result = await assembler.AssembleAsync(metadata, injectedUtoc, outputDir);

if (result.IsSuccess)
{
    Console.WriteLine($"OK: plugin generado en {result.Value!.OutputDirectory}");
    foreach (var issue in result.Value.Validation.Issues)
    {
        Console.WriteLine($"  [{issue.Severity}] {issue.Message}");
    }
}
else
{
    Console.WriteLine($"ERROR: {result.Error}");
}
