namespace DressCoder.Core.Models;

/// <summary>
/// Result of analyzing a replacer mod's manifest: which character/outfit/weapon it targets,
/// derived automatically from the asset paths, along with a confidence score and alternative
/// candidates when the path is ambiguous (see Analyzer module).
/// </summary>
public sealed class DetectedTarget
{
    /// <summary>Whether this replacer targets a character outfit or a weapon.</summary>
    public required ModTargetType TargetType { get; init; }

    /// <summary>Matched character definition, if TargetType is Character. Null if not resolved automatically.</summary>
    public CharacterDefinition? Character { get; init; }

    /// <summary>Costume/outfit index as found in the path, e.g. "00" for Standard.</summary>
    public string? CostumeIndex { get; init; }

    /// <summary>Original in-game root path that this replacer overwrites, e.g.
    /// "Character/Player/PC0003_00_Aerith_Standard".</summary>
    public required string OriginalRootPath { get; init; }

    /// <summary>0.0-1.0 confidence in this detection.</summary>
    public double Confidence { get; init; } = 1.0;

    /// <summary>Other plausible candidates found, for the UI to let the user pick when ambiguous.</summary>
    public IReadOnlyList<DetectedTarget> AlternativeCandidates { get; init; } = [];
}

public enum ModTargetType
{
    Unknown,
    Character,
    Weapon,
}
