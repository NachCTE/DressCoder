using DressCoder.Core.Converter;
using DressCoder.Core.Models;
using DressCoder.Infrastructure.ExternalTools;
using Microsoft.Extensions.Logging;

namespace DressCoder.Infrastructure.Assembly;

/// <summary>
/// Assembles the final Dresscode plugin folder in "Modo Simple" (a.k.a. Wrapper Mode):
/// the replacer's original .pak/.utoc/.ucas container is copied byte-for-byte, WITHOUT any
/// binary modification, and just renamed/relocated under the confirmed Dresscode plugin
/// layout (docs/01-investigacion-dresscode.md section 3) alongside a generated .uplugin and
/// icon.
///
/// This is deliberately NOT the "full Dresscode integration" (Modo B / repack with patched
/// DA_ModMetaData + ModData DataAssets), which requires binary-patching Zen-format packages —
/// an open research problem tracked in docs/03-spike-tecnico-conclusiones.md (incógnitas 1 y
/// 5). Because Modo Simple reuses the original, already-valid IoStore container verbatim,
/// it carries none of that risk: the resulting plugin behaves exactly like the original
/// replacer (it overrides the vanilla asset unconditionally), but becomes a toggleable,
/// icon-and-metadata-carrying plugin that Reunion Mod Loader can discover under
/// End/Mods/{PluginName}/. It will NOT appear as a selectable outfit inside Dresscode's own
/// in-game costume-swap menu — only the (not yet implemented) full integration mode achieves
/// that. See docs/02-documento-tecnico.md section on export modes.
/// </summary>
public sealed class PluginAssembler : IPluginAssembler
{
    private readonly ILogger<PluginAssembler> _logger;

    public PluginAssembler(ILogger<PluginAssembler> logger)
    {
        _logger = logger;
    }

    public async Task<Result<ConversionResult>> AssembleAsync(
        ModMetadataInput metadata,
        string containerUtocPath,
        string outputDirectory,
        CancellationToken ct = default)
    {
        var issues = new List<ValidationIssue>();

        string utocPath;
        try
        {
            utocPath = RetocPakReader.ResolveUtocPath(containerUtocPath);
        }
        catch (FileNotFoundException ex)
        {
            return Result<ConversionResult>.Failure(ex.Message);
        }

        var pakPath = Path.ChangeExtension(utocPath, ".pak");
        var ucasPath = Path.ChangeExtension(utocPath, ".ucas");

        if (!File.Exists(pakPath))
        {
            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                Message = $"No se encontró '{Path.GetFileName(pakPath)}' junto al .utoc; se omitirá " +
                          "(algunos replacers no lo generan, ver docs/03 sección 5.2).",
            });
        }

        if (!File.Exists(ucasPath))
        {
            return Result<ConversionResult>.Failure(
                $"No se encontró '{Path.GetFileName(ucasPath)}' junto a '{utocPath}'; el contenedor está incompleto.");
        }

        if (string.IsNullOrWhiteSpace(metadata.PluginName) ||
            metadata.PluginName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            metadata.PluginName.Contains(' '))
        {
            return Result<ConversionResult>.Failure(
                "El nombre del plugin no puede estar vacío, contener espacios ni caracteres inválidos para una carpeta.");
        }

        var pluginDirectory = Path.Combine(outputDirectory, metadata.PluginName);
        var paksDirectory = Path.Combine(pluginDirectory, "Content", "Paks", "WindowsNoEditor");
        var resourcesDirectory = Path.Combine(pluginDirectory, "Resources");

        try
        {
            if (Directory.Exists(pluginDirectory))
            {
                issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Message = $"Ya existía una carpeta '{metadata.PluginName}' en el destino; su contenido fue sobrescrito.",
                    CanAutoFix = true,
                });
            }

            Directory.CreateDirectory(paksDirectory);
            Directory.CreateDirectory(resourcesDirectory);

            var upluginPath = Path.Combine(pluginDirectory, $"{metadata.PluginName}.uplugin");
            await UpluginWriter.WriteAsync(upluginPath, metadata, ct);

            await WriteIconAsync(metadata, resourcesDirectory, ct);

            var destBaseName = $"{metadata.PluginName}-WindowsNoEditor";
            if (File.Exists(pakPath))
            {
                File.Copy(pakPath, Path.Combine(paksDirectory, destBaseName + ".pak"), overwrite: true);
            }

            File.Copy(utocPath, Path.Combine(paksDirectory, destBaseName + ".utoc"), overwrite: true);
            File.Copy(ucasPath, Path.Combine(paksDirectory, destBaseName + ".ucas"), overwrite: true);

            issues.Add(new ValidationIssue
            {
                Severity = ValidationSeverity.Info,
                Message = "Modo Simple: el contenedor original del replacer se copió sin modificar. " +
                          "El mod se comportará como el replacer original (reemplazo directo), no aparecerá " +
                          "como un outfit seleccionable dentro del menú propio de Dresscode.",
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensamblando el plugin '{PluginName}' en '{OutputDirectory}'",
                metadata.PluginName, outputDirectory);
            return Result<ConversionResult>.Failure($"Error al generar la estructura del plugin: {ex.Message}");
        }

        var result = new ConversionResult
        {
            PluginName = metadata.PluginName,
            OutputDirectory = pluginDirectory,
            Validation = new ValidationReport { Issues = issues },
            IsExperimentalRepack = false,
        };

        _logger.LogInformation("Plugin '{PluginName}' ensamblado (Modo Simple) en '{OutputDirectory}'",
            metadata.PluginName, pluginDirectory);

        return Result<ConversionResult>.Success(result);
    }

    private static async Task WriteIconAsync(ModMetadataInput metadata, string resourcesDirectory, CancellationToken ct)
    {
        var iconDestination = Path.Combine(resourcesDirectory, "Icon.png");

        if (!string.IsNullOrWhiteSpace(metadata.IconPath) && File.Exists(metadata.IconPath))
        {
            using var source = File.OpenRead(metadata.IconPath);
            using var dest = File.Create(iconDestination);
            await source.CopyToAsync(dest, ct);
            return;
        }

        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        await using var resourceStream = assembly.GetManifestResourceStream(
            "DressCoder.Infrastructure.Assembly.DefaultIcon.png");

        if (resourceStream is null)
        {
            throw new InvalidOperationException("No se encontró el ícono por defecto embebido.");
        }

        await using var fileStream = File.Create(iconDestination);
        await resourceStream.CopyToAsync(fileStream, ct);
    }
}
