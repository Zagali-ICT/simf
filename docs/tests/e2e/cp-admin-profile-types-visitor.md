# E2E test catalogue — Visitor profile types (`/admin/profile-types/visitor`)

| | |
|--|--|
| **Page** | [`cp/admin-profile-types-visitor.md`](../../pages/cp/admin-profile-types-visitor.md) |
| **Route** | `/admin/profile-types/visitor` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Required permission** | `ProfileTypes.View` (page) · `ProfileTypes.Create` / `.Edit` / `.Delete` (actions) |
| **Last reviewed** | 2026-06-02 |

> **Page surface (read from `VisitorProfileTypesList.razor` + `ProfileTypeForm.razor`).**
> A `SimfDataGrid` of `AdminProfileTypeSummary` rows pinned to `userType=Visitor` +
> `isVisitor=true`. Columns: **Account type** (localised `Visitor (audience)`),
> **Name**, **Name (Arabic)**, **Page colour**, **Active** (on/off `SimfPill`).
> Toolbar/row functions: **Add** (`OnAddAsync` → Add modal), **Edit** per row
> (`OnEditAsync` → Edit modal), **Details** per row (`OnDetailsAsync` → read-only
> modal), **Deactivate** per row (`OnDeleteAsync` → confirm modal), the grid
> **filter box** (Name), **sort** (Name / Name Arabic), the **numbered pager**
> (First / Prev / Next / Last + page-size), and **Multiselect** row checkboxes.
> The Add/Edit modals host `ProfileTypeForm` (`IsPartnerForm="false"`, so it does
> NOT render the MobileAppRole picker — that only shows on the Other page). Form
> fields: **Account type** (read-only display `Visitor (audience)`), **Name
> (English)**, **Name (Arabic)**, **Page colour** (D-120 paired text + native
> `<input type="color">` swatch), **Visible in pickers (active)** checkbox.
>
> **Backing API (via the BFF proxy in `AccountEndpoints.cs`).** All calls go
> through `simfAccount.*` to `/account/api/admin/profile-types*`, which forward to
> the API on `:5175`:
> - List → `POST /account/api/admin/profile-types/list` (body = `GridQuery` with
>   `Filters.userType=Visitor`, `Filters.isVisitor=true`) → API
>   `ListAdminProfileTypesEndpoint` (`ProfileTypes.View`).
> - Create → `POST /account/api/admin/profile-types` (`AdminCreateProfileTypeRequest`,
>   `UserType="Visitor"`, `IsVisitor=true`) → `CreateAdminProfileTypeEndpoint`
>   (`ProfileTypes.Create`, `auth` rate-limit).
> - Update → `PUT /account/api/admin/profile-types/{id}` (`AdminUpdateProfileTypeRequest`)
>   → `UpdateAdminProfileTypeEndpoint` (`ProfileTypes.Edit`, `auth` rate-limit).
> - Deactivate → `DELETE /account/api/admin/profile-types/{id}` →
>   `DeactivateAdminProfileTypeEndpoint` (`ProfileTypes.Delete`, `auth` rate-limit).
>
> **Server error codes (`ErrorCodes.cs` + `AdminProfileTypeCommandService.cs`).**
> `PROFILE_TYPE_NAME_TAKEN` (409, duplicate name within the Visitor UserType),
> `PROFILE_TYPE_IN_USE` (409, any `UserProfile` still references the row),
> `ProfileTypeInvalidUserType` (400, create with a non-Visitor `UserType` or an
> invalid `MobileAppRole`), `ProfileTypeNotFound` (404). Audit events:
> `ProfileTypeCreated`, `ProfileTypeUpdated`, `ProfileTypeDeactivated`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-VPT-001 | Full CRUD round-trip — Add → Edit → Details → Deactivate | happy | P0 | _to author_ |
| E2E-VPT-002 | PageColor D-120 paired text + native swatch behaviour | happy | P1 | _to author_ |
| E2E-VPT-003 | Grid filter by Name returns the matching subset | happy | P2 | _to author_ |
| E2E-VPT-004 | Sort by Name (asc/desc) reorders the grid | happy | P2 | _to author_ |
| E2E-VPT-005 | Numbered pager (First/Prev/Next/Last + page size) | happy | P2 | _to author_ |
| E2E-VPT-006 | Details modal renders read-only description list, Close dismisses | happy | P2 | _to author_ |
| E2E-VPT-007 | Empty list renders `SimfEmptyState` ("No profile types yet.") | happy | P1 | _to author_ |
| E2E-VPT-008 | Auth gate: signed-in admin lacking `ProfileTypes.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-VPT-009 | Validation: blank English name → bilingual modal error, no POST | error | P1 | _to author_ |
| E2E-VPT-010 | Validation: blank Arabic name + blank/too-long PageColor → bilingual error | error | P1 | _to author_ |
| E2E-VPT-011 | Conflict: duplicate name in Visitor scope → 409 `PROFILE_TYPE_NAME_TAKEN` | error | P1 | _to author_ |
| E2E-VPT-012 | Deactivate in-use → 409 `PROFILE_TYPE_IN_USE` (bilingual, row stays Active) | error | P0 | _to author_ |
| E2E-VPT-013 | Server 500 on `/list` → red fallback toast, no rows render | resilience | P2 | _to author_ |
| E2E-VPT-014 | RTL / Arabic render mirrors page + Add modal | i18n | P1 | _to author_ |

## Scenarios

### E2E-VPT-001 — Full CRUD round-trip

```gherkin
Feature: Visitor profile types CRUD round-trip
  As an Administrator
  I want to manage the colour-coded Visitor profile-type picker
  So that the walk-in registration wizard offers the correct tiles

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator (superadmin@zagali-ict.com) holding ProfileTypes.* has signed in
    via /login + /login/totp using the Get-Totp helper
  And they have landed on /admin/profile-types/visitor

Scenario: Create, edit, view, then deactivate one Visitor profile-type
  Given the grid currently shows {N} rows, all with Account type "Visitor (audience)"
  When the administrator clicks "Add profile type"
  Then the Add modal opens titled "Add profile type"
  And it shows a read-only Account type field reading "Visitor (audience)"
  And it shows the fields Name (English), Name (Arabic), Page colour, and the "Visible in pickers (active)" checkbox (ticked)
  And it does NOT show a Mobile-app role picker (that is Other-page only)
  When they fill Name (English)="VIP delegation"
  And they fill Name (Arabic)="وفد كبار الشخصيات"
  And they set Page colour to "#FFD700" via the text input
  And they click "Create profile type"
  Then the BFF forwards POST /account/api/admin/profile-types with UserType="Visitor" and IsVisitor=true
  And the API returns HTTP 200 with ApiResult.Success=true
  And the modal closes
  And a green toast reads "Created \"VIP delegation\"." / "تم إنشاء \"VIP delegation\"."
  And the grid shows {N + 1} rows
  And a row exists with Name="VIP delegation", Page colour "#FFD700", and the green "Yes" Active pill

  When the administrator clicks the "Edit" action on that row
  Then the Edit modal opens titled "Edit profile type — VIP delegation" with the row's values pre-filled
  And the "Visible in pickers (active)" checkbox is visible and ticked
  When they change Page colour to "#1E90FF"
  And they click "Save changes"
  Then the BFF forwards PUT /account/api/admin/profile-types/{id} (UserType absent from the body)
  And the API returns HTTP 200
  And the modal closes
  And a green toast reads "Saved \"VIP delegation\"." / "تم حفظ \"VIP delegation\"."
  And the row's Page colour column reads "#1E90FF"

  When the administrator clicks the "Details" action on that row
  Then a read-only modal opens titled "..." showing a description list with Account type, Name, Name (Arabic), Page colour, and Active="Yes"
  When they click "Close"
  Then the modal closes

  When the administrator clicks the "Deactivate" action on that row
  Then a confirm modal opens reading 'Deactivate the profile type "VIP delegation"? Existing users keep their assignment; ...'
  When they click "Deactivate"
  Then the BFF forwards DELETE /account/api/admin/profile-types/{id}
  And the API returns HTTP 200 with ApiResult.Data=true
  And a green toast reads "Deactivated \"VIP delegation\"." / "تم تعطيل \"VIP delegation\"."
  And the grid reloads without that row (it was unreferenced, so the soft-delete drops it from the active-filtered list)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-profile-types-visitor-001-before.png`
- Screenshot after each modal: `docs/screenshots/cp-admin-profile-types-visitor-001-{add,edit,details,delete}.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/profile-types*` call returns 200
- Audit rows: `AuditEvents.ProfileTypeCreated`, `ProfileTypeUpdated`, `ProfileTypeDeactivated` rows, each carrying the actor's id and `Detail` (id + name)

### E2E-VPT-002 — PageColor D-120 paired text + native swatch

```gherkin
Scenario: The colour swatch and the text input stay in sync per the D-120 contract
  Given the Add modal is open
  When the administrator types "#FFD700" into the Page colour text input
  Then the native colour swatch displays #FFD700 (text is the source of truth)
  When they type "var(--brand-blue)" into the text input
  Then the text input keeps "var(--brand-blue)" verbatim
  And the swatch falls back to the brand navy #244A77 for display (no write-back)
  When they pick a colour from the native swatch
  Then the swatch writes the chosen #rrggbb back into the text input
  And submitting persists the text-input value
```

### E2E-VPT-003 — Grid filter by Name

```gherkin
Scenario: Filtering the grid by Name narrows the rows server-side
  Given Visitor profile-types "VIP delegation" and "General" both exist
  When the administrator types "VIP" into the grid filter box
  Then a new POST /account/api/admin/profile-types/list fires
  And the request body keeps Filters.userType="Visitor" and Filters.isVisitor="true" (structural pins re-applied)
  And the body adds Search="VIP"
  And only rows whose Name or Name (Arabic) match "VIP" render
  When they clear the filter
  Then the full Visitor list returns
```

### E2E-VPT-004 — Sort by Name

```gherkin
Scenario: Sorting by Name reorders the grid without losing the UserType pins
  Given the Visitor grid shows several rows
  When the administrator clicks the "Name" column header to sort ascending
  Then a POST /account/api/admin/profile-types/list fires with Sort="name", SortDescending=false
  And the body still carries Filters.userType="Visitor" and Filters.isVisitor="true"
  And the rows render A→Z by English name
  When they click the "Name" header again
  Then the request sets SortDescending=true and the rows render Z→A
```

### E2E-VPT-005 — Numbered pager

```gherkin
Scenario: The numbered pager pages through the Visitor list
  Given more than one page of Visitor profile-types exist (Top defaults to 20)
  When the administrator clicks "Next"
  Then a POST /account/api/admin/profile-types/list fires with an increased Skip
  And the page summary updates (e.g. "Showing 21–40 of {total}")
  When they click "Last" then "First"
  Then the Skip jumps to the final then the first page respectively
  When they change the page size
  Then the list reloads with the new Top and the UserType pins preserved
```

### E2E-VPT-006 — Details modal

```gherkin
Scenario: The Details modal is read-only and closes cleanly
  Given a Visitor profile-type "General" exists
  When the administrator clicks the "Details" action on the "General" row
  Then a modal opens with a description list (no editable fields, no Save button)
  And it lists Account type="Visitor (audience)", Name="General", Name (Arabic), Page colour, Active
  And the only footer button is "Close"
  When they click "Close"
  Then the modal closes and no network request fired
```

### E2E-VPT-007 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no Visitor profile-type rows (or all are filtered out)
  When the administrator opens /admin/profile-types/visitor
  Then the grid body renders the SimfEmptyState component
  And the empty state shows the bilingual copy "No profile types yet." / "لا توجد أنواع ملفات بعد."
  And the toolbar still shows the "Add profile type" button
  And no error toast appears
```

### E2E-VPT-008 — Auth gate

```gherkin
Scenario: A signed-in admin lacking ProfileTypes.View is denied
  Given a signed-in Control Panel user whose role does NOT grant ProfileTypes.View
    (the page carries [RequirePermission(PermissionCatalog.ProfileTypes.View)])
  When they navigate to /admin/profile-types/visitor
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/profile-types/list request fires
  And the "Visitor profile types" nav item is hidden (CpNavigation RequiredPermission = ProfileTypes.View)
```

### E2E-VPT-009 — Validation: blank English name

```gherkin
Scenario: Blank English name shows the bilingual modal error and blocks the POST
  Given the Add modal is open
  When the administrator leaves Name (English) blank
  And fills Name (Arabic)="وفد" and Page colour="#244A77"
  And clicks "Create profile type"
  Then a SimfAlert error appears inside the modal
  And reads "Name must be 1–128 characters." / "يجب أن يتراوح الاسم بين 1 و128 حرفًا."
  And the modal stays open
  And no POST /account/api/admin/profile-types request fires (client-side guard in ProfileTypeForm)
```

### E2E-VPT-010 — Validation: Arabic name + PageColor

```gherkin
Scenario: Blank Arabic name and an invalid PageColor each surface their own bilingual error
  Given the Add modal is open with Name (English)="VIP delegation"
  When the administrator leaves Name (Arabic) blank
  And clicks "Create profile type"
  Then the modal error reads "Arabic name must be 1–128 characters." / "يجب أن يتراوح الاسم العربي بين 1 و128 حرفًا."
  And no POST fires

  When they fill Name (Arabic)="وفد" but set Page colour to a 33+ character string
  And click "Create profile type"
  Then the modal error reads "Page colour must be 1–32 characters." / "يجب أن يتراوح لون الصفحة بين 1 و32 حرفًا."
  And no POST fires
```

### E2E-VPT-011 — Conflict: duplicate name

```gherkin
Scenario: A duplicate name within the Visitor scope returns 409 with the bilingual server message
  Given a Visitor profile-type Name="General" already exists
  When the administrator opens the Add modal
  And fills Name (English)="General" + Name (Arabic)="عام" + Page colour="#244A77"
  And clicks "Create profile type"
  Then the BFF forwards POST /account/api/admin/profile-types
  And the API returns HTTP 409 with ApiResult.Error.Code="PROFILE_TYPE_NAME_TAKEN"
  And the modal stays open
  And the SimfAlert surfaces the bilingual MessageForCurrentCulture()
    "A profile type named 'General' already exists for Visitor."
    / "يوجد نوع ملف شخصي بالاسم 'General' لـ Visitor بالفعل."
```

### E2E-VPT-012 — Deactivate in-use → 409

```gherkin
Scenario: A profile-type still assigned to a visitor cannot be deactivated
  Given Visitor profile-type "General" is referenced by at least one UserProfile (ProfileTypeId)
  When the administrator clicks "Deactivate" on "General"
  And confirms by clicking "Deactivate" in the confirm modal
  Then the BFF forwards DELETE /account/api/admin/profile-types/{id}
  And the API returns HTTP 409 with ApiResult.Error.Code="PROFILE_TYPE_IN_USE"
  And a red toast surfaces the bilingual server message
    "The profile type cannot be removed while it is still assigned to one or more accounts."
    / "لا يمكن إزالة نوع الملف الشخصي طالما لا يزال مُسنداً إلى حساب واحد أو أكثر."
    (the page falls back to "This profile type is still assigned ..." only if the server message is empty)
  And the row stays visible with its green "Yes" Active pill
```

### E2E-VPT-013 — Server 500 on list

```gherkin
Scenario: API 500 on /list degrades to an empty grid without crashing
  Given the API is configured to return 500 on /admin/profile-types/list (e.g. DB unavailable)
  When the administrator opens /admin/profile-types/visitor
  Then the grid shows the loading indicator first
  And then renders the SimfEmptyState (the page falls back to GridPage.Of(empty) on a non-success envelope)
  And no unhandled exception or red error overlay appears
  And the console shows no JSException
```

### E2E-VPT-014 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add modal
  Given the administrator is on /admin/profile-types/visitor in English
  When they switch the UI language to Arabic
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "أنواع ملفات الزوار"
  And the Account type column renders the Arabic scope label "زائر (جمهور)"
  And the Active pills read "نعم" / "لا"
  And the nav rail + toolbar mirror to RTL and the pager arrows reverse

  When they click "إضافة نوع ملف"
  Then the Add modal opens in RTL
  And the field labels are Arabic (Name (English) / Name (Arabic) / Page colour / "ظاهر في القوائم (نشط)")
  And the read-only Account type shows "زائر (جمهور)"
  And the form actions appear in reverse order
```

---

## Implementation notes

- **Manual smoke is the canonical run today.** Until a Playwright project is
  adopted, the canonical execution is a Chrome DevTools MCP session: sign in per
  the Background, then walk each scenario and capture screenshots into
  `docs/screenshots/cp-admin-profile-types-visitor-*.png`.
- **Convert to Playwright** later by copying each Gherkin scenario into a
  `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created) plus a
  step-definition class. The steps are deliberately tool-agnostic.
- **API integration tests cover the same surface at a lower layer** in
  [`tests/SIMF.Api.Tests/AdminProfileTypeTests.cs`](../../../tests/SIMF.Api.Tests/AdminProfileTypeTests.cs):
  `Admin_can_create_get_list_and_soft_delete_a_visitor_profile_type` (E2E-VPT-001),
  `Admin_can_update_a_profile_type_without_touching_user_type` (E2E-VPT-001 edit leg),
  `Duplicate_name_within_the_same_user_type_returns_409` +
  `Same_name_across_audience_and_partner_scope_returns_409` (E2E-VPT-011),
  `Cannot_delete_a_profile_type_that_is_still_referenced_by_a_user_profile`
  (E2E-VPT-012), `Create_for_Admin_user_type_returns_400` (the
  `ProfileTypeInvalidUserType` guard behind the Visitor-only create),
  `A_non_admin_caller_is_forbidden_from_every_profile_type_endpoint`
  (E2E-VPT-008 at the API layer), `Get_returns_404_for_an_unknown_id`,
  `IsVisitor_round_trips_through_Create_Get_List`,
  `Update_flipping_IsVisitor_persists_and_audits_the_change`, and
  `Create_visitors_rejects_a_partner_profile_type`. The browser E2E layer adds
  the UI-only coverage (D-120 swatch, modals, empty state, RTL, grid
  filter/sort/pager, the BFF-proxy round-trip and toasts) that the API tests
  cannot reach.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
