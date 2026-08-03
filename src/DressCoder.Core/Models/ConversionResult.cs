namespace DressCoder.Core.Models;

/// <summary>
/// Final output of a conversion: the assembled Dresscode plugin folder, ready to be
/// copied into {GameDir}/End/Mods/ or zipped for distribution. Layout confirmed against
/// a real Dresscode mod during the technical spike (docs/03-spike-tecnico-conclusiones.md):
///
///   {PluginName}/
///   ├── {PluginName}.uplugin
///   ├── Resources/Icon.png
///   └── Content/Paks/WindowsNoEditor/
///       ├── {PluginName}End-WindowsNoEditor.pak
///       ├── {PluginName}End-WindowsNoEditor.utoc
///       └── {PluginName}End-WindowsNoEditor.ucas
/// </summary>
public sealed class ConversionResult
{
    public required string PluginName { get; init; }

    /// <summary>Absolute path to the assembled plugin folder on disk.</summary>
    public required string OutputDirectory { get; init; }

    public required ValidationReport Validation { get; init; }

    /// <summary>True if this was produced by the standalone repack pipeline (Modo B) rather
    /// than an Unreal Engine cook (Modo A, not implemented in the MVP).</summary>
    public bool IsExperimentalRepack { get; init; } = true;
}
