<!--
Sync Impact Report
- Version change: template/unversioned -> 1.0.0 (initial adoption; establishes project-wide principles)
- Principles established (initial adoption): I. Training-First and Offline-Ready; II. Authorization
  at Every Data Boundary; III. Layered, Testable Design; IV. Usable and Accessible Journeys;
  V. Specification and Verification Traceability (no prior principle titles)
- Added sections: Additional Constraints; Development Workflow
- Removed sections: none
- Templates: ✅ .specify/templates/plan-template.md; ✅ .specify/templates/spec-template.md;
  ✅ .specify/templates/tasks-template.md
- Runtime guidance: ✅ README.md reviewed; no update required. No
  .specify/templates/commands/*.md files exist; repository Speckit agent guidance reviewed.
- Follow-up TODOs: confirm the original ratification date (not recorded in repository history);
  no test project exists yet, so the first behavioral feature must add one or document an
  approved alternative verification approach.
-->

# ContosoDashboard Constitution

## Core Principles

### I. Training-First and Offline-Ready
Changes MUST preserve the application's local, offline training experience unless a feature
specification explicitly approves a scope change. The application and its mock authentication
MUST be described as training-only and not production-ready. This keeps the project usable
without cloud accounts or external services and prevents training examples from being mistaken
for deployment guidance.

### II. Authorization at Every Data Boundary
Protected user journeys MUST enforce authentication and authorization at both the UI entry point
and the service/data boundary. Data access MUST respect the current user's role, ownership, and
resource membership; hiding a UI control alone is not authorization. Changes affecting protected
data MUST include acceptance scenarios for both permitted and denied access. This preserves the
project's defense-in-depth and user-isolation teaching goals.

### III. Layered, Testable Design
New behavior MUST follow the existing separation between Blazor pages, services, models, and
EF Core data access. Infrastructure dependencies MUST use the established dependency-injection
and abstraction patterns where a replaceable implementation is needed. Business rules MUST
remain testable outside UI rendering where practical; this keeps the training code understandable
and supports the documented local-to-cloud migration path without requiring cloud services.

### IV. Usable and Accessible Journeys
User-facing changes MUST provide clear labels, keyboard-operable interactions, visible focus,
and understandable loading, success, empty, and error states as applicable. Acceptance scenarios
MUST cover the primary user journey and relevant boundary or failure states. These requirements
make the dashboard usable and the demonstrated behaviors independently verifiable.

### V. Specification and Verification Traceability
Each behavioral change MUST trace from a user need and testable acceptance criteria through
implementation tasks to verification. Automated tests MUST cover changed behavior, including
authorization boundaries when affected. If automation is not feasible, the plan MUST state why
and define a repeatable alternative validation. A feature MUST pass its relevant tests and build
checks before completion; documentation-only changes may omit behavioral tests.

## Additional Constraints

- The current application baseline is ASP.NET Core 8, Blazor Server, Entity Framework Core, and
  SQLite. Plans MUST identify and justify changes to this baseline.
- The default application MUST remain self-contained and local; cloud or other external service
  integrations require explicit feature scope and an offline-compatible training path.
- Mock authentication and authorization are educational examples only. Documentation MUST NOT
  imply they are suitable for production deployment.
- New functionality MUST fit the existing `ContosoDashboard/` project structure unless the plan
  records a concrete architectural need for a different structure.

## Development Workflow

- A feature specification MUST define observable acceptance scenarios and measurable outcomes.
  It MUST call out authentication, authorization, data ownership, offline operation, and
  accessibility constraints when the feature affects them.
- The implementation plan MUST complete the Constitution Check before research and repeat it
  after design. Any exception MUST identify the violated principle, rationale, and mitigation.
- Tasks MUST include verification for changed behavior and cross-cutting authorization or
  accessibility checks where applicable. Test tasks precede implementation when a failing test
  can be written first.
- Before completion, contributors MUST run the relevant automated tests and a .NET build, and
  MUST update user-facing documentation when behavior or setup instructions change.

## Governance

This constitution governs feature specifications, plans, tasks, implementation, and review.
Amendments MUST be proposed with rationale in a reviewed change, include an impact assessment
for dependent templates and guidance, and be approved by project maintainers before adoption.
Every amendment MUST update the version and last-amended date. Versioning follows semantic
versioning: MAJOR for incompatible governance or principle changes, MINOR for new principles or
materially expanded requirements, and PATCH for clarifications that do not change obligations.
Feature plans and implementation reviews MUST check compliance; any exception MUST be explicit,
justified, and recorded in the plan. The Constitution Check in
`.specify/templates/plan-template.md` is the planning record for that review.

**Version**: 1.0.0 | **Ratified**: TODO(RATIFICATION_DATE): original adoption date unknown in repository history | **Last Amended**: 2026-10-20
