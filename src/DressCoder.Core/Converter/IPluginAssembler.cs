using DressCoder.Core.Models;

namespace DressCoder.Core.Converter;

/// <summary>
/// Assembles the final Dresscode plugin folder structure (.uplugin + Resources/Icon.png +
/// Content/Paks/WindowsNoEditor/*) confirmed in docs/01-investigacion-dresscode.md section 3.
/// </summary>
public interface IPluginAssembler
{
    /// <summary>
    /// Creates the plugin folder at <paramref name="outputDirectory"/>\{PluginName}, writes
    /// the .uplugin manifest, copies/generates the icon, and places the container files
    /// produced by <see cref="IContainerBuilder"/> under Content/Paks/WindowsNoEditor/.
    /// </summary>
    Task<Result<ConversionResult>> AssembleAsync(
        ModMetadataInput metadata,
        string containerUtocPath,
        string outputDirectory,
        CancellationToken ct = default);
}
