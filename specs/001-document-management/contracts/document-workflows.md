# Document Workflow Contracts

These contracts describe the existing authenticated Blazor application surface and the one
streaming HTTP boundary needed for preview/download. They are not a promise of a general-purpose
public REST API. All service operations derive the requester from the authenticated training
principal; callers must not choose a user ID to impersonate.

## User journeys

| Surface | Required access and behavior |
|---|---|
| `/documents` | Authenticated library with My Documents, Project Documents available to permitted users, and Shared with Me. Search and filter results/counts are limited to documents the current user can read. |
| `/documents/{documentId:guid}` | Authenticated metadata/details view; rechecks current access and returns no document metadata when denied/deleted/pending. |
| `/projects/{projectId:int}` | Existing authorized project page gains a project-document section and upload entry point. Project membership/manager/admin/team-lead scope is checked in the document service, not only by the page. |
| `/tasks/{taskId:int}` or existing task details | Related document list and attach/upload actions. The task must be viewable and eligible; cross-project attachment is denied. Expansion from personal to project scope requires explicit owner confirmation. |
| `/documents/activity` | Administrator-only activity and date-range reports; all other roles receive denied behavior without audit disclosure. |
| Existing notifications | Document links navigate using a document identifier, never a public storage path. Notification list and link activation recheck current entitlement. |

All pages use labeled keyboard-operable controls, visible focus, logical focus order, a non-color
status channel, visible textual upload progress/results, and loading, empty, validation, success,
error, and access-denied states. Speech software integration and screen-reader-specific requirements
and tests are excluded; native browser compatibility is not deliberately disabled.

## Authenticated content response

### `GET /document-content/{documentId}`

| Item | Contract |
|---|---|
| Authentication | Existing training cookie identity required. |
| Path parameter | `documentId`: canonical GUID for a current document; not a storage key. |
| Query | `mode=inline` for PDF/JPEG/PNG preview; `mode=download` for attachment. Defaults to `download`. Unsupported inline types are denied as preview and expose an authorized download alternative in the UI. |
| Authorization | Re-evaluate current document access for every request. Apply the removed-project-uploader denial, share revocation, pending-operation state, and deletion state. |
| `200` | Stream current bytes with canonical detected content type, exact original display filename in a safely encoded `Content-Disposition`, and `X-Content-Type-Options: nosniff`. No byte-array buffering is required. |
| `302`/`401` | Unauthenticated request follows existing cookie login behavior or is returned as unauthenticated by the host; response contains no document data. |
| `404` | Document absent, deleted, pending, or not readable; use a uniform response to avoid document enumeration. |
| `400` | Invalid mode or malformed identifier; no storage path accepted. |
| `5xx` | Local storage failure; do not return partial content as a successful response. Log a safe operation/document reference, not file bytes or secrets. |

No route accepts an original filename, storage path, scanner output, or caller-supplied user ID.
Content is never served by `UseStaticFiles`.

## Service command/query contract

Implementation may use C# request/result types rather than HTTP routes. Each operation must be
authorized in the service boundary and return a typed result without leaking denied metadata.

| Operation | Inputs | Successful result | Failure behavior |
|---|---|---|---|
| List/search/filter | Authenticated caller; search, filters, sort/page | Only readable documents; counts and suggestions use the same authorized set | Invalid date range returns field validation; denied project returns empty/denied without metadata |
| Upload batch | Caller; per-file stream plus title/category/description/tags/eligible project | Independent per-file committed outcomes and byte progress | `empty-file`, `size-exceeded`, `unsupported-type`, `content-type-mismatch`, `malware-detected`, `scanner-unavailable`, `storage-full`, `storage-failure`, `invalid-metadata`, `authorization-denied`; no failed item is visible |
| Edit metadata | Caller; document ID, expected version, metadata | Updated metadata and next version; original uploader/time unchanged | `not-found-or-denied`, `validation-error`, `concurrency-conflict` |
| Replace file | Caller; document ID, expected version, stream and filename/type metadata | Same document ID with new current content; shares and attachments unchanged | Same validation/scanner/storage errors as upload; prior file remains current on failure |
| Permanently delete | Caller; document ID, expected version, explicit confirmation | Content, metadata, grants, and task links removed; audit reference retained | Cancel is no-op; failure reports unsuccessful/retryable and access remains denied while recovery is pending |
| Share/remove grant | Caller; document ID; exactly one user or nonblank department target | One effective recipient entry and one notification per newly entitled recipient | Owner/admin scope required; invalid target or duplicate operation does not duplicate access or notification |
| Attach/upload from task | Caller; task ID and document ID or per-file metadata | Single same-project attachment; new upload belongs to task project | Ineligible task/project, someone else's personal file, cross-project document, or missing consent is denied with no reassignment |
| Dashboard/report | Caller; optional report dates | Accessible recent items/counts; administrators receive audit aggregates | Non-admin report call denied; invalid dates validated; zero records return an empty state |

## Stable failure identifiers

Use stable machine-readable codes in result types and map them to understandable text in the UI:

- `invalid-metadata`
- `empty-file`
- `size-exceeded`
- `unsupported-type`
- `content-type-mismatch`
- `malware-detected`
- `scanner-unavailable`
- `scanner-error`
- `storage-full`
- `storage-failure`
- `authorization-denied`
- `not-found-or-denied`
- `concurrency-conflict`
- `project-documents-remain`
- `invalid-date-range`

Do not display scanner process output, local filesystem paths, unauthorized document titles, or
database exception details to the user.
