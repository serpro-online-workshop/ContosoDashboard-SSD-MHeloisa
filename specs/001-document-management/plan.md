# Implementation Plan: Document Upload and Management

**Branch**: `001-document-management` | **Date**: 2026-10-20 | **Spec**: [spec.md](spec.md)<br>
**Input**: Feature specification from `/specs/001-document-management/spec.md`

## Summary

Add an offline, training-only document library to the existing .NET 8 Blazor Server app. Keep
metadata and relationships in the existing EF Core/SQLite database, but store file bytes in a
private local directory outside `wwwroot`. Use a local ClamAV scanner as a required, fail-closed
upload/replacement prerequisite. Centralize current-user authorization in document services and
recheck it for every list, search, task link, notification link, preview, download, and mutation.
Use unique internal storage keys, staged file operations, optimistic concurrency, and a durable
operation journal so incomplete writes, replacements, and deletions cannot become usable documents.

Accepted clarifications are binding: project removal retains the original project document but
revokes the removed uploader's access even through ownership or shares; project deletion is blocked
until documents are explicitly deleted; original display/download names are preserved while never
being used as storage paths; permanent deletion removes content, metadata, shares, and task links
while revoking stale references; and storage exhaustion reports per-file retryable failures,
cleans partial artifacts, preserves independent batch successes, and leaves a replaced file intact.

## Technical Context

**Language/Version**: C# 12 / .NET 8<br>
**Primary Dependencies**: ASP.NET Core 8 Blazor Server; EF Core 8 SQLite; local ClamAV `clamscan`
process behind an `ILocalMalwareScanner` abstraction; xUnit test project to be added<br>
**Storage**: Existing SQLite database for metadata and audit; local filesystem storage outside
`wwwroot`, with staging on the same volume as finalized files<br>
**Testing**: New `ContosoDashboard.Tests` project; unit tests for validation, authorization,
storage/scanner failures and concurrency; SQLite-backed integration tests; browser/accessibility
and disconnected-network acceptance checks<br>
**Target Platform**: Existing local .NET 8 web app and training identities; no cloud account or
runtime internet dependency<br>
**Project Type**: Single ASP.NET Core web application with server-rendered Blazor UI<br>
**Performance Goals**: Preserve spec outcomes: at least 95% of valid uploads up to 25 MiB available
within 30 seconds; lists/search within 2 seconds and PDF/image previews within 3 seconds for up to
500 visible documents, measured in the documented local environment<br>
**Constraints**: 26,214,400-byte per-file maximum; each file scanned locally before publication;
scanner absence/error is a failed upload, never a pass; no file bytes under static web root; one
current version only; accessible per-file progress/outcome; safe internal naming; no partial or
broken document after storage failure; local writable storage and pre-provisioned scanner
definitions are environment prerequisites<br>
**Scale/Scope**: Up to 500 visible documents per user for acceptance performance; each selected
file is processed independently and streamed with bounded memory. No new identity/team
administration, external document platform, or public general-purpose REST API.

## Constitution Check

### Pre-research gate

| Gate | Status | Reason |
|---|---|---|
| Training/offline scope | **PASS** | The feature uses existing local mock identities, SQLite, local file storage, local scanning, and in-app notifications; it does not upgrade the app to production readiness. |
| Authorization/data boundaries | **PASS** | The design requires UI authorization plus service/content-boundary checks, including current project/department scope and the accepted project-removal override. |
| Layered design | **PASS** | New Blazor pages/components call document services; services use EF Core and storage/scanner abstractions; no database or filesystem access belongs in Razor UI. |
| Usability/accessibility | **PASS** | The design includes keyboard-operable controls, labels, focus, visible textual progress/status feedback, and empty/error/success states. Speech software and screen-reader-specific requirements/tests are excluded by the accepted clarification; native browser compatibility is retained. |
| Verification/traceability | **PASS** | The specification has acceptance scenarios for all functional areas; because no test project exists, planning includes one and traces acceptance to unit/integration/manual offline and accessibility validation. |

No constitution exception or unresolved specification clarification blocks research. The current
worktree had no existing changes before planning. The feature's requirements quality checklist
reports all 16 items satisfied, including the accepted clarification updates.

## Phase 0: Research Outcome

Research resolved the technical choices: retain the current .NET/Blazor/EF/SQLite baseline; use
bounded Blazor `InputFile` streaming; require a local ClamAV executable with fail-closed semantics;
store random-keyed content outside static files; use a journaled filesystem/database workflow;
model current entitlements centrally; and use application-managed concurrency tokens with SQLite.
See [research.md](research.md) for decisions, alternatives, and public technical references.

## Phase 1: Design

### Architecture and boundaries

1. **UI**: Add document library, upload/edit/share/replace/delete flows, project documents, task
   attachments, and administrator activity reporting in the existing `ContosoDashboard/Pages/`
   and shared component structure. Retain the existing application shell and training login.
2. **Application services**: Add document query/command, authorization, local storage, scanner,
   file-validation, and audit/report services under `ContosoDashboard/Services/`. Components pass
   the authenticated principal, not a client-selected user ID.
3. **Persistence**: Extend `ApplicationDbContext` and `Models/` for documents, tags, grants,
   attachments, activity, and file-operation journal. Add indexes and restrictive project
   relationships. Replace startup `EnsureCreated` for schema evolution with additive EF Core
   migrations so the existing SQLite training data is preserved.
4. **Content delivery**: Map one authenticated content endpoint for inline PDF/image preview and
   attachment download. It resolves document identity, authorizes through the document service,
   and streams only the current stored object. It never accepts a storage path or filename from
   the request and never serves bytes from static middleware.
5. **File lifecycle**: Stream each upload to a unique same-volume staging object, enforce limits
   while reading, validate extension/content, scan locally, and publish under a random internal
   key. Commit metadata only after the content is ready. Journal operations for recovery and
   cleanup. Process batch files independently. For replacement, keep the document unavailable
   while the candidate and old object are reconciled; if replacement cannot complete, restore
   the prior object as current. For deletion, mark the document inaccessible before removing bytes,
   then delete its grants, task links, and metadata while preserving only non-content audit
   records. A failed removal is reported as failure and remains inaccessible/retryable.
6. **Authorization**: A central policy service applies a deny-first check for a project uploader
   who is no longer a current member of that project. This explicit revocation defeats that
   user's ownership and share grants; remaining entitlements otherwise combine as specified.
   Permission-filtered queries must run before search/count/suggestion aggregation. Every direct
   reference, including notification/task links, repeats the check.
7. **Project deletion**: Require both a service-level “no associated documents” check and a
   restrictive SQLite foreign key. Pending-deletion records still block project deletion until
   successful explicit deletion completes.

See [data-model.md](data-model.md), [contracts/document-workflows.md](contracts/document-workflows.md),
and [quickstart.md](quickstart.md).

### Data migration and operational notes

- The current app calls `Database.EnsureCreated()` and has no migration history. Add a baseline
  migration for the existing schema plus the document migration. On startup, recognize a complete
  existing `EnsureCreated` schema and stamp only that verified baseline as applied before applying
  feature migrations; on a fresh database, apply the full migration chain. Reject an unknown or
  partial schema rather than stamping it or resetting it. Preserve the existing database and
  validate its schema before adoption; do not reset `ContosoDashboard.db`.
- Configure storage root and scanner executable through application configuration. Default binary
  storage is under the app content root in an `App_Data` directory, not `wwwroot`; validate the
  resolved path is outside the static-file root and create it with least-privilege local access.
- The local ClamAV binary and signature database must be provisioned before uploads are enabled.
  The app performs no network signature download. Missing executable/database, timeout, scanner
  error, or malware detection yields an explicit per-file failure and no published document.
- SQLite and the filesystem do not share a transaction. The operation journal, temporary
  same-volume objects, deny-while-pending document state, compensating cleanup, and startup
  reconciliation address the boundary; acceptance tests must inject failures between every
  filesystem and database transition.
- No task list, application code, migration, database update, or test project is generated by this
  planning workflow.

## Traceability and Verification Strategy

| Acceptance area | Design / verification |
|---|---|
| FR-001, FR-009, FR-011–FR-019; SC-005 | Authorization service and secured content endpoint; role/membership/share matrix tests, removed-uploader denial, filtered list/search/count tests, stale-link tests |
| FR-002–FR-008; SC-001, SC-006 | Per-file upload command, stream-time size enforcement, content validation, ClamAV adapter, operation journal; valid/invalid/malware/unavailable/disk-full batch tests |
| FR-010, FR-012–FR-013; SC-002, SC-004 | Authorized query service and content endpoint; combined filters/sorts/search, no-leak, preview/download, and 500-document timing tests |
| FR-014–FR-016, FR-028–FR-029; SC-005–SC-006 | Versioned metadata updates, replacement/deletion state machine, project guard and restrictive FK; conflict, rollback, stale-link and explicit-deletion tests |
| FR-017–FR-018, FR-023; SC-005 | Unique user/department grants and existing notification integration; duplicate/overlap/preferences/revocation tests |
| FR-020–FR-021; SC-005 | Project-scoped task attachment and explicit personal-to-project consent; cross-project/unauthorized task tests |
| FR-022–FR-027; SC-007–SC-008 | Dashboard/report queries, audit event snapshots, keyboard-operable page states with visible textual feedback; role denial, report reconciliation, offline and keyboard checks; no screen-reader-specific tests |

Before feature completion, run the test project, `dotnet build`, local scanner integration
scenarios, authenticated/unauthenticated browser checks, disconnected-network walkthrough, and
keyboard accessibility validation. No application tests/build were run during this documentation-
only planning phase.

## Constitution Check (post-design)

| Gate | Status | Reason |
|---|---|---|
| Training/offline scope | **PASS** | All runtime dependencies are local; the scanner is real and fail-closed; docs explicitly retain training-only/non-production wording. |
| Authorization/data boundaries | **PASS** | UI, query, mutation, notification/task reference, and byte-stream boundaries all recheck identity and current entitlement; project removal denial takes precedence for the removed uploader. |
| Layered design | **PASS** | Proposed pages, services, models, EF persistence, local filesystem adapter, and scanner adapter fit the existing app and use replaceable service boundaries. |
| Usability/accessibility | **PASS** | Every upload and lifecycle outcome has visible text/progress and controls are specified for keyboard operation, labels, and visible focus. Speech software and screen-reader-specific requirements/tests are excluded; native browser compatibility is retained. |
| Verification/traceability | **PASS** | All functional-requirement groups map to automated tests and repeatable offline/accessibility/performance checks; the absent test project is addressed in the design. |

**Gate result**: PASS. No unjustified violation, unresolved clarification, or constitution exception.

## Project Structure

### Documentation (this feature)

```text
specs/001-document-management/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── document-workflows.md
└── checklists/
    └── requirements.md
```

### Source Code (repository root)

```text
ContosoDashboard/
├── Data/
│   └── ApplicationDbContext.cs       # document mappings and indexes
├── Migrations/                       # additive SQLite schema migration
├── Models/                           # document, grant, tag, activity, operation entities
├── Pages/                            # document library/report and task/project integration
├── Services/                         # policy, query/command, audit, storage, scanner services
├── Endpoints/                        # authenticated content streaming endpoint
├── Shared/                           # existing navigation/layout and reusable UI
└── Program.cs                        # DI, migrations, and endpoint registration
ContosoDashboard.Tests/
├── Unit/
├── Integration/
└── Web/
```

**Structure Decision**: Keep one web project and extend its existing `Models/`, `Data/`,
`Services/`, `Pages/`, and `Program.cs` layers. Add a narrow `Endpoints/` area solely for
authorized streaming and add a separate test project. There is no API/frontend split and no
external document service.

## Complexity Tracking

No constitution violations; no exception or complexity waiver is required. The filesystem journal
is required because SQLite transactions cannot atomically commit local file operations.

## Planning Environment Note

`pwsh` is not installed in this environment. The setup script and common path helper were inspected;
their output was safely derived from the Git root/current branch and existing feature directory,
and the existing plan template was followed manually. The agent-context update script was also
inspected and its Copilot context output was produced as a safe equivalent because PowerShell was
unavailable. No hook file exists at repository root. `tasks.md` was intentionally not created.
