# Research: Document Upload and Management

**Feature**: `001-document-management`<br>
**Date**: 2026-10-20<br>
**Scope**: Technical decisions only. Research requests contained generic framework questions and
did not transmit private repository or stakeholder text to external systems.

All feature clarifications are accepted in `spec.md`; no open clarification remains.

## Decisions

### R-001 — Preserve the existing offline .NET application baseline

**Decision**: Keep the ASP.NET Core 8 / Blazor Server / EF Core 8 / SQLite app as the only web
application. Use existing cookie-based training identities, projects, memberships, tasks, and
in-app notifications. Add no cloud identity, storage, scanning, or notification dependency.

**Rationale**: The project constitution requires an offline, self-contained, training-only
experience. Existing `Program.cs`, `ApplicationDbContext`, `User`, `Project`, `ProjectMember`,
`TaskItem`, and `Notification` already provide the local composition and source-of-truth entities.

**Alternatives considered**: Cloud object storage or a hosted document-management API were rejected
because they violate offline scope and add deployment/credentials not present in the app. A new
database engine was rejected because SQLite is the established baseline and the feature scale is
bounded by the specification.

### R-002 — Stream uploads with explicit server-side limits

**Decision**: Use Blazor Server `InputFile` and `IBrowserFile.OpenReadStream` with the exact
26,214,400-byte maximum, then copy in bounded chunks into staging storage. Check actual bytes while
copying; do not trust the browser-supplied size or MIME type. Process selected files independently
and sequentially to bound memory and preserve per-file outcomes.

**Rationale**: The app already uses server-side Blazor. Microsoft’s Blazor upload guidance
specifically recommends an explicit maximum for `OpenReadStream`, treats client file names as
untrusted, and advises generating a server-side storage name. A streaming copy avoids holding
25-MiB objects or a whole batch in memory.

**Alternatives considered**: Buffering whole files in memory was rejected due to avoidable memory
pressure and scale with multi-file batches. A new upload REST API was rejected because there is no
existing public API contract or need for an external client; the UI uses application services.

### R-003 — Require a real local ClamAV scan and fail closed

**Decision**: Use a configured local `clamscan` executable through an `ILocalMalwareScanner`
adapter, scanning the completed staging object before any document can be read or listed. The
adapter passes arguments without a shell, applies a bounded timeout, and maps clean, detected, and
operational-error results separately. A missing executable, missing/unusable local signature
database, timeout, or ambiguous exit/output is an explicit failed check. There is no “scanner
disabled,” mock-success, or warning-only mode in the application path.

**Rationale**: ClamAV is a local malware-detection toolkit and its documented scanning utilities
operate on local files. This gives actual local checking without internet access or a new hosted
service. A replaceable interface allows deterministic tests while production code remains fail
closed. Local signature provisioning is an operator prerequisite; signature downloads are not
performed by application runtime.

**Alternatives considered**: Cloud scanning was rejected by offline requirements. Extension/MIME
checks alone are not malware checking. A no-op or simulated scanner was rejected explicitly by
FR-005 and the spec assumptions. A local daemon-based scanning service is a possible future
implementation, but was not selected because a configured executable is the narrower local
dependency for this app.

### R-004 — Separate display names from storage identity

**Decision**: Store original filename as metadata and create a cryptographically unpredictable
internal storage key (for example, a random GUID in `N` format with no user-supplied extension).
Resolve the final path only from the configured storage root plus that generated key. Store the
binary directory under app `App_Data` by default, outside `wwwroot`; keep staging on the same
volume. Preserve ordinary original filename characters in metadata and use safe framework header
encoding for downloads.

**Rationale**: This meets the clarification for display/download fidelity without allowing path
interpretation, traversal, or same-name overwrites. ASP.NET Core upload guidance also warns against
using client file names as physical storage names. Static-file middleware must never expose content.

**Alternatives considered**: Original-name paths and a public/static upload directory were rejected
because names are untrusted and the spec requires private files. A database BLOB was rejected in
favor of local filesystem storage, consistent with the existing SQLite app and clearer bounded
streaming behavior.

### R-005 — Use a journaled state machine across SQLite and the filesystem

**Decision**: Add a durable `DocumentFileOperation` journal and document storage states. Stage and
scan content before publishing it. Persist metadata only when a complete object is available.
During replacement/deletion, mark the document unavailable while the operation is in progress.
For replacement, preserve the old object until the new object and metadata switch complete; if the
operation cannot finish, restore the old object as current. For deletion, deny access before
removing bytes; complete metadata/share/attachment removal only after content deletion succeeds.
Recover or clean stale journaled staging/orphan objects on retry/startup. Never return success when
cleanup/deletion has failed.

**Rationale**: EF/SQLite transactions cover database changes, not filesystem operations. A durable
journal and explicit state make crashes, disk-full errors, and compensating actions observable and
recoverable. Keeping independent per-file operations avoids rolling back successful members of a
batch. A pending-delete record continues to block project deletion.

**Alternatives considered**: Treating a database transaction as if it included file writes was
rejected; the filesystem cannot participate in SQLite’s transaction. Deleting the old replacement
before validating/publishing the new one was rejected because a failed replacement must preserve the
prior file. Unjournaled best-effort cleanup alone was rejected because process interruption could
leave artifacts indefinitely.

### R-006 — Keep authorization current and centralized

**Decision**: Add a document authorization service used by list/search/report queries, commands,
task attachment operations, notifications, and the content endpoint. Derive caller identity from
the authenticated principal. Evaluate current role scope, project manager/member status, current
department, ownership, and explicit grants in the database. Apply the special removed-uploader
project denial before additive grants. Restrict query results before searching, counting, or
producing suggestions; reauthorize at time of content delivery.

**Rationale**: Blazor `[Authorize]` protects page entry, but a page attribute is not sufficient for
service operations or streamed bytes. Current database membership and department values are the
source of truth, which makes revocation effective on the next operation. Notification and task
references are identifiers, not capabilities.

**Alternatives considered**: UI-only checks, cached permission claims, static file links, and
long-lived share URLs were rejected because they can survive project removal, share removal, or
deletion. Treating a category label as a permission was rejected by FR-003.

### R-007 — Use SQLite-compatible optimistic concurrency

**Decision**: Add an application-managed integer version/concurrency token to each document and
perform conditional writes for metadata, replacement, and deletion state transitions. Return a
retryable conflict result when the expected version no longer matches. Make deletion terminal;
replacement/edit paths must not recreate a deleted or deleting record.

**Rationale**: EF Core documents optimistic concurrency as a token-based compare-on-save pattern.
SQLite does not provide SQL Server-style automatically generated rowversion semantics, so an
application-managed token fits the current provider. Existing SQLite transaction boundaries
protect related database updates.

**Alternatives considered**: Last-write-wins was rejected because competing replacement/deletion
could silently overwrite or revive data. Provider-specific rowversion types or changing database
providers were rejected to preserve the existing SQLite baseline.

### R-008 — Use additive EF migrations, not `EnsureCreated` for schema changes

**Decision**: Replace startup `EnsureCreated()` with EF Core migrations. Add a baseline migration
that represents the current pre-feature schema, followed by the document migration. Before
migrating, detect whether the database already contains the complete `EnsureCreated` baseline but
has no EF migration history; only after validating that schema, create/stamp the baseline as
applied, then apply feature migrations. On a fresh database, apply the baseline and feature
migrations normally. Stop with an explicit error on an unknown or partial schema rather than
stamping, dropping, or recreating it.

**Rationale**: Existing `Program.cs` calls `Database.EnsureCreated()` and the repository has no
migration history. That API does not evolve an already-created schema when new entities are
introduced, while a normal initial migration would otherwise try to create already-existing
tables. A verified baseline adoption makes the migration chain work for both existing training
databases and fresh installations without data loss.

**Alternatives considered**: Deleting/recreating the SQLite file was rejected because it loses
existing training state. Leaving `EnsureCreated()` unchanged was rejected because a populated
database would not receive the new schema. Blindly marking any database as baseline-applied was
rejected because an incomplete or unrelated SQLite schema could then fail later or be corrupted.

### R-009 — Avoid a general-purpose public API; define the actual application boundary

**Decision**: Keep user operations as Blazor UI-to-service calls. Define the authenticated
`GET /document-content/{documentId}` HTTP boundary for inline preview/download, and document
service-command and error contracts for upload, metadata, sharing, task integration, and reporting.

**Rationale**: The repository exposes a Blazor Server app, not a public document API. Browser
preview/download needs a streamable HTTP response; other workflows remain inside the existing
authenticated server-side component circuit.

**Alternatives considered**: A broad REST/OpenAPI service for all journeys was rejected because it
would create an unsupported external integration surface and duplicate the Blazor app contract.
Returning physical paths or static URLs was rejected because authorization must be repeated for
each content request.

### R-010 — Add a dedicated test project and verify failures at boundaries

**Decision**: Add `ContosoDashboard.Tests` with unit and SQLite integration coverage. Use a
replaceable scanner and storage abstraction for deterministic injected failures, while keeping
separate local ClamAV integration and browser checks. Exercise failure points around staging,
publish, database commit, replacement rollback, deletion retry, and stale references.

**Rationale**: No test project currently exists, while constitution principle V requires automated
tests for behavior and authorization. Disk exhaustion is safest and repeatable to validate with
failure injection instead of filling the host volume.

**Alternatives considered**: Manual-only validation was rejected for authorization and cleanup
invariants. A fake scanner as the sole proof of malware checking was rejected; real local scanner
integration remains a required acceptance check.

## Public technical references

- Microsoft Learn, [ASP.NET Core Blazor file uploads (.NET 8 view)](https://learn.microsoft.com/en-us/aspnet/core/blazor/file-uploads?view=aspnetcore-8.0): explicit stream size limits, untrusted filename handling, and file-upload security considerations.
- ClamAV Documentation, [Scanning](https://docs.clamav.net/manual/Usage/Scanning.html): local scan utilities and scanning behavior.
- Microsoft Learn, [Handling Concurrency Conflicts - EF Core](https://learn.microsoft.com/en-us/ef/core/saving/concurrency): optimistic token-based concurrency and provider-specific concurrency behavior.
- Microsoft Learn, [Transactions - EF Core](https://learn.microsoft.com/en-us/ef/core/saving/transactions): database transaction behavior and limits of database transaction scope.

These sources were queried with generic technology terms only. No repository files, private
configuration, credentials, sample data, or stakeholder text were sent to the sources.
