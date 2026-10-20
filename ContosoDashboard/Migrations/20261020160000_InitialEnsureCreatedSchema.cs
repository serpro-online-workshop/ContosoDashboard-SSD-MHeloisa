using ContosoDashboard.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ContosoDashboard.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20261020160000_InitialEnsureCreatedSchema")]
public sealed class InitialEnsureCreatedSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE "Users" (
                "UserId" INTEGER NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY AUTOINCREMENT,
                "Email" TEXT NOT NULL, "DisplayName" TEXT NOT NULL, "Department" TEXT NULL,
                "JobTitle" TEXT NULL, "Role" INTEGER NOT NULL, "ProfilePhotoUrl" TEXT NULL,
                "AvailabilityStatus" INTEGER NOT NULL, "CreatedDate" TEXT NOT NULL, "LastLoginDate" TEXT NULL,
                "PhoneNumber" TEXT NULL, "EmailNotificationsEnabled" INTEGER NOT NULL,
                "InAppNotificationsEnabled" INTEGER NOT NULL);
            CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");
            """);
        migrationBuilder.Sql("""
            CREATE TABLE "Projects" (
                "ProjectId" INTEGER NOT NULL CONSTRAINT "PK_Projects" PRIMARY KEY AUTOINCREMENT,
                "Name" TEXT NOT NULL, "Description" TEXT NULL, "ProjectManagerId" INTEGER NOT NULL,
                "StartDate" TEXT NOT NULL, "TargetCompletionDate" TEXT NULL, "Status" INTEGER NOT NULL,
                "CreatedDate" TEXT NOT NULL, "UpdatedDate" TEXT NOT NULL,
                CONSTRAINT "FK_Projects_Users_ProjectManagerId" FOREIGN KEY ("ProjectManagerId") REFERENCES "Users" ("UserId") ON DELETE RESTRICT);
            CREATE INDEX "IX_Projects_ProjectManagerId" ON "Projects" ("ProjectManagerId");
            CREATE INDEX "IX_Projects_Status" ON "Projects" ("Status");
            """);
        migrationBuilder.Sql("""
            CREATE TABLE "Tasks" (
                "TaskId" INTEGER NOT NULL CONSTRAINT "PK_Tasks" PRIMARY KEY AUTOINCREMENT,
                "Title" TEXT NOT NULL, "Description" TEXT NULL, "Priority" INTEGER NOT NULL, "Status" INTEGER NOT NULL,
                "DueDate" TEXT NULL, "AssignedUserId" INTEGER NOT NULL, "CreatedByUserId" INTEGER NOT NULL,
                "ProjectId" INTEGER NULL, "CreatedDate" TEXT NOT NULL, "UpdatedDate" TEXT NOT NULL,
                CONSTRAINT "FK_Tasks_Projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("ProjectId"),
                CONSTRAINT "FK_Tasks_Users_AssignedUserId" FOREIGN KEY ("AssignedUserId") REFERENCES "Users" ("UserId") ON DELETE RESTRICT,
                CONSTRAINT "FK_Tasks_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("UserId") ON DELETE RESTRICT);
            CREATE INDEX "IX_Tasks_AssignedUserId" ON "Tasks" ("AssignedUserId");
            CREATE INDEX "IX_Tasks_Status" ON "Tasks" ("Status");
            CREATE INDEX "IX_Tasks_DueDate" ON "Tasks" ("DueDate");
            CREATE INDEX "IX_Tasks_ProjectId" ON "Tasks" ("ProjectId");
            CREATE INDEX "IX_Tasks_CreatedByUserId" ON "Tasks" ("CreatedByUserId");
            """);
        migrationBuilder.Sql("""
            CREATE TABLE "ProjectMembers" (
                "ProjectMemberId" INTEGER NOT NULL CONSTRAINT "PK_ProjectMembers" PRIMARY KEY AUTOINCREMENT,
                "ProjectId" INTEGER NOT NULL, "UserId" INTEGER NOT NULL, "Role" TEXT NOT NULL, "AssignedDate" TEXT NOT NULL,
                CONSTRAINT "FK_ProjectMembers_Projects_ProjectId" FOREIGN KEY ("ProjectId") REFERENCES "Projects" ("ProjectId") ON DELETE CASCADE,
                CONSTRAINT "FK_ProjectMembers_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("UserId") ON DELETE CASCADE);
            CREATE INDEX "IX_ProjectMembers_ProjectId" ON "ProjectMembers" ("ProjectId");
            CREATE INDEX "IX_ProjectMembers_UserId" ON "ProjectMembers" ("UserId");
            """);
        migrationBuilder.Sql("""
            CREATE TABLE "TaskComments" (
                "CommentId" INTEGER NOT NULL CONSTRAINT "PK_TaskComments" PRIMARY KEY AUTOINCREMENT,
                "TaskId" INTEGER NOT NULL, "UserId" INTEGER NOT NULL, "CommentText" TEXT NOT NULL, "CreatedDate" TEXT NOT NULL,
                CONSTRAINT "FK_TaskComments_Tasks_TaskId" FOREIGN KEY ("TaskId") REFERENCES "Tasks" ("TaskId") ON DELETE CASCADE,
                CONSTRAINT "FK_TaskComments_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("UserId") ON DELETE CASCADE);
            CREATE INDEX "IX_TaskComments_TaskId" ON "TaskComments" ("TaskId");
            CREATE INDEX "IX_TaskComments_UserId" ON "TaskComments" ("UserId");
            """);
        migrationBuilder.Sql("""
            CREATE TABLE "Notifications" (
                "NotificationId" INTEGER NOT NULL CONSTRAINT "PK_Notifications" PRIMARY KEY AUTOINCREMENT,
                "UserId" INTEGER NOT NULL, "Title" TEXT NOT NULL, "Message" TEXT NOT NULL, "Type" INTEGER NOT NULL,
                "Priority" INTEGER NOT NULL, "IsRead" INTEGER NOT NULL, "CreatedDate" TEXT NOT NULL,
                CONSTRAINT "FK_Notifications_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("UserId") ON DELETE CASCADE);
            CREATE INDEX "IX_Notifications_UserId_IsRead" ON "Notifications" ("UserId", "IsRead");
            """);
        migrationBuilder.Sql("""
            CREATE TABLE "Announcements" (
                "AnnouncementId" INTEGER NOT NULL CONSTRAINT "PK_Announcements" PRIMARY KEY AUTOINCREMENT,
                "Title" TEXT NOT NULL, "Content" TEXT NOT NULL, "CreatedByUserId" INTEGER NOT NULL,
                "PublishDate" TEXT NOT NULL, "ExpiryDate" TEXT NULL, "IsActive" INTEGER NOT NULL,
                CONSTRAINT "FK_Announcements_Users_CreatedByUserId" FOREIGN KEY ("CreatedByUserId") REFERENCES "Users" ("UserId") ON DELETE CASCADE);
            CREATE INDEX "IX_Announcements_CreatedByUserId" ON "Announcements" ("CreatedByUserId");
            """);

        migrationBuilder.Sql("""
            INSERT INTO "Users" ("UserId","Email","DisplayName","Department","JobTitle","Role","AvailabilityStatus","CreatedDate","EmailNotificationsEnabled","InAppNotificationsEnabled")
            VALUES
             (1,'admin@contoso.com','System Administrator','IT','Administrator',3,0,'2026-10-20T00:00:00Z',1,1),
             (2,'camille.nicole@contoso.com','Camille Nicole','Engineering','Project Manager',2,0,'2026-10-20T00:00:00Z',1,1),
             (3,'floris.kregel@contoso.com','Floris Kregel','Engineering','Team Lead',1,0,'2026-10-20T00:00:00Z',1,1),
             (4,'ni.kang@contoso.com','Ni Kang','Engineering','Software Engineer',0,0,'2026-10-20T00:00:00Z',1,1);
            INSERT INTO "Projects" ("ProjectId","Name","Description","ProjectManagerId","StartDate","TargetCompletionDate","Status","CreatedDate","UpdatedDate")
            VALUES (1,'ContosoDashboard Development','Internal employee productivity dashboard',2,'2026-09-20T00:00:00Z','2026-12-19T00:00:00Z',1,'2026-09-20T00:00:00Z','2026-10-20T00:00:00Z');
            INSERT INTO "Tasks" ("TaskId","Title","Description","Priority","Status","DueDate","AssignedUserId","CreatedByUserId","ProjectId","CreatedDate","UpdatedDate")
            VALUES
             (1,'Design database schema','Create entity relationship diagram and database design',2,2,'2026-09-30T00:00:00Z',4,2,1,'2026-09-20T00:00:00Z','2026-09-30T00:00:00Z'),
             (2,'Implement authentication','Set up Microsoft Entra ID authentication',3,1,'2026-10-25T00:00:00Z',4,2,1,'2026-09-25T00:00:00Z','2026-10-20T00:00:00Z'),
             (3,'Create UI mockups','Design user interface mockups for all main pages',1,0,'2026-10-30T00:00:00Z',4,2,1,'2026-09-30T00:00:00Z','2026-09-30T00:00:00Z');
            INSERT INTO "ProjectMembers" ("ProjectMemberId","ProjectId","UserId","Role","AssignedDate")
            VALUES (1,1,3,'TeamLead','2026-09-20T00:00:00Z'),(2,1,4,'Developer','2026-09-20T00:00:00Z');
            INSERT INTO "Announcements" ("AnnouncementId","Title","Content","CreatedByUserId","PublishDate","ExpiryDate","IsActive")
            VALUES (1,'Welcome to ContosoDashboard','Welcome to the new ContosoDashboard application. This platform will help you manage your tasks and projects more efficiently.',1,'2026-10-20T00:00:00Z','2026-11-19T00:00:00Z',1);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TABLE IF EXISTS "Announcements"; DROP TABLE IF EXISTS "Notifications";
            DROP TABLE IF EXISTS "TaskComments"; DROP TABLE IF EXISTS "ProjectMembers";
            DROP TABLE IF EXISTS "Tasks"; DROP TABLE IF EXISTS "Projects"; DROP TABLE IF EXISTS "Users";
            """);
    }

    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "8.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 64);
        ApplicationDbContext.ConfigureModel(modelBuilder);
    }
}
