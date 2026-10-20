# Data Model: Document Upload and Management

This is a logical design for additive EF Core/SQLite entities. Existing `User`, `Project`,
`ProjectMember`, `TaskItem`, and `Notification` remain the identity and work-context sources.
Names and limits below are proposed implementation constraints where the specification does not
provide field lengths; they do not alter accepted user-visible behavior.

## Entities

### Document

Represents one current, privately stored file.

| Field | Type / constraint | Notes |
|---|---|---|
| `DocumentId` | `Guid`, primary key | Stable document identity used in routes and retained audit references. |
| `Title` | required, 1–255 chars after trimming | Searchable display title. |
| `Description` | optional, up to 2,000 chars | Searchable text. |
| `Category` | required enum | `Project Documents`, `Team Resources`, `Personal Files`, `Reports`, `Presentations`, or `Other`; category does not grant access. |
| `OriginalFileName` | required, up to 255 chars | Preserve ordinary special characters; reject control/path-separator forms; never use as a path. |
| `DeclaredContentType` | optional, up to 255 chars | Captured type description for metadata compatibility; never trusted for validation or response headers. |
| `DetectedContentType` | required canonical MIME | Determined from validated content and allow-list; used for response behavior. |
| `Extension` | required allow-listed extension | Lowercase normalized suffix from the submitted filename; must agree with detected file family. |
| `SizeBytes` | positive integer, at most 26,214,400 | Count actual streamed bytes, not client claim. |
| `StorageKey` | required generated opaque key | Random, unique, filename-independent, no path separators or user input. |
| `StorageState` | `Ready`, `Replacing`, or `Deleting` | Only `Ready` documents can be read, searched, or modified; pending states deny access. |
| `UploaderUserId` | required FK to `User` | Original owner/uploader; immutable through replacement and management. |
| `ProjectId` | nullable FK to `Project`, delete restricted | `null` means not project-associated; remains unchanged when uploader leaves. |
| `UploadedAtUtc` | required UTC timestamp | Initial upload time; replacement does not change it. |
| `UpdatedAtUtc` | required UTC timestamp | Updated on metadata/replacement state completion. |
| `Version` | required integer concurrency token | Incremented on each committed metadata or lifecycle mutation; application-managed for SQLite. |

**Relationships and indexes**: One uploader has many documents; one project has many documents.
Project deletion uses `Restrict` and a service-level precondition. Index `(UploaderUserId,
UploadedAtUtc)`, `(ProjectId, UploadedAtUtc)`, and fields used for common filters. Do not persist
file bytes in this row.

**Validation**: Title/category required; description bounded; original filename is metadata only;
extension/content pair must match; empty, unsupported, mismatched, oversized, or malware-detected
content is rejected. A non-`Ready` document is excluded from every read path.

### DocumentTag

Normalized searchable tag associated with a document.

| Field | Type / constraint | Notes |
|---|---|---|
| `DocumentTagId` | integer, primary key | |
| `DocumentId` | required FK to `Document` | Delete with document. |
| `DisplayValue` | required, 1–100 chars | Trimmed display value. |
| `NormalizedValue` | required | Case-folded search key. |

Unique index `(DocumentId, NormalizedValue)` prevents duplicate tags on one document. Tags do not
grant access.

### DocumentShare

An explicit read-only grant to either one named user or one current department.

| Field | Type / constraint | Notes |
|---|---|---|
| `DocumentShareId` | integer, primary key | |
| `DocumentId` | required FK to `Document` | Delete with document. |
| `RecipientUserId` | nullable FK to `User` | Exactly one of recipient user or department is set. |
| `Department` | nullable, trimmed nonblank string, max 100 | Current department membership determines team recipients. |
| `GrantedByUserId` | required FK to `User` | Actor authorized by FR-017. |
| `GrantedAtUtc` | required UTC timestamp | |

Enforce exactly one recipient form and unique active grants for `(DocumentId, RecipientUserId)` or
`(DocumentId, Department)`. Empty departments confer no entitlement. Overlapping direct/team
entitlements result in one listing and one notification per newly entitled user. Removing one
grant does not remove another entitlement, except that the project-removal override still denies
the removed uploader their own project document.

### TaskDocumentAttachment

Links a document to a task without copying content.

| Field | Type / constraint | Notes |
|---|---|---|
| `TaskId` | required FK to `TaskItem` | |
| `DocumentId` | required FK to `Document` | |
| `AttachedByUserId` | required FK to `User` | |
| `AttachedAtUtc` | required UTC timestamp | |

Composite primary key `(TaskId, DocumentId)` ensures one attachment per task/document. Validate
that the task and document use the same non-null project, or that the caller owns an unassociated
personal document and explicitly confirms associating it to that task's project. Reject
cross-project moves, others' personal documents, and tasks without an eligible project.
Document deletion cascades its attachment rows; project deletion remains restricted while any
document is associated, and task links are not a route to bypass document authorization.

### DocumentActivity

Append-only, non-content activity for administrator reporting. It intentionally has no cascading
foreign key to `Document`, so the reference remains after hard deletion.

| Field | Type / constraint | Notes |
|---|---|---|
| `DocumentActivityId` | integer, primary key | |
| `DocumentReferenceId` | required `Guid`, not an FK | Opaque reference retained after document deletion. |
| `ActorUserId` | required user identifier | Actor at event time; avoid cascade deletion. |
| `Action` | required enum | Upload, replacement, metadata edit, download, preview, deletion, share added, share removed. |
| `OccurredAtUtc` | required UTC timestamp | |
| `FileTypeAtEvent` | optional bounded canonical type | Non-content snapshot used to reconcile reports by file type, including deleted records. |
| `UploaderUserIdAtEvent` | optional user identifier | Snapshot for historical uploader ranking. |

Do not store document bytes, filename, title, description, tags, storage key, or share target in
the activity row. Restrict all activity queries and reports to administrators. Add indexes by
`OccurredAtUtc`, action, actor, and document reference.

### DocumentFileOperation

Durable journal for local filesystem operations that cannot be committed atomically with SQLite.

| Field | Type / constraint | Notes |
|---|---|---|
| `DocumentFileOperationId` | `Guid`, primary key | |
| `DocumentId` | nullable document reference | No cascade requirement; supports first upload before metadata exists. |
| `Kind` | required enum | Upload, replacement, deletion, or cleanup. |
| `State` | required enum | Staging, scanned, candidate-published, metadata-switched, cleanup-pending, completed, failed. |
| `PreviousStorageKey` | optional generated key | Used to restore the prior current object on failed replacement. |
| `CandidateStorageKey` | optional generated key | Never derived from a filename. |
| `StartedAtUtc` / `UpdatedAtUtc` | required UTC timestamps | |
| `FailureCode` | optional bounded enum/string | Safe classification (for example, storage-full, scanner-unavailable); no private content or path. |

Only authorized services read this journal. Recovery is idempotent: complete or compensate
interrupted operations, clean stale staging/unreferenced objects, and keep affected documents
unavailable until a consistent state is reached. Do not expose a candidate or pending delete
through lists, task links, notifications, preview, or download.

### Existing Notification (extension)

Extend `Notification` with:

| Field | Type / constraint | Notes |
|---|---|---|
| `RelatedDocumentId` | nullable `Guid`, no FK | Identifies document notifications while allowing old notifications to remain after hard deletion. |

Add document upload/share notification types and a safe application route/link as needed. Notification
reads must suppress or make generic any document notification whose related document is no longer
readable by that recipient. Activating a notification rechecks current authorization; a link itself
never grants access. Respect existing `InAppNotificationsEnabled`; no email service is added.

## Access model

Read entitlement is evaluated at operation time from current database state: admin; eligible
project manager/member; in-scope Team Lead; owner; or active direct/department share. A user's
removal from a project imposes a higher-priority denial for documents they originally uploaded to
that project, even if ownership, role, or an existing share would otherwise grant access. Rejoining
the project restores only currently applicable entitlements. Project membership is not inferred
from a stale authentication claim.

Management, sharing, task attach, and reporting use the exact role/scope matrix in
[spec.md — Authorization and Ownership](spec.md#authorization-and-ownership). Query result sets
must be permission-filtered before search, counts, suggestions, dashboard widgets, and reports
that are not administrator-only.

## Lifecycle and integrity

1. **New upload**: validate metadata → stream to unique staging file with byte limit → inspect
   content/type → local malware scan → publish under generated storage key → commit document,
   tags, audit event, and journal state. Any failure returns a per-file error, cleans staging or
   records cleanup work, and does not expose a document. Independent batch items continue.
2. **Replacement**: validate and scan a separate candidate; mark the document `Replacing` so no
   caller can read a half-transition; retain the current object until the candidate switch and
   cleanup finish; use the journal and version token to restore the old object on failure. Preserve
   document ID, metadata, grants, attachments, uploader, and original upload time; never provide
   previous-version access.
3. **Deletion**: persist `Deleting` and deny all document routes first; remove content; then in one
   database transaction preserve a minimal deletion activity reference and remove document
   metadata, tags, grants, and task links. On content/database failure, return failure, keep access
   denied, retain retry/recovery information, and do not claim deletion succeeded.
4. **Project deletion**: query for any associated document, including pending deletion; reject if
   any exist. A restrictive foreign key independently prevents accidental cascade deletion.
5. **Concurrency**: conditional version updates fail with an actionable retry result. Deletion
   wins over edit/replacement; no operation may resurrect a `Deleting` or removed document.
