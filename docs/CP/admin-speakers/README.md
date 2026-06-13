# Speakers — `/admin/speakers` · المتحدّثون

Per-page documentation folder for the Control Panel **Speakers** config page.
Everything about this admin page — the data it owns, the API it drives, the
functions an administrator performs, and the rules behind them — lives here.

Last updated: 2026-06-13 — CP config-page documentation (D-380).

> **What this page is.** A Control Panel admin page that manages the **speaker
> records** the mobile app and public website show: the bilingual name, the
> rank/title, the country (→ flag), the photo, the four bilingual rich-text
> sections, the consent toggles, the social URLs, the display order and the
> active flag. It is the single back-office source of truth for every speaker
> the app's **Sessions / session detail** ([Page_016](../../App/Page_016/README.md))
> and the public **Speakers / Speaker profile** screens render.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Design | [admin-speakers_Design.md](admin-speakers_Design.md) | CP screen layout — `SimfBanner` + `SimfDataGrid`, grid columns, toolbar, per-row actions, the `CrudShell`-framed Add/Edit and View/Delete forms, Page↔Popup toggle, states, RTL |
| API | [admin-speakers_API.md](admin-speakers_API.md) | The backend endpoints + DTOs (authoritative contract): admin CRUD + Excel + the public app/website reads |
| Function | [admin-speakers_Function.md](admin-speakers_Function.md) | What the administrator does — add, edit, view, deactivate, filter, sort, page, export, import, upload photo |
| Logic | [admin-speakers_Logic.md](admin-speakers_Logic.md) | Validation + normalisation, code uniqueness, country/contact validity, audit events, the app/website field mapping, the session↔speaker role (D-225) |

## Identity

| | |
|---|---|
| Route | `/admin/speakers` |
| Permission (page) | `@attribute [RequirePermission(PermissionCatalog.Speakers.View)]` |
| Nav | `CpNavigation` item `Module.Speakers` → `/admin/speakers`, `RequiredPermission = Speakers.View`, icon `mic` |
| Audience | Administrator (any role holding `Speakers.View`; `Administrator = "*"` wildcard) |
| Layout | `CpShellLayout`; banner title `Admin.Speakers.Title` (EN **Speakers** / AR **المتحدّثون**) |
| Pattern | Canonical `SimfDataGrid` CRUD + **D-353** Page↔Popup `CrudShell` framing + **D-356** generic Excel export/import via `CrudGridExcel` + **D-357** photo via the unified media-asset pipeline |
| Backed by | `dbo.Speakers` on `SimfAppDbContext` (D-199 build wave; SIMF-DAT-001 §5.4) |
| Status | ✅ Real — D-199 original; D-353 framing + D-356 Excel (2026-06-10); D-357 photo (2026-06-11) |

## CP pages (source `.razor`)

| File | Role |
|------|------|
| [`SpeakersList.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SpeakersList.razor) | The grid host — banner, `SimfDataGrid`, toolbar (Add / Export / Import / presentation toggle), `CrudShell` host, `CrudGridExcel` |
| [`SpeakersAddEdit.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SpeakersAddEdit.razor) | Reusable Add / Edit form (`CrudAddEditFormBase<AdminSpeakerDetail>`) |
| [`SpeakersViewDelete.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/SpeakersViewDelete.razor) | Reusable read-only Details + `SimfConfirm`-gated Deactivate form (`CrudViewDeleteFormBase<AdminSpeakerDetail>`) |

## Backend (source)

| File | Role |
|------|------|
| [`SpeakerEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SpeakerEndpoints.cs) | Admin CRUD — list / get / create / update / deactivate |
| [`SpeakersExcelEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Admin/SpeakersExcelEndpoints.cs) | D-356 Excel export + insert-only import |
| [`AdminSpeakerService.cs`](../../../src/Backend/SIMF.Infrastructure/Programme/AdminSpeakerService.cs) | The service — validation, normalisation, country/contact validity, audit |
| [`PublicSpeakerEndpoints.cs`](../../../src/Backend/SIMF.Api/Endpoints/Public/PublicSpeakerEndpoints.cs) | The app/website reads — `GET /app/speakers`, `GET /app/speakers/{id}` (anonymous) |
| [`Speakers.cs`](../../../src/Shared/SIMF.Contracts/Admin/Speakers.cs) | Admin contracts — `AdminSpeakerSummary`, `AdminSpeakerDetail`, create/update requests |
| [`PublicSpeakers.cs`](../../../src/Shared/SIMF.Contracts/Programme/PublicSpeakers.cs) | Public contracts — `PublicSpeakerSummary`, `PublicSpeakerDetail`, `PublicSpeakerSession` |

## Sources of truth (read first)

`SpeakersList.razor` + `SpeakersAddEdit.razor` + `SpeakersViewDelete.razor`
(the CP UI) · `SpeakerEndpoints.cs` + `SpeakersExcelEndpoints.cs` +
`AdminSpeakerService.cs` (the back office) · `PublicSpeakerEndpoints.cs` +
`PublicSpeakers.cs` (what the app/website read) · `PermissionCatalog.Speakers`
(the gates) · `CpNavigation` (`Module.Speakers`) · SIMF-DAT-001 §5.4 (the data
model) · `DECISIONS_LOG` D-199 (original module), **D-281..D-283** (shared
Contact link), **D-353** (Page↔Popup + `CrudShell` + `SimfConfirm` delete gate),
**D-356** (generic Excel), **D-357** (unified media-asset photo), **D-225**
(session↔speaker role), **D-271** (the app speaker country flag + photo).

## Related pages / cross-links

- **Existing CP reference doc:** [`docs/pages/cp/admin-speakers.md`](../../pages/cp/admin-speakers.md) (the page-index reference).
- **E2E catalogue:** [`docs/tests/e2e/cp-admin-speakers.md`](../../tests/e2e/cp-admin-speakers.md) — E2E-SPK-001…023.
- **App — Sessions / session detail (consumer):** [`docs/App/Page_016/README.md`](../../App/Page_016/README.md). The session list + detail embed the **ordered speaker cards** (`PublicSessionSpeaker`) — name, rank/title, country flag + photo, and the **session role** (`SessionSpeakerRole` 0=Speaker / 1=Host, D-225). The fields shown there are the same speaker records this page manages.
- **App — Speakers list / Speaker profile (consumer):** the public `GET /app/speakers` + `GET /app/speakers/{id}` reads (`PublicSpeakerSummary` / `PublicSpeakerDetail`), Mockup pages 19 + 20.
- **Sibling Programme CP modules:** Themes, Sponsors, Halls, Sessions, Speaker presentations (`/admin/speaker-presentations`, reuses `Speakers.*`), Speaker meeting requests (`/admin/speaker-meeting-requests`).

## Permission codes (`PermissionCatalog.Speakers`)

`Speakers.View`, `Speakers.Create`, `Speakers.Edit`, `Speakers.Delete`,
`Speakers.Export`, `Speakers.Import` — all `BaselineRoles = AdminOnly`. The
page is gated by `Speakers.View`; each action is additionally gated by its own
code (see [admin-speakers_API.md](admin-speakers_API.md)).
