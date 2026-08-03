using CoreResult = DressCoder.Core.Models.Result;

namespace DressCoder.Infrastructure.ExternalTools;

/// <summary>
/// Wraps `repak pack` to generate the legacy .pak that accompanies a Dresscode plugin's
/// .utoc/.ucas. Confirmed via spike (docs/03-spike-tecnico-conclusiones.md section 5.5) that
/// this is a real repak V11 pak containing AssetRegistry.bin + two boilerplate .ini files,
/// mounted at "../../../End/Mods/{PluginName}/" — NOT an empty IoStore stub.
/// There is no Core interface for this yet; it will be consumed directly by the future
/// IPluginAssembler implementation once the AssetRegistry.bin generation strategy is decided.
/// </summary>
public sealed class RepakLegacyPakBuilder
{
    private const string DresscodeRepakVersion = "V11";

    private readonly ExternalToolLocator _tools;
    private readonly ProcessRunner _processRunner;

    public RepakLegacyPakBuilder(ExternalToolLocator tools, ProcessRunner processRunner)
    {
        _tools = tools;
        _processRunner = processRunner;
    }

    /// <summary>
    /// Packs <paramref name="inputDirectory"/> (expected to already contain the
    /// AssetRegistry.bin + Config/*.ini layout under End/Mods/{PluginName}/) into
    /// <paramref name="outputPakPath"/>, using the mount point Dresscode plugins use.
    /// </summary>
    public async Task<CoreResult> PackAsync(
        string inputDirectory,
        string outputPakPath,
        string mountPoint,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(inputDirectory))
        {
            return CoreResult.Failure($"No existe el directorio de entrada '{inputDirectory}'.");
        }

        string repakExePath;
        try
        {
            repakExePath = _tools.RepakExePath;
        }
        catch (FileNotFoundException ex)
        {
            return CoreResult.Failure(ex.Message);
        }

        var outputDir = Path.GetDirectoryName(outputPakPath);
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        ProcessExecutionResult processResult;
        try
        {
            processResult = await _processRunner.RunAsync(
                repakExePath,
                ["pack", inputDirectory, outputPakPath, "--mount-point", mountPoint, "--version", DresscodeRepakVersion, "--quiet"],
                ct: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CoreResult.Failure($"No se pudo ejecutar repak: {ex.Message}");
        }

        if (!processResult.Succeeded)
        {
            return CoreResult.Failure($"repak pack falló (código {processResult.ExitCode}): {processResult.StdErr}");
        }

        if (!File.Exists(outputPakPath))
        {
            return CoreResult.Failure("repak pack no generó el archivo .pak esperado.");
        }

        return CoreResult.Success();
    }
}
