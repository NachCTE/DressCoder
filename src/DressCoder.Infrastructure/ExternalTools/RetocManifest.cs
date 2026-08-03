using System.Text.Json.Serialization;

namespace DressCoder.Infrastructure.ExternalTools;

/// <summary>
/// DTO matching the manifest.json produced by `retoc unpack-raw` / consumed by `retoc pack-raw`.
/// See docs/03-spike-tecnico-conclusiones.md section 4 for the confirmed shape:
/// { "chunk_paths": { chunkId: internalPath }, "version": "DirectoryIndex", "mount_point": "../../../" }.
/// </summary>
internal sealed class RetocManifest
{
    [JsonPropertyName("chunk_paths")]
    public Dictionary<string, string> ChunkPaths { get; set; } = new();

    [JsonPropertyName("version")]
    public string Version { get; set; } = "DirectoryIndex";

    [JsonPropertyName("mount_point")]
    public string MountPoint { get; set; } = "../../../";
}
