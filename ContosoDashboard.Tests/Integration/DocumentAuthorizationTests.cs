using System.Security.Claims;
using ContosoDashboard.Data;
using ContosoDashboard.Models;
using ContosoDashboard.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ContosoDashboard.Tests.Integration;

public sealed class DocumentAuthorizationTests
{
    [Fact]
    public async Task RemovedProjectUploaderIsDeniedDespiteOwnershipAndExistingShare()
    {
        await using var db = await CreateDatabaseAsync();
        var document = ProjectDocument("Removed uploader's private title");
        db.Documents.Add(document);
        db.DocumentShares.Add(new DocumentShare
        {
            DocumentId = document.DocumentId,
            RecipientUserId = 4,
            GrantedByUserId = 1,
            GrantedAtUtc = DateTime.UtcNow
        });
        db.ProjectMembers.Remove(await db.ProjectMembers.SingleAsync(m => m.ProjectId == 1 && m.UserId == 4));
        await db.SaveChangesAsync();

        var authorization = new DocumentAuthorizationService(db);
        var formerUploader = Principal(4, UserRole.Employee);
        var manager = Principal(2, UserRole.ProjectManager);
        var administrator = Principal(1, UserRole.Administrator);

        Assert.False(await authorization.CanAccessAsync(document.DocumentId, formerUploader));
        Assert.True(await authorization.CanAccessAsync(document.DocumentId, manager));
        Assert.True(await authorization.CanAccessAsync(document.DocumentId, administrator));

        var queries = new DocumentQueryService(db, authorization);
        var removedSearch = await queries.GetAccessibleDocumentsAsync(formerUploader,
            new DocumentLibraryQuery { Search = "private title" });
        var managerSearch = await queries.GetAccessibleDocumentsAsync(manager,
            new DocumentLibraryQuery { Search = "private title" });
        Assert.Empty(removedSearch);
        Assert.Single(managerSearch);
    }

    [Fact]
    public async Task ProjectRemovalDoesNotRevokeUnassociatedPersonalOwnership()
    {
        await using var db = await CreateDatabaseAsync();
        var document = new Document
        {
            Title = "Personal",
            Category = DocumentCategory.PersonalFiles,
            OriginalFileName = "notes.txt",
            DetectedContentType = "text/plain",
            Extension = "txt",
            SizeBytes = 5,
            StorageKey = Guid.NewGuid().ToString("N"),
            UploaderUserId = 4,
            UploadedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.Documents.Add(document);
        db.ProjectMembers.Remove(await db.ProjectMembers.SingleAsync(m => m.ProjectId == 1 && m.UserId == 4));
        await db.SaveChangesAsync();

        Assert.True(await new DocumentAuthorizationService(db).CanAccessAsync(
            document.DocumentId, Principal(4, UserRole.Employee)));
    }

    private static async Task<ApplicationDbContext> CreateDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        var db = new ApplicationDbContext(options);
        await db.Database.OpenConnectionAsync();
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static Document ProjectDocument(string title) => new()
    {
        Title = title,
        Category = DocumentCategory.ProjectDocuments,
        OriginalFileName = "project.pdf",
        DetectedContentType = "application/pdf",
        Extension = "pdf",
        SizeBytes = 8,
        StorageKey = Guid.NewGuid().ToString("N"),
        UploaderUserId = 4,
        ProjectId = 1,
        UploadedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    private static ClaimsPrincipal Principal(int id, UserRole role) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, id.ToString()),
            new Claim(ClaimTypes.Role, role.ToString())
        ], "test"));
}
