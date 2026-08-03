using DressCoder.Infrastructure.ExternalTools;

namespace DressCoder.Tests.Infrastructure;

public class ExternalToolLocatorTests : IDisposable
{
    private readonly string _tempRoot;

    public ExternalToolLocatorTests()
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
    public void RetocExePath_WhenExeExistsInGivenDirectory_ReturnsItsPath()
    {
        var expected = Path.Combine(_tempRoot, "retoc.exe");
        File.WriteAllText(expected, "");
        var locator = new ExternalToolLocator(_tempRoot);

        Assert.Equal(expected, locator.RetocExePath);
    }

    [Fact]
    public void RepakExePath_WhenExeMissing_ThrowsWithHelpfulMessage()
    {
        var locator = new ExternalToolLocator(_tempRoot);

        var ex = Assert.Throws<FileNotFoundException>(() => locator.RepakExePath);
        Assert.Contains("download-tools.ps1", ex.Message);
    }
}
