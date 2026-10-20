# Feature Specification: Document Upload and Management

**Feature Branch**: `001-document-management`  
**Created**: 2026-10-20  
**Status**: Draft  
**Input**: `/speckit.specify --file StakeholderDocs/document-upload-and-management-feature.md`  
**Source**: [Stakeholder requirements](../../StakeholderDocs/document-upload-and-management-feature.md)  
**Governing document**: [Root constitution v1.0.0](../../.specify/memory/constitution.md)

Provide employees with one controlled place to upload, find, organize, access, and share work
documents in their existing dashboard. This release includes project and task integration,
notifications, and administrator activity reporting. It remains an offline, training-only
application, not a production document repository.

## Clarifications

### Session 2026-10-20

- Q: When a user is removed from a project after uploading documents to that project, what should happen to those documents? → A: Keep the documents in the project; revoke the former member's access on removal; current project members and authorized roles retain access.
- Q: What happens when a project with associated documents is deleted? → A: Block project deletion until all associated documents have been explicitly deleted.
- Q: How should filenames containing ordinary special characters be handled? → A: Preserve the original filename for display and download, while using a safe internal storage name that cannot interpret it as a path.
- Q: What happens when a shared document is permanently deleted? → A: Permanently remove its content, metadata, shares, and task links; revoke recipient access, including through stale links.
- Q: What happens if storage fills during an upload? → A: Report a per-file failure with retry guidance, expose no partial document, clean partial artifacts, preserve independently successful files, and leave the previous file intact on replacement.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Upload work documents safely (Priority: P1)

An employee uploads one or more documents with descriptive metadata and knows which files
succeeded, failed validation, or could not be saved.

**Why this priority**: Reliable, controlled upload is the foundation of the document library.

**Independent Test**: Upload personal and project files as an employee, including a mixed-validity
batch, then verify the resulting documents and per-file outcomes.

**Acceptance Scenarios**:

1. **Given** an authenticated employee, **When** they submit supported files of at most 25 MB with a title and category for each, **Then** each clean file is saved once, appears in My Documents with its captured metadata, and has a visible success outcome.
2. **Given** a batch containing a valid file, an oversized file, an unsupported file, and a known malware test fixture, **When** it is submitted, **Then** only the clean valid file becomes available and each rejected file has a specific error.
3. **Given** a user who is neither a member nor manager of a project and is not an administrator, **When** they attempt to upload to that project, **Then** the request is denied without creating a document.
4. **Given** incomplete metadata, unavailable malware checking, interrupted upload, or insufficient storage, **When** upload is attempted, **Then** no successful document is reported or exposed for that file, the problem is explained, and retry is possible.
5. **Given** completed upload feedback, **When** the user selects another file, **Then** they can upload it without reloading the page.
6. **Given** storage fills during a multi-file upload, **When** some files have already succeeded and another file cannot be saved, **Then** each affected file reports a storage failure with retry guidance, no partial document is exposed, partial artifacts are cleaned, independently successful files remain available, and any failed replacement leaves its previous file intact.

---

### User Story 2 - Find and access permitted documents (Priority: P1)

An employee browses their own documents and their project's documents, searches and filters the
library, and downloads or previews a permitted document.

**Why this priority**: Centralization is valuable only if documents can be found and accessed safely.

**Independent Test**: Use seeded documents with varied owners, categories, projects, and dates;
verify sorting, filtering, search, previews, downloads, and denied access.

**Acceptance Scenarios**:

1. **Given** documents uploaded by the current user, **When** My Documents is opened, sorted by each supported field, and filtered by category, project, and date range together, **Then** only matching owned documents the user may still access appear in the requested order with title, category, upload date, size, and project.
2. **Given** a project member, **When** they open that project's documents, **Then** they can view and download all its documents; an unrelated employee cannot do so.
3. **Given** permitted and forbidden documents matching a title, description, tag, uploader, or project query, **When** a search runs, **Then** only permitted matches appear, including in totals and suggestions.
4. **Given** a permitted PDF or image, **When** preview is requested, **Then** it opens in the browser without a separate download step; other supported types offer download instead.
5. **Given** an unauthenticated visitor or an authenticated user without access, **When** they use a direct document reference to request metadata, download, or preview, **Then** authentication is required or access is denied without disclosing the document.
6. **Given** a user who uploaded documents to a project, including a document with a task attachment and an existing share grant to that user, **When** the user is removed from the project, **Then** the documents remain in the project with their original uploader, content, metadata, shares, and task attachments unchanged; other current project members and authorized roles retain their permitted access, but the former member cannot list, search, preview, download, edit, replace, delete, or share those documents, including through direct references or old notifications. Ownership or an existing share MUST NOT bypass this revocation.

---

### User Story 3 - Maintain documents within role boundaries (Priority: P2)

Owners and authorized managers correct metadata, replace the current file, and permanently
delete documents that are no longer needed.

**Why this priority**: The library must remain accurate without allowing uncontrolled modification.

**Independent Test**: With existing documents, exercise edit, replacement, and confirmed deletion
as owner, team lead, project manager, administrator, and read-only recipient.

**Acceptance Scenarios**:

1. **Given** an owner or authorized managing role, **When** they edit title, description, category, or tags, **Then** the changes persist and the original uploader and upload time remain unchanged.
2. **Given** a document with shares and task attachments, **When** an authorized user replaces it with a valid, clean file, **Then** its identity, shares, and attachments remain, subsequent access returns the replacement, and no earlier version is offered.
3. **Given** a replacement that fails validation, malware checking, or saving, **When** replacement is attempted, **Then** the previous document remains accessible and unchanged.
4. **Given** an authorized deletion of a shared document, **When** the user cancels confirmation, **Then** nothing changes; **When** they confirm and deletion succeeds, **Then** the content, metadata, shares, and task attachments are permanently removed and recipients lose access, including through stale direct references, notifications, or share links.
5. **Given** a non-owner with read-only access or a manager outside their permitted scope, **When** they attempt edit, replacement, or deletion directly, **Then** the operation is denied and the document is unchanged.
6. **Given** a project with one or more associated documents, **When** an authorized user attempts to delete the project, **Then** project deletion is blocked until every associated document has been explicitly permanently deleted; deleting the project does not implicitly delete documents.

---

### User Story 4 - Share with colleagues and teams (Priority: P2)

A document owner shares a document with named users or a team, and recipients find it in Shared
with Me and receive an in-app notification.

**Why this priority**: Controlled sharing replaces uncontrolled copies and email attachments.

**Independent Test**: Share a personal document with one user and one department team, verify
recipient access and notifications, then remove a grant.

**Acceptance Scenarios**:

1. **Given** an owner and selected user or team recipients, **When** sharing succeeds, **Then** recipients receive read-only access, see the document in Shared with Me, and receive one notification per newly granted recipient with notifications enabled.
2. **Given** overlapping user and team grants, **When** a document is shared again, **Then** it appears once per recipient and unchanged grants produce no duplicate notifications.
3. **Given** a read-only recipient, **When** they try to change, delete, or reshare the document, **Then** access is denied.
4. **Given** an owner removing a share, **When** the recipient has no remaining ownership, role, project, or sharing entitlement, **Then** subsequent access is denied, including from an old notification.

---

### User Story 5 - Work with task and project documents (Priority: P2)

A task participant sees related documents, attaches eligible existing documents, and uploads
new documents directly while working on a task.

**Why this priority**: Contextual documents reduce the effort of locating supporting information.

**Independent Test**: Use an existing project task and documents; attach an eligible document and
upload another from the task context without relying on dashboard widgets or reporting.

**Acceptance Scenarios**:

1. **Given** a user authorized to view a task and upload to its project, **When** they upload from task details, **Then** the new document is attached to that task and associated with the task's project.
2. **Given** a user allowed to attach documents to a task, **When** they attach a permitted document already in the same project, **Then** it appears once in the task's related documents.
3. **Given** an owner attaching their personal document, **When** they confirm the displayed expansion of access to the task's project, **Then** it becomes a project document and is attached; cancellation leaves it personal and unattached.
4. **Given** a document in another project, a personal document owned by someone else, or a task outside the user's scope, **When** attachment is attempted, **Then** it is denied without changing access or project association.

---

### User Story 6 - See recent activity from the dashboard (Priority: P3)

An employee sees recent personal uploads and document totals on the dashboard and learns about
new project documents through the existing notification experience.

**Why this priority**: These entry points improve discovery after the core library is usable.

**Independent Test**: Seed uploads and project memberships, open the dashboard, and inspect
notifications after adding a project document.

**Acceptance Scenarios**:

1. **Given** at least six surviving uploads by the current user that they may still access, **When** they view the dashboard, **Then** Recent Documents shows only their five latest accessible uploads, newest first, and the document summary count equals their accessible surviving uploads.
2. **Given** fewer than five accessible uploads or none, **When** the dashboard opens, **Then** it shows the available documents or an explanatory empty state, with an accurate count.
3. **Given** a successful new project upload, **When** notifications are delivered, **Then** other current project members and its manager with notifications enabled receive one in-app notification each; unrelated users do not.

---

### User Story 7 - Review document activity (Priority: P3)

An administrator reviews document activities and reports to understand usage and investigate access.

**Why this priority**: Visibility supports oversight of the document library.

**Independent Test**: Seed upload, download, preview, deletion, and sharing events, then compare
reports for a selected period with the expected activity totals.

**Acceptance Scenarios**:

1. **Given** recorded document activity, **When** an administrator selects a date range, **Then** reports show upload counts by file type, uploader rankings by upload count, and access counts by user and document.
2. **Given** a deleted document, **When** its past activity is reviewed, **Then** the audit reference remains but deleted content cannot be retrieved.
3. **Given** a non-administrator, **When** they request audit records or reports directly, **Then** access is denied.

---

### User Story 8 - Use the library offline and accessibly (Priority: P1)

A training participant completes document journeys without internet access and can operate all
controls using a keyboard with understandable status and validation feedback.

**Why this priority**: Offline availability and accessible journeys are constitutional requirements.

**Independent Test**: Disconnect external networking, use local sample identities and documents,
and complete upload, find, share, preview, replacement, and deletion using only a keyboard.

**Acceptance Scenarios**:

1. **Given** the local training environment and its malware-checking prerequisite are ready, **When** external networking is unavailable, **Then** all document journeys and in-app notifications still work without cloud accounts.
2. **Given** a keyboard-only user, **When** they operate document pages and dialogs, **Then** every control has a clear label, visible focus, logical focus order, and no keyboard trap.
3. **Given** loading, an empty library, upload progress, validation failure, success, or access denial, **When** that state occurs, **Then** it is conveyed in understandable text and is available to assistive technology without relying on color alone.

### Edge Cases

- Exactly 25 MB is accepted; a larger or empty file is rejected. Each file in a batch is evaluated independently.
- The original filename, including ordinary special characters such as `Q4 Report (2025) - Finance & Ops.pdf`, is preserved for display and download. Filenames are not interpreted as paths; a safe internal storage name is used, and identical filenames must not overwrite unrelated documents.
- A supported extension paired with incompatible file content is rejected, even if the supplied type claims otherwise.
- A malware finding or unavailable check must never result in a downloadable or previewable document.
- If storage fills during upload, each affected file must report failure and retry guidance; no partial document may be exposed, partial artifacts must be cleaned, independently successful files must remain available, and a failed replacement must leave the previous file intact.
- Upload or replacement failure must not leave a visible broken document, lose the previous file, or falsely report success.
- Permanently deleting a shared document must remove its content, metadata, shares, and task links and deny recipients access through stale direct references, notifications, or share links.
- A project with associated documents cannot be deleted; each document must first be explicitly permanently deleted.
- Revoked membership or a changed department must be reflected in subsequent access; an empty department grants no team entitlement.
- Removing an uploader from a project must retain their documents in that project, not delete them, transfer ownership, or convert them to personal documents. The former member's access is revoked on removal, including ownership- or share-based access; listings, search, widgets, counts, task links, and notifications must not expose those documents to them. Other current project members and authorized roles retain their permitted access.
- A document named in a task, search result, widget, or notification must still be checked when opened.
- Concurrent changes must either succeed coherently or produce a retry message; deletion must not be undone by a competing edit or replacement.
- A failed permanent deletion must report failure rather than claim success; successful deletion leaves no retrievable content.
- An invalid date range must produce validation feedback; no matching documents or report events must produce an empty state.
- Unsupported previews must offer authorized download without presenting a broken preview.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST require the existing training identity for every document journey and enforce the access rules below both at the user entry point and at the underlying data/content operation.
- **FR-002**: Users MUST be able to select one or multiple files and provide a separate required nonblank title and category, optional description and tags, and optional eligible project for each file before submission.
- **FR-003**: Categories MUST be Project Documents, Team Resources, Personal Files, Reports, Presentations, and Other. Category labels MUST NOT themselves grant access.
- **FR-004**: The system MUST accept PDF, Word (.doc, .docx), Excel (.xls, .xlsx), PowerPoint (.ppt, .pptx), plain text (.txt), JPEG (.jpg, .jpeg), and PNG (.png), with a per-file limit of 25 MB (26,214,400 bytes), rejecting empty, oversized, unsupported, or content/type-mismatched files with specific feedback.
- **FR-005**: Every new or replacement file MUST undergo a local malware check before becoming a saved, usable document. A malicious result, check failure, or unavailable checker MUST reject the file; bypassing the check MUST NOT count as success.
- **FR-006**: Upload MUST provide progress and a success or failure outcome for each selected file. One file's failure MUST NOT invalidate independently successful files, and the user MUST be able to select new files after completion without reloading the page.
- **FR-007**: Each saved document MUST capture its original filename, current file size and type, initial upload date/time, and uploader identity; file type descriptions up to 255 characters MUST be preserved. The original filename, including ordinary special characters, MUST be preserved for display and download.
- **FR-008**: Saving MUST not expose incomplete documents or overwrite unrelated files, including those with identical filenames. A safe internal storage name distinct from the original filename MUST be used; the original filename MUST NOT be interpreted as a path or determine the storage location. Failure MUST not leave a browsable document with missing content or unmanaged partial artifacts.
- **FR-009**: Files MUST remain private by default and inaccessible through an unprotected file location. A supplied filename or document reference MUST NOT bypass authentication, authorization, or content validation.
- **FR-010**: My Documents MUST list only the current user's uploads they may still access, with title, category, upload date, size, and project, support ascending/descending sorting by title, upload date, category, and size, and combine category, project, and inclusive date-range filters.
- **FR-011**: Project Documents MUST show all documents associated with the selected project to its current members, its assigned manager, and administrators. Team leads MAY see the subset whose uploaders fall within their team scope.
- **FR-012**: Search MUST match title, description, tags, uploader name, and associated project name, case-insensitively, within documents the current user may read. Results, counts, and suggestions MUST reveal no unauthorized document information.
- **FR-013**: Users with read access MUST be able to download the current file; PDF, JPEG, and PNG MUST also support browser preview. Other supported types MUST have an explicit download alternative.
- **FR-014**: Owners and authorized managing roles MUST be able to edit title, description, category, and tags. Required-field validation MUST apply to edits; uploader identity and original upload time MUST remain unchanged.
- **FR-015**: Owners and authorized managing roles MUST be able to replace the current file under the same validation and malware rules as upload. Replacement MUST retain document identity, metadata other than file attributes, shares, and attachments; failure MUST preserve the previous file. Successful replacement MUST offer no version history or retrieval of the prior content.
- **FR-016**: Owners and authorized managing roles MUST receive a confirmation before permanent deletion. Cancellation MUST change nothing; success MUST permanently remove the content, metadata, shares/grants, and task links and revoke all recipient access, including through stale direct references, notifications, or share links, while preserving the activity audit.
- **FR-017**: Owners MUST be able to grant and remove read-only access for named existing users and department teams. Administrators MUST be able to do the same for any document; other managing roles MUST NOT reshare a document they do not own.
- **FR-018**: Explicitly shared documents MUST appear once in each entitled recipient's Shared with Me section. Sharing MUST notify each newly granted recipient once, excluding the actor and respecting notification preferences; duplicate or overlapping grants MUST NOT duplicate entries or notifications.
- **FR-019**: Access MUST be checked against current ownership, role scope, project membership, and share grants on every subsequent operation. Removing one grant MUST NOT remove access conferred by another valid entitlement, except for the following project-removal rule: when an uploader is removed from a project, their documents MUST remain associated with that project, preserving content, metadata, original uploader/owner, shares, and task attachments, but the former member MUST lose access to those documents on every subsequent operation. This denial takes precedence over their other document entitlements, including ownership and existing shares. Other current project members and authorized roles MUST retain their permitted access.
- **FR-020**: An authorized task viewer who is also a member or manager of its project, or an administrator authorized to view that task, MUST be able to view permitted related documents, attach an eligible document once, and upload from task details.
- **FR-021**: Task uploads MUST automatically belong to the task's project. Attaching an existing document MUST require either the same project association or ownership of an unassociated personal document; the latter MUST explicitly confirm expanded project access. Documents in other projects or others' personal documents MUST NOT be reassigned by attachment.
- **FR-022**: The dashboard MUST show the current user's five most recent surviving uploads they may still access, newest first, and a summary count of all their accessible surviving uploads. Revoked documents MUST NOT appear in widgets or counts. A smaller collection MUST show all available items and an empty collection MUST show guidance.
- **FR-023**: A successful new project upload MUST notify other current project members and its assigned manager once each through existing in-app notifications, respecting preferences. Failed uploads, metadata edits, and replacements MUST NOT create new-upload notifications.
- **FR-024**: The system MUST record successful uploads, replacements, metadata edits, downloads, previews, deletions, and share additions/removals with actor, time, document reference, and action. Deleted content MUST NOT be retained in the audit.
- **FR-025**: Only administrators MUST be able to review activity records and generate date-range reports showing upload counts by file type, uploader rankings, and access counts by user and document, including zero-result states.
- **FR-026**: Core document journeys, malware checking, reporting, and notifications MUST work using local resources without internet access or cloud accounts. User guidance MUST identify this as training-only and not production-ready.
- **FR-027**: Document journeys MUST provide labeled, keyboard-operable controls, visible focus, accessible status/progress feedback, understandable validation messages, and loading, success, error, and empty states.
- **FR-028**: Concurrent edits, replacement, and deletion MUST NOT restore deleted content or silently publish a partial result. A conflicting operation MUST either complete consistently or fail with an actionable retry message.
- **FR-029**: Project deletion MUST be blocked while any documents remain associated with that project. Each associated document MUST be explicitly permanently deleted before project deletion may proceed; deleting a project MUST NOT implicitly delete its documents.

### Authorization and Ownership

Except for the project-removal denial in FR-019, entitlements are additive: a user may qualify
through ownership, role scope, project membership, or an explicit share. When an uploader is
removed from a project, the denial overrides their other entitlements to documents they uploaded
to that project; all permissions in the table below are subject to this exception. Other current
project members and authorized roles retain their permitted access.
A document remains owned by its original uploader after project removal, replacement, or
management by another role. "Manage" means edit metadata, replace content, and permanently delete;
it does not include resharing unless the user is the owner or an administrator.

| Actor or entitlement | Read/download/preview | Manage | Upload project documents | Share/remove grants |
| --- | --- | --- | --- | --- |
| Owner | Own documents | Own documents | Only eligible projects | Own documents |
| Project member | All documents in that project | Only if also owner or managing role | That project | Only if owner |
| Team Lead | Documents uploaded by users in the lead's current department | Same department scope | Only projects they belong to or manage | Only if owner |
| Assigned Project Manager | All documents in managed projects | Managed projects | Managed projects | Only if owner |
| Administrator | All documents | All documents | All projects | All documents |
| Named or team share recipient | Shared documents | None from sharing alone | None from sharing alone | None from sharing alone |
| Unauthenticated or unrelated user | None | None | None | None |

All authenticated roles may upload personal documents. Team Lead scope includes a team member's
personal uploads, as the stakeholder request explicitly grants management of team-member uploads.
The upload experience MUST explain that personal means not project-associated, not invisible to
authorized team leads or administrators. A manager title without the relevant scope grants no
global document access.

### Key Entities

- **Document**: A single current file and its title, description, category, tags, original filename,
  size, file type, upload time, original uploader/owner, and optional associated project.
  The original filename is user-visible display/download metadata; a distinct safe internal storage
  name prevents the supplied filename from being interpreted as a path.
  Project removal does not change the original uploader/owner or project association; retained
  ownership does not confer access on an uploader removed from that project.
- **Document Share**: A read-only grant connecting a document to one user or one department team,
  with the grantor and grant time.
- **Task Document Attachment**: A link between a document and a task in that document's project;
  linking a document does not create a separate copy.
- **Document Activity**: An action, actor, time, and document reference retained for administrator
  reporting even after the original document is deleted.
- **Existing User, Department Team, Project, and Task**: The identities, roles, membership, and
  work context that govern access. A department team groups current users with that department.
- **In-app Notification**: An existing notification addressed to a user for a new share or new
  project document; its link never grants access by itself.

### Assumptions and Dependencies

- The root `.specify/` is authoritative. The nested application-level constitution is an
  unfilled template and does not supersede the adopted root constitution.
- Existing identities, roles, project manager assignments, project memberships, task access,
  and in-app notifications remain the source of truth. No new identity or team administration
  journey is included.
- Because the current user model has departments rather than a separate team membership model,
  a team means a nonblank department. Team shares and Team Lead scope follow current department
  membership; users without a department gain no department-based entitlement.
- Selecting files starts a guided upload flow. The three-click goal excludes file-picker
  navigation, typing metadata, and choosing metadata values; it measures navigation/submit actions
  from the document library when the required values are ready.
- Date filters use the user's displayed calendar dates, inclusively. Replacements do not change
  original upload date or create another Recent Documents entry.
- Local writable storage and local malware checking are prerequisites. The checker must provide
  actual scanning; a simulated successful check is not an acceptable substitute. No specific
  scanner, external service, or production assurance is selected by this specification.
- Existing in-app notification preferences are respected. Notification links recheck access and
  handle deleted content. Team-share notifications target the team's members at the time of sharing;
  future members gain access through membership but do not receive a retrospective notification.
- Activity records are retained for the training dataset's lifetime; no legal compliance or
  production retention certification is claimed.
- Performance targets are measured on a documented local training environment with writable
  storage and malware checking ready, up to 500 documents visible to a user, and an upload path
  sustaining at least 10 Mbit/s. Upload timing includes checking and saving; user metadata entry
  time is excluded.
- The stakeholder's implementation sketches, storage interfaces, key types, category persistence,
  and framework-specific advice are design inputs for later planning, not prescribed implementation
  here. The safe internal naming mechanism is not selected by this specification. No database reset
  or destructive cleanup is required by this workflow.
- The stakeholder's “production-ready within 8–10 weeks” wording is treated as a delivery
  aspiration only: the constitution's training-only, non-production constraint takes precedence.

### Out of Scope

- Collaborative editing, version history or rollback, approval/routing workflows.
- External document platforms, cloud deployment or migration, production identity/security certification.
- Native mobile applications, document templates or generation, quotas, trash or recovery.
- Full-text file-content search, editing files in the browser, and previews beyond PDF/JPEG/PNG.
- Production audit compliance and legally mandated retention policies.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In the agreed training environment, at least 95% of valid uploads up to 25 MB become
  available with success feedback within 30 seconds from submission.
- **SC-002**: With up to 500 visible documents, at least 95% of document-list openings and searches
  show usable results within 2 seconds; at least 95% of supported previews become visible within 3 seconds.
- **SC-003**: In a representative employee exercise, at least 90% of participants upload a
  categorized document, locate it, and share it without assistance; the navigation/submit portion
  of upload requires at most three clicks under the stated counting convention.
- **SC-004**: Participants locate a specified permitted document in an average of less than
  30 seconds across representative search and browse tasks.
- **SC-005**: Every case in the acceptance access matrix passes: permitted operations succeed
  and all unauthenticated, unrelated, read-only modification, and revoked-entitlement attempts
  disclose no unauthorized document content or metadata. Project-removal cases MUST verify
  document retention, continued access for other current project members and authorized roles,
  and denial of the removed uploader's access, including through ownership, existing shares,
  lists, search, dashboard widgets/counts, task links, and old notifications. Shared-document
  deletion MUST remove document content, metadata, shares, and task links and deny access through
  stale recipient references; project deletion MUST remain blocked until associated documents are
  explicitly deleted.
- **SC-006**: All known-malware, unsupported-type, oversize, unavailable-check, failed-save, and
  failed-replacement and storage-exhaustion acceptance cases preserve the required safety outcome;
  no failed new upload becomes available, partial artifacts are cleaned, independently successful
  batch files remain available, and no failed replacement destroys the previous file.
- **SC-007**: Every primary journey passes with external networking disconnected and with
  keyboard-only operation; every required loading, empty, progress, error, and success state
  has understandable text available to assistive technology.
- **SC-008**: Audit reports exactly reconcile with a known set of upload and access events,
  including activity for deleted documents, and all non-administrator report requests are denied.
- **SC-009**: In a three-month stakeholder pilot, at least 70% of active dashboard users upload
  a document, at least 90% of sampled uploads have a category judged appropriate by stakeholder
  reviewers, and there are zero confirmed unauthorized-document-access incidents. An active user
  is a distinct user with at least one dashboard visit during that period; these pilot measures
  are follow-up business outcomes, not a claim of production readiness.
