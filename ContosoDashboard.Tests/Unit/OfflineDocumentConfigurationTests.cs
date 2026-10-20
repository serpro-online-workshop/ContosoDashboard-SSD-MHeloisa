using ContosoDashboard.Services;
using Xunit;

namespace ContosoDashboard.Tests.Unit;

public sealed class OfflineDocumentConfigurationTests
{
    [Fact]
    public void RequiresLocalScannerConfigurationAndFixedMaximum()
    {
        var options = new DocumentOptions();
        options.Validate();
        Assert.Equal("clamscan", options.ClamScanPath);
        Assert.Equal(26_214_400, options.MaxFileSizeBytes);

        Assert.Throws<InvalidOperationException>(() => new DocumentOptions { ClamScanPath = "" }.Validate());
        Assert.Throws<InvalidOperationException>(() => new DocumentOptions { MaxFileSizeBytes = 100 }.Validate());
    }

    [Fact]
    public void RejectsStorageInsideStaticFileRoot()
    {
        var temp = Path.Combine(Path.GetTempPath(), "document-config-" + Guid.NewGuid().ToString("N"));
        var contentRoot = Path.Combine(temp, "app");
        var webRoot = Path.Combine(contentRoot, "wwwroot");
        Directory.CreateDirectory(webRoot);
        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                DocumentOptions.ResolveAndValidateStorageRoot(contentRoot, webRoot, "wwwroot/uploads"));
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public void ResolvesLocalStorageOutsideStaticRootWithoutNetworkDependencies()
    {
        var temp = Path.Combine(Path.GetTempPath(), "document-config-" + Guid.NewGuid().ToString("N"));
        var contentRoot = Path.Combine(temp, "app");
        var webRoot = Path.Combine(contentRoot, "wwwroot");
        Directory.CreateDirectory(webRoot);
        try
        {
            var resolved = DocumentOptions.ResolveAndValidateStorageRoot(contentRoot, webRoot, "App_Data/Documents");
            Assert.StartsWith(contentRoot, resolved, StringComparison.Ordinal);
            Assert.DoesNotContain(Path.Combine("wwwroot", "Documents"), resolved, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }
}
