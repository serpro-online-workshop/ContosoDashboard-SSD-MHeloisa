# Tasks: Document Upload and Management

**Input**: Design documents in `specs/001-document-management/`
**Prerequisites**: `plan.md`, `spec.md`; also reviewed `research.md`, `data-model.md`,
`contracts/document-workflows.md`, `quickstart.md`, and `checklists/requirements.md`.

**Implementation baseline**: ASP.NET Core 8 / C# 12, Blazor Server, EF Core 8 with SQLite,
local filesystem content storage outside `wwwroot`, and a required local ClamAV scanner. Keep
the application offline and explicitly training-only; this feature does not make it production
ready.

**Verification**: Automated unit and SQLite-backed integration/web tests are included because
the plan and constitution require behavioral and authorization coverage. Browser, offline,
keyboard, performance, and real-scanner walkthroughs are also included where the design calls
for repeatable environment verification.

**Organization**: Tasks are grouped into one phase per specified user story, with stories in
priority order. User Story 8 is listed with the P1 stories; its final all-journey validation is
completed in Polish after the P2 workflows it exercises are available.

## Phase 1: Setup

**Purpose**: Establish the dedicated feature test project and its layout.

- [x] T001 Create `ContosoDashboard.Tests/ContosoDashboard.Tests.csproj` targeting .NET 8 with xUnit, a project reference to `ContosoDashboard/ContosoDashboard.csproj`, and `Unit/`, `Integration/`, and `Web/` test folders.

---

## Phase 2: Foundational

**Purpose**: Establish persistence, safe local file infrastructure, shared authorization and
operation recovery that every user story depends on. Do not start user-story implementation
until this phase is complete.

- [x] T002 [P] Add the `Document` entity, field constraints, storage state, immutable uploader/upload time, optional project relationship, and application-managed version in `ContosoDashboard/Models/Document.cs`.
- [x] T003 [P] Add normalized, uniquely indexed document tags in `ContosoDashboard/Models/DocumentTag.cs`.
- [x] T004 [P] Add single-user-or-department read grants with grantor and timestamp fields in `ContosoDashboard/Models/DocumentShare.cs`.
- [x] T005 [P] Add the composite-key task/document link with actor and timestamp fields in `ContosoDashboard/Models/TaskDocumentAttachment.cs`.
- [x] T006 [P] Add append-only, non-content activity snapshots with no cascading document foreign key in `ContosoDashboard/Models/DocumentActivity.cs`.
- [x] T007 [P] Add journal operation kind/state, previous/candidate opaque storage keys, timestamps, and safe failure code fields in `ContosoDashboard/Models/DocumentFileOperation.cs`.
- [x] T008 Extend `ContosoDashboard/Models/Notification.cs` with a nullable document identifier and document-upload/share notification types without using notification links as entitlements.
- [x] T009 Add project-document and task-attachment navigation properties in `ContosoDashboard/Models/Project.cs` and `ContosoDashboard/Models/TaskItem.cs`.
- [x] T010 Map all document entities, required indexes and uniqueness checks, cascade rules for document-owned tags/grants/links, restrictive project deletion, and SQLite-compatible concurrency configuration in `ContosoDashboard/Data/ApplicationDbContext.cs`.
- [x] T011 Create an additive baseline migration for the complete existing `EnsureCreated` schema and a document-feature migration in `ContosoDashboard/Migrations/`; preserve seeded/existing data and include the generated model snapshot.
- [x] T012 Replace startup `EnsureCreated()` in `ContosoDashboard/Program.cs` with migration startup that validates and adopts only the complete known legacy schema, applies the baseline and feature migrations, and stops on unknown/partial schemas. Preserve `ContosoDashboard/ContosoDashboard.db`.
- [x] T013 Define validated storage-root and scanner settings, including rejecting any resolved storage location under `wwwroot`, in `ContosoDashboard/Services/DocumentOptions.cs` and `ContosoDashboard/appsettings.json`.
- [x] T014 [P] Implement opaque-key, same-volume staging/finalization, streaming storage, cleanup, and outside-static-root checks in `ContosoDashboard/Services/DocumentFileStorage.cs`.
- [x] T015 [P] Implement the local `clamscan` adapter with argument-safe process invocation, timeout, clean/detected/error outcomes, and fail-closed behavior in `ContosoDashboard/Services/LocalClamAvScanner.cs`.
- [x] T016 Implement current-principal document policy evaluation using live role, ownership, membership, department, and grant data, including deny-first revocation for an uploader removed from their project, in `ContosoDashboard/Services/DocumentAuthorizationService.cs`.
- [x] T017 Implement idempotent startup reconciliation and cleanup for interrupted file-operation journal entries in `ContosoDashboard/Services/DocumentOperationRecoveryService.cs`.
- [x] T018 Implement a non-content activity writer and typed event recording API in `ContosoDashboard/Services/DocumentAuditService.cs`.
- [x] T019 Register storage/scanner options, authorization, audit, and recovery services and invoke recovery after validated migrations in `ContosoDashboard/Program.cs`.

**Checkpoint**: The existing database is preserved and safely migrated; shared data, authorization,
storage, scanning, audit, and recovery building blocks are ready.

---

## Phase 3: User Story 1 — Upload work documents safely (Priority: P1)

**Goal**: Accept per-file metadata and uploads with independent outcomes, bounded streaming,
content validation, local malware scanning, and no exposure of incomplete or failed content.

**Independent Test**: As an authenticated employee, upload personal and eligible project
documents, including a mixed-validity multi-file batch, then verify successful items, exact
metadata, visible progress/outcomes, retry behavior, and that rejected or interrupted items are
not listed or retrievable.

### Tests for User Story 1

- [x] T020 [P] [US1] Add unit tests for required metadata, supported extensions/content families, empty/25-MiB/over-limit input, unsafe filenames, content mismatch, and scanner failure codes in `ContosoDashboard.Tests/Unit/DocumentUploadValidationTests.cs`.
- [x] T021 [P] [US1] Add SQLite/storage integration tests for independent batch results, denied project uploads, fail-closed scanner behavior, staged-write cleanup, and no visible metadata on failed upload in `ContosoDashboard.Tests/Integration/DocumentUploadWorkflowTests.cs`.

### Implementation for User Story 1

- [x] T022 [US1] Implement streamed size and extension/content validation with stable contract failure codes in `ContosoDashboard/Services/DocumentFileValidator.cs`.
- [x] T023 [US1] Implement independent sequential per-file upload processing in `ContosoDashboard/Services/DocumentUploadService.cs`; stream to staging, scan before publication, generate an opaque internal key, commit document/tags/audit/journal state only on success, clean or journal partial artifacts, and preserve successful sibling files.
- [x] T024 [US1] Add authenticated per-file metadata entry, eligible-project selection, visible byte progress, validation/results/retry text, and file reselection without reload in `ContosoDashboard/Pages/DocumentUpload.razor`.
- [x] T025 [US1] Register `IDocumentFileValidator` and `IDocumentUploadService` implementations and retain authenticated page routing in `ContosoDashboard/Program.cs`.

**Checkpoint**: A valid upload succeeds and is private; every failed item reports its own outcome
without publishing partial content.

---

## Phase 4: User Story 2 — Find and access permitted documents (Priority: P1)

**Goal**: Provide an authorized library, search/filter/sort, metadata details, preview, and
download, checking current access at both query and byte-stream boundaries.

**Independent Test**: Seed documents across owners, projects, departments, dates, and grants;
verify authorized list/search/count/suggestion results, combined filters and sorting, PDF/JPEG/PNG
preview, download fallback for other supported formats, and uniform denial of unauthorized or
stale direct references.

### Tests for User Story 2

- [x] T026 [P] [US2] Add query tests proving current entitlement filtering precedes search, counts, suggestions, filters, and sort, including project-removal revocation and role-scope boundaries, in `ContosoDashboard.Tests/Unit/DocumentQueryAuthorizationTests.cs`.
- [x] T027 [P] [US2] Add authenticated/unauthenticated endpoint tests for GUID validation, uniform not-found/denied/pending/deleted responses, content disposition, canonical type, `nosniff`, inline allow-list, and streamed bytes in `ContosoDashboard.Tests/Web/DocumentContentEndpointTests.cs`.

### Implementation for User Story 2

- [x] T028 [US2] Implement permission-first library/search/filter/sort queries and authorized metadata lookup with inclusive displayed-date filtering in `ContosoDashboard/Services/DocumentQueryService.cs`.
- [x] T029 [US2] Implement `GET /document-content/{documentId}` using a canonical GUID, current service authorization on every request, streaming of the current object only, inline PDF/JPEG/PNG or download mode, safely encoded original filename, and uniform no-leak errors in `ContosoDashboard/Endpoints/DocumentContentEndpoint.cs`.
- [x] T030 [US2] Implement the authenticated `/documents` library with My Documents, Project Documents, Shared with Me, search, sort, filters, counts, loading/empty/error states, labels, keyboard controls, and visible focus in `ContosoDashboard/Pages/Documents.razor`.
- [x] T031 [US2] Implement the authenticated `/documents/{documentId:guid}` metadata/details view with reauthorization and download/preview actions in `ContosoDashboard/Pages/DocumentDetails.razor`.
- [x] T032 [P] [US2] Add the Documents navigation link to the existing navigation shell in `ContosoDashboard/Shared/NavMenu.razor`.
- [x] T033 [US2] Register the query service and map the content endpoint before static/fallback routing in `ContosoDashboard/Program.cs`; verify content is never served by static-file middleware.

**Checkpoint**: A user can locate only currently readable documents; every direct content request
rechecks authorization and cannot disclose denied metadata or bytes.

---

## Phase 5: User Story 8 — Use the library offline and accessibly (Priority: P1)

**Goal**: Preserve the local, offline training experience and make the delivered P1 upload/library
journeys keyboard-operable with visible textual states. Later story UI must preserve these same
requirements; full all-journey verification is a Polish gate after those workflows exist.

**Independent Test**: After the declared workflow prerequisites US1–US5 are implemented, disconnect
external networking and complete upload, find, share, preview, replacement, and deletion using
keyboard-only navigation. Verify focus order, labels, visible focus, progress, validation, success,
denial, and empty/loading text without relying on dashboard widgets or reports. Do not install or
integrate speech software or perform screen-reader-specific tests.

### Tests for User Story 8

- [x] T034 [P] [US8] Add configuration tests that require local storage/scanner settings, reject a storage root under `wwwroot`, and do not rely on cloud/network services in `ContosoDashboard.Tests/Unit/OfflineDocumentConfigurationTests.cs`.
- [ ] T035 [P] [US8] Define the full disconnected-network and keyboard-only walkthrough for upload, find, share, preview, replacement, and deletion, with expected focus and visible-text states, in `ContosoDashboard.Tests/Web/DocumentUsabilityAcceptance.md`; execute after US3–US5 are available.

### Implementation for User Story 8

- [x] T036 [US8] Add local ClamAV and storage prerequisites, training-only/non-production wording, and offline launch guidance to `README.md` without adding network signature downloads or any production-readiness claim.
- [x] T037 [US8] Complete and record labels, logical focus order, visible focus, non-color text status, and loading/empty/success/error/denied feedback for the upload and library/details pages in `ContosoDashboard/Pages/DocumentUpload.razor` and `ContosoDashboard/Pages/Documents.razor`.

**Checkpoint**: The acceptance procedure is defined here; its full journey completion gate is
verified after US3–US5 are available and again in Polish. Screen-reader-specific work remains
excluded.

---

## Phase 6: User Story 3 — Maintain documents within role boundaries (Priority: P2)

**Goal**: Support metadata edits, atomic replacement of the current file, confirmed permanent
deletion, concurrency handling, and explicit project deletion blocking while documents remain.

**Independent Test**: As owner and in-scope manager, edit, replace, cancel deletion, confirm
deletion, inject scanner/storage/database/concurrency failures, and attempt project deletion.
Verify immutable uploader/upload time, old-file preservation on failed replacement, stale-reference
revocation after deletion, and project deletion refusal until every associated document is gone.

### Tests for User Story 3

- [ ] T038 [P] [US3] Add unit tests for expected-version conflicts, terminal deletion, and role/scope checks for edit, replacement, and deletion in `ContosoDashboard.Tests/Unit/DocumentLifecycleTests.cs`.
- [ ] T039 [P] [US3] Add integration tests for replacement rollback, storage-full cleanup, interrupted delete/retry, stale references, and restrictive project-delete behavior in `ContosoDashboard.Tests/Integration/DocumentDeletionAndProjectGuardTests.cs`.

### Implementation for User Story 3

- [ ] T040 [US3] Implement version-checked metadata edit, staged/scanned replacement, deny-while-pending state, previous-object restoration on failure, and terminal confirmed deletion in `ContosoDashboard/Services/DocumentLifecycleService.cs`; retain document identity, uploader, upload time, shares, and attachments on replacement and preserve only non-content audit on deletion.
- [ ] T041 [US3] Add owner/authorized-manager edit, replace, and confirmation/cancellation deletion controls with visible conflict/failure/retry outcomes in `ContosoDashboard/Pages/DocumentDetails.razor`.
- [ ] T042 [US3] Add a project deletion service operation that checks for any associated or pending document before deletion and returns a safe blocking result in `ContosoDashboard/Services/ProjectService.cs`.
- [ ] T043 [US3] Register the lifecycle service and expose the project-deletion guard through the existing project management boundary in `ContosoDashboard/Program.cs` and `ContosoDashboard/Pages/ProjectDetails.razor`.

**Checkpoint**: Lifecycle transitions are coherent under failure/concurrency; no previous version
is available, successful deletion removes document data/grants/links, and project deletion never
implicitly deletes associated documents.

---

## Phase 7: User Story 4 — Share with colleagues and teams (Priority: P2)

**Goal**: Grant and revoke read-only access to one existing user or a current nonblank department,
with deduplicated listings and preference-aware notifications.

**Independent Test**: Share a personal document with a user and department, test overlapping grants
and duplicate requests, verify one Shared with Me entry and one notification for each newly
entitled recipient, then revoke grants and verify current entitlement is reevaluated on later
requests.

### Tests for User Story 4

- [ ] T044 [P] [US4] Add unit tests for exactly-one-recipient validation, owner/admin scope, department membership, overlapping grants, duplicate operations, and additive entitlement after one grant is removed in `ContosoDashboard.Tests/Unit/DocumentSharingTests.cs`.
- [ ] T045 [P] [US4] Add integration tests for one notification per newly entitled recipient, actor exclusion, in-app preference handling, read-only access, and old-notification revocation in `ContosoDashboard.Tests/Integration/DocumentShareNotificationTests.cs`.

### Implementation for User Story 4

- [ ] T046 [US4] Implement authorized, idempotent add/remove grant operations for exactly one existing user or nonblank department, deduplicated effective recipients, read-only behavior, and share audit events in `ContosoDashboard/Services/DocumentSharingService.cs`.
- [ ] T047 [US4] Implement accessible share/remove controls and recipient selection for named users and current departments in `ContosoDashboard/Shared/DocumentShareDialog.razor`.
- [ ] T048 [US4] Integrate successful grant notifications with the existing `INotificationService`, respecting preferences and avoiding duplicate notifications for duplicate/overlapping grants in `ContosoDashboard/Services/DocumentSharingService.cs`.
- [ ] T049 [US4] Register the sharing service and integrate its dialog with authorized document details in `ContosoDashboard/Program.cs` and `ContosoDashboard/Pages/DocumentDetails.razor`.

**Checkpoint**: Only entitled recipients can read shared documents; grants do not convey edit,
delete, or reshare rights, and removing a grant does not wrongly erase another valid entitlement.

---

## Phase 8: User Story 5 — Work with task and project documents (Priority: P2)

**Goal**: Integrate project documents and task attachments/uploads while preserving task access,
project scope, same-project rules, and explicit consent for personal-to-project association.

**Independent Test**: As an authorized project task viewer, open project documents, upload from a
task, attach a same-project document, confirm/cancel a personal-document scope expansion, and
verify cross-project, ineligible-task, and another-owner personal-document attempts are denied.

### Tests for User Story 5

- [ ] T050 [P] [US5] Add unit tests for task-viewer/project authorization, same-project attachment, uniqueness, eligible project upload, explicit personal-scope consent, and denied cross-project attachment in `ContosoDashboard.Tests/Unit/TaskDocumentAuthorizationTests.cs`.
- [ ] T051 [P] [US5] Add integration tests for task upload/attachment, denial without reassignment, and repeated task/document references rechecking current document entitlement in `ContosoDashboard.Tests/Integration/TaskDocumentWorkflowTests.cs`.

### Implementation for User Story 5

- [ ] T052 [US5] Implement authorized task-document listing, one-time same-project attachment, task-context upload, and explicit confirmation before associating an owned personal document with the task project in `ContosoDashboard/Services/TaskDocumentService.cs`.
- [ ] T053 [US5] Add an authorized `/tasks/{taskId:int}` details page with related-document links and attach/upload actions in `ContosoDashboard/Pages/TaskDetails.razor`.
- [ ] T054 [US5] Add the authorized project-document section and upload entry point to the existing project view in `ContosoDashboard/Pages/ProjectDetails.razor`.
- [ ] T055 [US5] Connect the existing task list’s details action to the task route and register the task-document service in `ContosoDashboard/Pages/Tasks.razor` and `ContosoDashboard/Program.cs`.

**Checkpoint**: Task/project links never create a copy or bypass document authorization; personal
scope changes only after explicit owner confirmation.

---

## Phase 9: User Story 6 — See recent activity from the dashboard (Priority: P3)

**Goal**: Show the current user’s five latest accessible surviving uploads and accurate count, and
notify other currently entitled project members/manager after a successful project upload.

**Independent Test**: Seed more than five, fewer than five, and zero accessible personal uploads;
verify exact recency/counts and revocation filtering. Upload successfully to a project and verify
one preference-aware in-app notification for each other current member/manager, with no notices for
unrelated users or failed uploads/replacements.

### Tests for User Story 6

- [ ] T056 [P] [US6] Add dashboard query tests for five newest accessible surviving uploads, accurate counts, empty states, and removed-uploader exclusions in `ContosoDashboard.Tests/Unit/DocumentDashboardTests.cs`.
- [ ] T057 [P] [US6] Add notification tests for project upload recipient deduplication, manager/member overlap, actor exclusion, preferences, and no notice after failed upload/replacement in `ContosoDashboard.Tests/Integration/ProjectDocumentNotificationTests.cs`.

### Implementation for User Story 6

- [ ] T058 [US6] Extend `IDashboardService` and `DashboardService` to return five latest accessible surviving uploads and the authorized total count in `ContosoDashboard/Services/DashboardService.cs`.
- [ ] T059 [US6] Render recent documents, document count, accessible links, and a useful empty state on the dashboard in `ContosoDashboard/Pages/Index.razor`.
- [ ] T060 [US6] Send one in-app document notification to each other current project member and assigned manager with notifications enabled only after a successful project upload in `ContosoDashboard/Services/DocumentUploadService.cs`.

**Checkpoint**: Dashboard widgets/counts obey the same live authorization as the library, and
project upload notifications do not become access capabilities.

---

## Phase 10: User Story 7 — Review document activity (Priority: P3)

**Goal**: Record successful non-content document events and provide administrator-only date-range
reports for upload types, uploader rankings, and access activity, including deleted references.

**Independent Test**: Seed a known event set including deleted-document activity; compare report
aggregates exactly, confirm zero-result states, and verify non-administrators are denied without
audit disclosure.

### Tests for User Story 7

- [ ] T061 [P] [US7] Add report tests for date validation, exact upload/type/uploader/access aggregates, retained deleted-document references, and empty results in `ContosoDashboard.Tests/Unit/DocumentActivityReportTests.cs`.
- [ ] T062 [P] [US7] Add web/service authorization tests proving every non-administrator report request is denied and does not disclose activity details in `ContosoDashboard.Tests/Web/DocumentActivityAuthorizationTests.cs`.

### Implementation for User Story 7

- [ ] T063 [US7] Implement administrator-authorized event queries, inclusive date-range validation, upload/file-type and uploader aggregates, and per-user/document access counts in `ContosoDashboard/Services/DocumentActivityReportService.cs`.
- [ ] T064 [US7] Add an administrator-only activity/report page with date controls, validation, loading, empty, and error states in `ContosoDashboard/Pages/DocumentActivity.razor`.
- [ ] T065 [US7] Record successful preview/download events and lifecycle events through the audit writer, retaining only non-content references after deletion in `ContosoDashboard/Endpoints/DocumentContentEndpoint.cs` and `ContosoDashboard/Services/DocumentLifecycleService.cs`.
- [ ] T066 [US7] Add the administrator-only activity navigation entry and register/report-authorize the service in `ContosoDashboard/Shared/NavMenu.razor` and `ContosoDashboard/Program.cs`.

**Checkpoint**: Reports reconcile against recorded events, including deleted references, while
non-administrators receive denied behavior and no audit disclosure.

---

## Phase 11: Polish & Cross-Cutting Concerns

**Purpose**: Verify the integrated feature against all clarified lifecycle, authorization, offline,
usability, and performance requirements; update user guidance without expanding scope.

- [ ] T067 [P] Add end-to-end access-matrix tests across owner, employee, Team Lead, project manager, administrator, share recipient, unrelated, and unauthenticated identities in `ContosoDashboard.Tests/Integration/DocumentAuthorizationMatrixTests.cs`; cover removed-uploader denial across lists, search, counts, dashboard, task links, notifications, and content requests.
- [ ] T068 [P] Add failure-injection coverage for scanner unavailable/detected/timeout, storage exhaustion after partial staging, database commit failure, replacement rollback, deletion retry, stale references, concurrent version conflicts, and startup cleanup recovery in `ContosoDashboard.Tests/Integration/DocumentFailureRecoveryTests.cs`.
- [ ] T069 [P] Add a real-local-ClamAV integration test requiring the configured executable/signature database and verifying an approved safe test fixture is scanned; do not substitute a fake scanner for this integration check in `ContosoDashboard.Tests/Integration/LocalClamAvIntegrationTests.cs`.
- [ ] T070 Validate the integrated upgrade path against both a fresh isolated SQLite database and an isolated copy of the existing `EnsureCreated` schema, confirming unknown/partial schemas stop safely and `ContosoDashboard/ContosoDashboard.db` is never reset; record the repeatable procedure in `specs/001-document-management/quickstart.md`.
- [ ] T071 Run the documented restore, test, and build commands and complete upload, authorization, replacement/deletion, task, notification, report, and disconnected-network walkthroughs from `specs/001-document-management/quickstart.md`; use isolated temporary DB/storage for destructive lifecycle checks and record local scanner/performance prerequisites there.
- [ ] T072 Complete keyboard-only checks for every feature page/dialog: labels, visible focus, logical order, no keyboard trap, and visible textual loading/empty/progress/success/error/denied states. Do not add speech software or screen-reader-specific tests; record results in `ContosoDashboard.Tests/Web/DocumentUsabilityAcceptance.md`.
- [ ] T073 Measure the documented 25-MiB upload/30-second, 500-document query/2-second, and supported-preview/3-second criteria in the specified local environment; record CPU, storage, scanner readiness, and upload-path conditions in `specs/001-document-management/quickstart.md`.
- [ ] T074 Review `README.md`, `ContosoDashboard/Pages/`, and `ContosoDashboard/Shared/` to confirm training-only/non-production wording, private content delivery, and no cloud dependency, public share URL, version history, content-file search, or out-of-scope screen-reader integration.

---

## Dependencies & Execution Order

### Phase dependencies

- **Setup**: No prerequisites; create the test project first.
- **Foundational**: Depends on Setup. All user-story work depends on the complete data model,
  migrations, storage/scanner, current-authorization policy, audit writer, and recovery mechanism.
- **US1**: Depends on Foundational.
- **US2**: Depends on Foundational and the document records/storage created by US1; it can use
  isolated seeded documents for independent tests.
- **US8**: Depends on the P1 upload/library pages (US1/US2). Its end-to-end keyboard/offline
  criterion for sharing, replacement, and deletion is revalidated in Polish after US3–US5.
- **US3**: Depends on Foundational and US1/US2 lifecycle/content boundaries.
- **US4**: Depends on Foundational and US2 query/details authorization; notifications must
  reauthorize recipients on activation.
- **US5**: Depends on Foundational and US1/US2 upload, query, and content authorization.
- **US6**: Depends on US1 upload outcomes and US2 access-filtered queries; notifications remain
  identifiers, not capabilities.
- **US7**: Depends on Foundational audit persistence and the event-producing operations in US1–US6.
- **Polish**: Depends on all user stories selected for delivery; do not claim complete acceptance
  until all listed cross-cutting checks pass.

### User-story dependency graph

```text
Setup → Foundational
Foundational → US1 → US2 → US8 (P1 UI/offline baseline)
US1 + US2 → US3
US2 + Foundational → US4
US1 + US2 + Foundational → US5
US1 + US2 → US6
US1–US6 + Foundational audit → US7
US3 + US4 + US5 → US8 full journey recheck → Polish
US1–US7 → Polish
```

The story phases are listed by priority. Where a story depends on an earlier or later story’s
boundary (notably full US8 validation and US7’s accumulated event sources), the dependency above
controls actual completion; phase order alone does not waive prerequisites.

### Requirement coverage

| Specification requirements | Primary tasks |
|---|---|
| FR-001, FR-009, FR-011–FR-013, FR-019 (authentication, privacy, current role/membership/grant scope, queries, preview/download, removed-uploader denial) | T016, T026–T033, T067 |
| FR-002–FR-008 (metadata, categories, formats/size, local malware scan, per-file progress, original names, opaque storage, partial-write recovery) | T002, T013–T025, T068–T069 |
| FR-010, FR-012–FR-013 (authorized library, combined filters, search/sort, content response) | T026–T033, T067 |
| FR-014–FR-016, FR-028–FR-029 (metadata edit, replacement rollback, permanent deletion, concurrency, project deletion guard) | T038–T043, T068 |
| FR-017–FR-019, FR-023 (user/department shares, deduplication, current entitlement, new project-upload notifications) | T044–T049, T056–T060, T067 |
| FR-020–FR-021 (task access, attachment, task-context upload, explicit personal-to-project consent) | T050–T055, T067 |
| FR-022 (dashboard recent accessible uploads/count) | T056–T059, T067 |
| FR-024–FR-025 (non-content audit, deleted references, administrator-only date-range reports) | T006, T018, T061–T066 |
| FR-026–FR-027 (offline training-only journeys; keyboard, labels, focus, visible text; no screen-reader-specific scope) | T013–T019, T034–T037, T071–T072 |
| Clarification: project removal retains original project documents but denies the removed uploader, even via ownership/share, while preserving access for others | T016, T026, T067 |
| Clarifications: explicit project-document deletion before project deletion; preserve display filename but never use it as a path; deleting a shared document removes content/metadata/grants/task links and invalidates stale references | T002, T010–T012, T014, T029, T038–T043, T067–T068 |
| Clarification: storage exhaustion yields per-file retry, cleans partial artifacts, preserves independent successes and previous replacement content | T014, T017, T020–T023, T039, T068 |
| SC-001–SC-009 | T020–T021, T026–T027, T034–T035, T038–T039, T044–T045, T050–T051, T056–T057, T061–T062, T067–T073 |

### Parallel execution opportunities

- **Foundational entities**: T002–T008 can be authored in parallel because they are separate
  model files; T009 is a separate model-file change. T010 must wait for all of them.
- **Storage/scanning**: T014 and T015 can proceed in parallel after T013; T016 and T018 can
  proceed independently once the model and `ApplicationDbContext` are ready. T017 requires
  storage and journal implementations.
- **US1 tests**: T020 and T021 can be written in parallel; both precede the upload implementation.
- **US2 tests**: T026 and T027 can be written in parallel; query and content endpoint implementations
  then proceed before their UI integration.
- **US3 tests**: T038 and T039 can be written in parallel; lifecycle implementation follows.
- **US4 tests**: T044 and T045 can be written in parallel; sharing implementation follows.
- **US5 tests**: T050 and T051 can be written in parallel; task service and UI follow.
- **US6 tests**: T056 and T057 can be written in parallel before dashboard/notification changes.
- **US7 tests**: T061 and T062 can be written in parallel before report implementation.
- **Polish tests**: T067–T069 can be authored in parallel in separate test files; run them after
  all required feature code is integrated.

### Parallel examples

```text
Foundational: T002, T003, T004, T005, T006, T007, T008, T009
Storage setup after T013: T014 and T015
US1 test-first: T020 and T021
US2 test-first: T026 and T027
US3 test-first: T038 and T039
US4 test-first: T044 and T045
US5 test-first: T050 and T051
US6 test-first: T056 and T057
US7 test-first: T061 and T062
Polish test authoring: T067, T068, and T069
```

## Implementation Strategy

### MVP first

1. Complete Setup and Foundational tasks without changing or recreating the existing training DB.
2. Complete US1 to deliver safe, locally scanned upload with independent per-file outcomes.
3. Complete US2 to provide authorized discovery, search, preview, and download.
4. Validate the P1 increment against its independent criteria and the access-denial cases before
   starting later workflows.

### Incremental delivery

1. Add US8’s offline/accessibility baseline to the P1 upload/library journey.
2. Add US3 lifecycle and project-deletion guard; validate rollback, deletion, and concurrency.
3. Add US4 read-only sharing; validate overlapping grants and revocation.
4. Add US5 project/task links; validate project scope and explicit consent.
5. Add US6 dashboard discovery and project-upload notifications.
6. Add US7 administrator reports and exact event reconciliation.
7. Complete all-story offline, keyboard, authorization, scanner, failure-recovery, and performance
   checks before considering the feature complete.

## Notes

- Every checkbox task includes a sequential ID, applicable story label, and explicit implementation
  or verification file path. `[P]` is used only for independent work in separate files.
- Do not use user-supplied filenames as storage paths; keep content outside `wwwroot`, fail closed
  when malware scanning is unavailable, and recheck current authorization at every data/content
  boundary.
- Preserve the accepted project-removal rule: keep project documents and original ownership, but
  deny the removed uploader even through ownership or an existing share; retain access for other
  currently entitled users.
- Permanently deleting a document removes content, metadata, grants, and task links and invalidates
  stale references; project deletion remains blocked until every associated document is explicitly
  deleted. Storage exhaustion is per-file, retryable, cleans partial artifacts, and preserves
  independent successes and prior replacement content.
- Screen-reader/speech-specific integration and testing, cloud services, public general-purpose
  REST APIs, public share URLs, full-text file-content search, collaboration/version history,
  production deployment/security certification, and production audit compliance are excluded.
