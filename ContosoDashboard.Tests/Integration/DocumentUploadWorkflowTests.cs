using System.Security.Claims;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Xunit;

namespace ContosoDashboard.Tests.Integration;

public sealed class DocumentUploadWorkflowTests
{
    [Fact]
    public async Task MissingLocalScannerFailsClosedAndRemovesStagedContent()
    {
        var temp = Path.Combine(Path.GetTempPath(), "document-upload-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var privateRoot = Path.Combine(temp, "private");
        var environment = new TestWebHostEnvironment(temp, Path.Combine(temp, "wwwroot"));
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var dbOptions = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection).Options;
        try
        {
            await using var db = new ApplicationDbContext(dbOptions);
            await db.Database.EnsureCreatedAsync();
            var options = new DocumentOptions
            {
                StorageRoot = privateRoot,
                ClamScanPath = Path.Combine(temp, "missing-clamscan")
            };
            var storage = new DocumentFileStorage(Options.Create(options), environment);
            var scanner = new LocalClamAvScanner(Options.Create(options), Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalClamAvScanner>.Instance);
            var authorization = new DocumentAuthorizationService(db);
            var service = new DocumentUploadService(db, storage, scanner, new DocumentFileValidator(), authorization,
                Options.Create(options), Microsoft.Extensions.Logging.Abstractions.NullLogger<DocumentUploadService>.Instance);
            using var payload = new MemoryStream("%PDF-1.7"u8.ToArray());

            var result = await service.UploadAsync(new UploadRequest
            {
                Title = "Safe report",
                Category = DocumentCategory.Reports,
                FileName = "Q4 Report (2025) - Finance & Ops.pdf",
                DeclaredContentType = "application/pdf",
                Payload = payload
            }, Principal(4));

            Assert.False(result.Success);
            Assert.Equal("scanner-unavailable", result.ErrorCode);
            Assert.Equal(0, await db.Documents.CountAsync());
            Assert.Empty(Directory.GetFiles(Path.Combine(privateRoot, "staging")));
            Assert.False(Directory.GetFiles(privateRoot, "*", SearchOption.AllDirectories).Any());
            Assert.DoesNotContain(await db.DocumentFileOperations.Select(o => o.State).ToListAsync(),
                state => state != DocumentFileOperationState.Failed);
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public async Task SizeLimitDuringStagingCleansPartialFile()
    {
        var temp = Path.Combine(Path.GetTempPath(), "document-storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var environment = new TestWebHostEnvironment(temp, Path.Combine(temp, "wwwroot"));
        try
        {
            var storage = new DocumentFileStorage(Options.Create(new DocumentOptions
            {
                StorageRoot = Path.Combine(temp, "private")
            }), environment);
            using var input = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });
            await Assert.ThrowsAsync<DocumentSizeLimitException>(() => storage.SaveStagedAsync(
                input, "safe.txt", storage.NewStagingKey(), 4));
            Assert.Empty(Directory.GetFiles(Path.Combine(temp, "private", "staging")));
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private static ClaimsPrincipal Principal(int userId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId.ToString())], "test"));

    private sealed class TestWebHostEnvironment(string contentRoot, string webRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "ContosoDashboard.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = webRoot;
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
