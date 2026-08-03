using DressCoder.Infrastructure.ExternalTools;

namespace DressCoder.Tests.Infrastructure;

/// <summary>
/// Tests for the pure path-resolution logic in <see cref="RetocPakReader"/> that doesn't
/// require invoking the real retoc.exe subprocess.
/// </summary>
public class RetocPakReaderTests : IDisposable
{
    private readonly string _tempRoot;

    public RetocPakReaderTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "DressCoderTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    [Fact]
    public void ResolveUtocPath_GivenPakFile_ReturnsSiblingUtoc()
    {
        var utoc = CreateFile("Mod-WindowsNoEditor.utoc");
        CreateFile("Mod-WindowsNoEditor.pak");
        var pak = Path.Combine(_tempRoot, "Mod-WindowsNoEditor.pak");

        var resolved = RetocPakReader.ResolveUtocPath(pak);

        Assert.Equal(utoc, resolved);
    }

    [Fact]
    public void ResolveUtocPath_GivenUcasFile_ReturnsSiblingUtoc()
    {
        var utoc = CreateFile("Mod-WindowsNoEditor.utoc");
        CreateFile("Mod-WindowsNoEditor.ucas");
        var ucas = Path.Combine(_tempRoot, "Mod-WindowsNoEditor.ucas");

        var resolved = RetocPakReader.ResolveUtocPath(ucas);

        Assert.Equal(utoc, resolved);
    }

    [Fact]
    public void ResolveUtocPath_GivenFolder_FindsUtocRecursively()
    {
        var nested = Path.Combine(_tempRoot, "Content", "Paks", "WindowsNoEditor");
        Directory.CreateDirectory(nested);
        var utoc = Path.Combine(nested, "Mod-WindowsNoEditor.utoc");
        File.WriteAllText(utoc, "");

        var resolved = RetocPakReader.ResolveUtocPath(_tempRoot);

        Assert.Equal(utoc, resolved);
    }

    [Fact]
    public void ResolveUtocPath_GivenFolderWithoutUtoc_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => RetocPakReader.ResolveUtocPath(_tempRoot));
    }

    [Fact]
    public void ResolveUtocPath_GivenUnrelatedExtension_Throws()
    {
        var txt = CreateFile("readme.txt");

        Assert.Throws<FileNotFoundException>(() => RetocPakReader.ResolveUtocPath(txt));
    }

    [Fact]
    public void ResolveUtocPath_GivenPakWithoutSiblingUtoc_Throws()
    {
        var pak = CreateFile("Orphan.pak");

        Assert.Throws<FileNotFoundException>(() => RetocPakReader.ResolveUtocPath(pak));
    }

    [Fact]
    public void ResolveUtocPath_GivenNonExistentPath_Throws()
    {
        var missing = Path.Combine(_tempRoot, "does-not-exist.pak");

        Assert.Throws<FileNotFoundException>(() => RetocPakReader.ResolveUtocPath(missing));
    }

    private string CreateFile(string relativeName)
    {
        var path = Path.Combine(_tempRoot, relativeName);
        File.WriteAllText(path, "");
        return path;
    }
}
