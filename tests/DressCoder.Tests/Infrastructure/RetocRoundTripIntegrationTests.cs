using DressCoder.Infrastructure.ExternalTools;
using Microsoft.Extensions.Logging.Abstractions;

namespace DressCoder.Tests.Infrastructure;

/// <summary>
/// End-to-end smoke test against the real retoc.exe binary and a real sample mod, validating
/// the confirmed unpack-raw -> pack-raw round trip (docs/03-spike-tecnico-conclusiones.md
/// section 4). Skips itself gracefully if tools/bin or example/ aren't present locally (e.g.
/// a clean CI checkout without the downloaded tools or the copyrighted sample mods) so it
/// never fails the build for reasons unrelated to the code under test.
/// </summary>
public class RetocRoundTripIntegrationTests
{
    private static string RepoRoot => FindRepoRoot();

    [Fact]
    public async Task ExtractAndRebuild_RealReplacerSample_RoundTripsSuccessfully()
    {
        var toolsBin = Path.Combine(RepoRoot, "tools", "bin");
        var examplesDir = Path.Combine(RepoRoot, "example", "replacer");

        if (!File.Exists(Path.Combine(toolsBin, "retoc.exe")))
        {
            Console.WriteLine("SKIP: retoc.exe no está presente en tools/bin (ejecutá tools/download-tools.ps1).");
            return;
        }

        if (!Directory.Exists(examplesDir))
        {
            Console.WriteLine("SKIP: example/replacer no está presente (contenido de terceros, no versionado).");
            return;
        }

        var sourceMod = Directory.GetDirectories(examplesDir).FirstOrDefault();
        if (sourceMod is null)
        {
            Console.WriteLine("SKIP: no hay ningún mod de ejemplo dentro de example/replacer.");
            return;
        }

        var tools = new ExternalToolLocator(toolsBin);
        var runner = new ProcessRunner(NullLogger<ProcessRunner>.Instance);
        var reader = new RetocPakReader(tools, runner, NullLogger<RetocPakReader>.Instance);
        var builder = new RetocContainerBuilder(tools, runner);

        var extractResult = await reader.ExtractAsync(sourceMod);
        Assert.True(extractResult.IsSuccess, extractResult.Error);

        var project = extractResult.Value!;
        Assert.NotEmpty(project.Chunks);
        Assert.True(Directory.Exists(Path.Combine(project.StagingDirectory, "chunks")));
        Assert.True(File.Exists(Path.Combine(project.StagingDirectory, "manifest.json")));

        var rebuildDir = Path.Combine(Path.GetTempPath(), "DressCoderTests_rebuild_" + Guid.NewGuid().ToString("N"));
        try
        {
            var outputUtoc = Path.Combine(rebuildDir, "Rebuilt-WindowsNoEditor.utoc");
            var buildResult = await builder.BuildAsync(project.StagingDirectory, outputUtoc);

            Assert.True(buildResult.IsSuccess, buildResult.Error);
            Assert.True(File.Exists(outputUtoc));
            Assert.True(File.Exists(Path.ChangeExtension(outputUtoc, ".ucas")));
        }
        finally
        {
            if (Directory.Exists(rebuildDir)) Directory.Delete(rebuildDir, recursive: true);
            if (Directory.Exists(project.StagingDirectory)) Directory.Delete(project.StagingDirectory, recursive: true);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "DressCoder.sln"))) return dir;
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        throw new InvalidOperationException("No se pudo encontrar la raíz del repo (DressCoder.sln) desde " + AppContext.BaseDirectory);
    }
}
