using DressCoder.Core.Models;

namespace DressCoder.Core.Converter;

/// <summary>
/// Rewrites a replacer project's manifest so that asset paths that used to overwrite the
/// original game location instead live under the new plugin's own namespace, e.g.:
///
///   ../../../End/Content/Character/Player/PC0003_00_Aerith_Standard/Model/PC0003_00.uasset
///   -> ../../../End/Mods/{PluginName}/Content/Skin/PC0003_00.uasset
///
/// This is required because Dresscode mods must not permanently overwrite the vanilla
/// asset; the character's Skeleton must still point at the original game path, but the
/// Skeletal Mesh/materials/textures the replacer brought must be relocated into the plugin.
/// See docs/03-spike-tecnico-conclusiones.md section 4.
/// </summary>
public interface IManifestRewriter
{
    /// <summary>
    /// Produces a new set of chunk path mappings (chunk id -> new internal path) for the
    /// target plugin. Does not touch the chunk file contents themselves (see
    /// <see cref="IDataAssetPatcher"/> for asset-internal reference patching).
    /// </summary>
    IReadOnlyDictionary<string, string> RewritePaths(ReplacerModProject project, string pluginName);
}
