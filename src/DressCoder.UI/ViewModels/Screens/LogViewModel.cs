using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DressCoder.UI.ViewModels.Screens;

/// <summary>
/// "Log de errores" screen: shows the content of today's rolling log file (see
/// DressCoder.Infrastructure.Logging.FileLoggerProvider), with a manual refresh command
/// since the file is appended to outside of any UI-observable event.
/// </summary>
public partial class LogViewModel : ObservableObject
{
    [ObservableProperty]
    private string logContent = string.Empty;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public LogViewModel()
    {
        Refresh();
    }

    [RelayCommand]
    private void Refresh()
    {
        try
        {
            var logFile = Path.Combine(AppPaths.LogDirectory, $"dresscoder-{DateTime.Now:yyyy-MM-dd}.log");
            if (File.Exists(logFile))
            {
                using var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                LogContent = reader.ReadToEnd();
                StatusMessage = $"Log: {logFile}";
            }
            else
            {
                LogContent = string.Empty;
                StatusMessage = "Todavía no hay entradas de log hoy.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"No se pudo leer el log: {ex.Message}";
        }
    }
}
