using ContosoDashboard.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ContosoDashboard.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261020161000_DocumentManagementMvp")]
public sealed class DocumentManagementMvp : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("ALTER TABLE \"Notifications\" ADD COLUMN \"RelatedDocumentId\" TEXT NULL;");
        migrationBuilder.Sql("""
            CREATE TABLE "Documents" (
                "DocumentId" TEXT NOT NULL CONSTRAINT "PK_Documents" PRIMARY KEY,
                "Title" TEXT NOT NULL CHECK(length(trim("Title")) BETWEEN 1 AND 255),
                "Description" TEXT NULL, "Category" INTEGER NOT NULL,
                "OriginalFileName" TEXT NOT NULL, "DeclaredContentType" TEXT NULL,
                "DetectedContentType" TEXT NOT NULL, "Extension" TEXT NOT NULL,
                "SizeBytes" INTEGER NOT NULL CHECK("SizeBytes" > 0 AND "SizeBytes" <= 26214400),
                "StorageKey" TEXT NOT NULL, "StorageState" INTEGER NOT NULL,
                "UploaderUserId" INTEGER NOT NULL, "ProjectId" INTEGER NULL,
                "UploadedAtUtc" TEXT NOT NULL, "UpdatedAtUtc" TEXT NOT NULL, "Version" INTEGER NOT NULL,
                CONSTRAINT "FK_Documents_Users_UploaderUserId" FOREIGN KEY ("UploaderUserId") REFERENCES "Users" ("UserId") ON DELETE RESTRICT,
                CONSTRAINT "FK_Documents_Projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("ProjectId") ON DELETE RESTRICT);
            CREATE UNIQUE INDEX "IX_Documents_StorageKey" ON "Documents" ("StorageKey");
            CREATE INDEX "IX_Documents_UploaderUserId_UploadedAtUtc" ON "Documents" ("UploaderUserId", "UploadedAtUtc");
            CREATE INDEX "IX_Documents_ProjectId_UploadedAtUtc" ON "Documents" ("ProjectId", "UploadedAtUtc");
            """);
        migrationBuilder.Sql("""
            CREATE TABLE "DocumentTags" (
                "DocumentTagId" INTEGER NOT NULL CONSTRAINT "PK_DocumentTags" PRIMARY KEY AUTOINCREMENT,
                "DocumentId" TEXT NOT NULL, "DisplayValue" TEXT NOT NULL, "NormalizedValue" TEXT NOT NULL,
                CONSTRAINT "FK_DocumentTags_Documents_DocumentId" FOREIGN KEY ("DocumentId") REFERENCES "Documents" ("DocumentId") ON DELETE CASCADE);
            CREATE UNIQUE INDEX "IX_DocumentTags_DocumentId_NormalizedValue" ON "DocumentTags" ("DocumentId","NormalizedValue");
            """);
        migrationBuilder.Sql("""
            CREATE TABLE "DocumentShares" (
                "DocumentShareId" INTEGER NOT NULL CONSTRAINT "PK_DocumentShares" PRIMARY KEY AUTOINCREMENT,
                "DocumentId" TEXT NOT NULL, "RecipientUserId" INTEGER NULL, "Department" TEXT NULL,
                "GrantedByUserId" INTEGER NOT NULL, "GrantedAtUtc" TEXT NOT NULL,
                CONSTRAINT "CK_DocumentShares_ExactlyOneRecipient" CHECK(
                    ("RecipientUserId" IS NOT NULL AND "Department" IS NULL) OR
                    ("RecipientUserId" IS NULL AND "Department" IS NOT NULL AND length(trim("Department")) > 0)),
                CONSTRAINT "FK_DocumentShares_Documents_DocumentId" FOREIGN KEY ("DocumentId") REFERENCES "Documents" ("DocumentId") ON DELETE CASCADE,
                CONSTRAINT "FK_DocumentShares_Users_RecipientUserId" FOREIGN KEY ("RecipientUserId") REFERENCES "Users" ("UserId") ON DELETE RESTRICT,
                CONSTRAINT "FK_DocumentShares_Users_GrantedByUserId" FOREIGN KEY ("GrantedByUserId") REFERENCES "Users" ("UserId") ON DELETE RESTRICT);
            CREATE UNIQUE INDEX "IX_DocumentShares_DocumentId_RecipientUserId" ON "DocumentShares" ("DocumentId","RecipientUserId");
            CREATE UNIQUE INDEX "IX_DocumentShares_DocumentId_Department" ON "DocumentShares" ("DocumentId","Department");
            """);
        migrationBuilder.Sql("""
            CREATE TABLE "TaskDocumentAttachments" (
                "TaskId" INTEGER NOT NULL, "DocumentId" TEXT NOT NULL,
                "AttachedByUserId" INTEGER NOT NULL, "AttachedAtUtc" TEXT NOT NULL,
                CONSTRAINT "PK_TaskDocumentAttachments" PRIMARY KEY ("TaskId","DocumentId"),
                CONSTRAINT "FK_TaskDocumentAttachments_Tasks_TaskId" FOREIGN KEY ("TaskId") REFERENCES "Tasks" ("TaskId") ON DELETE CASCADE,
                CONSTRAINT "FK_TaskDocumentAttachments_Documents_DocumentId" FOREIGN KEY ("DocumentId") REFERENCES "Documents" ("DocumentId") ON DELETE CASCADE);
            """);
        migrationBuilder.Sql("""
            CREATE TABLE "DocumentActivities" (
                "DocumentActivityId" INTEGER NOT NULL CONSTRAINT "PK_DocumentActivities" PRIMARY KEY AUTOINCREMENT,
                "DocumentReferenceId" TEXT NOT NULL, "ActorUserId" INTEGER NOT NULL, "Action" INTEGER NOT NULL,
                "OccurredAtUtc" TEXT NOT NULL, "FileTypeAtEvent" TEXT NULL, "UploaderUserIdAtEvent" INTEGER NULL);
            CREATE INDEX "IX_DocumentActivities_OccurredAtUtc" ON "DocumentActivities" ("OccurredAtUtc");
            CREATE INDEX "IX_DocumentActivities_DocumentReferenceId" ON "DocumentActivities" ("DocumentReferenceId");
            """);
        migrationBuilder.Sql("""
            CREATE TABLE "DocumentFileOperations" (
                "DocumentFileOperationId" TEXT NOT NULL CONSTRAINT "PK_DocumentFileOperations" PRIMARY KEY,
                "DocumentId" TEXT NULL, "Kind" INTEGER NOT NULL, "State" INTEGER NOT NULL,
                "PreviousStorageKey" TEXT NULL, "CandidateStorageKey" TEXT NULL, "StagingKey" TEXT NULL,
                "StartedAtUtc" TEXT NOT NULL, "UpdatedAtUtc" TEXT NOT NULL, "FailureCode" TEXT NULL);
            CREATE INDEX "IX_DocumentFileOperations_State_UpdatedAtUtc" ON "DocumentFileOperations" ("State","UpdatedAtUtc");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS "DocumentFileOperations"; DROP TABLE IF EXISTS "DocumentActivities";
            DROP TABLE IF EXISTS "TaskDocumentAttachments"; DROP TABLE IF EXISTS "DocumentShares";
            DROP TABLE IF EXISTS "DocumentTags"; DROP TABLE IF EXISTS "Documents";
            """);
        migrationBuilder.Sql("ALTER TABLE \"Notifications\" DROP COLUMN \"RelatedDocumentId\";");
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 64);
        ApplicationDbContext.ConfigureModel(modelBuilder);
    }
}
