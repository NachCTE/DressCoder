using DressCoder.Core.Models;

namespace DressCoder.Core.Converter;

/// <summary>
/// Generates the two Unreal DataAsset chunks Dresscode requires (DA_ModMetaData and the
/// PDA_ModData_Character/_Weapon "ModData"), by binary-patching known string fields inside
/// pre-built template chunks. Templates are Zen-format packages (not readable by UAssetAPI,
/// which only supports legacy .uasset) extracted once from a reference plugin and embedded
/// in the app -- see docs/03-spike-tecnico-conclusiones.md section 5.1 and 5.4.
/// </summary>
public interface IDataAssetPatcher
{
    /// <summary>
    /// Produces the DA_ModMetaData chunk bytes for the given metadata (FriendlyName,
    /// Description, CreatedBy, VersionName).
    /// </summary>
    byte[] BuildModMetaData(ModMetadataInput metadata);

    /// <summary>
    /// Produces the ModData chunk bytes (PDA_ModData_Character or _Weapon) referencing the
    /// relocated Skeletal Mesh path and the resolved PlayerType.
    /// </summary>
    byte[] BuildModData(ModMetadataInput metadata, DetectedTarget target, string relocatedSkeletalMeshPath);
}
