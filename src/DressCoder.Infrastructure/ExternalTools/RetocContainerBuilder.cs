using DressCoder.Core.Converter;
using CoreResult = DressCoder.Core.Models.Result;

namespace DressCoder.Infrastructure.ExternalTools;

/// <summary>
/// Wraps `retoc pack-raw` to build the final IoStore container (.utoc/.ucas) from a directory
/// of raw chunks + manifest.json. Note: this does NOT produce the accompanying legacy `.pak`
/// (see <see cref="RepakLegacyPakBuilder"/> and docs/03-spike-tecnico-conclusiones.md section 5.5) —
/// that is a separate, real legacy PAK with its own metadata files, not an empty IoStore stub.
/// </summary>
public sealed class RetocContainerBuilder : IContainerBuilder
{
    private readonly ExternalToolLocator _tools;
    private readonly ProcessRunner _processRunner;

    public RetocContainerBuilder(ExternalToolLocator tools, ProcessRunner processRunner)
    {
        _tools = tools;
        _processRunner = processRunner;
    }

    public async Task<CoreResult> BuildAsync(string rawChunksDirectory, string outputUtocPath, CancellationToken ct = default)
    {
        if (!Directory.Exists(rawChunksDirectory))
        {
            return CoreResult.Failure($"No existe el directorio de chunks '{rawChunksDirectory}'.");
        }

        if (!File.Exists(Path.Combine(rawChunksDirectory, "manifest.json")))
        {
            return CoreResult.Failure($"No se encontró manifest.json en '{rawChunksDirectory}'.");
        }

        string retocExePath;
        try
        {
            retocExePath = _tools.RetocExePath;
        }
        catch (FileNotFoundException ex)
        {
            return CoreResult.Failure(ex.Message);
        }

        var outputDir = Path.GetDirectoryName(outputUtocPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        // Limpiamos restos de una corrida previa para evitar mezclar contenedores viejos/nuevos.
        foreach (var ext in new[] { ".utoc", ".ucas", ".pak" })
        {
            var sibling = Path.ChangeExtension(outputUtocPath, ext);
            if (File.Exists(sibling)) File.Delete(sibling);
        }

        ProcessExecutionResult processResult;
        try
        {
            processResult = await _processRunner.RunAsync(
                retocExePath,
                ["pack-raw", rawChunksDirectory, outputUtocPath],
                ct: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CoreResult.Failure($"No se pudo ejecutar retoc: {ex.Message}");
        }

        if (!processResult.Succeeded)
        {
            return CoreResult.Failure($"retoc pack-raw falló (código {processResult.ExitCode}): {processResult.StdErr}");
        }

        if (!File.Exists(outputUtocPath))
        {
            return CoreResult.Failure("retoc pack-raw no generó el archivo .utoc esperado.");
        }

        return CoreResult.Success();
    }
}
