using System.Text.Json;
using DressCoder.Core.Models;

namespace DressCoder.Infrastructure.Assembly;

/// <summary>
/// Writes the {PluginName}.uplugin JSON manifest. Layout confirmed against a real Dresscode
/// mod (docs/03-spike-tecnico-conclusiones.md section 1): plain JSON, no "Modules" array
/// (the plugin only carries content, no native code).
/// </summary>
internal static class UpluginWriter
{
    public static async Task WriteAsync(string upluginPath, ModMetadataInput metadata, CancellationToken ct = default)
    {
        var document = new
        {
            FileVersion = 3,
            Version = 1,
            VersionName = metadata.VersionName,
            FriendlyName = metadata.FriendlyName,
            Description = metadata.Description,
            Category = "Modding",
            CreatedBy = metadata.CreatedBy,
            CreatedByURL = "",
            DocsURL = "",
            MarketplaceURL = "",
            SupportURL = "",
            CanContainContent = true,
            IsBetaVersion = false,
            IsExperimentalVersion = false,
            Installed = false,
        };

        var json = JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(upluginPath, json, ct);
    }
}
