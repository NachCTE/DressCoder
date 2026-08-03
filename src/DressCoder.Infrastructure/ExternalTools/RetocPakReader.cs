using System.Text.Json;
using DressCoder.Core.Models;
using DressCoder.Core.Parser;
using Microsoft.Extensions.Logging;

namespace DressCoder.Infrastructure.ExternalTools;

/// <summary>
/// Wraps `retoc unpack-raw` to extract a replacer's IoStore container (.pak/.utoc/.ucas)
/// into a staging directory (manifest.json + chunks/) and parses it into a
/// <see cref="ReplacerModProject"/>. See docs/03-spike-tecnico-conclusiones.md section 4.
/// </summary>
public sealed class RetocPakReader : IPakReader
{
    private readonly ExternalToolLocator _tools;
    private readonly ProcessRunner _processRunner;
    private readonly ILogger<RetocPakReader> _logger;

    public RetocPakReader(ExternalToolLocator tools, ProcessRunner processRunner, ILogger<RetocPakReader> logger)
    {
        _tools = tools;
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<Result<ReplacerModProject>> ExtractAsync(string sourcePath, CancellationToken ct = default)
    {
        string utocPath;
        try
        {
            utocPath = ResolveUtocPath(sourcePath);
        }
        catch (FileNotFoundException ex)
        {
            return Result<ReplacerModProject>.Failure(ex.Message);
        }

        string retocExePath;
        try
        {
            retocExePath = _tools.RetocExePath;
        }
        catch (FileNotFoundException ex)
        {
            return Result<ReplacerModProject>.Failure(ex.Message);
        }

        // retoc requiere que el directorio de salida NO exista todavía (ver spike), por eso
        // solo calculamos el path acá y dejamos que retoc lo cree.
        var stagingDirectory = CreateStagingDirectoryPath(sourcePath);
        Directory.CreateDirectory(Path.GetDirectoryName(stagingDirectory)!);

        ProcessExecutionResult processResult;
        try
        {
            processResult = await _processRunner.RunAsync(
                retocExePath,
                ["unpack-raw", utocPath, stagingDirectory],
                ct: ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<ReplacerModProject>.Failure($"No se pudo ejecutar retoc: {ex.Message}");
        }

        var manifestPath = Path.Combine(stagingDirectory, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            return Result<ReplacerModProject>.Failure(
                $"retoc no generó manifest.json para '{sourcePath}'. Salida: {processResult.StdErr}");
        }

        // NOTA: retoc puede emitir por stderr un warning sobre el ContainerHeader (formato
        // custom del engine de FF7R, ver docs/03 sección 5.4) y aun así extraer los chunks
        // correctamente. No tratamos ese warning como fallo si el manifest se generó.
        if (!processResult.Succeeded)
        {
            _logger.LogWarning(
                "retoc unpack-raw devolvió código {ExitCode} pero generó manifest.json; continuando. StdErr: {StdErr}",
                processResult.ExitCode, processResult.StdErr);
        }

        RetocManifest manifest;
        try
        {
            var json = await File.ReadAllTextAsync(manifestPath, ct);
            manifest = JsonSerializer.Deserialize<RetocManifest>(json)
                       ?? throw new InvalidOperationException("manifest.json deserializó a null");
        }
        catch (Exception ex)
        {
            return Result<ReplacerModProject>.Failure($"No se pudo leer manifest.json: {ex.Message}");
        }

        var chunksDirectory = Path.Combine(stagingDirectory, "chunks");
        var chunks = new List<AssetChunk>();
        foreach (var (chunkId, internalPath) in manifest.ChunkPaths)
        {
            var rawFilePath = Path.Combine(chunksDirectory, chunkId);
            if (!File.Exists(rawFilePath))
            {
                _logger.LogWarning(
                    "Chunk {ChunkId} referenciado en manifest pero no encontrado en disco ({Path})",
                    chunkId, rawFilePath);
                continue;
            }

            chunks.Add(new AssetChunk
            {
                ChunkId = chunkId,
                InternalPath = internalPath,
                RawFilePath = rawFilePath,
            });
        }

        var project = new ReplacerModProject
        {
            Name = Path.GetFileNameWithoutExtension(utocPath),
            SourcePath = sourcePath,
            MountPoint = manifest.MountPoint,
            Chunks = chunks,
            StagingDirectory = stagingDirectory,
        };

        return Result<ReplacerModProject>.Success(project);
    }

    /// <summary>
    /// Accepts a .pak/.utoc/.ucas file (any one of the triplet) or a folder containing them,
    /// and returns the .utoc path retoc expects.
    /// </summary>
    internal static string ResolveUtocPath(string sourcePath)
    {
        if (Directory.Exists(sourcePath))
        {
            var found = Directory.EnumerateFiles(sourcePath, "*.utoc", SearchOption.AllDirectories).FirstOrDefault();
            if (found is null)
            {
                throw new FileNotFoundException($"No se encontró ningún archivo .utoc dentro de '{sourcePath}'.");
            }

            return found;
        }

        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"No se encontró el archivo o carpeta '{sourcePath}'.");
        }

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension is not (".utoc" or ".ucas" or ".pak"))
        {
            throw new FileNotFoundException(
                $"'{sourcePath}' no es un .pak/.utoc/.ucas ni una carpeta que los contenga.");
        }

        var utoc = Path.ChangeExtension(sourcePath, ".utoc");
        if (!File.Exists(utoc))
        {
            throw new FileNotFoundException($"No se encontró el archivo .utoc correspondiente a '{sourcePath}'.");
        }

        return utoc;
    }

    private static string CreateStagingDirectoryPath(string sourcePath)
    {
        var baseName = Directory.Exists(sourcePath)
            ? new DirectoryInfo(sourcePath).Name
            : Path.GetFileNameWithoutExtension(sourcePath);

        var root = Path.Combine(Path.GetTempPath(), "DressCoder", "staging");

        // retoc exige que el directorio de salida no exista; usamos un sufijo único por
        // extracción para poder correr varias veces sin colisionar (docs/03 nota sobre retoc).
        return Path.Combine(root, $"{baseName}_{Guid.NewGuid():N}");
    }
}
