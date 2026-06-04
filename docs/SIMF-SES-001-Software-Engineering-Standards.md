# Software Engineering Standards and Conventions

| Field | Value |
|-------|-------|
| Document ID | SIMF-SES-001 |
| Title | Software Engineering Standards and Conventions |
| Version | 1.1 |
| Status | Approved |
| Classification | Confidential — to be confirmed by the owner |
| Prepared by | Engineering & Architecture Team, STARTIME |
| Owner | Solution Architect |
| Approver | Solution Architect |
| Date issued | 2026-05-20 |
| Related documents | SIMF-DMP-001, SIMF-PGP-001, SIMF-CON-001, SIMF-SAD-001 |

### Revision history

| Version | Date | Author | Summary of change |
|---------|------|--------|-------------------|
| 1.0 | 2026-05-20 | Engineering & Architecture Team | First issue. |
| 1.1 | 2026-05-20 | Engineering & Architecture Team | Added §4.4 Configuration and environment; added §6.3 Shared component library (Smif*) and amended §6.1; added open item OI-5. |

---

## 1. Purpose

This document is the engineering rulebook for SIMF. It tells everyone who writes
code for the project how the code is structured, named, reviewed, tested and
secured. Its job is to make any SIMF file look like it was written by one
careful team, and to keep that true as the team grows during the build phase.

The rules here are not suggestions. A pull request that breaks one of them does
not merge. Where a rule has an exception, the exception is written down, not
improvised.

## 2. Scope

The standards apply to all SIMF source code and its three deliverables: the
.NET backend and APIs, the Blazor Control Panel, and the Flutter mobile
application. They also cover the Git repository, the build, and the definition
of when a piece of work is finished.

They do not cover infrastructure provisioning or the CI/CD pipeline design;
those belong to the operations document (SIMF-OPS-001) and the programme plan.

## 3. Engineering principles

Five principles sit above every specific rule. When a specific rule is unclear,
these decide.

1. **Readable beats clever.** Code is read far more often than it is written.
   A junior engineer should be able to follow any SIMF method without a guide.
2. **One clear way.** For any given job there is one accepted pattern in the
   codebase. Duplication and second ways of doing the same thing are removed,
   not accumulated.
3. **No hidden work.** A change does only what its task says. No drive-by
   refactors, renames, reformatting or deletions ride along inside an unrelated
   change.
4. **Fail loudly.** An invalid state is logged and throws. The code never
   swallows an error or quietly carries on with a guessed value.
5. **Prove it.** Behaviour changes come with tests. "It works on my machine"
   is not evidence.

## 4. Solution and repository structure

### 4.1 Repository layout

The repository is organised so that the three deliverables and the shared code
are obvious at the top level.

```
/src
  /Backend        .NET solution: domain, application, infrastructure, API
  /ControlPanel   Blazor Server/WebAssembly Control Panel
  /MobileApp      Flutter application
  /Shared         Cross-cutting .NET projects (contracts, enums, constants)
/tests            Test projects, mirroring /src
/docs             Controlled documents (see SIMF-DMP-001)
/build            Build and pipeline scripts
```

The exact .NET project breakdown inside `/src/Backend` is defined by the
architecture document (SIMF-SAD-001). This section fixes only the top-level
shape.

### 4.2 Domain-driven layering

The backend follows a domain-driven design layering. Dependencies point inward
only.

```
API / Presentation  →  Application  →  Domain
        │                  │
        └──────────────────┴──→  Infrastructure  →  Domain
```

- **Domain** holds entities, aggregates, value objects, domain events and the
  business rules. It has no dependency on a framework, on EF Core, or on the
  web. If a domain rule needs a database to be expressed, the model is wrong.
- **Application** holds use-case logic, orchestrates the domain, and defines the
  interfaces that infrastructure implements.
- **Infrastructure** holds EF Core, external services, messaging and anything
  that talks to the outside world.
- **API / Presentation** holds the FastEndpoints endpoints and request/response
  contracts.

The Control Panel and the mobile app are separate presentation surfaces over
the same API. Neither reaches into the backend's internal layers; both go
through the published API.

### 4.3 Project settings

`Directory.Build.props` sets `Nullable`, `ImplicitUsings` and
`TreatWarningsAsErrors` once, for the whole solution. Individual `.csproj` files
do not redeclare these. Project files are not edited as a side effect of
feature work; a change to a `.csproj` is its own reviewed task.

### 4.4 Configuration and environment

Application configuration is split by environment, and production secrets are
kept out of the repository entirely.

- `appsettings.json` holds settings that are shared across environments and are
  not sensitive. No secret — no connection string, key or token — is placed in
  it.
- `appsettings.Development.json` holds the overrides for local development.
- `appsettings.E2E.json` holds the overrides for the end-to-end test
  environment.
- There is no `appsettings.Production.json`. Production configuration is not
  carried in a committed file.

Production configuration — the production overrides and every secret, including
connection strings, the JWT signing key and external provider keys — is applied
as Machine-scope environment variables. Each service has its own script,
`set-env-<service>.ps1` (for example `set-env-api.ps1`), that sets them. The
variables use the ASP.NET Core double-underscore convention, where `__` maps to
a nested configuration key — for example `ConnectionStrings__AppData`.

The `set-env-<service>.ps1` script committed to the repository is a placeholder
template only: it lists the variable names with empty or dummy values. Real
secret values are never committed. This follows the secrets rule in section 12
and the data-protection approach in SIMF-SAD-001 section 8.4.

## 5. Backend conventions (.NET and C#)

### 5.1 Style

- No clever code. No deep nesting. Guard clauses and early returns instead of
  arrowheads of nested `if` blocks.
- Small methods with explicit names. A method name says what it does without a
  comment.
- No utility or helper classes created only to look tidy. Logic stays in the
  feature it belongs to, or in a clearly named service.
- No magic strings or magic numbers. Use constants and enums. Roles come from
  `AppRoles` in `OnlineErpSystem.V10.Shared.Enums` style constants, never the
  literal string `"Admin"`.
- Comments explain why, not what. Code that needs a comment to explain what it
  does is usually code that needs renaming or splitting.

### 5.2 API endpoints

The backend uses FastEndpoints. Every endpoint:

- Implements `Configure()` and `HandleAsync()`.
- Returns the standard `ApiResult<T>` wrapper, so every response — success or
  failure — has the same shape. The contract for `ApiResult<T>` is defined in
  SIMF-API-001.
- Declares its authorisation. No endpoint is anonymous unless it is on the
  short, explicitly approved list (sign-in, sign-up, password reset). Every
  other endpoint enforces a role or permission.
- Follows the standard CRUD shape where it is a CRUD endpoint: GET list, GET by
  id, POST create, PUT update, DELETE for a soft delete.

### 5.3 Validation

Validation rules are stated once per layer and kept in step across layers. A
field's maximum length in FluentValidation (`MaximumLength(n)`), in the EF
configuration (`HasMaxLength(n)`), and in the UI (`MaxLength="n"`) is the same
number. When that number changes, it changes in all three places in the same
commit.

Validation failures throw `DataValidationException`. Illegal state transitions
in the domain throw a domain-specific exception named for the rule it protects.

### 5.4 Persistence

- EF Core, code-first migrations.
- Soft delete is the default. `entity.Deactivate()` sets `IsActive = false`.
  List endpoints filter on `Where(x => x.IsActive)`. Rows are not physically
  deleted unless a specific, reviewed reason says otherwise.
- A migration is reviewed like code. Generated migrations are read before they
  are committed, not trusted blindly.

### 5.5 Errors and logging

- Exceptions are never swallowed. An empty `catch` block does not pass review.
- Structured logging through Serilog. Log messages carry context (the entity
  id, the user, the operation), not just a sentence.
- User input that fails a check produces a clear, localised error through
  `ApiResult<T>`; it does not produce a stack trace in the response.

## 6. Control Panel conventions (Blazor)

### 6.1 Component model

The Control Panel is a Blazor application using the MudBlazor component library.
A page does not place MudBlazor components directly; it composes its UI from the
shared `Smif*` component library (section 6.3), whose Control Panel components
wrap MudBlazor. Components are kept small and focused. A page composes
components; it does not become a thousand-line file.

### 6.2 CSS and theming

The Control Panel must support Arabic and English (RTL and LTR) and more than
one visual theme. The CSS rules protect that.

- No inline styles. The `style="..."` attribute is not used.
- The styling order of preference is: MudBlazor component properties first,
  then theme overrides, then global CSS, then scoped component CSS, then page
  CSS. A lower layer is used only when the one above genuinely cannot do the
  job.
- `theme.tokens.css` is the single source of truth for colours, fonts, shadows,
  radii and spacing. There are zero hardcoded hex colours, zero hardcoded
  font-family values, and zero duplicate `:root` or `[data-theme]` blocks
  outside that file. A token that does not exist yet is added to
  `theme.tokens.css` first, then used.
- Colours come from the MudBlazor `Color` enum or from CSS variables, never as
  raw values in a component.
- Class names follow BEM. CSS variables are used, not repeated literals.

The token values themselves come from the brand. **SIMF-VID-001 (Visual
Identity and Design Tokens)** is the source for the colour palette, the
typography and the brand tokens; `theme.tokens.css` implements SIMF-VID-001. A
colour or font is never set from anywhere else.

The full theming and layout design, including how RTL/LTR and multi-theme are
implemented, is specified in SIMF-CPD-001, which builds on SIMF-VID-001.

### 6.3 Shared component library (Smif*)

SIMF's user interfaces are built from a shared library of wrapper components
whose names all carry the `Smif` prefix — `SmifButton`, `SmifInputText`,
`SmifInputNumber`, `SmifInputCheck`, `SmifInputTabs`, `SmifInputDropdownList`,
`SmifError`, `SmifBanner`, `SmifTable`, `SmifPager`, `SmifPopup`, `SmifConfirm`,
`SmifLoader`, and the rest as they are needed.

- Both the Control Panel (Blazor) and the mobile application (Flutter) compose
  their screens from `Smif*` components. The two platforms keep the same
  component vocabulary even though their underlying technologies differ.
- A page never places a raw HTML input, a raw framework widget, or a framework
  primitive directly. It composes `Smif*` components.
- A UI primitive is added to the `Smif*` library first, and only then used in a
  page. The library grows ahead of the pages, not as a by-product of them.
- In the Control Panel, a `Smif*` component wraps the matching MudBlazor
  component (section 6.1). In the Flutter app, a `Smif*` component wraps the
  matching widget from the design system delivered by the external designer
  (SIMF-MAA-001 section 12).

The `Smif*` layer is a thin wrapper, not a replacement for the underlying
library or the designer's component library. It gives SIMF one consistent
component vocabulary, one place to apply a cross-cutting change, and the freedom
to adjust the underlying library without rewriting every page.

How the `Smif*` layer reconciles with the external designer's component-library
handoff in SIMF-MAA-001 section 12 is recorded as open item OI-5 for the
Solution Architect to confirm.

## 7. Mobile application conventions (Flutter)

The mobile application is built in Flutter for Android and iOS from one
codebase. Its detailed architecture — folder structure, state management,
networking layer, localisation and theming — is defined in SIMF-MAA-001. The
baseline rules that apply regardless of those choices:

- Dart code is formatted with `dart format` and analysed with the project
  `analysis_options.yaml`. The analyzer runs clean.
- The app talks to the backend only through the API. It holds no business rule
  that belongs in the domain.
- Arabic is the primary language and the app is RTL-first; English and LTR are
  fully supported. No user-facing string is hardcoded; all strings go through
  the localisation system.
- The visual design is produced by the external UI/UX designer. The Flutter
  team builds the structure, navigation and API integration against the agreed
  41-screen scope, and applies the visual design when it is delivered. The
  handoff contract with the designer is defined in SIMF-MAA-001.

## 8. Naming conventions

| Element | Convention | Example |
|---------|------------|---------|
| C# namespace, class, method, property | PascalCase | `RegistrationService` |
| C# local variable, parameter | camelCase | `visitorId` |
| C# constant | PascalCase | `MaxNameLength` |
| C# interface | PascalCase with `I` prefix | `IRegistrationRepository` |
| C# async method | PascalCase with `Async` suffix | `ApproveAsync` |
| Dart class | PascalCase | `SessionCard` |
| Dart variable, function | camelCase | `loadAgenda` |
| Dart file | snake_case | `session_card.dart` |
| Database table | PascalCase, singular | `Visitor` |
| Database column | PascalCase | `IsActive` |
| CSS class | BEM, lower kebab-case | `cp-card__header--active` |
| Git branch | see section 9 | `feature/SIMF-123-login-api` |

Names are in English. They describe the thing, not its type — `visitors`, not
`visitorList`. Abbreviations are avoided unless they are well known in the
domain (`QR`, `OTP`, `VIP`).

## 9. Source control

### 9.1 Branching

The repository uses a trunk-based model with short-lived branches.

- `main` is always releasable. It is protected; nothing is pushed to it
  directly.
- Work happens on a branch named `<type>/<work-item>-<short-title>`, where type
  is `feature`, `fix`, `chore` or `docs`. Example:
  `feature/SIMF-123-login-api`.
- A branch is short-lived. It is merged within a few days, not left to drift
  for weeks.

A safety rule applies before any file is edited on a task: there is a clean
checkpoint commit to return to. Destructive Git operations — hard reset, force
push, `checkout -- .`, history rewrites — are not run without explicit approval
from the project lead, even when something is broken.

### 9.2 Commits

- One commit is one coherent change. It builds and its tests pass.
- The message has a short imperative summary line, then a body explaining why
  if the why is not obvious. The summary references the work item.
- Generated files, secrets and local settings are never committed. `.gitignore`
  is kept honest.

### 9.3 Pull requests

- Every change reaches `main` through a pull request. No exceptions.
- A pull request is small enough to review properly. A 4,000-line pull request
  is a planning failure, not a normal event.
- The pull request description says what changed and how it was tested, and
  links the work item.
- Branch policy requires at least one approving review and a green build before
  merge.

## 10. Code review

Every pull request is read by at least one engineer who did not write it. The
reviewer checks, at minimum:

- The change does what the work item asks, and nothing it does not ask.
- It follows these standards — structure, naming, validation, error handling.
- It has tests, and the tests actually exercise the new behaviour.
- It introduces no duplication of logic that already exists.
- It has no security regression (section 12).
- Authorisation is present and correct on any new or changed endpoint.

A review comment is resolved by a change or by a written reason. Disagreements
that the author and reviewer cannot settle go to the Solution Architect.

## 11. Testing

### 11.1 The three layers

No SIMF feature ships unless all three layers pass and keep passing.

- **Unit tests** cover a method or a class in isolation: every branch, the edge
  cases, and the error paths. They are fast and have no external dependency.
  They run on every commit.
- **Integration tests** cover an API endpoint end to end: the happy path plus
  every error code the endpoint can return. They catch contract drift between
  layers.
- **End-to-end tests** cover a full user scenario, including failure and
  recovery, the way the user will actually use the system.

### 11.2 Rules

- A behaviour change comes with the tests that prove it, in the same pull
  request.
- Every fixed bug gets a regression test that fails before the fix and passes
  after it.
- A changed backend file carries a `// Tests:` header that names the tests
  covering the change, so the link from code to test is visible in the file.
- Tests are not weakened or skipped to make a build go green. A failing test is
  a real signal; it is investigated, not silenced.

### 11.3 Coverage

Coverage is measured and reported, but the number is a floor, not the goal. A
high coverage figure over shallow tests is worse than honest coverage of the
paths that matter. The target floor and the reporting tool are set in
SIMF-TST-001.

## 12. Security baseline

SIMF must satisfy the NCA Secure Application Development Standard and the
controls it references — ECC-1:2018, CSCC-1:2019, the OWASP Top 10 (2021) and
OWASP ASVS. The day-to-day engineering rules that follow from that:

- **Authorisation everywhere.** Authentication alone is never enough. Every
  endpoint enforces a role or permission. `AllowAnonymous` is limited to
  sign-in, sign-up and password reset, and each of those is justified.
- **Validate all input.** Input is validated and the model bound before it is
  used. Output that returns to a client is encoded for its context.
- **Parameterised data access.** Queries are parameterised through EF Core.
  String-concatenated SQL is not written.
- **Secrets stay out of the repository.** Connection strings, keys and tokens
  come from configuration and a secrets store, never from committed files or
  source constants.
- **Least privilege.** Code, service accounts and database users get the
  narrowest rights that let them do their job.
- **Memory-safe and current.** Third-party packages come from trusted, licensed
  sources, are kept patched, and are checked for known vulnerabilities in the
  pipeline.
- **Auditable actions.** Security-relevant actions — sign-in, permission change,
  approval, configuration change — are logged through Serilog with enough
  context to reconstruct what happened.
- **Source code separation.** Development and production code and data are kept
  separate. Test accounts and test data are removed before anything moves to
  production.
- **Peer security review.** Source code is reviewed by an engineer who did not
  write it before it goes to production, with security as one of the review
  checks.

A change that touches authentication, authorisation, cryptography or personal
data gets a closer review and, where the architecture document calls for it, a
security reviewer on the pull request.

## 13. Build quality

`dotnet build -c Release` passes with zero warnings and zero errors. Warnings
are treated as errors by the solution settings, so a warning breaks the build
by design. The Flutter analyzer and the Blazor build are held to the same line:
clean output, no accepted noise.

When a build problem appears, it is traced to its cause and fixed there. It is
not patched at the symptom, and a failing check is not bypassed to get a merge
through.

## 14. Definition of done

A piece of work is done when all of the following are true. Not most of them —
all of them.

1. The code meets these standards and the design it was built from.
2. It builds clean in Release: zero warnings, zero errors.
3. Unit, integration and end-to-end tests for the change exist and pass.
4. Any bug fixed in the work has a regression test.
5. The change has been reviewed and approved by another engineer.
6. Authorisation is enforced and correct on every new or changed endpoint.
7. The relevant documentation is updated — the API specification, the feature
   design specification, or the data model, as applicable.
8. The work item is updated to reflect reality.

Work that meets only some of these is in progress, not done, and is reported
as in progress.

## 15. Freeze governance

The programme plan defines gates. After a gate, the work behind it is frozen.
A change to frozen work is not made quietly: it needs recorded approval, it is
made as the smallest possible diff, and it is traced through every document the
change touches. Before the project's freeze milestone, dead and legacy code is
removed; after it, nothing is deleted, renamed or moved without approval.

## 16. Open items

| ID | Item | Needed for |
|----|------|-----------|
| OI-1 | Confirm the official document classification with the owner | Control block, section 12 |
| OI-2 | Coverage floor and the test tooling, to be fixed in SIMF-TST-001 | Section 11.3 |
| OI-3 | Final .NET project breakdown, to be fixed in SIMF-SAD-001 | Section 4.1 |
| OI-4 | The shared-constants namespace for SIMF (the `AppRoles` equivalent) | Section 5.1 |
| OI-5 | Reconcile the `Smif*` shared component library with the external designer's component-library handoff in SIMF-MAA-001 section 12 — confirm that `Smif*` wraps the designer's components and how design tokens flow into the wrappers | Section 6.3 |

---

End of document.
