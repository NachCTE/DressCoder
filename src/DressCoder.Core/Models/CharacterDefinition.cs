namespace DressCoder.Core.Models;

/// <summary>
/// A known FF7 Rebirth playable character, used to resolve the "PC00XX" style
/// codes found in replacer asset paths to a friendly name and Dresscode's PlayerType enum value.
/// This dictionary is community-sourced and must be extensible without recompiling the app
/// (see Configuration module).
/// </summary>
public sealed class CharacterDefinition
{
    /// <summary>Internal game code, e.g. "PC0003".</summary>
    public required string Code { get; init; }

    /// <summary>Friendly display name, e.g. "Aerith".</summary>
    public required string Name { get; init; }

    /// <summary>Value expected by Dresscode's EPlayerType enum for this character.</summary>
    public required string PlayerType { get; init; }
}
