namespace DressCoder.Core.Converter;

/// <summary>
/// Builds the final IoStore container (.pak/.utoc/.ucas) for a plugin from a directory of
/// raw chunks plus a manifest, by wrapping the external `retoc pack-raw` tool. Implemented
/// in Infrastructure. See docs/03-spike-tecnico-conclusiones.md section 4 for the confirmed
/// round-trip (unpack-raw -> modify -> pack-raw).
/// </summary>
public interface IContainerBuilder
{
    /// <summary>
    /// Packs <paramref name="rawChunksDirectory"/> (containing manifest.json + chunks/) into
    /// a new container at <paramref name="outputUtocPath"/> (siblings .pak/.ucas are created
    /// alongside it).
    /// </summary>
    Task<Models.Result> BuildAsync(string rawChunksDirectory, string outputUtocPath, CancellationToken ct = default);
}
