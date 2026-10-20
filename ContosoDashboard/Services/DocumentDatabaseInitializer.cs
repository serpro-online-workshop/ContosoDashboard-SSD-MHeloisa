using System.Data;
using Microsoft.EntityFrameworkCore;
using ContosoDashboard.Data;

namespace ContosoDashboard.Services;

public sealed class DocumentDatabaseInitializer
{
    private const string BaselineMigrationId = "20261020160000_InitialEnsureCreatedSchema";
    private const string DocumentMigrationId = "20261020161000_DocumentManagementMvp";
    private static readonly IReadOnlyDictionary<string, string[]> FeatureColumns = new Dictionary<string, string[]>
    {
        ["Documents"] = ["DocumentId", "Title", "Description", "Category", "OriginalFileName", "DeclaredContentType",
            "DetectedContentType", "Extension", "SizeBytes", "StorageKey", "StorageState", "UploaderUserId",
            "ProjectId", "UploadedAtUtc", "UpdatedAtUtc", "Version"],
        ["DocumentTags"] = ["DocumentTagId", "DocumentId", "DisplayValue", "NormalizedValue"],
        ["DocumentShares"] = ["DocumentShareId", "DocumentId", "RecipientUserId", "Department", "GrantedByUserId", "GrantedAtUtc"],
        ["TaskDocumentAttachments"] = ["TaskId", "DocumentId", "AttachedByUserId", "AttachedAtUtc"],
        ["DocumentActivities"] = ["DocumentActivityId", "DocumentReferenceId", "ActorUserId", "Action", "OccurredAtUtc", "FileTypeAtEvent", "UploaderUserIdAtEvent"],
        ["DocumentFileOperations"] = ["DocumentFileOperationId", "DocumentId", "Kind", "State", "PreviousStorageKey",
            "CandidateStorageKey", "StagingKey", "StartedAtUtc", "UpdatedAtUtc", "FailureCode"]
    };
    private static readonly IReadOnlyDictionary<string, string[]> LegacyColumns = new Dictionary<string, string[]>
    {
        ["Users"] = ["UserId", "Email", "DisplayName", "Department", "JobTitle", "Role", "ProfilePhotoUrl",
            "AvailabilityStatus", "CreatedDate", "LastLoginDate", "PhoneNumber", "EmailNotificationsEnabled", "InAppNotificationsEnabled"],
        ["Projects"] = ["ProjectId", "Name", "Description", "ProjectManagerId", "StartDate",
            "TargetCompletionDate", "Status", "CreatedDate", "UpdatedDate"],
        ["Tasks"] = ["TaskId", "Title", "Description", "Priority", "Status", "DueDate", "AssignedUserId",
            "CreatedByUserId", "ProjectId", "CreatedDate", "UpdatedDate"],
        ["ProjectMembers"] = ["ProjectMemberId", "ProjectId", "UserId", "Role", "AssignedDate"],
        ["TaskComments"] = ["CommentId", "TaskId", "UserId", "CommentText", "CreatedDate"],
        ["Notifications"] = ["NotificationId", "UserId", "Title", "Message", "Type", "Priority", "IsRead", "CreatedDate"],
        ["Announcements"] = ["AnnouncementId", "Title", "Content", "CreatedByUserId", "PublishDate", "ExpiryDate", "IsActive"]
    };

    private readonly ApplicationDbContext _db;
    private readonly ILogger<DocumentDatabaseInitializer> _logger;

    public DocumentDatabaseInitializer(ApplicationDbContext db, ILogger<DocumentDatabaseInitializer> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var connection = _db.Database.GetDbConnection();
        var closeWhenDone = connection.State != ConnectionState.Open;
        if (closeWhenDone) await connection.OpenAsync(cancellationToken);
        try
        {
            var tables = await ReadTableNamesAsync(connection, cancellationToken);
            var hasHistory = tables.Contains("__EFMigrationsHistory");
            if (tables.Count == 0)
            {
                _logger.LogInformation("Applying migrations to a fresh local database.");
            }
            else if (!hasHistory)
            {
                await ValidateLegacyAndStampAsync(connection, tables, hasHistory: false, cancellationToken);
            }
            else
            {
                var applied = await ReadAppliedMigrationIdsAsync(connection, cancellationToken);
                var known = new HashSet<string>([BaselineMigrationId, DocumentMigrationId], StringComparer.Ordinal);
                if (applied.Any(id => !known.Contains(id)))
                    throw new InvalidOperationException("The database contains an unknown EF migration. No document migration was applied.");

                if (applied.Contains(DocumentMigrationId))
                {
                    await ValidateFeatureSchemaAsync(connection, tables, cancellationToken);
                }
                else if (applied.Contains(BaselineMigrationId))
                {
                    await ValidateLegacySchemaAsync(connection, tables, hasHistory: true, cancellationToken);
                }
                else if (applied.Count == 0 && tables.Count == 1)
                {
                    // An empty history table is equivalent to a fresh database.
                    _logger.LogInformation("Applying migrations to an empty local database.");
                }
                else if (applied.Count == 0)
                {
                    await ValidateLegacyAndStampAsync(connection, tables, hasHistory: true, cancellationToken);
                }
                else
                {
                    throw new InvalidOperationException("The database migration history is incomplete or unknown. No document migration was applied.");
                }
            }
        }
        finally
        {
            if (closeWhenDone) await connection.CloseAsync();
        }

        await _db.Database.MigrateAsync(cancellationToken);
    }

    private static async Task ValidateLegacyAndStampAsync(
        System.Data.Common.DbConnection connection,
        HashSet<string> existingTables,
        bool hasHistory,
        CancellationToken cancellationToken)
    {
        await ValidateLegacySchemaAsync(connection, existingTables, hasHistory, cancellationToken);
        if (!hasHistory)
        {
            await using var createHistory = connection.CreateCommand();
            createHistory.CommandText = """
                CREATE TABLE "__EFMigrationsHistory" (
                    "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                    "ProductVersion" TEXT NOT NULL);
                """;
            await createHistory.ExecuteNonQueryAsync(cancellationToken);
        }

        await using var stampBaseline = connection.CreateCommand();
        stampBaseline.CommandText = """
            INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
            VALUES ('20261020160000_InitialEnsureCreatedSchema', '8.0.0');
            """;
        await stampBaseline.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ValidateLegacySchemaAsync(
        System.Data.Common.DbConnection connection,
        HashSet<string> existingTables,
        bool hasHistory,
        CancellationToken cancellationToken)
    {
        var expectedTables = LegacyColumns.Keys.ToHashSet(StringComparer.Ordinal);
        if (hasHistory) expectedTables.Add("__EFMigrationsHistory");
        if (!existingTables.SetEquals(expectedTables))
            throw new InvalidOperationException(
                "The existing database is not the complete recognized EnsureCreated schema. No migration history was changed. Back up and inspect the database before continuing.");

        foreach (var (table, expectedColumns) in LegacyColumns)
        {
            var actual = await ReadColumnsAsync(connection, table, cancellationToken);
            if (!actual.SetEquals(expectedColumns))
                throw new InvalidOperationException(
                    $"The existing database table '{table}' does not match the recognized schema. No migration history was changed.");
        }
    }

    private static async Task ValidateFeatureSchemaAsync(
        System.Data.Common.DbConnection connection,
        HashSet<string> existingTables,
        CancellationToken cancellationToken)
    {
        var expected = LegacyColumns.Keys.Concat(FeatureColumns.Keys)
            .Append("__EFMigrationsHistory").ToHashSet(StringComparer.Ordinal);
        if (!existingTables.SetEquals(expected))
            throw new InvalidOperationException("The applied document migration history does not match the complete feature schema.");

        foreach (var (table, expectedColumns) in LegacyColumns.Concat(FeatureColumns))
        {
            var actual = await ReadColumnsAsync(connection, table, cancellationToken);
            var expectedSet = expectedColumns.ToHashSet(StringComparer.Ordinal);
            if (table == "Notifications") expectedSet.Add("RelatedDocumentId");
            if (!actual.SetEquals(expectedSet))
                throw new InvalidOperationException($"The applied migration history does not match the expected '{table}' schema.");
        }
    }

    private static async Task<HashSet<string>> ReadTableNamesAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%';";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var tables = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken)) tables.Add(reader.GetString(0));
        return tables;
    }

    private static async Task<HashSet<string>> ReadAppliedMigrationIdsAsync(
        System.Data.Common.DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MigrationId FROM \"__EFMigrationsHistory\";";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var migrations = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken)) migrations.Add(reader.GetString(0));
        return migrations;
    }

    private static async Task<HashSet<string>> ReadColumnsAsync(
        System.Data.Common.DbConnection connection,
        string table,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\");";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = new HashSet<string>(StringComparer.Ordinal);
        while (await reader.ReadAsync(cancellationToken)) columns.Add(reader.GetString(1));
        return columns;
    }
}
