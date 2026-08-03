using DressCoder.Core.Models;

namespace DressCoder.Core.Parser;

/// <summary>
/// Reads a replacer mod's IoStore container (.pak/.utoc/.ucas) and extracts its chunks
/// and manifest into a staging directory, producing a <see cref="ReplacerModProject"/>.
/// Implemented in Infrastructure as a wrapper around the external `retoc` tool
/// (see docs/03-spike-tecnico-conclusiones.md).
/// </summary>
public interface IPakReader
{
    /// <summary>
    /// Extracts the given .pak/.utoc/.ucas triplet (identified by any one of the three paths,
    /// or a folder containing them) into a staging directory and parses its manifest.
    /// </summary>
    Task<Result<ReplacerModProject>> ExtractAsync(string sourcePath, CancellationToken ct = default);
}
