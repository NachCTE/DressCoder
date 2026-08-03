using DressCoder.Core.Models;

namespace DressCoder.Core.Analyzer;

/// <summary>
/// Community-sourced lookup of FF7 Rebirth character codes (e.g. "PC0003") to friendly
/// names and Dresscode PlayerType enum values. Must be updatable without recompiling the
/// app (see docs/02-documento-tecnico.md section 9, "mejoras futuras").
/// </summary>
public interface ICharacterCatalog
{
    /// <summary>Looks up a character by its in-game code (e.g. "PC0003" -> Aerith). Null if unknown.</summary>
    CharacterDefinition? FindByCode(string code);

    IReadOnlyList<CharacterDefinition> All { get; }
}
