namespace DressCoder.Core.Models;

/// <summary>
/// Represents a single raw chunk extracted from an IoStore container (.utoc/.ucas)
/// via `retoc unpack-raw`. Mirrors an entry in the tool's manifest.json.
/// </summary>
public sealed class AssetChunk
{
    /// <summary>Chunk identifier as reported by retoc (hex string, e.g. "1b2132876dddb74200000002").</summary>
    public required string ChunkId { get; init; }

    /// <summary>
    /// Mount-relative internal path as recorded in the manifest, e.g.
    /// "../../../End/Content/Character/Player/PC0003_00_Aerith_Standard/Model/PC0003_00.uasset".
    /// </summary>
    public required string InternalPath { get; init; }

    /// <summary>Absolute path to the raw chunk file on disk after extraction.</summary>
    public required string RawFilePath { get; init; }

    /// <summary>File extension without the dot (uasset, uexp, ubulk, ...), lowercase.</summary>
    public string Extension => Path.GetExtension(InternalPath).TrimStart('.').ToLowerInvariant();

    /// <summary>File name without directory, e.g. "PC0003_00.uasset".</summary>
    public string FileName => Path.GetFileName(InternalPath);
}
