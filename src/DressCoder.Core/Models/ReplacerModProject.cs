namespace DressCoder.Core.Models;

/// <summary>
/// A replacer mod loaded into the app: its source container files, the extracted
/// manifest/chunks, and the analysis results. This is the central aggregate the
/// whole pipeline (Import -> Analyze -> Configure -> Export) operates on.
/// </summary>
public sealed class ReplacerModProject
{
    /// <summary>Display name derived from the source file/folder name.</summary>
    public required string Name { get; init; }

    /// <summary>Path to the source .pak/.utoc/.ucas triplet (or folder containing them).</summary>
    public required string SourcePath { get; init; }

    /// <summary>Mount point reported by the container (usually "../../../").</summary>
    public string MountPoint { get; init; } = "../../../";

    /// <summary>All chunks extracted from the container.</summary>
    public IReadOnlyList<AssetChunk> Chunks { get; init; } = [];

    /// <summary>Chunks annotated with their detected asset kind.</summary>
    public IReadOnlyList<DetectedAsset> DetectedAssets { get; init; } = [];

    /// <summary>Detected conversion target(s). A replacer touching multiple characters/outfits
    /// at once will have more than one entry here (rare, but must be supported per the "multiple
    /// candidates" requirement).</summary>
    public IReadOnlyList<DetectedTarget> DetectedTargets { get; init; } = [];

    /// <summary>Absolute path to the temp/staging directory where chunks were extracted.</summary>
    public required string StagingDirectory { get; init; }
}
