using DressCoder.Core.Models;

namespace DressCoder.Infrastructure.ExternalTools;

/// <summary>
/// Locates the Oodle compression library (oo2core_*.dll) from the user's own FF7 Rebirth
/// installation at runtime. NEVER bundled with the app — it's proprietary (RAD Game Tools/
/// Epic Games), see docs/04-licencias-terceros.md. Not required for the current MVP pipeline:
/// `retoc pack-raw` produces valid (if uncompressed and larger) containers without it — see
/// docs/03-spike-tecnico-conclusiones.md section 5.3. Kept for a future optional "compress
/// output" step.
/// </summary>
public sealed class OodleLibraryResolver
{
    private static readonly string[] RelativeCandidates =
    [
        Path.Combine("End", "Binaries", "Win64"),
        Path.Combine("Binaries", "Win64"),
    ];

    public Result<string> TryResolve(string? gameInstallDirectory)
    {
        if (string.IsNullOrWhiteSpace(gameInstallDirectory) || !Directory.Exists(gameInstallDirectory))
        {
            return Result<string>.Failure(
                "No se indicó (o no existe) la carpeta de instalación del juego; no se puede localizar oo2core_*.dll.");
        }

        foreach (var relative in RelativeCandidates)
        {
            var dir = Path.Combine(gameInstallDirectory, relative);
            if (!Directory.Exists(dir)) continue;

            var found = Directory.EnumerateFiles(dir, "oo2core_*.dll").FirstOrDefault();
            if (found is not null) return Result<string>.Success(found);
        }

        // Fallback: búsqueda recursiva (más lenta) por si la instalación tiene una estructura distinta.
        var recursive = Directory
            .EnumerateFiles(gameInstallDirectory, "oo2core_*.dll", SearchOption.AllDirectories)
            .FirstOrDefault();
        if (recursive is not null) return Result<string>.Success(recursive);

        return Result<string>.Failure(
            $"No se encontró oo2core_*.dll dentro de '{gameInstallDirectory}'. " +
            "Verificá que sea la carpeta de instalación correcta de FF7 Rebirth.");
    }
}
