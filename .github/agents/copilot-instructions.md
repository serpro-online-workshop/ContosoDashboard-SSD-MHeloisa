# ContosoDashboard-SSD-MHeloisa Development Guidelines

Auto-generated from feature planning context. Last updated: 2026-10-20

## Active Technologies

- C# 12 / .NET 8 + ASP.NET Core 8 Blazor Server (001-document-management)
- EF Core 8 + SQLite
- Local filesystem storage outside `wwwroot`; local ClamAV scanner

## Project Structure

```text
ContosoDashboard/
├── Data/
├── Models/
├── Services/
├── Pages/
├── Shared/
└── Program.cs
ContosoDashboard.Tests/   # planned test project
specs/001-document-management/
```

## Commands

```sh
dotnet build ContosoDashboard/ContosoDashboard.csproj
dotnet test ContosoDashboard.Tests/ContosoDashboard.Tests.csproj
dotnet run --project ContosoDashboard/ContosoDashboard.csproj
```

## Code Style

C# 12: follow existing nullable-enabled conventions, keep UI thin, put authorization and business
rules in testable services, and use EF Core/DI patterns already established in the app.

## Recent Changes

- 001-document-management: Planned an offline local document library using Blazor Server, SQLite,
  private local storage, and fail-closed local malware scanning.

<!-- MANUAL ADDITIONS START -->
<!-- MANUAL ADDITIONS END -->
