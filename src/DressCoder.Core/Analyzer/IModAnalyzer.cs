using DressCoder.Core.Models;

namespace DressCoder.Core.Analyzer;

/// <summary>
/// Inspects a <see cref="ReplacerModProject"/>'s chunks/manifest and produces asset
/// classifications and target (character/outfit/weapon) detections, purely from static
/// analysis of internal paths and file naming conventions -- no Unreal Engine required.
/// See docs/02-documento-tecnico.md section 2 for what can/cannot be derived automatically.
/// </summary>
public interface IModAnalyzer
{
    /// <summary>Classifies every chunk in the project (SkeletalMesh, MaterialInstance, Texture, ...).</summary>
    IReadOnlyList<DetectedAsset> ClassifyAssets(ReplacerModProject project);

    /// <summary>
    /// Detects which character/outfit/weapon this replacer targets, based on its internal
    /// asset root path(s). May return multiple candidates when ambiguous (e.g. a replacer
    /// touching more than one character), which the UI must let the user resolve.
    /// </summary>
    IReadOnlyList<DetectedTarget> DetectTargets(ReplacerModProject project);
}
