using System.Security.Claims;
using ContosoDashboard.Endpoints;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ContosoDashboard.Tests.Web;

public sealed class DocumentContentEndpointTests
{
    [Fact]
    public async Task UnauthenticatedAndDeniedRequestsReturnNoDocumentBytes()
    {
        var id = Guid.NewGuid();
        var context = NewContext(id.ToString("D"));
        var services = new FakeServices(null, "%PDF-1.7"u8.ToArray());
        var unauthenticated = await DocumentContentEndpoint.HandleAsync(id.ToString("D"), context,
            services.Query, services.Storage, services.Audit, services.LoggerFactory);
        Assert.Equal(StatusCodes.Status401Unauthorized,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(unauthenticated).StatusCode);
        Assert.Empty(((MemoryStream)context.Response.Body).ToArray());

        context = NewContext(id.ToString("D"));
        context.User = Principal(4);
        var denied = await DocumentContentEndpoint.HandleAsync(id.ToString("D"), context,
            services.Query, services.Storage, services.Audit, services.LoggerFactory);
        Assert.Equal(StatusCodes.Status404NotFound,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(denied).StatusCode);
        Assert.Empty(((MemoryStream)context.Response.Body).ToArray());
    }

    [Fact]
    public async Task MalformedIdentifierAndInvalidModeAreRejected()
    {
        var services = new FakeServices(null, []);
        var context = NewContext("not-a-guid");
        context.User = Principal(4);
        var malformed = await DocumentContentEndpoint.HandleAsync("not-a-guid", context,
            services.Query, services.Storage, services.Audit, services.LoggerFactory);
        Assert.Equal(StatusCodes.Status400BadRequest,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(malformed).StatusCode);

        context = NewContext(Guid.NewGuid().ToString("D") + "?mode=upload");
        context.User = Principal(4);
        context.Request.QueryString = new QueryString("?mode=upload");
        var invalidMode = await DocumentContentEndpoint.HandleAsync(Guid.NewGuid().ToString("D"), context,
            services.Query, services.Storage, services.Audit, services.LoggerFactory);
        Assert.Equal(StatusCodes.Status400BadRequest,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(invalidMode).StatusCode);
    }

    [Fact]
    public async Task StreamsCurrentContentWithSafeHeadersAndInlineAllowList()
    {
        var document = SampleDocument("application/pdf", "Q4 Report (2025) - Finance & Ops.pdf");
        var bytes = "%PDF-1.7"u8.ToArray();
        document.SizeBytes = bytes.Length;
        var services = new FakeServices(document, bytes);
        var context = NewContext(document.DocumentId.ToString("D"));
        context.User = Principal(4);
        context.Request.QueryString = new QueryString("?mode=inline");

        var result = await DocumentContentEndpoint.HandleAsync(document.DocumentId.ToString("D"), context,
            services.Query, services.Storage, services.Audit, services.LoggerFactory);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.EmptyHttpResult>(result);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal("application/pdf", context.Response.ContentType);
        Assert.Equal("nosniff", context.Response.Headers["X-Content-Type-Options"]);
        Assert.Contains("inline", context.Response.Headers["Content-Disposition"].ToString());
        Assert.Contains("filename*=utf-8''", context.Response.Headers["Content-Disposition"].ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&", context.Response.Headers["Content-Disposition"].ToString());
        Assert.DoesNotContain("\r", context.Response.Headers["Content-Disposition"].ToString());
        Assert.DoesNotContain("\n", context.Response.Headers["Content-Disposition"].ToString());
        Assert.Equal(bytes, ((MemoryStream)context.Response.Body).ToArray());
        Assert.Equal(1, services.Audit.Records);
    }

    [Fact]
    public async Task UnsupportedInlineTypeHasDownloadAlternativeWithoutOpeningStorage()
    {
        var document = SampleDocument("text/plain", "notes.txt");
        var services = new FakeServices(document, "hello"u8.ToArray());
        var context = NewContext(document.DocumentId.ToString("D"));
        context.User = Principal(4);
        context.Request.QueryString = new QueryString("?mode=inline");

        var result = await DocumentContentEndpoint.HandleAsync(document.DocumentId.ToString("D"), context,
            services.Query, services.Storage, services.Audit, services.LoggerFactory);

        Assert.Equal(StatusCodes.Status400BadRequest,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal(0, services.Storage.OpenCount);
    }

    private static DefaultHttpContext NewContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/document-content/" + path.Split('?')[0];
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static ClaimsPrincipal Principal(int id) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test"));

    private static Document SampleDocument(string contentType, string filename) => new()
    {
        DocumentId = Guid.NewGuid(),
        Title = "Authorized title",
        OriginalFileName = filename,
        DetectedContentType = contentType,
        Extension = Path.GetExtension(filename).TrimStart('.'),
        SizeBytes = 8,
        StorageKey = Guid.NewGuid().ToString("N"),
        UploaderUserId = 4,
        UploadedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    private sealed class FakeServices
    {
        public FakeServices(Document? document, byte[] bytes)
        {
            Query = new FakeQuery(document);
            Storage = new FakeStorage(bytes);
            Audit = new FakeAudit();
            LoggerFactory = Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance;
        }
        public IDocumentQueryService Query { get; }
        public FakeStorage Storage { get; }
        public FakeAudit Audit { get; }
        public ILoggerFactory LoggerFactory { get; }
    }

    private sealed class FakeQuery(Document? document) : IDocumentQueryService
    {
        public Task<List<Document>> GetAccessibleDocumentsAsync(ClaimsPrincipal principal, DocumentLibraryQuery? query = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new List<Document>());
        public Task<Document?> GetDocumentForUserAsync(Guid documentId, ClaimsPrincipal principal, CancellationToken cancellationToken = default) =>
            Task.FromResult(document?.DocumentId == documentId ? document : null);
    }

    private sealed class FakeStorage(byte[] bytes) : IDocumentFileStorage
    {
        public int OpenCount { get; private set; }
        public string NewStorageKey() => Guid.NewGuid().ToString("N");
        public string NewStagingKey() => Guid.NewGuid().ToString("N");
        public string GetStagedFilePath(string stagingKey) => throw new NotSupportedException();
        public Task<long> SaveStagedAsync(Stream input, string originalFileName, string stagingKey, long maximumBytes, IProgress<long>? progress = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task FinalizeToStorageAsync(string stagingKey, string storageKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteStagedIfExistsAsync(string stagingKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Stream> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            OpenCount++;
            return Task.FromResult<Stream>(new MemoryStream(bytes, writable: false));
        }
    }

    private sealed class FakeAudit : IDocumentAuditService
    {
        public int Records { get; private set; }
        public Task RecordAsync(Guid documentId, int actorUserId, DocumentActivityAction action, string? contentType = null, int? uploaderUserIdAtEvent = null)
        {
            Records++;
            return Task.CompletedTask;
        }
    }
}
