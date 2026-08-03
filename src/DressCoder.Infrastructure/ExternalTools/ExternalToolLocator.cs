namespace DressCoder.Infrastructure.ExternalTools;

/// <summary>
/// Locates the repak/retoc executables bundled under tools/bin next to the app. These binaries
/// are NOT committed to source control (see .gitignore) — they must be downloaded once via
/// tools/download-tools.ps1. Both are freely redistributable (MIT/Apache-2.0), see
/// docs/04-licencias-terceros.md; only the runtime download avoids shipping binary blobs in git.
/// </summary>
public sealed class ExternalToolLocator
{
    private readonly string _toolsBinDirectory;

    public ExternalToolLocator(string? toolsBinDirectory = null)
    {
        _toolsBinDirectory = toolsBinDirectory ?? FindDefaultToolsBinDirectory();
    }

    public string RetocExePath => ResolveExecutable("retoc.exe");

    public string RepakExePath => ResolveExecutable("repak.exe");

    private string ResolveExecutable(string exeName)
    {
        var path = Path.Combine(_toolsBinDirectory, exeName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"No se encontró '{exeName}' en '{_toolsBinDirectory}'. " +
                "Ejecutá tools/download-tools.ps1 para descargar las herramientas externas (repak/retoc).",
                path);
        }

        return path;
    }

    /// <summary>
    /// Walks up from the app's base directory looking for a tools/bin folder, so this works
    /// both when run from bin/Debug during development and when published portably with a
    /// tools/bin folder shipped alongside the exe.
    /// </summary>
    private static string FindDefaultToolsBinDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "tools", "bin"),
            Path.Combine(AppContext.BaseDirectory, "..", "tools", "bin"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "tools", "bin"),
        };

        foreach (var candidate in candidates)
        {
            var full = Path.GetFullPath(candidate);
            if (Directory.Exists(full))
            {
                return full;
            }
        }

        // Fall back to the first candidate; ResolveExecutable() will raise a clear error
        // pointing at the download script if the tools truly aren't present.
        return Path.GetFullPath(candidates[0]);
    }
}
