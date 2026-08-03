using DressCoder.Infrastructure.ExternalTools;

namespace DressCoder.Application.Services;

/// <inheritdoc cref="IStartupDiagnosticsService"/>
public sealed class StartupDiagnosticsService : IStartupDiagnosticsService
{
    private readonly ExternalToolLocator _tools;

    public StartupDiagnosticsService(ExternalToolLocator tools)
    {
        _tools = tools;
    }

    public StartupDiagnostics CheckExternalTools()
    {
        var retocOk = TryResolve(() => _tools.RetocExePath, out var retocError);
        var repakOk = TryResolve(() => _tools.RepakExePath, out var repakError);

        return new StartupDiagnostics(retocOk, repakOk, retocError, repakError);
    }

    private static bool TryResolve(Func<string> resolve, out string? error)
    {
        try
        {
            resolve();
            error = null;
            return true;
        }
        catch (FileNotFoundException ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
