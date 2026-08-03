using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DressCoder.Application.Services;
using DressCoder.Core.Parser;
using Microsoft.Extensions.Logging;

namespace DressCoder.UI.ViewModels.Screens;

/// <summary>
/// "Importar Mod" screen: lets the user pick a .pak/.utoc/.ucas file (or a folder containing
/// them) and extracts it via <see cref="IPakReader"/> (retoc unpack-raw under the hood),
/// showing the detected asset paths as a flat tree. Asset classification/target detection
/// (IModAnalyzer) is not implemented yet — see docs/02-documento-tecnico.md Etapa 6.
/// </summary>
public partial class ImportViewModel : ObservableObject
{
    private readonly IPakReader _pakReader;
    private readonly IConversionSessionState _session;
    private readonly ILogger<ImportViewModel> _logger;

    [ObservableProperty]
    private string? sourcePath;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "Elegí un archivo .pak/.utoc/.ucas o una carpeta de mod para empezar.";

    public ObservableCollection<string> DetectedAssetPaths { get; } = new();

    public ImportViewModel(IPakReader pakReader, IConversionSessionState session, ILogger<ImportViewModel> logger)
    {
        _pakReader = pakReader;
        _session = session;
        _logger = logger;
    }

    [RelayCommand]
    private async Task ImportAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        IsBusy = true;
        StatusMessage = $"Extrayendo '{path}'...";
        DetectedAssetPaths.Clear();

        try
        {
            var result = await _pakReader.ExtractAsync(path);
            if (result.IsSuccess)
            {
                var project = result.Value!;
                SourcePath = path;
                _session.SourcePath = path;
                _session.SourceName = project.Name;
                foreach (var chunk in project.Chunks.OrderBy(c => c.InternalPath))
                {
                    DetectedAssetPaths.Add(chunk.InternalPath);
                }

                StatusMessage = $"✔ {project.Chunks.Count} assets detectados en '{project.Name}'. " +
                                 "Continuá en Configuración para generar el mod Dresscode.";
                _logger.LogInformation("Mod importado: {Name} ({Count} chunks)", project.Name, project.Chunks.Count);
            }
            else
            {
                StatusMessage = $"⚠ {result.Error}";
                _logger.LogWarning("Fallo al importar '{Path}': {Error}", path, result.Error);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"⚠ Error inesperado: {ex.Message}";
            _logger.LogError(ex, "Error inesperado importando '{Path}'", path);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
