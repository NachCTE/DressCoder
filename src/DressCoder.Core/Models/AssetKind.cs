namespace DressCoder.Core.Models;

/// <summary>
/// Classifies the role a chunk/asset plays within a replacer mod, inferred from
/// file name prefix, extension and internal path conventions observed in FF7R assets.
/// </summary>
public enum AssetKind
{
    Unknown,
    SkeletalMesh,
    MaterialInstance,
    Texture,
    Blueprint,
    Animation,
    Vfx,
    BulkData,
    ContainerHeader,
    ExportBundleData,
}
