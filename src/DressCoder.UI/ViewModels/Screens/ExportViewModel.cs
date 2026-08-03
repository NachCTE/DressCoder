using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DressCoder.Application.Services;
using DressCoder.Core.Converter;
using DressCoder.Core.Models;
using DressCoder.Core.Validator;
using Microsoft.Extensions.Logging;

namespace DressCoder.UI.ViewModels.Screens;

/// <summary>
/// "Exportación" screen: runs <see cref="IPluginAssembler"/> (Modo Simple/Wrapper — ver
/// docs/02) sobre el replacer importado y la metadata configurada, valida el resultado con
/// <see cref="IModValidator"/> y muestra la carpeta final lista para copiar a End/Mods/.
/// </summary>
public partial class ExportViewModel : ObservableObject
{
    private readonly IConversionSessionState _session;
    private readonly IPluginAssembler _assembler;
    private readonly IModValidator _validator;
    private readonly ILogger<ExportViewModel> _logger;

    [ObservableProperty]
    private string? outputDirectory;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "Elegí una carpeta de salida y presioná Exportar. " +
                                    "Necesitás haber importado un mod y guardado la configuración antes.";

    [ObservableProperty]
    private string? resultPluginDirectory;

    public ObservableCollection<string> ValidationMessages { get; } = new();

    public ExportViewModel(
        IConversionSessionState session,
        IPluginAssembler assembler,
        IModValidator validator,
        ILogger<ExportViewModel> logger)
    {
        _session = session;
        _assembler = assembler;
        _validator = validator;
        _logger = logger;

        OutputDirectory = Path.Combine(AppContext.BaseDirectory, "output");
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        ValidationMessages.Clear();
        ResultPluginDirectory = null;

        if (string.IsNullOrWhiteSpace(_session.SourcePath))
        {
            StatusMessage = "⚠ Todavía no importaste ningún mod (pantalla Importar Mod).";
            return;
        }

        if (_session.Metadata is null)
        {
            StatusMessage = "⚠ Todavía no guardaste la configuración del mod (pantalla Configuración).";
            return;
        }

        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            StatusMessage = "⚠ Elegí una carpeta de salida.";
            return;
        }

        IsBusy = true;
        StatusMessage = "Generando el plugin...";

        try
        {
            Directory.CreateDirectory(OutputDirectory);

            var result = await _assembler.AssembleAsync(_session.Metadata, _session.SourcePath, OutputDirectory);
            if (!result.IsSuccess)
            {
                StatusMessage = $"⚠ {result.Error}";
                _logger.LogWarning("Falló el ensamblado del plugin: {Error}", result.Error);
                return;
            }

            var conversion = result.Value!;
            var outputValidation = _validator.ValidateOutput(conversion.OutputDirectory);

            foreach (var issue in conversion.Validation.Issues.Concat(outputValidation.Issues))
            {
                ValidationMessages.Add($"[{issue.Severity}] {issue.Message}");
            }

            if (outputValidation.HasErrors)
            {
                StatusMessage = "⚠ El plugin se generó pero la validación final encontró errores. Revisá los mensajes.";
            }
            else
            {
                ResultPluginDirectory = conversion.OutputDirectory;
                StatusMessage = $"✔ Plugin generado en '{conversion.OutputDirectory}'. " +
                                 "Copiá esa carpeta dentro de <Juego>/End/Mods/ para probarlo.";
            }

            _logger.LogInformation("Exportación completa: {OutputDirectory}", conversion.OutputDirectory);
        }
        catch (Exception ex)
        {
            StatusMessage = $"⚠ Error inesperado: {ex.Message}";
            _logger.LogError(ex, "Error inesperado exportando el plugin");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
