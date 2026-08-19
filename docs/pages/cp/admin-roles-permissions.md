# Role permissions editor - `/admin/roles/{id}/permissions`

| | |
|--|--|
| **Route** | `/admin/roles/{RoleId:guid}/permissions` (`@page` directive, `RolePermissionsEditor.razor` line 6) |
| **Layout** | `CpShellLayout` (`@layout CpShellLayout`) |
| **Surface** | Control Panel |
| **Audience** | Any signed-in CP admin holding `Roles.AssignPermissions`. Administrator holds it implicitly through the `PermissionCatalog.Wildcard` (`"*"`) claim. |
| **Auth** | Page: `@attribute [RequirePermission(PermissionCatalog.Roles.AssignPermissions)]`. API GET: `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Roles.View), nameof(AuthorizationPolicies.RequireApprovedAccount))`. API PUT: `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Roles.AssignPermissions), nameof(AuthorizationPolicies.RequireApprovedAccount))` + `Options(routeBuilder => routeBuilder.RequireRateLimiting("auth"))`. |
| **Pattern** | Not a CRUD list page. A single `EditForm` over the whole `PermissionCatalog.All` catalogue, grouped into cards by `PermissionDef.Page`, one `SimfCheckbox` per code. No `SimfDataGrid`, no toolbar, no pager. |
| **Status** | Real. First shipped in `becdd9549` (2026-05-31, "Issue-1 Phase 4 - role→permission editor + user→role assignment UI"). |
| **Implements use case(s)** | N/A - `docs/SIMF-UCS-001-Use-Case-Specifications.md` carries no use case for permission assignment. The nearest authored entries are `UC-ROL-CREATE-001`, `UC-ROL-RENAME-001` and `UC-ROL-DELETE-001`, all on the parent `/admin/roles` page. `UC-ROL-CREATE-001`'s postcondition names this page as "the follow-up permission editor". |
| **Backend endpoints** | BFF: `GET /account/api/admin/roles/{id}/permissions`, `PUT /account/api/admin/roles/{id}/permissions`. API: `GET /api/v1/admin/roles/{id}/permissions`, `PUT /api/v1/admin/roles/{id}/permissions`. |
| **Source files** | [`RolePermissionsEditor.razor`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/RolePermissionsEditor.razor), [`RolePermissionsEditor.razor.cs`](../../../src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/RolePermissionsEditor.razor.cs) |
| **Backend service** | [`AdminRoleService.GetPermissionsAsync` / `.SetPermissionsAsync`](../../../src/Backend/SIMF.Infrastructure/Identity/AdminRoleService.cs) over the existing `SimfRole` + `Permission` + `RolePermission` entities in `SimfIdentityDbContext`. |
| **Tests** | [`docs/tests/e2e/cp-admin-roles-permissions.md`](../../tests/e2e/cp-admin-roles-permissions.md) (E2E-RPM-001..015 + two element scenarios), [`tests/SIMF.Api.Tests/RolePermissionsEndpointsTests.cs`](../../../tests/SIMF.Api.Tests/RolePermissionsEndpointsTests.cs). No bUnit test in `tests/SIMF.ControlPanel.Tests/` references `RolePermissionsEditor` - a repo-wide search of `tests/` for that type name returns only the API test file. |
| **Last reviewed** | 2026-08-19 |

---

## 1. Purpose

`/admin/roles` creates, renames and deletes roles but grants nothing. This page
is where a role acquires its actual rights: it renders the whole permission
catalogue, pre-ticked from the role's current grants, and saves the ticked set
back as the role's complete grant list. An administrator walks in holding a
custom role that does nothing and walks out with that role gating exactly the
pages and actions they intended, because the codes ticked here are the same
codes the API endpoints and the CP `[RequirePermission]` attributes check.

The catalogue it renders is not a page-specific list. `PermissionCatalog.All` is
described in its own source as "the single source of truth" for endpoint gating,
CP page gating, side-menu filtering and the seeder, so what is offered here is
the entire authorisation surface of the system, grouped by the page each code
belongs to. Baseline roles are shown read-only rather than hidden, so an
administrator can inspect what a built-in role grants without being able to
change it.

## 2. Audience + permissions

- **Who can reach it:** a signed-in CP admin whose `perm` claim carries
  `Roles.AssignPermissions`, or the `"*"` wildcard. The page gate is
  `@attribute [RequirePermission(PermissionCatalog.Roles.AssignPermissions)]`;
  `RequirePermissionAttribute` is an `AuthorizeAttribute` whose `Policy` is set
  to `PermissionCatalog.PolicyFor(permissionCode)`, i.e. `perm:Roles.AssignPermissions`.
- **Who can edit/write on it:** the same set, minus baseline roles. `SaveAsync`
  returns early when `_role.IsBaseline`, the Save button is not rendered at all
  for a baseline role, and `AdminRoleService.SetPermissionsAsync` throws
  `ErrorCodes.RoleIsBaseline` (409) if a hand-crafted PUT arrives anyway.
- **Authorisation gates, all three layers:**

  | Layer | Gate |
  |-------|------|
  | CP page | `[RequirePermission(PermissionCatalog.Roles.AssignPermissions)]` -> policy `perm:Roles.AssignPermissions` |
  | BFF route | No permission gate of its own, but not ungated: the whole group is `routes.MapGroup("/account/api").RequireAuthorization()` (`AccountEndpoints.cs`), so a cookie-authenticated principal is required before the handler runs. The handler in `AccountEndpoints.FaqAndRoles.cs` then reads the access token from the cookie with `http.GetTokenAsync("access_token")` and returns `Results.Unauthorized()` when there is none. The permission check happens at the API. |
  | API `GET /api/v1/admin/roles/{id}/permissions` | `perm:Roles.View` + `RequireApprovedAccount` |
  | API `PUT /api/v1/admin/roles/{id}/permissions` | `perm:Roles.AssignPermissions` + `RequireApprovedAccount` + `RequireRateLimiting("auth")` |

  The GET and the page carry **different** codes. The page needs
  `Roles.AssignPermissions`; the load call needs `Roles.View`. A role granted
  only `Roles.AssignPermissions` therefore passes the page gate and then fails
  its own load. See section 7.
- **What an unauthenticated user sees:** `Routes.razor` picks `RedirectToLogin`
  for the unauthenticated case. A signed-in admin lacking the permission gets
  `RedirectToNotPermitted`, which does `Nav.NavigateTo("/not-permitted")` - an
  ordinary client-side navigation, not a `forceLoad`, because "the circuit and
  the session are both healthy here". `Program.cs` also sets the cookie
  `options.AccessDeniedPath = "/not-permitted"`, but the source comment records
  that the Blazor interactive router never goes through that path.
- If the API rejects the session mid-page, `simf-account.js` handles it below
  the page: `simfReadEnvelope` sees HTTP 401, calls
  `window.location.assign('/login')` and returns a never-resolving promise so
  the calling page cannot act on a bogus body while the navigation is in flight.

## 3. Screenshots

No screenshots of this page exist in the repository. The table below records
where they would live; every Captured cell reads "Not captured" because none has
been taken. Only the first two file names are named by the E2E catalogue - the
remaining five are this doc's own extension of the same naming convention, not
paths the catalogue asks for.

| State | File | Captured |
|-------|------|----------|
| Custom role, no grants (all unticked) | `docs/screenshots/cp-admin-roles-permissions-golden-before.png` | Not captured |
| Custom role, grants ticked + success alert | `docs/screenshots/cp-admin-roles-permissions-golden-after.png` | Not captured |
| Baseline role (info notice, checkboxes disabled, no Save) | `docs/screenshots/cp-admin-roles-permissions-baseline.png` | Not captured |
| Loading state | `docs/screenshots/cp-admin-roles-permissions-loading.png` | Not captured |
| Load failure (error alert, no cards) | `docs/screenshots/cp-admin-roles-permissions-load-failed.png` | Not captured |
| Save failure (error alert, selection intact) | `docs/screenshots/cp-admin-roles-permissions-save-failed.png` | Not captured |
| RTL (Arabic) | `docs/screenshots/cp-admin-roles-permissions-rtl.png` | Not captured |

The first two file names are quoted from the "Evidence captured" block of
E2E-RPM-001; the rest follow the same `cp-admin-roles-permissions-*.png`
convention the catalogue's implementation notes set.

## 4. UI affordances

### 4.1 Banner / page header

`<SimfPageHeader Title="@_pageTitle" />`, which renders
`<header class="simf-page-header"><h1 class="simf-page-header__title">`. No
`Actions` fragment is passed, so the header is title-only.

`_pageTitle` is set twice. `OnInitializedAsync` sets it to
`L["Admin.RolePermissions.Title"]` ("Role permissions" / "صلاحيات الدور") before
the load. On a successful load it becomes
`string.Format(L["Admin.RolePermissions.TitleFor"], _role.RoleName)`, whose EN
format string is `Permissions — {0}` and whose AR format string is
`الصلاحيات — {0}`. A failed load leaves the generic title in place.

The browser tab is `<PageTitle>@_pageTitle · SIMF</PageTitle>`.

The page body is `<div class="simf-page-wide"><div class="simf-surface">`.
`.simf-page-wide` exists so "the content can grow to a comfortable reading
width" per its CSS comment.

Directly under the surface sit, in order: the message alert (4.6), then either
the loading paragraph, or - when `_role is not null` - the baseline notice or
the intro paragraph, then the form.

### 4.2 Toolbar (CRUD pages only)

N/A - not a CRUD list page. There is no toolbar, no Add / Edit / Details /
Delete, no Copy / Paste / Duplicate, and no Excel Import / Export. The page's
only two controls are the form actions:

| Button | Component | Type | Wired to | Notes |
|--------|-----------|------|----------|-------|
| Save permissions (`Admin.RolePermissions.Save`) | `SimfButton` | `submit` (the `SimfButton` default) | the `EditForm`'s `OnValidSubmit="SaveAsync"` | Rendered only when `!_role.IsBaseline`. `Loading="_busy"`, `LoadingLabel="@L["Admin.RolePermissions.Saving"]"` ("Saving…" / "جارٍ الحفظ…"). While `Loading` the button renders a spinner `<span role="status" aria-label="@LoadingLabel">` in place of its label and is `disabled`. |
| Back to roles (`Admin.RolePermissions.Back`) | `SimfButton Variant="secondary" Type="button"` | `OnClick="BackToList"` | `Nav.NavigateTo("/admin/roles")`. `Disabled="_busy"`. Always rendered, baseline or not. No unsaved-changes prompt - see section 7. |

Both live in `<div class="simf-form__actions">` inside the `EditForm`.

### 4.3 Grid columns (CRUD pages only)

N/A - not a CRUD list page. The equivalent structure is the card-per-page
grouping:

```csharp
private static readonly IReadOnlyList<IGrouping<string, PermissionDef>> _groups =
    PermissionCatalog.All.GroupBy(permission => permission.Page).ToList();
```

Each group renders as `<section class="simf-card">` with `<h3>@group.Key</h3>`
and one `SimfCheckbox` per `PermissionDef` in the group. `group.Key` is the raw
`PermissionDef.Page` string, and each checkbox label is the raw
`PermissionDef.DisplayName`.

`_groups` is `static readonly`, so the catalogue is materialised once per
process and is identical for every role. LINQ `GroupBy` preserves first-appearance
order, so the cards appear in `PermissionCatalog.BuildAll()` declaration order,
not alphabetically.

**Size, as a dated snapshot.** Counted on 2026-08-19 from the `BuildAll()` body
(`PermissionCatalog.cs` lines 872-1280): **287** `PermissionDef` entries across
**71** distinct `Page` values, so the page renders 71 cards and 287 checkboxes
for every role. No test pins this count, so treat the numbers as a measurement
of that day's catalogue rather than a contract. The `Page` values, in
alphabetical order, are:

`Accounts, Admins, AiDashboard, AiInvocations, AiPrompts, Announcements,
Archive, Assistant, Attendance, Attendees, BadgeUpdateRequests, Banners,
Bookings, Booths, BusinessMeetings, Configuration, ContactInquiries,
ContentBlocks, Countries, DelegationMeetings, DeviceKeys, Editions,
EmailTemplates, Exhibitors, Faq, Files, Gates, HallAllocations, HallArrivals,
HallAvailability, Halls, Interests, Invitations, Logs, Media, MediaLibrary,
MediaPartners, MeetingTables, News, OperationLog, Operations, Organisations,
OrganizationProfile, Others, ParticipationDocumentRequests, ProfileTypes,
ProgrammeDays, ProgrammeTimeline, Questions, RatingConfig, Ratings, Regions,
Reports, Roles, SeatLayouts, SeatPlans, Seating, ServicesMonitor,
SessionCategories, SessionModeration, SessionModerators, SessionSummaries,
Sessions, SpeakerMeetingRequests, Speakers, Sponsors, Statistics, Themes,
VenueMap, Vips, Visitors`.

The page's own group, `Roles`, holds seven codes: `Roles.View` ("View roles"),
`Roles.Create` ("Create roles"), `Roles.Edit` ("Rename roles"), `Roles.Delete`
("Delete roles"), `Roles.AssignPermissions` ("Assign permissions to roles"),
`Roles.Export` ("Export roles") and `Roles.Import` ("Import roles"), all with
`BaselineRoles` = `AdminOnly`.

### 4.4 Pager

N/A - the page has no pager. Every catalogue entry renders on one screen; there
is no server-side paging, no page-size selector and no "Showing X-Y of Z"
caption. The GET returns only the role's granted codes
(`AdminRolePermissionsResponse.GrantedCodes`), never the catalogue, because -
per the contract's own summary - "The full catalogue is not returned: the CP
builds it from `PermissionCatalog.All`."

### 4.5 Form fields

The form has one repeated control rather than a field set.

| Field | Type | Required | MaxLength | Validation | Locale |
|-------|------|----------|-----------|------------|--------|
| One checkbox per `PermissionDef` (287 of them on 2026-08-19) | `SimfCheckbox` -> `<label class="simf-checkbox"><input type="checkbox">` | no - an empty selection is a valid save that clears every grant | N/A | Server-side only, in `SetPermissionsAsync`: every submitted code must exist in `PermissionCatalog.All`. No client-side check. | Label is `PermissionDef.DisplayName`, an English literal in `PermissionCatalog.cs`. Not localised - see section 8. |

Wiring per checkbox:

```razor
<SimfCheckbox Value="_selected.Contains(permission.Code)"
              ValueChanged="@((bool on) => Toggle(permission.Code, on))"
              Disabled="_role.IsBaseline || _busy">
    @permission.DisplayName
</SimfCheckbox>
```

`Toggle(code, on)` adds to or removes from `_selected`, a
`HashSet<string>` built with `StringComparer.Ordinal`. `_selected` is also the
`EditForm`'s `Model`, which means no `DataAnnotationsValidator` is attached and
`OnValidSubmit` fires on every submit.

### 4.6 Message alert

The page renders its outcome messages as an **inline alert at the top of the
surface**, not as a floating toast:

```razor
@if (_toast is not null)
{
    <SimfAlert Variant="@_toast.Variant">@_toast.Message</SimfAlert>
}
```

`Toast` is a page-local `record Toast(string Variant, string Message)`. The
variant is `"success"` on a saved set and `"error"` on a failed load or save.
The E2E catalogue calls these "toasts"; the rendered element is a
`SimfAlert`, which is a static block that stays until the next render clears it.
There is no dismiss control and no auto-dismiss timer.

### 4.7 Baseline notice and intro

When `_role.IsBaseline` the page shows
`<SimfAlert Variant="info">@L["Admin.RolePermissions.BaselineNotice"]</SimfAlert>`:
"This is a built-in role; its permissions are managed by the system and cannot
be edited here." / "هذا دور أساسي؛ تُدار صلاحياته بواسطة النظام ولا يمكن تعديلها هنا."

Otherwise it shows `<p>@L["Admin.RolePermissions.Intro"]</p>`: "Select the pages
and actions this role can use. Changes apply the next time a holder of the role
signs in." / "حدّد الصفحات والإجراءات التي يمكن لهذا الدور استخدامها. تُطبَّق
التغييرات عند تسجيل دخول حامل الدور في المرة التالية."

The "next time a holder signs in" wording is load-bearing: permission codes are
baked into the JWT at token-mint time, so a grant change does not reach a
signed-in holder until their token is re-minted.

**Which roles are baseline.** `IdentitySeeder` loops
`foreach (var role in AppRoles.CpRoles)` and calls `EnsureRoleAsync`, which
creates each with `IsBaseline = true`. `AppRoles.CpRoles` is
`[Administrator, GateOperator, PublicRelations, SecurityTeam,
ScientificCommittee]`, so all five are read-only here. The comment at the top of
`RolePermissionsEditor.razor` names only "GateOperator / PublicRelations" and
predates `SecurityTeam` and `ScientificCommittee`; the seeder is the current
list.

## 5. Data flow

```
Page load (OnInitializedAsync -> LoadAsync)
  -> JS.InvokeAsync<ApiResult<AdminRolePermissionsResponse>>(
       "simfAccount.getJson", "/account/api/admin/roles/{RoleId}/permissions")
  -> simfAccount.getJson: fetch(url, { credentials: 'same-origin' })
  -> BFF  AccountEndpoints.FaqAndRoles.cs  group.MapGet("/admin/roles/{id:guid}/permissions")
       http.GetTokenAsync("access_token")  (401 Unauthorized when absent)
  -> SimfAdminClient.GetRolePermissionsAsync -> GET api/v1/admin/roles/{id}/permissions
  -> GetRolePermissionsEndpoint (perm:Roles.View + RequireApprovedAccount)
  -> AdminRoleService.GetPermissionsAsync
       SimfIdentityDbContext.Roles (AsNoTracking, SingleOrDefault)
       + RolePermissions join Permissions -> the granted Code list
  -> ApiResult<AdminRolePermissionsResponse>.Ok
  -> _role set, _selected filled from GrantedCodes, _pageTitle -> "Permissions — {RoleName}"

Save permissions (EditForm OnValidSubmit -> SaveAsync)
  -> body = new AdminSetRolePermissionsRequest { Codes = _selected.ToList() }
  -> JS.InvokeAsync<ApiResult<bool>>(
       "simfAccount.putJson", "/account/api/admin/roles/{RoleId}/permissions", body)
  -> simfAccount.putJson: fetch(..., method 'PUT', Content-Type application/json)
  -> BFF  group.MapPut("/admin/roles/{id:guid}/permissions")
  -> SimfAdminClient.SetRolePermissionsAsync -> PUT api/v1/admin/roles/{id}/permissions
  -> SetRolePermissionsEndpoint (perm:Roles.AssignPermissions + RequireApprovedAccount
                                 + RequireRateLimiting("auth"))
  -> AdminRoleService.SetPermissionsAsync
       RoleManager.FindByIdAsync           -> 404 ROLE_NOT_FOUND
       role.IsBaseline                     -> 409 ROLE_IS_BASELINE
       codes not in PermissionCatalog.All  -> 400 VALIDATION_FAILED
       resolve codes -> Permission ids, diff against existing RolePermission rows,
       RemoveRange(toRemove) + Add(toAdd), one SaveChangesAsync
       auditLog.WriteSuccessAsync(AuditEvents.RolePermissionsUpdated, ...)
  -> ApiResult<bool>.Ok(true)
  -> success SimfAlert "Permissions saved."

Back to roles
  -> BackToList() -> Nav.NavigateTo("/admin/roles")   (no backend call)
```

`SimfAdminClient` is registered in `Program.cs` with
`client.BaseAddress = apiBaseUri` and its `BasePath` is `"api/v1/admin/"`, which
is why its own method bodies use the relative `$"roles/{id}/permissions"`.

Every backend call this page makes:

| When | Method + path (BFF) | Forwards to (API) | Request body | Response shape |
|------|---------------------|-------------------|--------------|----------------|
| `OnInitializedAsync` -> `LoadAsync` | `GET /account/api/admin/roles/{id}/permissions` | `GET /api/v1/admin/roles/{id}/permissions` | none | `ApiResult<AdminRolePermissionsResponse>` |
| Save permissions | `PUT /account/api/admin/roles/{id}/permissions` | `PUT /api/v1/admin/roles/{id}/permissions` | `AdminSetRolePermissionsRequest { List<string> Codes }` | `ApiResult<bool>` |

`AdminRolePermissionsResponse` is
`record (Guid RoleId, string RoleName, bool IsBaseline, IReadOnlyList<string> GrantedCodes)`.

The API endpoint binds its own request type,
`SetRolePermissionsRequest { Guid Id; List<string> Codes }`, so the route id and
the body's `Codes` bind together; the wire body the CP sends is the contract's
`AdminSetRolePermissionsRequest`, which carries `Codes` only.

## 6. Validation + error handling

- **Client-side guards.** `SaveAsync` opens with
  `if (_busy || _role is null || _role.IsBaseline) { return; }`, so a
  double-submit, a submit before load and a submit on a baseline role all fire
  nothing. The Save button is not rendered for a baseline role, and every
  checkbox carries `Disabled="_role.IsBaseline || _busy"`. There is no
  client-side check of the codes themselves.
- **Server-side validation.** There is **no FluentValidation validator** for
  `AdminSetRolePermissionsRequest` - no `AbstractValidator` in `SIMF.Api`
  references it. Validation is the imperative sequence in
  `AdminRoleService.SetPermissionsAsync`, in this order:
  1. `RoleManager.FindByIdAsync` returns null -> `ApiException(ErrorCodes.RoleNotFound, 404, "The role was not found.", "لم يتم العثور على الدور.")`.
  2. `role.IsBaseline` -> `ApiException(ErrorCodes.RoleIsBaseline, 409, "Baseline roles' permissions cannot be edited.", "لا يمكن تعديل صلاحيات الأدوار الأساسية.")`.
  3. Submitted codes are de-duplicated with `Distinct(StringComparer.Ordinal)`, then checked against `PermissionCatalog.All`. Any unknown -> `ApiException(ErrorCodes.ValidationFailed, 400, $"Unknown permission code(s): {string.Join(", ", unknown)}.", "رمز صلاحية واحد أو أكثر غير معروف.")`. The whole request is rejected; valid codes in the same body are not applied.

  The GET side has one check: `GetPermissionsAsync` returning null makes
  `GetRolePermissionsEndpoint` throw the same `ErrorCodes.RoleNotFound` 404.
- **Error envelope.** Standard `ApiResult<T>.Error` with a `Code` from
  `ErrorCodes` plus bilingual `Message` / `MessageArabic`. The three codes this
  page can surface are the string constants
  `ErrorCodes.RoleNotFound = "ROLE_NOT_FOUND"`,
  `ErrorCodes.RoleIsBaseline = "ROLE_IS_BASELINE"` and
  `ErrorCodes.ValidationFailed = "VALIDATION_FAILED"`.
- **Language selection.** Both failure paths render
  `envelope?.Error?.MessageForCurrentCulture()`, which
  `ApiErrorExtensions` resolves as "Arabic when the culture is `ar` (or any
  `ar-XX`), English otherwise", falling back to the page's own resx string when
  the envelope itself is null.
- **Non-JSON responses.** `simfReadEnvelope` in `simf-account.js` turns a
  non-JSON body into a synthesised envelope with
  `code: 'BAD_RESPONSE'` and the message "The server returned an unexpected
  response (HTTP {status})." / "أعاد الخادم استجابة غير متوقعة (HTTP {status})."
  so the page shows an alert instead of throwing a `JSException` into the global
  Blazor error UI. An empty body returns `null`, which both handlers treat as a
  failure and fall back to their resx string.
- **Alert strategy and its resx keys.**

  | Situation | Variant | Key | EN | AR |
  |-----------|---------|-----|----|----|
  | Save succeeded | `success` | `Admin.RolePermissions.Saved` | Permissions saved. | تم حفظ الصلاحيات. |
  | Load failed, no envelope message | `error` | `Admin.RolePermissions.LoadFailed` | The role permissions could not be loaded. | تعذّر تحميل صلاحيات الدور. |
  | Save failed, no envelope message | `error` | `Admin.RolePermissions.Fallback` | The permissions could not be saved. Please try again. | تعذّر حفظ الصلاحيات. حاول مرة أخرى. |

  When the envelope does carry an error, its own bilingual message wins over
  these two fallbacks.

## 7. Edge cases + known limitations

- **The page gate and the load gate are different codes.** The page needs
  `Roles.AssignPermissions`; `GetRolePermissionsEndpoint` needs `Roles.View`. A
  role granted the first without the second renders the page, fires the GET, is
  refused at the API, and lands on the `Admin.RolePermissions.LoadFailed` alert
  with no cards. Grant both together.
- **A failed load renders nothing but the alert.** `LoadAsync` only assigns
  `_role` on success, and the markup gates the whole form behind
  `else if (_role is not null)`. On failure the surface holds the error alert
  and nothing else - no cards, no Save, and no Back button, since Back lives
  inside the `EditForm`. The way out is the shell navigation.
- **Save replaces; it does not append.** `SetPermissionsAsync` computes
  `toRemove` and `toAdd` against the existing `RolePermission` rows. Saving with
  everything unticked leaves the role with zero grants (E2E-RPM-007). The diff
  exists for a specific reason recorded in the source: an already-granted code
  must not be "deleted and re-inserted in the same unit of work (that would trip
  the EF change tracker on the composite key)".
- **A no-delta save still writes an audit row.** The `SaveChangesAsync` is
  guarded by `if (toRemove.Count > 0 || toAdd.Count > 0)`, but
  `auditLog.WriteSuccessAsync(AuditEvents.RolePermissionsUpdated, ...)` runs
  unconditionally afterwards. Pressing Save twice writes two
  `Role.PermissionsUpdated` rows with `Detail = "id={id}; granted={count}"` and
  changes nothing the second time. `AuditEvents.RolePermissionsUpdated` is the
  literal `"Role.PermissionsUpdated"`.
- **The audit count is the resolved count, not the requested count.**
  `granted={requestedPermissions.Count}` counts the `Permission` ids the
  database returned for the submitted codes.
- **Codes are validated against the in-memory catalogue but persisted from the
  database.** A code passes step 3 because it is in `PermissionCatalog.All`, then
  is resolved with `dbContext.Permissions.Where(permission => requested.Contains(permission.Code))`.
  A catalogue code with no seeded `Permission` row would drop out silently and
  the save would still return 200. In practice `IdentitySeeder.SeedPermissionCatalogAsync`
  is idempotent and re-runs on every boot, so the two normally cannot diverge.
- **No unsaved-changes guard.** Back to roles calls `Nav.NavigateTo` directly;
  ticks made since the last save are discarded with no prompt (E2E-RPM-005).
- **Baseline roles are visible but inert.** The cards still render, so an
  administrator can read what `Administrator`, `GateOperator`,
  `PublicRelations`, `SecurityTeam` or `ScientificCommittee` grants. The
  Administrator row is a special case: its permissions are the `"*"` wildcard
  resolved at token-mint time, so its ticked set here reflects whatever
  `RolePermission` rows exist rather than its effective rights.
- **The whole catalogue renders every time.** 287 checkboxes in 71 cards, on
  every role, with no search box, no filter, no expand/collapse and no
  select-all-in-group. Finding one code means scrolling or using the browser's
  find. The cards do not tile: the `EditForm` carries `class="simf-form"`, which
  is `display: flex; flex-direction: column`, and `.simf-card` is
  `inline-size: 400px; max-inline-size: 100%`. So the 71 cards stack vertically
  in a single 400px column, inside a `.simf-page-wide` capped at 1600px. On a
  wide monitor most of the page is empty to the side of that column.
- **`_toast` is not cleared when a load is retried.** `SaveAsync` sets
  `_toast = null` before its call; `LoadAsync` does not. `LoadAsync` runs once
  from `OnInitializedAsync`, so this is latent rather than observable today.
- **Grants do not take effect until the holder's token is re-minted.** Stated in
  the intro copy and true of the whole permission system: codes are baked into
  the JWT.

## 8. i18n + RTL

- The page **chrome** is fully localised: all eleven `Admin.RolePermissions.*`
  keys exist in both `src/ControlPanel/SIMF.ControlPanel/Resources/Strings.resx`
  (lines 3350-3360) and `Strings.ar.resx` (lines 3340-3350), read through
  `IStringLocalizer<Strings> L`. They are `Title`, `TitleFor`, `Intro`,
  `BaselineNotice`, `Loading`, `Saving`, `Save`, `Back`, `Saved`, `LoadFailed`
  and `Fallback`.
- The **catalogue is not localised.** Card headings come from
  `PermissionDef.Page` and checkbox labels from `PermissionDef.DisplayName`,
  both English literals in `PermissionCatalog.cs` ("Assign permissions to
  roles", "View sessions", and so on). In Arabic the headings and every checkbox
  label stay in English. E2E-RPM-013 hedges this as "in Arabic where localised";
  the precise position is that only the eleven resx strings above translate.
- Language toggle: `CpShellLayout.razor` renders
  `<SimfLanguageSwitch Label='@L["Shell.SwitchLanguage"]' />`.
- RTL: `App.razor` sets
  `dir="@(CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? "rtl" : "ltr")"`
  on the document, so the mirroring is document-level and this page inherits it.
  The page adds no direction-specific markup of its own. `.simf-checkbox` and
  `.simf-form__actions` are flexbox rows spaced with `gap`, and `.simf-card` /
  `.simf-page-wide` size themselves with the logical `inline-size` /
  `max-inline-size` rather than `width`, so the layout follows the document
  direction rather than fighting it. No physical `left` / `right` property was
  found on any of the four classes this page uses.
- Error text from the API is bilingual on the wire and picked by
  `MessageForCurrentCulture` (section 6).

## 9. Accessibility

Asserted only from the four component sources and the shared stylesheet read
this session.

- **Alerts.** `SimfAlert` renders `role="alert"` for the error variant
  (announced assertively) and `role="status" aria-live="polite"` for the success
  and info variants. The baseline notice is therefore announced politely, a save
  failure assertively.
- **Buttons.** `SimfButton` renders `aria-busy="true"` while `Loading`, and its
  spinner is `<span class="simf-button__spinner" role="status"
  aria-label="@LoadingLabel">`, so the label announced during a save is the
  localised "Saving…" string. `disabled="@(Disabled || Loading)"` removes it from
  the tab order while in flight.
- **Checkboxes.** `SimfCheckbox` wraps the `<input type="checkbox">` inside its
  `<label class="simf-checkbox">`, so the visible text is the accessible name
  without needing an `id`/`for` pair. `disabled` is set from the `Disabled`
  parameter, so on a baseline role the whole catalogue is skipped by the tab
  order.
- **Focus indicators.** `simf-components.css` defines `:focus-visible` rules
  bound to the `--focus-ring` token for `.simf-button`, `.simf-link`,
  `.simf-control`, `.simf-field__input` and others. `.simf-checkbox` has **no**
  focus rule of its own - it sets `display`, `align-items`, `gap`, `font-size`,
  `color` and `cursor` only, and the child rule
  `.simf-checkbox input[type="checkbox"]` adds `inline-size`, `block-size` and
  `accent-color: var(--color-accent-blue)` - so the 287 checkboxes fall back to
  the browser's default focus ring rather than the SIMF one.
- **Headings.** One `<h1>` from `SimfPageHeader`, then one `<h3>` per card. The
  `<h2>` level is skipped.
- **Keyboard.** Tab order is document order: checkboxes in catalogue order, then
  Save, then Back. There are no modals on this page, so there is no focus trap
  or ESC handling to describe.
- **Colour contrast:** Unverified - the token values behind `--color-text`,
  `--color-accent-blue` and the alert variants were not measured this session.
- **Screen-reader walkthrough:** Unverified - no assistive-technology pass has
  been run against this page.

## 10. Related use cases (UCS-001)

| UC ID | Title | Notes |
|-------|-------|-------|
| N/A | Assign permissions to a role | `SIMF-UCS-001` has no entry for this page. A search of the document for role use cases returns only the three below. |
| UC-ROL-CREATE-001 | Create a custom role (D-134 Sprint A) | Its postcondition reads "Ready for the follow-up permission editor to grant rights and the follow-up user editor to assign users". The permission editor named there is this page. |
| UC-ROL-RENAME-001 | Rename a custom role | Same baseline guard, same 409 `RoleIsBaseline` on the server. |
| UC-ROL-DELETE-001 | Delete a custom role | Deletes the role's `RolePermission` rows, i.e. everything this page writes. |

Authoring an entry for this page would follow §9 of `SIMF-UCS-001`, "How to
author the remaining entries", which reads the page doc's §1, §5 and §6 - now
available above.

## 11. Related E2E test scenarios

All scenarios live in
[`docs/tests/e2e/cp-admin-roles-permissions.md`](../../tests/e2e/cp-admin-roles-permissions.md).

| Scenario | ID | Coverage |
|----------|----|----------|
| Golden round-trip - tick, save, reload | E2E-RPM-001 | Both calls, the success alert, and that the reload shows exactly the saved set |
| Custom role with no grants | E2E-RPM-002 | Cards still render; every checkbox unticked; no error |
| Toggle on then off before save | E2E-RPM-003 | Exercises the `toRemove` / `toAdd` diff path |
| Save persists the selected set | E2E-RPM-004 | The `Saving…` loading label and the return to idle |
| Back to roles without saving | E2E-RPM-005 | No PUT fires; the unsaved tick is discarded |
| Baseline role is read-only | E2E-RPM-006 | Info notice, all checkboxes disabled, no Save button |
| Clear-all leaves zero grants | E2E-RPM-007 | PUT with `Codes = []`; parent grid shows Permissions = 0 |
| Auth gate | E2E-RPM-008 | Admin without `Roles.AssignPermissions` lands on `/not-permitted` |
| Role not found | E2E-RPM-009 | 404 on the GET; error alert; no cards |
| Baseline edit refused at the API | E2E-RPM-010 | Hand-crafted PUT -> 409 |
| Unknown permission code | E2E-RPM-011 | Hand-crafted PUT -> 400; nothing persisted |
| Server 500 on save | E2E-RPM-012 | Fallback bilingual alert; selection intact; button not stuck |
| RTL / Arabic render | E2E-RPM-013 | `dir="rtl"`, Arabic chrome, reversed button order |
| SecurityTeam baseline grants + nav | E2E-RPM-014 | The eight access-control codes, read-only (D-752) |
| ScientificCommittee baseline grants + nav | E2E-RPM-015 | The 31 programme codes, read-only (D-752) |
| Element inventory (LTR + RTL) | E2E-RPM-ELS-001 | Every control present, named and gated |
| Element health | E2E-RPM-ELS-002 | No dead control, zero console errors, no horizontal overflow |

**Note on error-code naming.** The catalogue writes the three error codes as C#
constant names (`RoleNotFound`, `RoleIsBaseline`, `ValidationFailed`). The values
on the wire are the constants' string values, `ROLE_NOT_FOUND`,
`ROLE_IS_BASELINE` and `VALIDATION_FAILED`, per `ErrorCodes.cs` lines 10, 147 and
149. Assert against the string values.

**Lower-layer coverage.**
[`tests/SIMF.Api.Tests/RolePermissionsEndpointsTests.cs`](../../../tests/SIMF.Api.Tests/RolePermissionsEndpointsTests.cs)
covers the API without a browser:
`Put_then_get_round_trips_and_replaces_the_grant_set` (E2E-RPM-001 / -007),
`Put_on_a_baseline_role_is_refused` (E2E-RPM-010, asserts
`HttpStatusCode.Conflict`) and `Put_with_an_unknown_code_is_rejected`
(E2E-RPM-011, asserts `HttpStatusCode.BadRequest`). The baseline grant sets in
E2E-RPM-014 / -015 are pinned at build time by
`tests/SIMF.Application.Tests/IdentityAccess/PermissionCatalogBaselineTests.cs`
and at the database layer by `tests/SIMF.Api.Tests/IdentitySeederTests.cs`.
The permission gates themselves are pinned by
`tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` and
`tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs`; the deny
redirect by `tests/SIMF.ControlPanel.Tests/PermissionDeniedRoutingTests.cs`.

## 12. Related docs

- Parent page doc: [`admin-roles.md`](admin-roles.md) - `/admin/roles`, which
  links here from the grid's `<RowActions>` slot, wrapped in
  `<AuthorizedAction Permission="@PermissionCatalog.Roles.AssignPermissions">`,
  and from `RolesViewDelete.OpenPermissionEditor()`.
- E2E catalogue: [`cp-admin-roles-permissions.md`](../../tests/e2e/cp-admin-roles-permissions.md).
- Permission system design and playbook:
  [`docs/manuals/SIMF-Auth-Permissions-Dev-Guide.md`](../../manuals/SIMF-Auth-Permissions-Dev-Guide.md),
  companion [`docs/SIMF-Permission-Catalogue.md`](../../SIMF-Permission-Catalogue.md).
- API spec: [`SIMF-API-001`](../../SIMF-API-001-API-Specification.md) - the
  `ApiResult<T>` envelope and error model this page consumes.
- Architecture: [`SIMF-SAD-001`](../../SIMF-SAD-001-Software-Architecture-Document.md).
- Use cases: [`SIMF-UCS-001`](../../SIMF-UCS-001-Use-Case-Specifications.md)
  §UC-ROL-CREATE-001 / RENAME / DELETE.
- Catalogue source: [`PermissionCatalog.cs`](../../../src/Shared/SIMF.Common/PermissionCatalog.cs),
  roles list [`AppRoles.cs`](../../../src/Shared/SIMF.Common/AppRoles.cs).
- Manual chapter: Unverified - no chapter covering this route was located this
  session.
- Component catalogue: Unverified - `SIMF-CMP-001` was not opened this session.
  The components this page uses are `SimfPageHeader` (Layout), `SimfAlert`,
  `SimfButton` and `SimfCheckbox` (all Forms), plus the framework `EditForm` and
  `PageTitle`.

## 13. Changelog

No decision id (D-nnn) is attached to this page in any source read this session.
The source comments identify the work as "Issue-1", and the commits below are
the page's full git history.

| Date | Commit | Change |
|------|--------|--------|
| 2026-05-31 | `becdd9549` | Issue-1 Phase 4 - role→permission editor + user→role assignment UI. The page ships: catalogue grouped by `PermissionDef.Page`, checkbox per code, baseline roles read-only, `GET`/`PUT /account/api/admin/roles/{id}/permissions`. |
| 2026-07-02 | `c1a48e55b` | clean(cp): code-behind sweep - 26 more pages (Admin + 2 Account). The inline `@code` block moved to `RolePermissionsEditor.razor.cs` as a partial class. |
| 2026-07-20 | (E2E doc only) | D-752 added E2E-RPM-014 / -015 for the `SecurityTeam` and `ScientificCommittee` baseline grant sets. The page itself did not change; the two new baseline roles simply appear here as read-only. |
| 2026-07-30 | `c5db596a6` | refactor(cp): remove 1442 dead using directives from 164 files. Touched the code-behind's using list only. |

---

_Last reviewed:_ 2026-08-19 by Claude (first authoring of this page doc; the
route had a row in `PAGE-INDEX.md` and an authored E2E catalogue but no page
reference doc). If the page has changed and this doc has not been re-reviewed in
60 days, it is out of date. Re-walk the page in a browser and update every
section that drifted - in particular the 287 / 71 catalogue counts in §4.3,
which nothing pins.
