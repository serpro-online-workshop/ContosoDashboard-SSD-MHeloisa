# Quickstart: Document Management Validation

This guide is for implementation verification after the planned feature is built. It does not
create application code, database changes, fixtures, or tasks as part of this planning workflow.

## Prerequisites

- .NET 8 SDK.
- Local writable SQLite and document storage.
- Local ClamAV `clamscan` executable with a usable signature database already provisioned.
  Uploads must fail clearly when the scanner executable/database is absent or an actual scan
  cannot complete. Do not disable scanning to make a test pass.
- Existing training identities and seeded project/task data. Keep this environment offline; no
  cloud account or signature download is used at runtime.
- `ContosoDashboard.Tests` (to be added during implementation).

## Build and migrate

From the repository root:

```sh
dotnet restore ContosoDashboard/ContosoDashboard.csproj
dotnet build ContosoDashboard/ContosoDashboard.csproj
dotnet test ContosoDashboard.Tests/ContosoDashboard.Tests.csproj
```

Use the generated EF migration and the implementation's documented startup/migration path. The
upgrade must verify the existing pre-feature `EnsureCreated` schema, stamp only the matching
baseline as applied, and then apply feature migrations. A fresh database applies the full
migration chain. Unknown/partial schemas must stop with an actionable error. Preserve the existing
`ContosoDashboard.db`; do not delete or recreate that database. For an explicit CLI migration
workflow, use the implementation's documented command, for example:

```sh
dotnet ef database update --project ContosoDashboard --startup-project ContosoDashboard
```

Confirm the configured storage directory resolves outside `ContosoDashboard/wwwroot`, is writable
by the app process, and is not served by static-file middleware. Confirm the configured local
scanner is available (for example, `clamscan --version`) without initiating a network update.

## Run the application

```sh
dotnet run --project ContosoDashboard/ContosoDashboard.csproj
```

Sign in using the documented local training identity for each role. Verify the UI explicitly
describes the app as training-only and not production-ready. Stop the app normally after checking;
use a temporary isolated DB/storage root for destructive lifecycle scenarios.

## Acceptance walkthrough

1. **Upload and validation**: Upload one supported file with required title/category, then upload
   a multi-file batch containing valid, empty, unsupported, oversized, content-mismatched, and
   local known-malware-test fixture cases. Confirm independent results, byte progress, explicit
   retryable error text, no failed item in any list, and selection of a new file without reload.
   Use a safe EICAR test string fixture only in the isolated test environment to verify scanner
   detection; it is a test signature, not an active malware sample.
2. **Filename and storage safety**: Upload a name such as
   `Q4 Report (2025) - Finance & Ops.pdf` and a second file with the same name. Confirm display and
   download name preservation, distinct generated storage objects, no path interpretation, and no
   direct static URL. Verify PDF/JPEG/PNG inline preview and download fallback for other formats.
3. **Authorization and search**: Use employee, Team Lead, Project Manager, administrator,
   unrelated user, and unauthenticated browser sessions. Verify list/search/counts and content
   responses against the matrix in `spec.md`; test current membership and department changes.
   Remove an uploader from a project and confirm their retained document, task link, ownership,
   and grants are not accessible to that former member through lists, search, counts, widgets,
   task links, notifications, preview, or download, while other entitled users remain authorized.
4. **Sharing and task links**: Grant named-user and department access, including overlaps. Confirm
   one Shared with Me entry and one notification per newly entitled recipient, no duplicate
   notification, preference handling, read-only behavior, and revocation on every subsequent
   direct-reference request. Confirm task upload/attachment rules and explicit consent when a
   personal document becomes project-associated.
5. **Replacement, deletion, and project guard**: Replace a file successfully and verify unchanged
   document identity, original upload time, shares, and task links with no previous-version route.
   Inject scanner, storage, concurrency, and database failures and verify the old file remains
   current on failed replacement. Cancel deletion and confirm no change; confirm successful
   deletion removes bytes, metadata, shares, and task links while preserving only non-content
   audit. Test stale notifications/share links and confirm project deletion is blocked until every
   associated document is explicitly deleted.
6. **Disk exhaustion and recovery**: Inject a storage-full exception after partial staged writes
   for one batch item. Verify that item reports failure/retry, partial artifacts are removed or
   journaled for cleanup, other successful items remain available, and a failed replacement keeps
   its previous file. Restart/reconcile an interrupted journal and confirm no partial document is
   exposed.
7. **Dashboard, reports, and usability**: Confirm five most recent accessible uploads and accurate
   count; administrator reports reconcile against known upload/access events, including deleted
   document references, and non-admin requests are denied. Disconnect external networking and
   repeat core flows. Complete each flow keyboard-only, inspect visible focus, labels, logical
   focus order, visible textual progress/status feedback, and loading/empty/error/success states.
   Do not install or integrate speech software or run screen-reader-specific tests for this feature.

## Automated checks and performance

- Run `dotnet test` with isolated temporary SQLite databases and temporary storage roots.
- Automated failure injection must cover scanner unavailable/detected/timeout, content mismatch,
  storage full after partial write, DB commit failure, replacement rollback, delete retry, stale
  content references, concurrent version conflicts, and cleanup recovery.
- At least one scanner integration test must use the configured real local ClamAV scanner; fakes
  are for isolated error-path tests, not proof that malware checking is implemented.
- Run `dotnet build` after tests. Record local CPU/storage, scanner/database readiness, and upload
  path conditions when measuring the spec's 25-MiB/30-second, 500-document/2-second, and
  preview/3-second success criteria.
- Tests must not depend on internet access, cloud services, real malware, or filling the host disk.
