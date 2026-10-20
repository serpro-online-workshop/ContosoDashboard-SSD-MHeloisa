using ContosoDashboard.Data;
using ContosoDashboard.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ContosoDashboard.Tests.Integration;

public sealed class DocumentDatabaseMigrationTests
{
    [Fact]
    public async Task FreshDatabaseAppliesBaselineAndDocumentMigrations()
    {
        var temp = Path.Combine(Path.GetTempPath(), "document-migration-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var path = Path.Combine(temp, "fresh.db");
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;
            await using var db = new ApplicationDbContext(options);
            var initializer = new DocumentDatabaseInitializer(db, NullLogger<DocumentDatabaseInitializer>.Instance);

            await initializer.InitializeAsync();

            Assert.Contains("20261020160000_InitialEnsureCreatedSchema", await db.Database.GetAppliedMigrationsAsync());
            Assert.Contains("20261020161000_DocumentManagementMvp", await db.Database.GetAppliedMigrationsAsync());
            Assert.Equal(4, await db.Users.CountAsync());
            Assert.True(await TableExistsAsync(path, "Documents"));
            Assert.True(await TableExistsAsync(path, "DocumentFileOperations"));
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public async Task RecognizedEnsureCreatedDatabaseIsAdoptedWithoutRecreatingItsTrainingData()
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var source = Path.Combine(repositoryRoot, "ContosoDashboard", "ContosoDashboard.db");
        Assert.True(File.Exists(source), "The repository training database fixture must exist.");

        var temp = Path.Combine(Path.GetTempPath(), "document-legacy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var path = Path.Combine(temp, "legacy.db");
        File.Copy(source, path);
        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;
            await using var db = new ApplicationDbContext(options);
            var before = await db.Users.OrderBy(u => u.UserId).Select(u => new { u.UserId, u.Email }).ToListAsync();

            await new DocumentDatabaseInitializer(db, NullLogger<DocumentDatabaseInitializer>.Instance).InitializeAsync();

            var after = await db.Users.OrderBy(u => u.UserId).Select(u => new { u.UserId, u.Email }).ToListAsync();
            Assert.Equal(before, after);
            Assert.True(await TableExistsAsync(path, "Documents"));
            Assert.Contains("20261020161000_DocumentManagementMvp", await db.Database.GetAppliedMigrationsAsync());
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    [Fact]
    public async Task UnknownPartialSchemaStopsWithoutCreatingMigrationHistory()
    {
        var temp = Path.Combine(Path.GetTempPath(), "document-partial-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        var path = Path.Combine(temp, "partial.db");
        try
        {
            await using (var connection = new SqliteConnection($"Data Source={path}"))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE Users (UserId INTEGER PRIMARY KEY);";
                await command.ExecuteNonQueryAsync();
            }

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite($"Data Source={path}")
                .Options;
            await using var db = new ApplicationDbContext(options);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new DocumentDatabaseInitializer(db, NullLogger<DocumentDatabaseInitializer>.Instance).InitializeAsync());
            Assert.False(await TableExistsAsync(path, "__EFMigrationsHistory"));
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }

    private static async Task<bool> TableExistsAsync(string path, string table)
    {
        await using var connection = new SqliteConnection($"Data Source={path}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name=$name";
        command.Parameters.AddWithValue("$name", table);
        return await command.ExecuteScalarAsync() is not null;
    }
}
