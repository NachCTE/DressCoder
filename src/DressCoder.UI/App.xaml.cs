using System.IO;
using System.Windows;
using DressCoder.Application;
using DressCoder.UI.Navigation;
using DressCoder.UI.ViewModels;
using DressCoder.UI.ViewModels.Screens;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DressCoder.UI;

/// <summary>
/// Composition root: builds the generic host (DI container + logging), resolves MainWindow
/// through it, and owns the host's lifetime. Kept as a thin wiring layer — no business logic.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var logDirectory = AppPaths.LogDirectory;

        var builder = Host.CreateApplicationBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddDebug();
        builder.Logging.AddDressCoderFileLogging(logDirectory);
        builder.Logging.SetMinimumLevel(LogLevel.Information);

        builder.Services.AddDressCoderApplication();
        builder.Services.AddSingleton<INavigationService, NavigationService>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<HomeViewModel>();
        builder.Services.AddSingleton<ImportViewModel>();
        builder.Services.AddSingleton<AnalysisViewModel>();
        builder.Services.AddSingleton<ConfigurationViewModel>();
        builder.Services.AddSingleton<PreviewViewModel>();
        builder.Services.AddSingleton<ExportViewModel>();
        builder.Services.AddSingleton<LogViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
        _host.Start();

        var logger = _host.Services.GetRequiredService<ILogger<App>>();
        logger.LogInformation("DressCoder iniciado. Logs en: {LogDirectory}", logDirectory);

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.Services.GetRequiredService<ILogger<App>>().LogInformation("DressCoder cerrando.");
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}

