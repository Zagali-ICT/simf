# E2E test catalogue — Other profile types (`/admin/profile-types/other`)

| | |
|--|--|
| **Page** | [`cp/admin-profile-types-other.md`](../../pages/cp/admin-profile-types-other.md) |
| **Route** | `/admin/profile-types/other` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **D-186 context (read before driving the page).** The page file is named
> `OtherProfileTypesList.razor`, but the server-side `UserType.Other` was folded
> into `Visitor`; the audience-vs-partner split now lives on
> `ProfileType.IsVisitor`. This page is the **partner / staff pool** — it loads
> the grid with the pinned filters `userType=Visitor` **and** `isVisitor=false`,
> and its Add modal hosts `ProfileTypeForm` with `IsPartnerForm="true"`, which
> POSTs `UserType="Visitor"`, `IsVisitor=false`. Consequently the
> **Mobile-app role** column + picker (None / Staff / Moderator) are present on
> THIS page and absent on the Visitor sibling. Treat "Other" throughout as
> "partner / staff (Sponsor, Exhibitor, Media, …)" — the Account-type column /
> Details field renders the localised string `Admin.ProfileTypes.Scope.Partner`
> ("Partner / staff (Sponsor, Exhibitor, Media, …)" / "شريك / فريق (راعي، عارض،
> إعلام، …)").

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-OPT-001 | Full CRUD round-trip — Add → Edit → Details → Deactivate | happy | P0 | _to author_ |
| E2E-OPT-002 | Add partner type with Mobile-app role = Staff | happy | P0 | _to author_ |
| E2E-OPT-003 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-OPT-004 | Filter / search by Name narrows the grid | happy | P2 | _to author_ |
| E2E-OPT-005 | Sort by Name (ascending / descending) | happy | P2 | _to author_ |
| E2E-OPT-006 | Pager — page size + next / prev / first / last | happy | P2 | _to author_ |
| E2E-OPT-007 | Details modal opens read-only and closes | happy | P2 | _to author_ |
| E2E-OPT-008 | Add modal Cancel discards (no POST) | happy | P2 | _to author_ |
| E2E-OPT-009 | Auth gate — user lacking `ProfileTypes.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-OPT-010 | Validation — blank Name → in-modal `SimfAlert`, no POST | error | P1 | _to author_ |
| E2E-OPT-011 | Validation — blank Arabic name / blank Page colour | error | P1 | _to author_ |
| E2E-OPT-012 | Conflict — duplicate Name → 409 `ProfileTypeNameTaken` | error | P1 | _to author_ |
| E2E-OPT-013 | Deactivate in-use type → 409 `ProfileTypeInUse` (bilingual) | error | P0 | _to author_ |
| E2E-OPT-014 | Server 500 on `/list` → empty grid, no crash | resilience | P2 | _to author_ |
| E2E-OPT-015 | RTL / Arabic render mirrors page + Add modal | i18n | P1 | _to author_ |
| E2E-OPT-016 | "Show in the app sign-up picker" toggle hides the type from the app (D-725) | happy | P1 | _to author_ |
| E2E-OPT-017 | "Show in Meet People" toggle hides the whole partner type from the networking directory + recommender (D-760) | happy | P1 | authored ✓ (API twins) |

## Scenarios

### E2E-OPT-001 — Full CRUD round-trip

```gherkin
Feature: Other (partner / staff) profile types CRUD round-trip
  As an Administrator
  I want to manage the partner / staff profile-type pool
  So that the Others walk-in wizard and the mobile-app role mapping stay accurate

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator with the "ProfileTypes.View/Create/Edit/Delete" permissions
    has signed in via /login + /login/totp (TOTP from the Get-Totp helper)
  And they have navigated to /admin/profile-types/other
  And the grid has finished loading (the loading indicator is gone)

Scenario: Create, edit, view, then deactivate one partner profile type
  Given the grid currently shows {N} rows
  And every visible row's "Account type" column reads "Partner / staff (Sponsor, Exhibitor, Media, …)"
  When the administrator clicks "Add profile type"
  Then the Add modal opens titled "Add profile type"
  And it shows the read-only "Account type" value "Partner / staff (Sponsor, Exhibitor, Media, …)"
  And it shows the fields: Name (English), Name (Arabic), Page colour (text + colour swatch),
    Mobile-app role (select), the "Visible in pickers (active)" checkbox (ticked), and
    the "Show in the app sign-up picker" checkbox (ticked by default, D-725)
  When they fill Name (English)="Sponsor staff"
  And they fill Name (Arabic)="فريق الرعاة"
  And they set Page colour to "#FFD700" via the paired text input (the swatch mirrors it)
  And they leave Mobile-app role on "None — no operational authority"
  And they click "Create profile type"
  Then the POST /account/api/admin/profile-types body carries
    UserType="Visitor", IsVisitor=false, Name="Sponsor staff", PageColor="#FFD700"
  And the API returns HTTP 200
  And the modal closes
  And a green toast reads "Saved \"Sponsor staff\"."
  And the grid reloads and shows {N + 1} rows
  And a row exists with Name="Sponsor staff", Page colour="#FFD700",
    Mobile-app role="None — no operational authority", and the green "Yes" Active pill

  When the administrator clicks the "Edit" action on that row
  Then the Edit modal opens titled "Edit profile type — Sponsor staff" with the values pre-filled
  And the "Account type" line reads "Partner / staff (Sponsor, Exhibitor, Media, …)" with the read-only hint
  When they change Page colour to "#1E90FF"
  And they change Mobile-app role to "Staff — gate operations"
  And they click "Save changes"
  Then the PUT /account/api/admin/profile-types/{id} returns HTTP 200
  And the modal closes
  And a green toast reads "Saved \"Sponsor staff\"."
  And the row's Page colour column reads "#1E90FF" and Mobile-app role reads "Staff — gate operations"

  When the administrator clicks the "Details" action on that row
  Then a read-only modal opens titled "Profile type details — Sponsor staff"
  And the description list shows Account type, Name, Name (Arabic), Page colour,
    Mobile-app role, and Active = "Yes"
  When they click "Close"
  Then the modal closes with no network call

  When the administrator clicks the "Deactivate" action on that row
  Then a confirm modal titled "Deactivate profile type" asks
    "Deactivate the profile type \"Sponsor staff\"? Existing users keep their assignment; …"
  When they click "Deactivate"
  Then the DELETE /account/api/admin/profile-types/{id} returns HTTP 200
  And a green toast reads "Deactivated \"Sponsor staff\"."
  And the grid reloads and the "Sponsor staff" row no longer appears
    (the default list filter still shows only partner types; deactivated rows fall out of the picker)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-profile-types-other-crud-before.png`
- Screenshot after each step: `docs/screenshots/cp-admin-profile-types-other-{add-modal,edit-modal,details-modal,deactivate-confirm}.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/profile-types/*` call returns 200
- Audit rows: `OperationLog` rows with `Event = 'ProfileType.Created'`, `ProfileType.Updated`,
  and `ProfileType.Deactivated`, each carrying the signed-in administrator's id. The
  `ProfileType.Created` Detail records `userType=Visitor`; the Update Detail records
  `isVisitorChanged=false` (the round-trip never flips the audience/partner flag).

### E2E-OPT-002 — Add partner type with Mobile-app role = Staff

```gherkin
Scenario: The Mobile-app role picker is present and persists Staff
  Given the Add modal is open on /admin/profile-types/other
  Then the "Mobile-app role" select is visible (this picker is partner-only;
    it is absent on the Visitor sibling page)
  And its options are exactly:
    "None — no operational authority", "Staff — gate operations",
    "Moderator — content & user authority"
  When the administrator fills Name (English)="Gate operator"
  And fills Name (Arabic)="مشغّل البوابة"
  And keeps Page colour "#244A77"
  And selects Mobile-app role = "Staff — gate operations"
  And clicks "Create profile type"
  Then the POST body carries MobileAppRole="Staff", UserType="Visitor", IsVisitor=false
  And the API returns HTTP 200
  And the new row's Mobile-app role column reads "Staff — gate operations"
```

### E2E-OPT-003 — Empty list

```gherkin
Scenario: No partner profile types renders SimfEmptyState
  Given the database has no ProfileType rows with IsVisitor=false
  When the administrator opens /admin/profile-types/other
  Then the grid body renders the SimfEmptyState component
  And the empty state shows the bilingual copy "No profile types yet." / "لا توجد أنواع ملفات بعد."
  And the "Add profile type" toolbar action is still available
  And no error toast appears
```

### E2E-OPT-004 — Filter / search by Name

```gherkin
Scenario: The filter box narrows the grid server-side without losing the partner filter
  Given the grid shows several partner profile types including "Sponsor staff" and "Press"
  When the administrator types "Press" into the filter box for the Name column
  Then a POST /account/api/admin/profile-types/list fires with Search/name="Press"
    AND the pinned filters userType="Visitor" + isVisitor="false" still set
  And the grid shows only the "Press" row
  When they clear the filter
  Then the grid reloads to the full partner list
```

### E2E-OPT-005 — Sort by Name

```gherkin
Scenario: Clicking the Name header toggles ascending / descending sort
  Given the grid shows at least three partner profile types
  When the administrator clicks the "Name" column header
  Then a POST /account/api/admin/profile-types/list fires with Sort="name", SortDescending=false
  And the rows render in ascending name order
  When they click the "Name" header again
  Then the list reloads with SortDescending=true
  And the rows render in descending name order
  And the pinned userType/isVisitor filters survive both reloads
```

### E2E-OPT-006 — Pager

```gherkin
Scenario: Page size and the first / prev / next / last controls page the grid
  Given the database has more than one page of partner profile types
  When the administrator opens /admin/profile-types/other
  Then the pager summary reads "{from}–{to} of {total}" and "Page 1 of {pages}"
  When they click "Next"
  Then a /list request fires with Skip advanced by the page size and the grid shows page 2
  When they click "Last"
  Then the grid jumps to the final page
  When they change the page-size selector
  Then a /list request fires with the new Top and the grid re-pages from the start
```

### E2E-OPT-007 — Details modal read-only

```gherkin
Scenario: Details opens a read-only description list
  Given the grid shows the partner profile type "Sponsor staff"
  When the administrator clicks the "Details" action on that row
  Then a read-only modal titled "Profile type details — Sponsor staff" opens
  And it lists Account type = "Partner / staff (Sponsor, Exhibitor, Media, …)",
    Name, Name (Arabic), Page colour, Mobile-app role, and Active = "Yes"
  And no editable input is present and no save action exists
  When they click "Close"
  Then the modal closes and no network request fired
```

### E2E-OPT-008 — Add modal Cancel

```gherkin
Scenario: Cancelling the Add modal discards input and fires no POST
  Given the Add modal is open
  When the administrator fills Name (English)="Discard me"
  And clicks "Cancel"
  Then the modal closes
  And no POST /account/api/admin/profile-types request fires
  And the grid row count is unchanged
```

### E2E-OPT-009 — Auth gate

```gherkin
Scenario: A signed-in admin lacking ProfileTypes.View is denied
  Given a signed-in Control-Panel user whose role does NOT grant
    the permission code "ProfileTypes.View" (and is not the Administrator wildcard "*")
  When they navigate to /admin/profile-types/other
  Then the RequirePermission(PermissionCatalog.ProfileTypes.View) attribute denies them
  And they land on /not-permitted with HTTP 200
  And no /account/api/admin/profile-types/list request fires
```

### E2E-OPT-010 — Validation: blank Name

```gherkin
Scenario: Empty English name shows an in-modal SimfAlert before any POST
  Given the Add modal is open
  When the administrator leaves Name (English) blank
  And fills Name (Arabic)="فريق الرعاة" and Page colour "#244A77"
  And clicks "Create profile type"
  Then a SimfAlert error appears at the top of the modal
  And it reads "Name must be 1–128 characters." / "يجب أن يتراوح الاسم بين 1 و128 حرفًا."
  And the modal stays open
  And no POST /account/api/admin/profile-types request fires
    (the form validates Name client-side before the network call)
```

### E2E-OPT-011 — Validation: blank Arabic name / blank Page colour

```gherkin
Scenario: Blank Arabic name and blank Page colour each block submit with their own SimfAlert
  Given the Add modal is open with Name (English)="Sponsor staff"
  When the administrator leaves Name (Arabic) blank and clicks "Create profile type"
  Then a SimfAlert reads
    "Arabic name must be 1–128 characters." / "يجب أن يتراوح الاسم العربي بين 1 و128 حرفًا."
  And no POST fires
  When they fill Name (Arabic)="فريق الرعاة" but clear the Page colour text input
  And click "Create profile type"
  Then a SimfAlert reads
    "Page colour must be 1–32 characters." / "يجب أن يتراوح لون الصفحة بين 1 و32 حرفًا."
  And no POST fires
```

### E2E-OPT-012 — Conflict: duplicate Name

```gherkin
Scenario: Duplicate Name (same Visitor scope) returns 409 ProfileTypeNameTaken
  Given a profile type named "Sponsor staff" already exists in the Visitor scope
  When the administrator opens the Add modal
  And fills Name (English)="Sponsor staff", Name (Arabic)="فريق الرعاة", Page colour "#244A77"
  And clicks "Create profile type"
  Then the BFF forwards POST /admin/profile-types
  And the API returns HTTP 409 with ApiResult.Error.Code = "ProfileTypeNameTaken"
  And the modal stays open
  And the in-modal SimfAlert surfaces the bilingual server message verbatim
    ("A profile type named 'Sponsor staff' already exists for Visitor." /
     "يوجد نوع ملف شخصي بالاسم 'Sponsor staff' لـ Visitor بالفعل.")
  And no row is created
```

### E2E-OPT-013 — Deactivate in-use type → 409

```gherkin
Scenario: A partner profile type assigned to a user cannot be deactivated
  Given the partner profile type "Sponsor staff" is referenced by at least one UserProfile
  When the administrator clicks "Deactivate" on that row and confirms in the modal
  Then the DELETE /account/api/admin/profile-types/{id} request returns HTTP 409
  And ApiResult.Error.Code = "ProfileTypeInUse"
  And a red toast surfaces the bilingual server message
    ("The profile type cannot be removed while it is still assigned to one or more accounts." /
     "لا يمكن إزالة نوع الملف الشخصي طالما لا يزال مُسنداً إلى حساب واحد أو أكثر.")
  And the row stays Active in the grid
```

### E2E-OPT-014 — Server 500 on list

```gherkin
Scenario: API 500 on /list leaves an empty grid without crashing the page
  Given the API is configured to return 500 on /admin/profile-types/list (e.g. DB down)
  When the administrator opens /admin/profile-types/other
  Then the grid shows the loading indicator, then resolves to an empty page
    (LoadAsync falls back to GridPage.Of(empty) on a non-success envelope)
  And the SimfEmptyState renders with no rows
  And the page does not throw and the shell stays usable
```

### E2E-OPT-015 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add modal
  Given the administrator is on /admin/profile-types/other in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "أنواع الملفات الأخرى"
  And the grid headers render in Arabic
    (الاسم / دور تطبيق الجوّال / نوع الحساب)
  And the nav rail and toolbar mirror right-to-left
  When they click "إضافة نوع ملف"
  Then the Add modal opens in RTL
  And the Account-type line reads "شريك / فريق (راعي، عارض، إعلام، …)"
  And the field labels are Arabic (الاسم (إنجليزي) / الاسم (عربي) / لون الصفحة / دور تطبيق الجوّال)
  And the form actions ("إنشاء نوع الملف" / "إلغاء") appear in reverse order
```

### E2E-OPT-016 — "Show in the app sign-up picker" toggle (D-725)

```gherkin
Scenario: Un-ticking "Show in the app sign-up picker" hides the type from mobile registration
  Given the Add modal is open on /admin/profile-types/other
  Then the "Show in the app sign-up picker" checkbox is present and ticked by default
  And a helper reads "When off, this type is admin-assigned only and never appears when a user
    registers in the mobile app (e.g. Staff, Moderator)."
  When the administrator fills Name (English)="Ops lead", Name (Arabic)="قائد العمليات",
    Page colour "#6366F1", Mobile-app role = "Moderator — content & user authority"
  And un-ticks "Show in the app sign-up picker"
  And clicks "Create profile type"
  Then the POST /account/api/admin/profile-types body carries IsAppRegisterable=false
  And the API returns HTTP 200
  When a mobile client (or a direct GET) calls /api/v1/app/account/profile-types?isVisitor=false
  Then the "Ops lead" row is ABSENT from the picker (IsAppRegisterable=false is filtered out)
  When the administrator re-opens the row's Edit modal, re-ticks the box, and saves
  Then the PUT body carries IsAppRegisterable=true and the row re-appears in the app picker

Scenario: The seeded Staff / Moderator types are hidden out of the box
  Given a freshly seeded / migrated database
  When a mobile client calls /api/v1/app/account/profile-types (any scope)
  Then neither the seeded "Staff" nor the seeded "Moderator" type appears
    (the D-725 migration data step + the IdentitySeeder derive IsAppRegisterable=false
     for MobileAppRole IN (Staff, Moderator))
  And their rows still appear in THIS CP grid (CP admin listings show every type)
```

### E2E-OPT-017 — "Show in Meet People (same interests)" toggle (D-760)

```gherkin
Scenario: Un-ticking "Show in Meet People" hides the whole partner type from networking
  Given the Add modal is open on /admin/profile-types/other
  Then the "Show in Meet People (same interests)" checkbox is present and ticked by default
  And a helper reads "When off, no account of this partner type appears in the app's Meet
    People directory or the \"people like you\" suggestions, even if the person opted in."
  And this checkbox is ABSENT on the Visitor sibling page (/admin/profile-types/visitor)
  When the administrator fills Name (English)="Press", Name (Arabic)="إعلام", Page colour "#EF4444"
  And un-ticks "Show in Meet People (same interests)"
  And clicks "Create profile type"
  Then the POST /account/api/admin/profile-types body carries ShowInPartnerDirectory=false
  And the API returns HTTP 200
  Given an Approved "Press"-type account exists with ShowInMeetLikeYou=true (opted in)
  When any approved app user opens Meet People (GET /app/networking/partner-directory)
  Then that account is ABSENT from the directory — the type master switch overrides the opt-in
  And it is likewise absent from the "people like you" recommender
  When the administrator re-opens the row's Edit modal, re-ticks the box, and saves
  Then the PUT body carries ShowInPartnerDirectory=true and the account re-appears in Meet People
```

---

## Implementation notes

- **Manual smoke is the canonical run today.** Until Playwright is adopted, drive
  these scenarios through a Chrome DevTools MCP session: sign in per the Auth
  setup, walk each row, capture screenshots into
  `docs/screenshots/cp-admin-profile-types-other-{scenario}.png`. Keep the
  Gherkin tool-agnostic so it ports to a `.feature` file under
  `tests/SIMF.E2E.Tests/` (project to be created) without rewrites.
- **D-186 trap for the driver.** The page is the *partner* pool — it always sends
  `UserType="Visitor"`, `IsVisitor=false`, and shows the Mobile-app role picker.
  Do not assert any `userType=Other` wire value; the API rejects creating
  anything but the Visitor scope (`ProfileTypeInvalidUserType`, 400).
- **API integration tests cover the same surface at a lower layer** in
  [`tests/SIMF.Api.Tests/AdminProfileTypeTests.cs`](../../../tests/SIMF.Api.Tests/AdminProfileTypeTests.cs):
  - `Admin_can_create_get_list_and_soft_delete_a_visitor_profile_type` (CRUD round-trip),
  - `Admin_can_update_a_profile_type_without_touching_user_type` (Edit; UserType immutable),
  - `Cannot_delete_a_profile_type_that_is_still_referenced_by_a_user_profile` (→ `ProfileTypeInUse`, mirrors E2E-OPT-013),
  - `A_non_admin_caller_is_forbidden_from_every_profile_type_endpoint` (auth, lower-layer twin of E2E-OPT-009),
  - `IsVisitor_round_trips_through_Create_Get_List` (Theory) + `Update_flipping_IsVisitor_persists_and_audits_the_change` (the audience/partner flag + audit Detail),
  - `IsAppRegisterable_round_trips_through_Create_Get_and_Update` (D-725 — the app-picker visibility flag persists + flips, backing E2E-OPT-016),
  - `ShowInPartnerDirectory_round_trips_through_Create_Get_and_Update` (D-760 — the Meet-People visibility flag persists + flips, backing E2E-OPT-017),
  - `Create_others_rejects_an_audience_profile_type` (the partner-side guard backing this page).
  The D-760 Meet-People exclusion (a hidden partner type drops all its accounts) is covered by
  [`tests/SIMF.Api.Tests/PartnerDirectoryServiceTests.cs`](../../../tests/SIMF.Api.Tests/PartnerDirectoryServiceTests.cs)
  `Other_type_hidden_from_meet_people_never_appears_even_when_opted_in`.
  The app-side exclusion is covered by
  [`tests/SIMF.Api.Tests/ProfileTypePickerTests.cs`](../../../tests/SIMF.Api.Tests/ProfileTypePickerTests.cs)
  `Non_app_registerable_types_are_excluded` (a non-registerable partner row never reaches the picker).
  The Mobile-app role rules also have lower-layer coverage in
  [`tests/SIMF.Api.Tests/MobileAppRoleTests.cs`](../../../tests/SIMF.Api.Tests/MobileAppRoleTests.cs)
  and the CP form is unit-tested in
  [`tests/SIMF.ControlPanel.Tests/ProfileTypeFormTests.cs`](../../../tests/SIMF.ControlPanel.Tests/ProfileTypeFormTests.cs).

---

_Last reviewed:_ 2026-07-23 by Claude (added E2E-OPT-017 for the D-760 "Show in
Meet People" per-type master switch — hides a whole partner type from the
networking directory + recommender). Prior: 2026-06-02 (E2E catalogue rebuild).
