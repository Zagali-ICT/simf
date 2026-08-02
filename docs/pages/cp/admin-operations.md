# Operations toggles — `/admin/operations`

| | |
|--|--|
| **Route** | `/admin/operations` |
| **Layout** | `CpShellLayout` |
| **Surface** | Control Panel |
| **Audience** | Administrator (any role holding `Operations.View`) |
| **Auth** | `@attribute [RequirePermission(PermissionCatalog.Operations.View)]`. API: the two **GET**s require `Operations.View`, the two **PUT**s require `Operations.Edit` — both plus `RequireApprovedAccount`. The PUTs also carry `RequireRateLimiting("auth")`. |
| **Pattern** | **Not CRUD.** Two independent **singleton** toggle sections on one surface — no grid, no pager, no add/edit/delete, no empty state. |
| **Status** | ✅ Real (D-166, gap doc G4; PDF §2.3 registration gate + §2.4 archive visibility) |
| **Backend endpoints** | BFF `GET|PUT /account/api/admin/registration-gate`, `GET|PUT /account/api/admin/archive/visibility` → API `/api/v1/admin/…`. Plus the **anonymous** public read `GET /api/v1/app/archive/visibility`, which the Website and the Flutter app call. |
| **Source** | [`OperationsToggles.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/OperationsToggles.razor) + [`.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/OperationsToggles.razor.cs), [`RegistrationGateEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Operations/RegistrationGateEndpoints.cs), [`ArchiveVisibilityEndpoints`](../../../src/Backend/SIMF.Api/Endpoints/Operations/ArchiveVisibilityEndpoints.cs), [`OperationsToggleService`](../../../src/Backend/SIMF.Infrastructure/Operations/OperationsToggleService.cs), [`RegistrationGateAutoCloseWorker`](../../../src/Backend/SIMF.Infrastructure/Operations/RegistrationGateAutoCloseWorker.cs) |
| **Backed by** | `dbo.RegistrationGate` + `dbo.ArchiveVisibility` on `SimfAppDbContext` — one row each, keyed by a fixed `SingletonId`. |
| **Tests** | [`tests/SIMF.Api.Tests/OperationsTogglesTests.cs`](../../../tests/SIMF.Api.Tests/OperationsTogglesTests.cs), [`ArchiveTests.cs`](../../../tests/SIMF.Api.Tests/ArchiveTests.cs); E2E [`docs/tests/e2e/cp-admin-operations.md`](../../tests/e2e/cp-admin-operations.md) (E2E-OPS-001…011 + two element rows) |
| **Last reviewed** | 2026-08-02 |

---

## 1. Purpose

Two switches that decide what the **public** can do, kept on one page because
both are event-lifecycle levers rather than content: **can anyone still create an
account**, and **is the past-events archive visible**. An administrator walks in
at a specific moment — registration has filled up, or the previous edition's
archive should go dark before the new programme is announced — flips one switch,
and expects the public surfaces to follow immediately.

Neither switch is content and neither is a lookup, so neither belongs on
`/admin/configuration`. They are singletons: there is nothing to add, nothing to
delete, and no list — which is why this page looks unlike every other admin page
in the Control Panel.

## 2. Audience + permissions

- **Who can reach it:** any admin whose role holds `Operations.View`
  (`Administrator` is the wildcard `*`, so it holds both).
- **Who can save on it:** `Operations.Edit`, enforced **at the API only**.
- **Gates:** page `[RequirePermission(PermissionCatalog.Operations.View)]`;
  nav item `CpNavigation.cs:280` → `RequiredPermission: Operations.View`;
  endpoints `Policies(PolicyFor(Operations.{View|Edit}), RequireApprovedAccount)`.
- **Without `Operations.View`:** the nav item is hidden and a direct navigation
  lands on `/not-permitted`; no BFF call fires.
- **With `View` but not `Edit`:** the page renders and both sections load, but
  every Save returns **403**. See §7 — the buttons are not disabled.

## 3. Screenshots

**None captured.** The page has not been re-walked in a browser since this doc
was written; the E2E catalogue names the files it expects
(`docs/screenshots/cp-admin-operations-{golden-before,golden-after,autoclose-after,archive-before,archive-after,rtl}.png`)
and they do not exist yet. Do not treat this section as evidence of a live check.

## 4. UI affordances

`SimfBanner` (title only) → `.simf-page-wide` → `.simf-surface`, containing a
shared toast slot, the **Registration gate** section, an `<hr class="simf-divider" />`,
and the **Archive visibility** section.

### 4.1 The three states each section can be in

This is the page's most easily broken behaviour and was a fix in its own right
(§6.16 / F-U5-007): "Loading…" used to key off `_gate is null`, so a load that
**failed** left the section reading "Loading…" for as long as the admin cared to
wait. The states are now distinct and driven by two different fields:

| State | Condition | Renders |
|-------|-----------|---------|
| Loading | `_loading` | `Admin.Operations.Loading` — "Loading…" |
| Failed | `!_loading && _gate is null` (resp. `_archive`) | error `SimfAlert` with `_gateError` / `_archiveError`, plus a **Retry** button calling `LoadAsync` |
| Loaded | otherwise | the controls below |

The two sections fail **independently** — one can be loaded while the other shows
its Retry.

### 4.2 Controls

| # | Section | Control | Wired to |
|---|---------|---------|----------|
| 1 | Registration gate | `SimfCheckbox` "Registration is open" | `_gateIsOpen` |
| 2 | Registration gate | `SimfTextField` type `datetime-local` "Auto-close (Saudi time)" | `_gateAutoCloseInput` |
| 3 | Registration gate | `SimfButton` "Save" | `SaveGateAsync` → `PUT …/registration-gate` |
| 4 | Archive visibility | `SimfCheckbox` "Archive is visible to the public" | `_archiveIsVisible` |
| 5 | Archive visibility | `SimfButton` "Save" | `SaveArchiveAsync` → `PUT …/archive/visibility` |

Plus, per section, a read-only **"Last changed"** `<dl class="simf-dl">` and — only
in the failed state — a **Retry** `SimfButton`.

Both Save buttons carry `Loading="_busy"` / `LoadingLabel="Saving"`, and `_busy`
is a **single shared field**: while either section is saving, every control on
the page is disabled.

### 4.3 "Last changed" is Saudi local time, not UTC

Rendered `@_gate.LastChangedAt.FormatSaudi("dd-MM-yyyy hh:mm tt")` — Saudi
wall-clock, 12-hour with AM/PM, per the local-time-everywhere rule (D-770). The
column is stored UTC; only the display is converted. The same applies to the
auto-close field, which is labelled "Auto-close (**Saudi time**)".

### 4.4 Pager

N/A — singletons, no list.

### 4.5 Form fields

| Field | Type | Required | Editable | Validation |
|-------|------|----------|----------|------------|
| Registration is open | checkbox | — | always | none (a bool) |
| Auto-close (Saudi time) | `datetime-local` | **no** — blank means "no scheduled close" | always | client-side `DateTime.TryParse`; on failure a red toast and **no request fires** |
| Archive is visible to the public | checkbox | — | always | none (a bool) |

There is **no FluentValidation validator** on either request
(`UpdateRegistrationGateRequest`, `UpdateArchiveVisibilityRequest`). The only
input that can be malformed is the auto-close string, and it is caught in the
page before the call.

**Time conversion, both directions** — the input carries no zone, so the page
supplies one explicitly:

- **Read:** `_gate.AutoClose?.ToSaudi().ToString("yyyy-MM-ddTHH:mm")`
- **Write:** `SaudiTime.FromSaudiWallClock(parsed)` — what the admin typed is
  read as **Saudi wall-clock** and converted to the offset the API stores.

## 5. Data flow

```
Save (gate)  → SaveGateAsync → JS simfAccount.putJson
             → BFF PUT /account/api/admin/registration-gate  (AccountEndpoints.cs:1685)
             → API PUT /api/v1/admin/registration-gate       (Operations.Edit)
             → OperationsToggleService.UpdateRegistrationGateAsync
             → dbo.RegistrationGate + OperationLog audit row
             → ApiResult<RegistrationGateState> → toast + refreshed "Last changed"
```

| When | BFF route | API route | Permission |
|------|-----------|-----------|------------|
| `OnInitializedAsync` | `GET /account/api/admin/registration-gate` | `GET /admin/registration-gate` | `Operations.View` |
| `OnInitializedAsync` | `GET /account/api/admin/archive/visibility` | `GET /admin/archive/visibility` | `Operations.View` |
| Save (gate) | `PUT /account/api/admin/registration-gate` | `PUT /admin/registration-gate` | `Operations.Edit` |
| Save (archive) | `PUT /account/api/admin/archive/visibility` | `PUT /admin/archive/visibility` | `Operations.Edit` |
| _(not this page)_ | — | `GET /api/v1/app/archive/visibility` | **anonymous** |

Both GETs run inside one `try` in `LoadAsync`, sequentially, on page init.

### 5.1 Where each switch is actually read

A toggle is only worth as much as the code that honours it:

- **Registration gate** — `IOperationsToggleService.IsRegistrationOpenAsync`,
  called on the public sign-up path. Closed → sign-up returns **403**
  `REGISTRATION_CLOSED` and **no account row is created**.
- **Archive visibility** — the anonymous `GET /api/v1/app/archive/visibility`,
  which the Website and the Flutter app read to decide whether to show the
  past-events archive.

### 5.2 The auto-close worker

`RegistrationGateAutoCloseWorker` (hosted in the API) polls **every minute**
after a **1-minute startup delay** (so it cannot race migrations and seeding on
boot — without it, integration-test fixtures raced the worker's first SELECT).
Each tick it writes only when `IsOpen` is still true **and** `AutoClose <= now`,
then sets `LastChangedByUserId = null` (a worker is not a person) and writes a
`RegistrationGateAutoClosed` audit row.

It cannot race an administrator: an admin who re-opens the gate clears
`AutoClose`, after which the worker has nothing to flip. It registers with
`IWorkerHeartbeatRegistry`, so its health is visible on the worker-ops monitor.

## 6. Validation + error handling

**The failure mode this page is built around:** `simfReadEnvelope` turns a
transport or HTTP failure into a **returned** `ApiResult.Fail` rather than a
throw. The `catch` in `LoadAsync` therefore never sees the common failure — every
envelope must be checked explicitly, which is what the `is { Success: true, Data:
not null }` patterns do. A future edit that "simplifies" those to a bare `try`
would silently reintroduce the stuck-on-"Loading…" bug.

| Situation | Result |
|-----------|--------|
| Unparseable auto-close | red toast `Auto-close must be a valid date and time.` / `يجب أن يكون الإغلاق التلقائي وقتاً صحيحاً.` — **no PUT fires**, `_busy` cleared by `finally` |
| Load envelope not success | that section renders `_gateError` / `_archiveError` (server message via `MessageForCurrentCulture()`, else `The operations toggles could not be loaded.` / `تعذّر تحميل مفاتيح التشغيل.`) + Retry |
| Save envelope not success (incl. **403** for a View-only admin) | red toast — server message, else `The change could not be saved.` / `تعذّر حفظ التغيير.` |
| Save success | green toast `Registration gate updated.` / `تم تحديث بوّابة التسجيل.` or `Archive visibility updated.` / `تم تحديث إظهار الأرشيف.`; `_gate` / `_archive` replaced from the response so "Last changed" refreshes without a reload |

**Audit.** `UpdateRegistrationGateAsync` writes `RegistrationGateUpdated`
(`Detail = "isOpen=…; autoClose=…"`) and `UpdateArchiveVisibilityAsync` writes
`ArchiveVisibilityUpdated` (`Detail = "isVisible=…"`) — **only when the value
actually changed**. The worker writes `RegistrationGateAutoClosed` with a null
actor.

## 7. Edge cases + known limitations

- **A missing singleton self-heals.** `LoadRegistrationGateAsync` /
  `LoadArchiveVisibilityAsync` insert a default row (**open** / **visible**) when
  none is found, so the page can never render "no rows". _Note: the
  `OperationsToggleService` class comment still says the service "never creates
  rows, only updates them" — that comment contradicts the code and is stale._
- **The public read fails open, and does not self-heal.**
  `IsRegistrationOpenAsync` returns `true` when the row is missing and uses
  `AsNoTracking` — it never inserts. So a lost seed row leaves sign-up **open**
  until an admin opens this page, which is what re-creates the row.
- **A past auto-close closes the gate before the worker ticks.**
  `IsRegistrationOpenAsync` compares `AutoClose <= now` on every read, so
  registration behaves closed immediately even while `IsOpen` is still `true` in
  the database and the display still shows the box ticked.
- **Saving an unchanged value is a confirmed no-op.** Success toast, HTTP 200, but
  no `SaveChanges`, no audit row, and "Last changed" does not advance.
- **The Save buttons are not permission-aware.** There is no
  `<AuthorizedAction Permission="…">` wrapper on this page, so a `View`-only
  admin sees enabled Save buttons and learns of the denial only from the 403
  toast. Not a security hole — the API is gated — but it is inconsistent with
  every other admin page and with the project's own action-gating rule.
- **One toast for two sections.** `_toast` is a single field rendered at the top
  of the surface. Saving the **Archive** section — the lower one — puts the
  confirmation above the fold, potentially out of view, and clears any toast the
  gate section had just shown.
- **One busy flag for two sections.** Saving either section disables both.
- **No confirmation on either Save.** Closing public registration is a
  high-consequence, instantly public action taken with a single click, with no
  `SimfConfirm` gate — unlike the delete flows elsewhere in the Control Panel.
- **The PUTs are rate-limited (deliberately).** They keep
  `RequireRateLimiting("auth")` and are **not** part of the D-809 operational
  exemption: these are twice-an-event administrative flips, not the on-site
  operational surface (gate scans, hall arrivals, walk-in registration, approve,
  offline batch) whose limits were removed.

## 8. i18n + RTL

All strings from `Admin.Operations.*` in `Strings.resx` / `Strings.ar.resx`, EN ↔
AR verified in pairs: `Title`, `Loading`, `LoadFailed`, `SaveFailed`, `Save`,
`Saving`, `RegistrationGate.{Heading,Desc,IsOpen,AutoClose,AutoCloseHint,AutoCloseInvalid,LastChangedAt,Saved}`,
`Archive.{Heading,Desc,IsVisible,LastChangedAt,Saved}`, plus the shared
`Common.Retry`. The nav label is `Module.OperationsToggles`.

The page mirrors to RTL under Arabic (`<html dir="rtl" lang="ar">`); the banner
reads "مفاتيح التشغيل", the two headings "بوّابة التسجيل" and "إظهار الأرشيف".

## 9. Accessibility

- Toasts and load errors are `SimfAlert`: the **error** variant renders
  `role="alert"` (assertive), **success** renders `role="status" aria-live="polite"` —
  so both the 403 denial and the save confirmation are announced.
- Labels come from `SimfCheckbox` child content and the `SimfTextField`
  `Label` / `Helper` parameters; the auto-close hint is a helper, not a
  placeholder.
- Save buttons expose a `LoadingLabel` while busy rather than only a spinner.
- **Not verified:** tab order, focus handling on Retry, and contrast have not
  been re-walked on a live render for this doc.

## 10. Related use cases

No UCS-001 entry maps to this page. Its authority is **gap doc G4** and the
requirements PDF **§2.3** (registration gate) and **§2.4** (archive visibility),
which is what D-166 was raised against.

## 11. Related E2E test scenarios

[`docs/tests/e2e/cp-admin-operations.md`](../../tests/e2e/cp-admin-operations.md) —
E2E-OPS-001 gate round-trip (with a public sign-up probe proving 403
`REGISTRATION_CLOSED`), 002 auto-close round-trip, 003 archive toggle against the
anonymous endpoint, 004 no-`View` → `/not-permitted`, 005 **View-only → Save
403**, 006 malformed auto-close, 007 idempotent no-op writes no audit row, 008
singleton self-heal, 009 / 010 server 500 on load and on save, 011 RTL, plus
E2E-OPS-ELS-001/002 element inventory and health.

The API layer is covered lower down by `OperationsTogglesTests.cs`: the seeded
open state, closing the gate (and a past auto-close) making sign-up return 403,
the anonymous archive read, and a non-admin getting 403 on both PUTs.

## 12. Related docs

- Decisions: `docs/decisions/DECISIONS_LOG.md` **D-166** (the page + both
  singletons + the auto-close worker), **D-770** (Saudi local time everywhere),
  **D-809** (the operational rate-limit exemption — which this page is
  deliberately outside of).
- Permissions: `PermissionCatalog.Operations.{View,Edit}` in
  [`PermissionCatalog.cs`](../../../src/Shared/SIMF.Common/PermissionCatalog.cs)
  (both `AdminOnly` baseline).
- Sibling page: [`admin-configuration.md`](admin-configuration.md) — the
  key/value settings store, which is where a setting goes when it is *not* one of
  these two lifecycle switches.

## 13. Changelog

| Date | Decision | Change |
|------|----------|--------|
| (gap wave) | D-166 | Original — `/admin/operations` with the registration gate (PDF §2.3) and archive visibility (PDF §2.4) singletons, the admin GET/PUT pairs, the anonymous public archive read, and the auto-close background worker. |
| (§6.16) | F-U5-007 | Split the per-section state into `_loading` + `_gateError` / `_archiveError` so a **failed** load shows an error and a Retry instead of "Loading…" forever. |
| 2026-07-25 | D-770 | "Last changed" and the auto-close field moved to Saudi local time (`FormatSaudi` / `ToSaudi` / `SaudiTime.FromSaudiWallClock`); the field label became "Auto-close (Saudi time)". |
| 2026-08-01 | D-809 | The on-site operational endpoints dropped their rate limits; these two PUTs deliberately **kept** `RequireRateLimiting("auth")`. |

---

_Last reviewed:_ 2026-08-02 by Claude — authored from source (D-809 Definition of
Done). **Not re-walked in a browser**: §3 has no screenshots and §9 keyboard /
contrast are unverified.
