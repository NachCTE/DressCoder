using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DressCoder.Application.Services;
using DressCoder.Core.Models;

namespace DressCoder.UI.ViewModels.Screens;

/// <summary>
/// "Configuración" screen: collects the minimum, non-derivable metadata needed to assemble
/// a Dresscode plugin (see docs/02-documento-tecnico.md, "qué información debe pedirle al
/// usuario"). Saves the result into <see cref="IConversionSessionState"/> so the Export
/// screen can pick it up.
/// </summary>
public partial class ConfigurationViewModel : ObservableObject
{
    private readonly IConversionSessionState _session;

    [ObservableProperty]
    private string pluginName = string.Empty;

    [ObservableProperty]
    private string friendlyName = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;

    [ObservableProperty]
    private string createdBy = string.Empty;

    [ObservableProperty]
    private string versionName = "1.0.0";

    [ObservableProperty]
    private string? iconPath;

    [ObservableProperty]
    private string statusMessage = "Completá los datos del mod. El nombre del plugin no puede tener espacios.";

    public ConfigurationViewModel(IConversionSessionState session)
    {
        _session = session;

        if (!string.IsNullOrWhiteSpace(session.SourceName))
        {
            var suggested = new string(session.SourceName!.Where(char.IsLetterOrDigit).ToArray());
            PluginName = suggested;
            FriendlyName = session.SourceName!;
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (string.IsNullOrWhiteSpace(PluginName) || PluginName.Contains(' ') ||
            PluginName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            StatusMessage = "⚠ El nombre del plugin es obligatorio, sin espacios ni caracteres inválidos.";
            return;
        }

        _session.Metadata = new ModMetadataInput
        {
            PluginName = PluginName.Trim(),
            FriendlyName = string.IsNullOrWhiteSpace(FriendlyName) ? PluginName.Trim() : FriendlyName.Trim(),
            Description = Description.Trim(),
            CreatedBy = CreatedBy.Trim(),
            VersionName = string.IsNullOrWhiteSpace(VersionName) ? "1.0.0" : VersionName.Trim(),
            IconPath = string.IsNullOrWhiteSpace(IconPath) ? null : IconPath,
        };

        StatusMessage = "✔ Configuración guardada. Continuá en Exportación.";
    }
}
