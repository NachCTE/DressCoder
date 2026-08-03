namespace DressCoder.Core.Models;

/// <summary>
/// User-provided (non-derivable) metadata required to build a Dresscode plugin,
/// collected in the "Configuración" screen. See docs/02-documento-tecnico.md section 3.
/// </summary>
public sealed class ModMetadataInput
{
    /// <summary>Plugin technical name (folder name, .uplugin name, pak prefix). No spaces.</summary>
    public required string PluginName { get; init; }

    /// <summary>Friendly name shown in Dresscode's menu; also used for grouping variants.</summary>
    public required string FriendlyName { get; init; }

    public string Description { get; init; } = string.Empty;
    public string CreatedBy { get; init; } = string.Empty;
    public string VersionName { get; init; } = "1.0.0";

    /// <summary>Optional manual override when auto-detection is ambiguous or wrong.</summary>
    public string? PlayerTypeOverride { get; init; }

    /// <summary>Optional explicit group key (see Dresscode custom grouping feature).</summary>
    public string? GroupKey { get; init; }

    /// <summary>Absolute path to a user-provided icon (PNG). Falls back to a bundled default if null.</summary>
    public string? IconPath { get; init; }

    /// <summary>Absolute path to a user-provided preview image/texture source, if any.</summary>
    public string? PreviewImagePath { get; init; }
}
