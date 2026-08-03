using DressCoder.Infrastructure.IoStore;

// Test end-to-end: parchea el template DA_ModMetaData.uasset (extraído del mod Dresscode real)
// con nuevos valores, e inyecta ese chunk dentro del contenedor de un replacer de ejemplo,
// dejando todo lo demás (incluido el ContainerHeader) intacto. Ver docs/03 sección 9.4.

var templatePath = @"D:\Development\PersonalProjects\DressCoder\example\dresscode\AerithNierEC\_extracted\MetaData\DA_ModMetaData.uasset";
var sourceMetadataUtoc = @"D:\Development\PersonalProjects\DressCoder\example\dresscode\AerithNierEC\Content\Paks\WindowsNoEditor\AerithNierECEnd-WindowsNoEditor.utoc";
var replacerUtoc = @"D:\Development\PersonalProjects\DressCoder\example\replacer\ZAerithBahamutRobeStandard_P\ZAerithBahamutRobeStandard_P.utoc";
var outputDir = @"D:\Development\PersonalProjects\DressCoder\tools\inject-metadata-test\output";

Console.WriteLine("== 1. Parcheando strings del template ==");
var templateBytes = File.ReadAllBytes(templatePath);
var replacements = new Dictionary<string, string>
{
    ["Aerith Nier"] = "Bahamut Robe (DressCoder Test)",
    ["By TJ"] = "DressCoder",
};
var patchedBytes = MetadataTemplatePatcher.PatchStrings(templateBytes, replacements);
Console.WriteLine($"Template original: {templateBytes.Length} bytes -> parcheado: {patchedBytes.Length} bytes");

// Verificación: re-parsear el resultado y confirmar que el export table y el contenido cuadran.
var patchedPkg = new ZenPackage(patchedBytes);
Console.WriteLine($"Exports tras el patch: {patchedPkg.Exports.Count}, SerialSize={patchedPkg.Exports[0].SerialSize}");
var payloadStart = patchedPkg.ExportDataStart();
var actualPayloadLength = patchedBytes.Length - payloadStart;
Console.WriteLine($"Longitud real del payload: {actualPayloadLength} (¿coincide con SerialSize? {actualPayloadLength == patchedPkg.Exports[0].SerialSize})");

var payloadText = System.Text.Encoding.UTF8.GetString(patchedBytes, payloadStart, actualPayloadLength);
Console.WriteLine($"¿Contiene 'Bahamut Robe (DressCoder Test)'? {payloadText.Contains("Bahamut Robe (DressCoder Test)")}");
Console.WriteLine($"¿Contiene 'DressCoder'? {payloadText.Contains("DressCoder")}");
Console.WriteLine($"¿Ya NO contiene 'Aerith Nier'? {!payloadText.Contains("Aerith Nier")}");

Console.WriteLine();
Console.WriteLine("== 2. Buscando el chunk ID original del template en AerithNierEC ==");
using var sourceToc = new IoStoreToc(sourceMetadataUtoc);
var metadataChunkIndex = sourceToc.Paths.First(kv => kv.Value.EndsWith("DA_ModMetaData.uasset")).Key;
var templateChunkId = sourceToc.ChunkIds[metadataChunkIndex];
Console.WriteLine($"Chunk ID original: {Convert.ToHexString(templateChunkId)}");

Console.WriteLine();
Console.WriteLine("== 3. Inyectando el chunk parcheado en el contenedor del replacer ==");
using var replacerToc = new IoStoreToc(replacerUtoc);
Console.WriteLine($"Replacer original: {replacerToc.ChunkIds.Count} chunks, mount='{replacerToc.MountPoint}'");

var outputUtoc = Path.Combine(outputDir, Path.GetFileName(replacerUtoc));
var newChunk = new NewContainerChunk
{
    Id = templateChunkId,
    Payload = patchedBytes,
    Path = "MetaData/DA_ModMetaData.uasset",
};

ContainerChunkInjector.InjectAndWrite(replacerToc, outputUtoc, [newChunk], oodle: null);

Console.WriteLine($"Escrito: {outputUtoc}");
using var verifyToc = new IoStoreToc(outputUtoc);
Console.WriteLine($"Contenedor resultante: {verifyToc.ChunkIds.Count} chunks");
foreach (var (idx, path) in verifyToc.Paths.OrderBy(kv => kv.Key))
{
    Console.WriteLine($"  [{idx}] {path}");
}

// Copiamos también el .pak original (stub vacío) para que el triplete quede completo.
File.Copy(Path.ChangeExtension(replacerUtoc, ".pak"), Path.ChangeExtension(outputUtoc, ".pak"), overwrite: true);
Console.WriteLine();
Console.WriteLine("LISTO.");
