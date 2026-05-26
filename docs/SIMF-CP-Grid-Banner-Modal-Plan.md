# SIMF Control Panel — Grid, Banner & Modal Plan

| | |
|--|--|
| **Status** | DRAFT — awaiting owner approval before any code edit |
| **Owner** | Tech (tech@ammn.com.sa) |
| **Created** | 2026-05-26 |
| **Working branch** | `feature/login-api` (current) |
| **Baseline commit** | `f17799b` (post D-111) |
| **Frozen surface respected** | D-110 — schema + enum names + migration history |
| **Decision-log entry to be added on commit** | **D-112** |

---

## 1. Context

Triggered from the admins list at `/admin/admins` ([UsersList.razor](../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/UsersList.razor)). The page is the most complete CRUD surface in the Control Panel today (built across D-042 → D-045) and the owner wants it elevated to the **canonical pattern** every future CP list page copies from.

Five things must change on this page **and** on the shared component library that powers it:

1. The page must demonstrate the **single, unique grid style** used by every CRUD page in the system.
2. The bottom pager must be the **full, industry-standard pager** (First / Prev / numbered / Next / Last + page-size).
3. The toolbar and per-row icons must be **semantically correct** (today they reuse `check`, `user`, `mail`, `close` for actions those icons do not describe).
4. The page title must be rendered via a new **reusable `SimfBanner`** primitive (parameterised, no hardcoded label).
5. **Add / Edit / Details must open as popups (modals)**, not as navigation to a separate page — while the existing `/admin/admins/new` URL keeps working as a fallback so deep-links survive.

A controlled out-of-scope note from the owner: a **generic CRUD API** will be specified later. Until then, the **Edit** modal is wired as a stub. **No new backend endpoints are invented in this plan.**

---

## 2. Approvals already locked (do not re-litigate)

| Question | Owner answer |
|--|--|
| **Q-A** — How should "table template … `<SimfTable>`" be realised? | **(d)** Keep `SimfDataGrid` as the single canonical CRUD grid; document the pattern. **Do not** rename, do not regress to the smaller `SimfTable` primitive. |
| **Q-B** — What does "add bottom page nav" mean? | **ALL** — First / Prev / numbered pages / Next / Last + page-size selector. Industry-standard. |
| **Q-C** — `SimfBanner` shape? | **Keep `SimfPageHeader` untouched.** Add `SimfBanner` as a new reusable primitive. Title is a parameter — **no hardcoded label**. |
| **Q-D** — Modals for Add / Edit / Details? | **Yes**, modals on the list page. **Keep `/admin/admins/new` as a fallback** route. |

---

## 3. Constraints

| Constraint | Source |
|--|--|
| Do not touch EF schema, enum names/values, migration history | FREEZE — D-110, commit `67e2263` |
| Do not edit `.csproj`, `Directory.Build.props`, `Directory.Packages.props`, `appsettings.*.json` | Global CLAUDE.md §1.7, §9 |
| No raw CSS colours, no hardcoded fonts, no duplicate token blocks | Global CLAUDE.md §8 — `theme.tokens.css` is the single source of truth |
| `dotnet build -c Release` must pass with 0 warnings, 0 errors after edits | Global CLAUDE.md §9 |
| One commit per finished change; no push | Global CLAUDE.md §17 |
| Add a `// Tests:` header where backend behaviour changes | Global CLAUDE.md §3 — N/A here (UI-only) |
| Decision/Assumption log entry required | Global CLAUDE.md §17 — D-112 |
| Per-file Risk tag in plan | Global CLAUDE.md §17 |
| Before/after `git status --porcelain` shown | Global CLAUDE.md §17 |
| Run `simplify` skill on changed files before final commit | Global CLAUDE.md §17 |

---

## 4. Scope expansion — what each of the five asks means precisely

### 4.1 Canonical CRUD grid (Q-A.d)

- Keep `src/Shared/SIMF.Components/Forms/SimfDataGrid.razor` as the **only** grid used across CP CRUD pages.
- Treat `UsersList.razor` as the **gold-standard reference** other list pages copy.
- Publish a short pattern doc (`docs/dev/SIMF_TABLE_PATTERN.md`) pointing at it.
- The existing small `SimfTable.razor` primitive is left untouched (it is a different shape — a typed `<table>` wrapper for non-CRUD lists).

### 4.2 Full pager (Q-B = ALL)

Today the pager has **Prev / Next + summary** only. Final shape:

```
[« First] [‹ Prev] [1] [2] [3] [4] [5] [Next ›] [Last »]   Page 3 of 12    Show: [20 ▾]    1-20 of 234
```

- New parameters on `SimfDataGrid<TItem>`: `PageSizeOptions`, `FirstLabel`, `LastLabel`, `PageSizeLabel`, `PageLabel` (formatter `(current, total) => "Page {0} of {1}"`).
- New methods: `GoFirstAsync`, `GoLastAsync`, `GoToPageAsync(int)`, `ChangePageSizeAsync(int)`.
- Selection-clearing on page/size change — same rule the existing Prev/Next already follows (D-045 H1).
- The existing `PrevLabel` / `NextLabel` callers keep working unchanged.

### 4.3 Icon fixes (toolbar + per-row)

The icon set in [SimfIcon.razor](../src/Shared/SIMF.Components/SimfIcon.razor) is missing nine of the icons the grid needs, so the existing code is reusing the wrong icons. Today's mapping vs the new one:

| Action | Today | New |
|--|--|--|
| Add | `check` | **`plus`** |
| Edit | `user` | **`edit`** (pencil) |
| Delete (row + bulk) | `close` | **`trash`** |
| Copy (row + bulk) | `check` | **`copy`** |
| Paste | `check` | **`copy`** (clipboard variant) |
| Duplicate | `user` | **`copy`** |
| Import | `mail` | **`upload`** |
| Export | `mail` | **`download`** |
| Select all | `check` | `check` (unchanged) |
| Pager First | — | **`chevron-first`** |
| Pager Prev | — | **`chevron-left`** |
| Pager Next | — | **`chevron-right`** |
| Pager Last | — | **`chevron-last`** |

**Rule:** the icon-set change is **additive only**. No existing icon name is renamed or removed — other components that already bind to `mail`, `user`, `check`, `close` keep working.

### 4.4 `SimfBanner` (Q-C)

- New component at `src/Shared/SIMF.Components/Layout/SimfBanner.razor`.
- Parameters: `Title` (required), `Subtitle` (optional), `Actions` (optional `RenderFragment`).
- Title typography matches today's "Users" h1 (`var(--font-size-title)`, `var(--line-height-title)`, `var(--font-weight-heading)`).
- Container is a tinted surface with a leading accent rule — so the banner reads as a banner, not a plain header.
- `SimfPageHeader` stays **untouched**; both primitives coexist (header for content-region titles, banner for page-top branded statements).

### 4.5 Add / Edit / Details as popups (Q-D)

| Action | Behaviour | Endpoint |
|--|--|--|
| **Add** | Toolbar `+ Add` opens `SimfModal` hosting `<CreateAdminForm>` (new child component lifted from `CreateUser.razor`). On success the modal closes, a toast fires, the grid reloads. | Existing `POST /account/api/admin/admins` — unchanged. |
| **Add (fallback)** | Direct navigation to `/admin/admins/new` still resolves to the same form (just rendered inside `CreateUser.razor` instead of the modal). | Same. |
| **Edit** | Toolbar `Edit` (single-row selected) and per-row `Edit` open `SimfModal` showing `Admin.Users.Edit.NotYet` ("Edit ships with the User Management module"). | **None — stub** until generic CRUD API lands. |
| **Details** | New per-row `Details` action (via `RowActions` slot) opens a read-only `SimfModal` listing every `AdminUserSummary` field (Email, DisplayName, AccountState, IsAdministrator, TwoFactorEnabled). | No endpoint needed — the row already carries the data. |

---

## 5. File-by-file plan

> Every row carries a **Risk** tag per global CLAUDE.md §17.

| # | File | Change | Risk |
|--|--|--|--|
| 1 | [src/Shared/SIMF.Components/SimfIcon.razor](../src/Shared/SIMF.Components/SimfIcon.razor) | Add 10 SVG paths: `copy`, `edit`, `plus`, `trash`, `upload`, `download`, `chevron-left`, `chevron-right`, `chevron-first`, `chevron-last`. No rename, no removal. | `none` |
| 2 | [src/Shared/SIMF.Components/Forms/SimfDataGrid.razor](../src/Shared/SIMF.Components/Forms/SimfDataGrid.razor) | (a) Icon swaps per §4.3 table. (b) Extend `.simf-grid__pager` markup with First / numbered pages / Last + page-size `<select>`. (c) Add parameters + methods per §4.2. | `none` |
| 3 | [src/Shared/SIMF.Components/wwwroot/css/simf-components.css](../src/Shared/SIMF.Components/wwwroot/css/simf-components.css) | Append `.simf-grid__pager-pages`, `.simf-grid__pager-page`, `.simf-grid__pager-page--active`, `.simf-grid__pager-size`, `.simf-banner`, `.simf-banner__title`, `.simf-banner__subtitle`, `.simf-banner__actions`. Tokens only — verify each new value resolves to a token already in [theme.tokens.css](../src/Shared/SIMF.Components/wwwroot/css/theme.tokens.css); add the token there first if missing. | `none` |
| 4 | `src/Shared/SIMF.Components/Layout/SimfBanner.razor` *(new)* | Parameters: `Title` (required), `Subtitle`, `Actions`. Render shape per §4.4. | `none` |
| 5 | `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/CreateAdminForm.razor` *(new)* | Child component holding the form lifted out of `CreateUser.razor`. Parameters: `OnSuccess` (`EventCallback<AdminCreateUserResponse>`), `OnCancel` (`EventCallback`). Same `Model`, same validation, same POST. | `none` |
| 6 | [src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/CreateUser.razor](../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/CreateUser.razor) | Replace the inline `<EditForm>` block with `<CreateAdminForm OnSuccess="…" OnCancel="…" />`. URL `/admin/admins/new` stays. | `none` (URL preserved) |
| 7 | [src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/UsersList.razor](../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/UsersList.razor) | (a) `<SimfPageHeader>` → `<SimfBanner Title="@L[\"Admin.Users.Title\"]" />`. (b) `OnAddAsync` opens modal hosting `<CreateAdminForm>` (no `Nav.NavigateTo`). (c) Wire `OnEditOne` to a stub modal showing `Admin.Users.Edit.NotYet`. (d) Add per-row Details action via `RowActions` slot → read-only modal. (e) Pass new pager-label parameters. | `breaking` (UX change for Add — `/admin/admins/new` fallback preserves deep-link) |
| 8 | [src/ControlPanel/SIMF.ControlPanel/Resources/Strings.resx](../src/ControlPanel/SIMF.ControlPanel/Resources/Strings.resx) + [Strings.ar.resx](../src/ControlPanel/SIMF.ControlPanel/Resources/Strings.ar.resx) | Add **in both**: `Admin.Users.Action.Details`, `Admin.Users.Add.Title`, `Admin.Users.Edit.Title`, `Admin.Users.Edit.NotYet`, `Admin.Users.Edit.Close`, `Admin.Users.Details.Title`, `Admin.Users.Details.Close`, `Admin.Users.Pager.First`, `Admin.Users.Pager.Last`, `Admin.Users.Pager.PageSize`, `Admin.Users.Pager.Page` (`"Page {0} of {1}"`). | `none` |
| 9 | [docs/decisions/DECISIONS_LOG.md](decisions/DECISIONS_LOG.md) | New entry **D-112** capturing all five decisions and the Edit-stub deferral. | `none` |
| 10 | `docs/dev/SIMF_TABLE_PATTERN.md` *(new)* | One-pager: "The canonical CRUD list page is `UsersList.razor`. Copy its wiring." | `none` |

**Total new files:** 3 (`SimfBanner.razor`, `CreateAdminForm.razor`, `SIMF_TABLE_PATTERN.md`).
**Total edited files:** 7.
**Backend files touched:** 0.

---

## 6. Out of scope (explicit)

- Backend endpoints, DTOs, handlers, EF contexts, migrations, enums, interceptors (FREEZE — D-110).
- Project files (`.csproj`), `Directory.Build.props`, `Directory.Packages.props`, `appsettings.*.json`.
- Other CP list pages (`VisitorsList`, `PendingVisitors`, `PendingStaff`, `OthersList`, `PendingOthers`, `InterestsList`, `LogsViewer`) — they automatically pick up icon + pager improvements via items #1–#3, but their modal conversions, banners and detail flows are **not** part of this plan.
- Auth pages, Profile, Notifications, TotpPairing.
- The small `SimfTable.razor` primitive (left untouched).
- A generic CRUD API or generic CRUD endpoint base class — **deferred** to a separate plan you will provide.

---

## 7. Execution sequence

1. **Baseline.** Run `git status --porcelain` and show the result. Confirm the working tree matches what was approved.
2. **Item #1** — add 10 icons to `SimfIcon.razor`. Build.
3. **Item #3 (banner CSS subset)** — add `.simf-banner*` classes first so item #4 has its styles ready.
4. **Item #4** — create `SimfBanner.razor`. Build.
5. **Item #2** — icon swaps + pager extension in `SimfDataGrid.razor`. Build.
6. **Item #3 (pager CSS subset)** — `.simf-grid__pager-pages`, `.simf-grid__pager-size`, etc. Build.
7. **Item #8** — add resx keys in `Strings.resx` and `Strings.ar.resx` (Arabic translations included).
8. **Item #5** — create `CreateAdminForm.razor`. Build.
9. **Item #6** — convert `CreateUser.razor` to host `<CreateAdminForm>`. Build.
10. **Item #7** — update `UsersList.razor` (banner, Add modal, Edit stub, Details modal, pager labels). Build.
11. **Verification.** Run `dotnet build -c Release` — must pass with 0 warnings, 0 errors. Launch the CP, sign in as Administrator, open `/admin/admins`, and exercise: full pager (First/Prev/numbers/Next/Last + page size), Add modal, Edit stub modal, Details modal, deep-link `/admin/admins/new` still resolves to the form. Capture a fresh screenshot under `docs/screenshots/`.
12. **Simplify pass.** Run the `simplify` skill on each changed file; address what it surfaces.
13. **Items #9–#10.** Write decision log entry **D-112** and the pattern doc.
14. **Final `git status --porcelain`.** Every changed file must appear in §5; nothing else. If anything unexpected is present, stop and report.
15. **Commit.** One commit, clear message: `feat: D-112 — canonical CRUD grid (SimfBanner + full pager + correct icons + Add/Edit/Details modals)`. **Do not push.**

---

## 8. Acceptance criteria

- [ ] `/admin/admins` renders the new `SimfBanner` instead of `SimfPageHeader`.
- [ ] Toolbar icons match §4.3 mapping (visually correct for every action).
- [ ] Per-row Copy / Edit / Delete icons match §4.3 mapping; a per-row Details icon is present and opens a read-only modal.
- [ ] Pager renders First / Prev / numbered pages / Next / Last + page-size selector + "Page X of Y" + existing summary. Selection clears on every page/size change (parity with today's Prev/Next).
- [ ] Add button opens a modal containing the working create-user form. Submitting reloads the grid and closes the modal.
- [ ] Edit button (toolbar and per-row) opens a stub modal that explicitly says Edit ships with the User Management module.
- [ ] Direct navigation to `/admin/admins/new` still resolves to a working create-user page.
- [ ] `dotnet build -c Release` is **0 warnings, 0 errors**.
- [ ] Final `git status --porcelain` lists only files in §5.
- [ ] D-112 added to `docs/decisions/DECISIONS_LOG.md`; pattern doc created.

---

## 9. Risks & mitigations

| Risk | Mitigation |
|--|--|
| Other list pages (VisitorsList, PendingVisitors, …) render different toolbar wording with the new icons | Visual only; nothing functional changes for them. Captured as a follow-up item if the owner wants a sweep. |
| Numbered-pages markup hurts narrow viewports | The pager already collapses to column layout under 640 px (line 1422 of `simf-components.css`); new numbered links are wrapped in the same media query. |
| Stub Edit modal confuses an admin | Modal text explicitly says "Edit will ship with the User Management module" so the limitation is transparent, not silent. |
| Banner accent colour clashes with the dark theme | Use `--color-accent` token that already resolves in both themes (no hex). |
| `CreateAdminForm` lift breaks the existing `CreateUser.razor` page | The form is moved verbatim; the page only changes its host wrapper. Smoke-test the fallback URL after the change. |

---

## 10. Decision log entry to add (D-112) — draft text

> **D-112 — Canonical CRUD grid: SimfBanner + full pager + correct icons + Add/Edit/Details modals**
>
> The admin/admins page is promoted to the canonical CRUD list pattern. `SimfDataGrid` stays the single grid for every CRUD page. The toolbar and per-row icon set is corrected (additively — no rename of existing icon names). The pager gains First / numbered / Last + page-size. A new `SimfBanner` primitive is added beside `SimfPageHeader` (both retained; banner is parameterised — no hardcoded label). Add / Edit / Details now open as `SimfModal` overlays on the list page; `/admin/admins/new` is preserved as a deep-link fallback. Edit is wired to a stub modal awaiting the **generic CRUD API** (separate plan).

---

## 11. Open additions — owner input batch #2 (2026-05-26)

> Each item below carries open questions that must be answered before it's promoted into §5.
> Per global CLAUDE.md §1.2 I will not guess on these — answer Q-E through Q-H first.

---

### 11.1  Logs page — wrap action row to two lines

- **Ask:** "improve UI/UX for log, for `/admin/logs` make button into two line."
- **Surface:** [LogsViewer.razor](../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/LogsViewer.razor) (lines 67–80 — `simf-form__actions` row holding Refresh / Auto-refresh checkbox / Download).
- **Current state:** all three controls render inline on one line.
- **Risk:** `none` (CSS + light Razor restructure, no behaviour change).

**Q-E — ANSWERED (2026-05-26):** option **(iii)** — collapse the current ~5 stacked lines into **exactly 2 visual rows**, with grouped + aligned buttons. Row 1 = filters (Project / File / Lines selects + Auto-refresh checkbox), Row 2 = action buttons (Refresh / Download). On narrow viewports the rows may wrap further, but the **desktop layout is locked at 2 rows**.

**Locked files (will be promoted to §5 on `go`)**:
| File | Change | Risk |
|--|--|--|
| `src/Shared/SIMF.Components/wwwroot/css/simf-components.css` | Add `.simf-logs-actions` grid (or `.simf-form__actions--grouped` modifier): row 1 — `display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)) auto; gap: var(--space-3); align-items: end;` (3 selects + checkbox). Row 2 — `display: flex; gap: var(--space-3); justify-content: flex-start;` (action buttons). Both rows aligned to the same starting column. Narrow viewport: stack at `<640px` (matches existing pager breakpoint). | `none` |
| [LogsViewer.razor](../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/LogsViewer.razor) | Move the Auto-refresh checkbox **out of** `simf-form__actions` and **into** the `simf-form__fields` row alongside Project / File / Lines. Rename the wrapping containers to the new grid classes. Buttons (Refresh / Download) stay together in the second row, left-aligned, equal width. | `none` |

---

### 11.2  Visitors + Others list pages — unify with admin/admins toolbar

- **Ask:** "improve OTHER AND VISITOR `/admin/visitors`, use same SimfTable and button as in admin users."
- **Surface:** [VisitorsList.razor](../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/VisitorsList.razor) and [OthersList.razor](../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/OthersList.razor). Same template, different endpoint + UserType filter.
- **Current gap:** both pages already use `SimfDataGrid`, but with `Multiselect="false"` and **zero toolbar callbacks** — no Add / Edit / Delete / Copy / Paste / Duplicate / Import / Export. Create lives as a one-off `<SimfButton>` in the page header (different from the admins page's toolbar-Add).
- **Backend reality:** for visitors/others the following endpoints exist today: list, create-visitor (separate page), create-other (separate page), per-row approve/reject. The following endpoints **do not exist**: bulk-delete, duplicate, export, import. Per your earlier note, the **generic CRUD API** is the source of truth for these.
- **Risk:** `breaking` for the page-header CTA (moves into the toolbar).

**Q-F — ANSWERED (2026-05-26):** **backend first**. Owner requested a hand-off prompt for the backend agent to build the generic CRUD endpoints **before** any front-end CRUD work begins on `/admin/visitors` and `/admin/others`. See **§11C — Prompt A** for the deliverable. This item is **blocked** until Prompt A ships and `/admin/visitors/list`, `/admin/others/list`, plus the bulk-delete / duplicate / export / import endpoints exist for both UserTypes.

**Suggested files** *(execute only after Prompt A backend lands)*:
| File | Change | Risk |
|--|--|--|
| `VisitorsList.razor` | Replace `<SimfPageHeader Actions>` create-button with `OnAdd` (modal). Enable `Multiselect="true"`. Wire `OnDeleteSelected`, `OnDeleteOne`, `OnCopySelected`, `OnCopyOne`, `OnPaste`, `OnDuplicateOne`, `OnImport`, `OnExport` — each opens an "Awaiting generic CRUD" stub modal except the existing flows. Add new Banner + new per-row Details modal (same shape as the admins page from §5). | `breaking` (header CTA moves) |
| `OthersList.razor` | Same treatment, scoped to UserType=Other. | `breaking` |
| `CreateVisitor.razor` | Lift form into a `CreateVisitorForm.razor` child so the modal can reuse it (mirrors `CreateAdminForm` pattern from §5). Page stays as `/admin/visitors/new` fallback. | `none` |
| `CreateOther.razor` | Same — extract `CreateOtherForm.razor`. | `none` |
| `Strings.resx` (en + ar) | New keys: `Admin.Visitors.Add.Title`, `Admin.Visitors.Details.Title`, `Admin.Visitors.Edit.NotYet`, equivalents for `Admin.Others.*`, plus a shared `Admin.Crud.AwaitingGenericApi` for the stub modal body. | `none` |
| `DECISIONS_LOG.md` | D-113 entry — Visitors/Others toolbar unified, stubs pending generic CRUD. | `none` |

---

### 11.3  Approve / Reject — confirm dialog with profile preview

- **Ask:** "in approve add details to show profile before approve all details, full profile. Approve or reject add dialog for confirm and in reject add input txt for reason."
- **Surface:** [PendingVisitors.razor](../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/PendingVisitors.razor), [PendingStaff.razor](../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/PendingStaff.razor), [PendingOthers.razor](../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/PendingOthers.razor).
- **Current state:** **Approve** is a one-click direct action — no confirm, no details preview. **Reject** already opens a modal with a reason textarea bound to 10–500 chars ([PendingVisitors.razor:72-98](../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/PendingVisitors.razor#L72-L98)) — that side already matches your ask exactly.
- **Backend gap:** there is **no admin endpoint that returns another user's full profile**. [UserProfileGetEndpoint](../src/Backend/SIMF.Api/Endpoints/Account/UserProfileGetEndpoint.cs) is self-only (reads the signed-in user's own row). The pending-list row (`AdminPendingUserSummary`) only carries Id / Email / DisplayName / CreatedAt — far short of "full profile".
- **Risk:** `breaking` (approval gains a confirm step — every existing reviewer's muscle memory changes).

**Q-G — ANSWERED (2026-05-26):** **NO generic admin profile-read endpoint.** Profile read is **scoped to the approval flow only** — a reviewer can read a target user's profile **only when the target is in `PendingApproval` state and is a Visitor or Other**. Once the user is approved/rejected, the endpoint stops returning their profile. Staff (`PendingStaff`) is out of scope for this item.

**Endpoint shape (locked):**
- `GET /admin/visitors/{id:guid}/profile-for-approval` → `ApiResult<PendingProfileResponse>`
- `GET /admin/others/{id:guid}/profile-for-approval` → `ApiResult<PendingProfileResponse>`
- Both guarded by: Administrator role **AND** target user must have `AccountState == PendingApproval` **AND** target `UserType` matches the route. Otherwise → `404` (not "403", so the endpoint also hides existence of approved users).
- Reads existing `UserProfile` columns — **no schema change**.
- Row-audit log captures every read (auto via the D-109 interceptor).

**Locked files (will execute once §5 lands)**:
| File | Change | Risk |
|--|--|--|
| `src/Backend/SIMF.Api/Endpoints/Admin/PendingVisitorProfileGetEndpoint.cs` *(new)* | `GET /admin/visitors/{id:guid}/profile-for-approval`. Calls `IAdminApprovalReadService.GetPendingVisitorProfileAsync(id, ct)`. Returns 404 if not pending or not a visitor. | `security` (state-and-type guard is the whole safety boundary) |
| `src/Backend/SIMF.Api/Endpoints/Admin/PendingOtherProfileGetEndpoint.cs` *(new)* | Same shape for `Other`. | `security` |
| `src/Application/IdentityAccess/AdminApprovalReadService.cs` *(new)* | `GetPendingVisitorProfileAsync` and `GetPendingOtherProfileAsync`. Each filters on `AccountState == PendingApproval` and the matching `UserType` before composing the DTO. | `none` |
| `src/Shared/SIMF.Contracts/Admin/PendingProfileResponse.cs` *(new)* | DTO — Id, Email, DisplayName, UserType, ProfileTypeName, Phone, Country, Organization, JobTitle, Interests, IdDocument metadata, CreatedAt. **Only fields already stored.** | `none` |
| `PendingVisitors.razor` | New `_approveTarget` + `_approveProfile` state. Approve button opens `SimfModal` titled "Confirm approval — {email}", body fetches profile from the visitor endpoint and renders a read-only `<dl>`. Footer = Cancel / Approve. Reject modal stays as-is. | `breaking` (extra click) |
| `PendingOthers.razor` | Same treatment, hits the `/others/.../profile-for-approval` route. | `breaking` |
| `PendingStaff.razor` | **No change** — staff approval is out of Q-G scope. | `none` |
| `src/Shared/SIMF.Components/Layout/SimfDescriptionList.razor` *(new)* | Reusable `<dl>`-styled key/value list primitive for the profile preview body. | `none` |
| `Strings.resx` (en + ar) | New keys: `Admin.Pending.Approve.Title`, `Admin.Pending.Approve.Confirm`, `Admin.Pending.Approve.Submit`, `Admin.Pending.Approve.Cancel`, `Admin.Pending.Profile.Loading`, `Admin.Pending.Profile.Field.*` for every profile label. | `none` |
| `SIMF.Api.Tests/PendingProfileReadTests.cs` *(new)* | Regression matrix: (a) admin reads pending-visitor profile → 200; (b) admin reads approved visitor → 404 (state guard); (c) admin reads visitor via `/others/.../profile-for-approval` → 404 (type guard); (d) non-admin → 403; (e) missing id → 404. (Per global CLAUDE.md §3 — `// Tests:` header on each new endpoint file.) | `none` |
| `DECISIONS_LOG.md` | D-114 — Scoped pending-profile-read endpoints (visitor + other), approval confirm + profile-preview modal, new test matrix. **Explicitly notes** that no general admin profile-read endpoint exists by design (Q-G). | `none` |

---

### 11.4  Profile-types CRUD page (VIP, Normal, …)

- **Ask:** "Add page for profile type (VISITOR OR OTHER): vip, normal, ... etc."
- **Surface:** new CP page(s) + new backend endpoints. Reference shape: [InterestsList.razor](../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/InterestsList.razor) (CRUD lookup table — closest match).
- **Current state:** `ProfileType` is an existing entity. [ListProfileTypesEndpoint](../src/Backend/SIMF.Api/Endpoints/Admin/ListProfileTypesEndpoint.cs) exposes `GET /admin/profile-types?userType=Visitor|Other`. **No Create / Update / Delete endpoints** exist — they would all be new application surface on top of the frozen schema (which is fine; the schema itself isn't changing).
- **Risk:** `none` (lookup table CRUD on existing schema).

**Q-H — ANSWERED (2026-05-26):** **backend first**. Owner requested a hand-off prompt for the backend agent to build the profile-types CRUD endpoints **before** any front-end work begins. See **§11C — Prompt B** for the deliverable. The layout choice (Q-H.1) and UserType scope (Q-H.2) are **deferred** until the backend agent returns with the endpoint shape — UI plan to be finalised after.

**Suggested files** *(execute only after Prompt B backend lands)*:
| File | Change | Risk |
|--|--|--|
| `src/Backend/SIMF.Api/Endpoints/Admin/ProfileTypeEndpoints.cs` *(new — group)* | `POST /admin/profile-types/list` (paged), `POST /admin/profile-types`, `PUT /admin/profile-types/{id:guid}`, `DELETE /admin/profile-types/{id:guid}` (soft-delete via `Deactivate()` per global CLAUDE.md §7). | `none` (no schema change — `ProfileType` table exists) |
| `src/Application/IdentityAccess/AdminProfileTypeCommandService.cs` *(new)* | Handlers for create / update / soft-delete, validation, uniqueness per UserType. | `none` |
| `src/Shared/SIMF.Contracts/.../AdminProfileType*.cs` *(new)* | Request/response DTOs — Create, Update, summary row. | `none` |
| `src/Backend/SIMF.Application/Validation/AdminProfileType*Validator.cs` *(new)* | FluentValidation matched to EF `HasMaxLength` per global CLAUDE.md §7. | `none` |
| `src/ControlPanel/.../Pages/Admin/ProfileTypes/VisitorProfileTypesList.razor` *(new)* | CRUD list grid, modeled on `InterestsList.razor` (Add/Edit/Delete/Banner/Modal). UserType filter pinned to `Visitor`. | `none` |
| `src/ControlPanel/.../Pages/Admin/ProfileTypes/OtherProfileTypesList.razor` *(new)* | Same, UserType pinned to `Other`. | `none` |
| `src/ControlPanel/.../Pages/Admin/ProfileTypes/CreateProfileTypeForm.razor` *(new)* | Reusable form — Name (en + ar), DisplayOrder, IsActive flag, UserType (hidden, set by host page). | `none` |
| Left-nav `CpShellLayout.razor` | Add menu entries "Visitor profile types" and "Other profile types" under the relevant admin group. | `none` |
| `Strings.resx` (en + ar) | Full `Admin.ProfileTypes.*` localisation block (Title, Add, Edit, Delete, Name, NameArabic, DisplayOrder, Active, Confirm, ...). | `none` |
| `SIMF.Api.Tests/AdminProfileTypeTests.cs` *(new)* | CRUD happy-path + 403 for non-admin + uniqueness regression. | `none` |
| `DECISIONS_LOG.md` | D-115 — Profile-types CRUD endpoints + page. | `none` |

**This item is the biggest in the batch** — likely a sprint on its own. I'd suggest doing it **after** §5 (the D-112 batch) so the canonical grid pattern is in before we copy it three more times.

---

### 11.5  Image cropping — adopt `Cropper.Blazor` from V10 ERP

- **Ask:** "copy img cropping code exactly as in ERP system `D:\Online_ERP_System\V10\src\OnlineErpSystem.V10.Web.Client`. same package same code."
- **What's actually in V10** *(per investigation 2026-05-26)*:
  - **Path correction**: the cropper does **not** live in `OnlineErpSystem.V10.Web.Client` (that's the WASM shell — nearly empty). It lives in [`OnlineErpSystem.V10.Web\Components\Pages\Account\UserLogoCropperDialog.razor`](D:\Online_ERP_System\V10\src\OnlineErpSystem.V10.Web\Components\Pages\Account\UserLogoCropperDialog.razor).
  - **Package**: `Cropper.Blazor` **v1.5.1** in [`OnlineErpSystem.V10.Web.csproj`](D:\Online_ERP_System\V10\src\OnlineErpSystem.V10.Web\OnlineErpSystem.V10.Web.csproj#L21).
  - **Shape**: MudBlazor dialog containing `<CropperComponent>`. Options: `AspectRatio=1`, `ViewMode.Vm1`, `DragMode="move"`, `AutoCrop=true`, `AutoCropArea=0.9`, zoomable, responsive, restore on. Output: 400×400 PNG via `GetCroppedCanvasDataInBackgroundAsync` → byte stream → base64 data-URL → `DialogResult.Ok(result)`.
- **Conflicts with the "exact copy" ask**:
  1. The V10 component depends on **MudBlazor** (`IMudDialogInstance`, `Variant`, `Icons.Material.Filled.Crop`) and on V10's MudBlazor wrappers (`ErpDialog`, `ErpButton`, `ErpProgressCircular`). **SIMF does not use MudBlazor at all** — it has its own Simf* primitives ([SimfModal](../src/Shared/SIMF.Components/Forms/SimfModal.razor), [SimfButton](../src/Shared/SIMF.Components/Forms/SimfButton.razor), [SimfSpinner](../src/Shared/SIMF.Components/Forms/SimfSpinner.razor)). Pulling MudBlazor in is a large architectural drift the SIMF SES rules don't sanction.
  2. The V10 file uses an inline `<style>` block and a hardcoded hex `#1a1a2e`. Both **violate** global CLAUDE.md §8 (no inline styles, no raw colours — `theme.tokens.css` is the single source of truth).
- **Risk:** `breaking` (new package; new component; first use sits in the existing avatar-upload flow).

**Q-I — ANSWERED (2026-05-26):** option **(i)** — **same package, ported component.** Install `Cropper.Blazor 1.5.1`. Carry over V10's cropping idea verbatim: same `Options` block, same 400×400 PNG output, same base64-data-URL return. Dialog chrome wraps `<SimfModal>` / `<SimfButton>` / `<SimfSpinner>` (NOT MudBlazor). Styles live in `simf-components.css` with theme tokens — no inline `<style>`, no hex.

**Q-J — ANSWERED by owner default (2026-05-26):** option **(c)** — designed **reusable from day 1**. First consumer is the Account avatar-upload flow only; ID-document cropping is **not** in scope (aspect-ratio mismatch, circular overlay is wrong for IDs). The component takes `AspectRatio`, `OutputWidth`, `OutputHeight`, `OutputMimeType` parameters so future surfaces can opt in without forking.

**Locked files (will execute once §5 lands — can run in parallel)**:
| File | Change | Risk |
|--|--|--|
| `src/ControlPanel/SIMF.ControlPanel/SIMF.ControlPanel.csproj` | **Add** `PackageReference Include="Cropper.Blazor" Version="1.5.1"`. *(Explicitly authorised by the owner per this ask — overrides global CLAUDE.md §1.7's csproj guard.)* | `breaking` (new dependency — must verify it targets .NET 10) |
| `src/Website/SIMF.Web/SIMF.Web.csproj` | Same package add **only if Q-J (c)** and the Website also needs cropping. Otherwise skip. | `breaking` |
| `src/Shared/SIMF.Components/Forms/SimfImageCropperModal.razor` *(new)* | The ported component. Hosts `<CropperComponent>` inside `<SimfModal>`. Parameters: `ImageUrl`, `AspectRatio` (default 1), `OutputWidth` / `OutputHeight` (default 400×400), `OutputMimeType` (default `image/png`), `OnCropped` (`EventCallback<string>` base64), `OnCancel`. Internal `Options` block **byte-identical** to V10. Output bytes through `GetImageChunkStreamAsync` then `Convert.ToBase64String` — identical to V10. | `none` (component-level) |
| `src/Shared/SIMF.Components/wwwroot/css/simf-components.css` | Add `.simf-cropper-wrapper`, `.simf-cropper-element`, circular-overlay rules — using `--simf-cropper-bg` token (added to `theme.tokens.css` first if absent). **Zero inline styles, zero hex.** | `none` |
| `src/Shared/SIMF.Components/wwwroot/css/theme.tokens.css` | New token `--simf-cropper-bg` (the V10 `#1a1a2e` equivalent) resolved against the existing surface/dark palette so both themes work. | `none` |
| `src/ControlPanel/.../Pages/Account/Profile.razor` (or whichever page hosts the avatar upload today) | After file pick, open `<SimfImageCropperModal>` instead of uploading raw bytes; on confirm, POST the cropped base64 to `AvatarUploadEndpoint`. | `breaking` (UX adds a step before save) |
| `_Imports.razor` in CP | Add `@using Cropper.Blazor.Components` + `@using Cropper.Blazor.Models` if used directly outside the wrapper. | `none` |
| `Strings.resx` (en + ar) | New keys: `Crop.Title`, `Crop.Cancel`, `Crop.Submit`, `Crop.Processing`. | `none` |
| `DECISIONS_LOG.md` | D-116 — Cropper.Blazor adopted (v1.5.1, matching V10); ported into `SimfImageCropperModal`; styles tokenised. Logs the §1.7 csproj-edit override owner-approved here. | `none` |

**What §11.5 will NOT do** (even with approval): pull MudBlazor in; copy V10's inline styles; copy the `#1a1a2e` hex. These three are non-negotiable against §8 unless you explicitly say otherwise on Q-I.

---

## 11A. Dependencies and recommended ordering

```
§5 (D-112)  ──► 11.1 (logs CSS)         ──► 11.2 (visitors/others toolbar)  ──► 11.4 (profile types)
                                          │
                                          └──► 11.3 (approve confirm + profile preview)

§5 (D-112)  ──► 11.5 (Cropper.Blazor)    ──► [Account avatar flow]
```

- §5 must land first — every other item reuses `SimfBanner`, the corrected icons, the full pager, and the modal pattern.
- §11.1 is independent (CSS-only) — can land in parallel with §5 if you want.
- §11.2 and §11.3 are independent of each other (different pages).
- §11.4 reuses everything above and is the heaviest — last.
- §11.5 only depends on `SimfModal` being available (already true) — can land in parallel with §11.1 / §11.2 / §11.3.

---

## 11B. Questions block — answer status

| ID | Status | Answer |
|--|--|--|
| **Q-E** | ✅ answered 2026-05-26 | (iii) — 2 rows exactly, group-aligned (filters / actions) |
| **Q-F** | ✅ answered 2026-05-26 | **backend first** — see §11C **Prompt A** |
| **Q-G** | ✅ answered 2026-05-26 | **NO** general admin profile-read endpoint. Scoped to pending-visitor / pending-other only, state + type guarded |
| **Q-H.1** | ⏸ deferred | finalised after Prompt B backend lands |
| **Q-H.2** | ⏸ deferred | finalised after Prompt B backend lands |
| **Q-H (overall)** | ✅ answered 2026-05-26 | **backend first** — see §11C **Prompt B** |
| **Q-I** | ✅ answered 2026-05-26 | (i) — same package, ported component (SimfModal-hosted) |
| **Q-J** | ✅ answered by default 2026-05-26 | (c) — reusable from day 1; first consumer is the Account avatar |

---

## 11C. Backend hand-off prompts (for a separate backend session/agent)

> Both prompts are self-contained — they can be pasted into a fresh Claude / Codex / other-agent session. Each ends with explicit acceptance criteria and a "what NOT to touch" list grounded in the SIMF freeze rules (D-110) and global CLAUDE.md.

---

### 11C — Prompt A — Generic CRUD endpoints for `/admin/visitors` and `/admin/others`

```
ROLE
You are a senior C# / FastEndpoints / EF Core backend engineer working on the SIMF
codebase at d:\SIMF\System\V1.0.0. You will add missing CRUD endpoints to two
existing admin user-type lists (Visitors and Others) so the Control Panel can
use the same canonical CRUD toolbar that /admin/admins already uses.

WHAT EXISTS TODAY (verify before assuming)
- /admin/admins has a full CRUD surface implemented in:
    src/Backend/SIMF.Api/Endpoints/Admin/CreateUserEndpoint.cs
    src/Backend/SIMF.Api/Endpoints/Admin/BulkDeleteUsersEndpoint.cs
    src/Backend/SIMF.Api/Endpoints/Admin/DuplicateUserEndpoint.cs
    src/Backend/SIMF.Api/Endpoints/Admin/ExportUsersEndpoint.cs
    src/Backend/SIMF.Api/Endpoints/Admin/ImportUsersEndpoint.cs
    src/Backend/SIMF.Api/Endpoints/Admin/ListUsersEndpoint.cs
  Study them. They are your reference shape.
- /admin/visitors and /admin/others have ONLY: list endpoint, create endpoint,
  per-row approve/reject. They lack: bulk-delete, duplicate, export, import.
- Frontend wiring is blocked on these endpoints. UI plan lives in
  docs/SIMF-CP-Grid-Banner-Modal-Plan.md §11.2.

WHAT TO BUILD
For BOTH visitors (UserType = Visitor) AND others (UserType = Other), add:

  1. POST /admin/{kind}/bulk-delete
       Request : AdminBulkDeleteRequest { Ids: Guid[], Reason: string }
                 Reason: required, 10..500 chars.
       Response: ApiResult<AdminBulkDeleteResponse> { Deleted: int, Skipped: int }
       Semantics: soft-delete (set IsActive=false / call Deactivate()).
                  Idempotent — already-deleted ids count toward Skipped.

  2. POST /admin/{kind}/duplicate
       Request : AdminDuplicateUserRequest { SourceId: Guid, NewEmail: string }
       Response: ApiResult<AdminCreateUserResponse>
       Semantics: clone the source user's DisplayName / UserType / ProfileTypeId
                  with the new email. New user starts in PendingApproval state.

  3. POST /admin/{kind}/export
       Request : AdminExportUsersRequest { Ids: Guid[]?, Query: GridQuery? }
       Response: Excel (.xlsx) binary stream, Content-Type
                 application/vnd.openxmlformats-officedocument.spreadsheetml.sheet
       Semantics: if Ids is non-empty, export those rows; else export everything
                  matching Query. Reuse the admins ExportUsersEndpoint sheet
                  format exactly — do NOT invent a new column layout.

  4. POST /admin/{kind}/import
       Request : multipart/form-data with a single .xlsx file
       Response: ApiResult<AdminImportUsersResponse>
                  { Created: int, Skipped: int, Errors: ImportError[] }
       Semantics: mirror admins ImportUsersEndpoint; the UserType column on each
                  imported row is FIXED to {kind} — ignore any UserType column
                  in the spreadsheet to prevent type-smuggling.

CONVENTIONS YOU MUST FOLLOW (these are SIMF SES rules)
- FastEndpoints: sealed Endpoint<TReq, TRes> with Configure() + HandleAsync().
- ApiResult<T> wrapper for EVERY response.
- Policies(nameof(AuthorizationPolicies.AdministratorOnly),
           nameof(AuthorizationPolicies.RequireApprovedAccount))
  on every new endpoint.
- Tags("Admin"); Summary(summary => summary.Summary = "...").
- Soft-delete via entity.Deactivate() — never EF Remove().
- FluentValidation .MaximumLength(N) MUST match the EF .HasMaxLength(N)
  on the corresponding column. Validators live in
  src/Backend/SIMF.Application/Validation/.
- DI services live in src/Application/IdentityAccess/. Add interfaces
  + implementations next to the existing IAdminUserService family.
- AppRoles / route policy constants — use the constants in
  OnlineErpSystem... no, SIMF: SIMF.Common.Enums.AppRoles. Never hardcode
  "Administrator".
- Every new endpoint file starts with: // Tests: SIMF.Api.Tests/<TestFile>.cs
  Add the corresponding test file.
- Errors: throw ApiException(ErrorCode, statusCode, "en message", "ar message").
  Add new ErrorCodes constants if needed; do not reuse a wrong-meaning code.

WHAT YOU MUST NOT TOUCH (FREEZE — D-110, commit 67e2263)
- EF schema. No new columns, tables, indexes. ProfileType / User / UserProfile
  schema is final at InitialCreate.
- Enum NAMES or VALUES. Adding new values is acceptable ONLY if you can prove it
  doesn't shadow an existing name and is genuinely needed; ask first if in doubt.
- Migration history. No new migrations beyond the one InitialCreate per context.
- appsettings.*.json — do not change keys. New configurable values get a typed
  options class registered in DI.
- .csproj / Directory.Build.props / Directory.Packages.props — do not edit
  unless a new NuGet package is genuinely required, AND you justify it in
  the decision-log entry.

ACCEPTANCE CRITERIA
- 8 new endpoint files compile under: dotnet build -c Release with 0 warnings
  / 0 errors (TreatWarningsAsErrors is enforced).
- AdminApprovalTests-style integration tests cover the happy path + the
  "actor without admin role gets 403" path + the "type-smuggling defense"
  for import (a row claiming UserType=Administrator in the visitors sheet
  is silently coerced to Visitor).
- Decision log entry D-113 added describing every new route + the policy
  + the soft-delete semantics.
- A short "Frontend consumer" section in the decision log calling out which
  CP files (UsersList.razor, VisitorsList.razor, OthersList.razor) are
  expected to start calling these endpoints next.

PROCESS
- Follow the §11 mandatory pre-approval format from ~/.claude/CLAUDE.md:
  read first, plan first, get owner approval, then code.
- Tests are part of "done". Do not declare complete without a passing
  dotnet test run quoted in the final report.
```

---

### 11C — Prompt B — Profile-types CRUD endpoints

```
ROLE
You are a senior C# / FastEndpoints / EF Core backend engineer working on the
SIMF codebase at d:\SIMF\System\V1.0.0. You will add the missing CRUD endpoints
for the ProfileType lookup table so the Control Panel can manage VIP / Normal /
… subtypes for both Visitor and Other UserTypes.

WHAT EXISTS TODAY (verify before assuming)
- ProfileType is an existing entity in the SIMF schema (D-110 baseline).
- Only ONE endpoint exists today:
    src/Backend/SIMF.Api/Endpoints/Admin/ListProfileTypesEndpoint.cs
  which exposes GET /admin/profile-types?userType=Visitor|Other and returns
  AdminProfileTypeSummary rows via IAdminProfileTypeQueryService.
- A near-identical CRUD pattern is already implemented for Interests in:
    src/Backend/SIMF.Api/Endpoints/Admin/InterestEndpoints.cs
  — that is the canonical reference. Mirror its structure (List + Get +
  Create + Update + Deactivate, all as sealed classes in ONE file).

WHAT TO BUILD
A new file src/Backend/SIMF.Api/Endpoints/Admin/ProfileTypeEndpoints.cs
exposing FIVE endpoints, scoped to AdministratorOnly + RequireApprovedAccount:

  1. POST /admin/profile-types/list
       Request : GridQuery (existing type)
       Response: ApiResult<GridPage<AdminProfileTypeSummary>>
       Semantics: paged + filterable, identical wire shape to
                  /admin/interests/list. Must support optional filter
                  Filters["userType"] = "Visitor" | "Other".

  2. GET /admin/profile-types/{id:guid}
       Response: ApiResult<AdminProfileTypeSummary>
       Semantics: 404 via ApiException(ErrorCodes.ProfileTypeNotFound, 404,
                  "Profile type not found.", "لم يتم العثور على نوع الملف.")

  3. POST /admin/profile-types
       Request : AdminCreateProfileTypeRequest
                  { UserType: string, Name: string, NameArabic: string,
                    DisplayOrder: int, IsActive: bool }
       Response: ApiResult<AdminProfileTypeSummary>
       Semantics: UserType is restricted to "Visitor" or "Other" — any other
                  value returns 400 (ApiException, validation error). Name
                  must be unique per UserType (case-insensitive).

  4. PUT /admin/profile-types/{id:guid}
       Request : AdminUpdateProfileTypeRequest
                  { Name: string, NameArabic: string,
                    DisplayOrder: int, IsActive: bool }
       Response: ApiResult<AdminProfileTypeSummary>
       Semantics: UserType is NOT updatable post-creation (a profile type
                  cannot move between Visitor and Other).

  5. DELETE /admin/profile-types/{id:guid}
       Response: ApiResult<bool>
       Semantics: SOFT-DELETE via entity.Deactivate() (IsActive = false).
                  Idempotent. Must refuse deletion if any UserProfile rows
                  still reference this ProfileTypeId — return
                  ApiException(ErrorCodes.ProfileTypeInUse, 409, ...).

PLUS:
- New service src/Application/IdentityAccess/AdminProfileTypeCommandService.cs
  implementing IAdminProfileTypeCommandService { Create / Update / Deactivate /
  GetAsync / ListAllAsync(GridQuery) }. Register in DI.
- New DTOs in src/Shared/SIMF.Contracts/Admin/ProfileType/:
    AdminCreateProfileTypeRequest.cs
    AdminUpdateProfileTypeRequest.cs
  (AdminProfileTypeSummary already exists — reuse it.)
- New validators in src/Backend/SIMF.Application/Validation/:
    AdminCreateProfileTypeRequestValidator.cs
    AdminUpdateProfileTypeRequestValidator.cs
  .MaximumLength(N) values MUST match the EF .HasMaxLength(N) on
  ProfileType.Name / NameArabic. Read the entity config to confirm before
  picking a number.
- New ErrorCodes constants: ProfileTypeNotFound, ProfileTypeInUse,
  ProfileTypeInvalidUserType, ProfileTypeNameTaken.
- New test file SIMF.Api.Tests/AdminProfileTypeTests.cs:
    - admin can create / update / soft-delete a Visitor profile type
    - admin can create / update / soft-delete an Other profile type
    - non-admin gets 403 on every endpoint
    - duplicate name within the same UserType → 409
    - same name across different UserTypes → allowed
    - cannot delete a profile type referenced by a UserProfile → 409
    - update with UserType change attempt → silently ignored (the route
      does not accept UserType in the body)

CONVENTIONS YOU MUST FOLLOW (these are SIMF SES rules)
- FastEndpoints: sealed Endpoint<TReq, TRes> with Configure() + HandleAsync().
- ApiResult<T> wrapper for EVERY response.
- AuthorizationPolicies.AdministratorOnly + RequireApprovedAccount on every
  endpoint.
- Tags("Admin"); Summary("...").
- Options(routeBuilder => routeBuilder.RequireRateLimiting("auth")) on
  Create / Update / Delete.
- // Tests: SIMF.Api.Tests/AdminProfileTypeTests.cs header on the endpoint file.
- ApiException(ErrorCode, statusCode, "en msg", "ar msg") — both locales.

WHAT YOU MUST NOT TOUCH (FREEZE — D-110, commit 67e2263)
- EF schema. ProfileType columns / table / indexes are final at InitialCreate.
- Migration history.
- Enum names/values (UserType, AccountState, etc.).
- appsettings.*.json.
- .csproj / Directory.Build.props / Directory.Packages.props.

ACCEPTANCE CRITERIA
- dotnet build -c Release : 0 warnings, 0 errors.
- dotnet test : the new AdminProfileTypeTests file passes 100% along with the
  full existing suite.
- Decision log entry D-115 added describing every new route + UserType
  validation + the IsInUse delete guard.
- A "Frontend consumer" line in D-115 noting that the CP page is tracked in
  docs/SIMF-CP-Grid-Banner-Modal-Plan.md §11.4 and will be built next.

PROCESS
- Follow the §11 mandatory pre-approval format from ~/.claude/CLAUDE.md:
  read first, plan first, get owner approval, then code.
- Tests are part of "done". Do not declare complete without a passing
  dotnet test run quoted in the final report.
```

---

---

## 12. Approval gate

**Status:** awaiting owner reply.
Reply `go` to authorise execution of §5 in the order of §7, or redirect any cell.
Per global CLAUDE.md §1.9 and §11, no code edits will be made until that reply.
