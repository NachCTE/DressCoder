namespace DressCoder.Application.Services;

/// <summary>
/// Result of checking whether the external tools (repak/retoc) the app depends on are
/// available at startup. Surfaced in the UI so the user gets a clear message (with a pointer
/// to tools/download-tools.ps1) instead of a cryptic failure the first time they try to import.
/// </summary>
public sealed record StartupDiagnostics(
    bool RetocAvailable,
    bool RepakAvailable,
    string? RetocError,
    string? RepakError)
{
    public bool AllToolsAvailable => RetocAvailable && RepakAvailable;
}

/// <summary>
/// Checks the application's environment (external tools) at startup. Implemented in
/// Application (not Infrastructure directly) so the UI layer only depends on Application,
/// keeping the module boundaries from docs/02-documento-tecnico.md intact.
/// </summary>
public interface IStartupDiagnosticsService
{
    StartupDiagnostics CheckExternalTools();
}
