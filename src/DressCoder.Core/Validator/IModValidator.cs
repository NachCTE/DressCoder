using DressCoder.Core.Models;

namespace DressCoder.Core.Validator;

/// <summary>
/// Checks the hard Dresscode rules and common error conditions before export, per
/// docs/02-documento-tecnico.md section 4 ("Reglas duras") and section 5 (validaciones):
///   1. Exactly one DA_ModMetaData per plugin, under MetaData/.
///   2. At most one ModData DataAsset per type (Character/Weapon) per plugin.
///   3. Skeleton references must still point at the original game path.
///   4. Material slot names on the custom mesh must match EndMaterialPack entries, if used.
///   5. Output layout must match the exact confirmed plugin structure.
/// Also checks for broken references, missing assets and naming conflicts with existing
/// mods in the target End/Mods/ folder.
/// </summary>
public interface IModValidator
{
    ValidationReport Validate(ReplacerModProject project, ModMetadataInput metadata);

    /// <summary>Validates the final assembled output on disk (post-generation sanity check).</summary>
    ValidationReport ValidateOutput(string pluginOutputDirectory);
}
