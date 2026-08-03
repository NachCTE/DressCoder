namespace DressCoder.Core.Models;

/// <summary>
/// A chunk annotated with the analyzer's classification and confidence, ready for
/// display in the UI's "detected assets" tree and for use by the converter.
/// </summary>
public sealed class DetectedAsset
{
    public required AssetChunk Chunk { get; init; }
    public required AssetKind Kind { get; init; }

    /// <summary>0.0-1.0 confidence score for the Kind classification.</summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>Human-readable reason for the classification, shown in the UI (e.g. "prefix SK_" or "class SkeletalMesh").</summary>
    public string? Reason { get; init; }
}
