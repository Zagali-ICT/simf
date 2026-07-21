# E2E test catalogue — VIP list + bulk-notify (`/admin/vips`)

| | |
|--|--|
| **Page** | [`cp/admin-vips.md`](../../pages/cp/admin-vips.md) |
| **Route** | `/admin/vips` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-07-21 (VIP edit — New VIP + row Edit added) |

> **What this page is.** The VIP desk is a `SimfDataGrid` over the subset of
> `UserProfiles` whose `ProfileType.Name` is in `{VVIP, VIP, Gold}` (the
> `VipProfileTypes.All` discriminator). Its mutating flows are: **bulk-notify**
> (tick recipients → the `CustomToolbar` "Notify selected (N)" send-icon button →
> fill the bilingual title/body modal → "Send"); **New VIP** (the toolbar Add
> button navigates to `/admin/visitors/vip`, the dedicated VVIP/VIP registration
> page); and **row Edit** (the per-row edit icon opens a modal hosting the shared
> `EditAccountForm`, keyed by the account id `AdminVipSummary.UserId`, scope
> `visitors`, with `ShowVipPhoto=true` — change name / email / tier / profile
> photo / ID image / VIP welcome photo). The **New VIP** and **Edit** affordances
> are UX-gated: New VIP shows only for admins holding `Visitors.RegisterOnsite`
> and Edit only for `Visitors.Edit` (the API enforces the same policies); an admin
> with only `Vips.View` sees neither. There is no Details / row-delete slot. The
> grid renders `Multiselect` checkboxes (row + Select-all) and the
> standard `SimfDataGrid` pager (First / Prev / numbered / Next / Last + page-size
> selector + "Showing …" summary) at `Top = 20`. **None of the four columns set
> `Filterable="true"` or `Sortable="true"`, so the grid shows neither a per-column
> filter row nor sortable headers** — `/list` is fetched once per page change with
> no `Sort` / `Filters` ever set. Grounding:
> `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/VipsList.razor`,
> API `src/Backend/SIMF.Api/Endpoints/Admin/VipEndpoints.cs`,
> service `src/Backend/SIMF.Infrastructure/PublicRelations/AdminInvitationService.cs`
> (`ListVipsAsync` / `NotifyVipsAsync`).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-VIP-001 | Golden path — select VIPs → notify → bilingual success toast + audit row | happy | P0 | _to author_ |
| E2E-VIP-002 | Notify button disabled while no row is selected; counter updates on toggle | happy | P1 | _to author_ |
| E2E-VIP-003 | Cancel the notify modal without sending | happy | P2 | _to author_ |
| E2E-VIP-004 | Empty list renders `SimfEmptyState` ("No VIPs match the filter.") | happy | P1 | _to author_ |
| E2E-VIP-005 | Auth gate: signed-in admin lacking `Vips.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-VIP-006 | Auth gate: holds `Vips.View` but lacks `Vips.Notify` → notify 403 | auth | P1 | _to author_ |
| E2E-VIP-007 | Validation: blank title/body → 400 `InvitationInvalid`, modal stays open | error | P1 | _to author_ |
| E2E-VIP-008 | Empty selection guard → 400 `VIP_NOTIFY_EMPTY` | error | P1 | _to author_ |
| E2E-VIP-009 | Over-batch guard (>500 ids) → 400 `VIP_NOTIFY_TOO_LARGE` | error | P2 | _to author_ |
| E2E-VIP-010 | Non-VIP id in selection → silently skipped, reported in `SkippedProfileIds` | error | P1 | _to author_ |
| E2E-VIP-011 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-VIP-012 | RTL render: Arabic toggle mirrors page + notify modal | i18n | P1 | _to author_ |
| E2E-VIP-013 | Excel export (D-356): toolbar Export downloads an .xlsx of the selected rows / whole filtered grid | happy | P1 | _to author_ |
| E2E-VIP-014 | New VIP: toolbar Add navigates to `/admin/visitors/vip` (VVIP/VIP registration) | happy | P0 | _to author_ |
| E2E-VIP-015 | Row Edit: change a VIP's name / email / tier → PUT + success toast + grid reloads | happy | P0 | _to author_ |
| E2E-VIP-016 | Row Edit: replace the profile photo + VIP welcome photo → uploaded on Save | happy | P1 | _to author_ |
| E2E-VIP-017 | Row Edit validation: ID image with no human face → 400 face-gate, modal stays open, core fields already saved | error | P1 | _to author_ |
| E2E-VIP-018 | Auth gate: an admin with `Vips.View` but not `Visitors.Edit`/`RegisterOnsite` sees no Add/Edit affordances | auth | P0 | _to author_ |
| E2E-VIP-019 | RTL render: Arabic toggle mirrors the Edit modal (labels + upload fields) | i18n | P1 | _to author_ |

## Scenarios

### E2E-VIP-001 — Golden path (select → notify → success)

```gherkin
Feature: VIP bulk-notify golden path
  As a PR-desk Administrator
  I want to broadcast one bilingual message to selected VIPs
  So that VVIP / VIP / Gold guests get a coordinated in-app + email notice

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator (Vips.View + Vips.Notify) has signed in via /login + /login/totp
  And they have landed on /admin/vips
  And at least two UserProfiles exist with ProfileType.Name in {VVIP, VIP, Gold}

Scenario: Select two VIPs and send a bilingual broadcast
  Given the grid lists VIP rows with columns: checkbox, Name, Job title, Profile type, Email
  And the toolbar shows the button "Notify selected (0)" in a disabled state
  When the administrator ticks the checkbox on the row "HRH Faisal Al Saud" (Profile type "VVIP")
  And ticks the checkbox on the row "Adm. Turki Al Maliki" (Profile type "VIP")
  Then the toolbar button text changes to "Notify selected (2)" and becomes enabled
  When they click "Notify selected (2)"
  Then the "Notify VIPs" modal opens with four blank fields:
    """
    Title (English) | Title (Arabic) | Body (English) | Body (Arabic)
    """
  When they fill Title (English)="SIMF 2026 — VIP reception"
  And they fill Title (Arabic)="منتدى الرياض البحري 2026 — استقبال كبار الضيوف"
  And they fill Body (English)="You are invited to the VIP reception on day one at 18:00."
  And they fill Body (Arabic)="تتشرّفون بحضور استقبال كبار الضيوف في اليوم الأول الساعة 18:00."
  And they click "Send"
  Then the BFF forwards POST /account/api/admin/vips/notify
  And the API returns HTTP 200 with ApiResult.Data.Dispatched=2
  And the modal closes
  And a green toast reads "Sent to 2 VIPs (N emails enqueued)." (N = recipients with an email on file)
  And the selection clears (the toolbar button reads "Notify selected (0)" and is disabled again)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-vips-golden-before.png` (list with 2 rows ticked, button "Notify selected (2)")
- Screenshot modal: `docs/screenshots/cp-admin-vips-notify-modal.png` (four bilingual fields filled)
- Screenshot after: `docs/screenshots/cp-admin-vips-golden-after.png` (green success toast, selection cleared)
- Console errors: 0 expected
- Network: `POST /account/api/admin/vips/list` returns 200; `POST /account/api/admin/vips/notify` returns 200
- Audit row: `OperationLog` / audit row with `EventType = 'Vip.NotificationSent'`, `Outcome = Success`,
  the actor's id, and `Detail = "dispatched=2; emails=N; skipped=0"`
- Notification: one `NotificationKind.VipBroadcast` in-app row per recipient (Severity = Info)

### E2E-VIP-002 — Notify button disabled until a row is selected

```gherkin
Scenario: The notify button is gated by the selection count
  Given the administrator is on /admin/vips with VIP rows present and nothing selected
  Then the toolbar button reads "Notify selected (0)" and is disabled
  And clicking it does nothing (the notify modal does not open)
  When they tick one row checkbox
  Then the button reads "Notify selected (1)" and is enabled
  When they untick that same row
  Then the button returns to "Notify selected (0)" and is disabled again
```

### E2E-VIP-003 — Cancel the notify modal

```gherkin
Scenario: Closing the modal without sending leaves the selection intact
  Given one VIP row is ticked and the "Notify VIPs" modal is open
  When the administrator types a partial Title (English)="draft"
  And clicks "Cancel"
  Then the modal closes
  And no POST /account/api/admin/vips/notify request fires
  And the row stays selected (the toolbar still reads "Notify selected (1)")
  When they reopen the modal via "Notify selected (1)"
  Then all four fields are blank again (the modal resets its draft on open)
```

### E2E-VIP-004 — Empty list

```gherkin
Scenario: No VIP profiles renders SimfEmptyState
  Given the database has no UserProfile whose ProfileType.Name is in {VVIP, VIP, Gold}
  When the administrator opens /admin/vips
  Then the body renders the SimfEmptyState component
  And the empty-state title reads "No VIPs match the filter." / "لا توجد نتائج مطابقة."
  And the "Notify selected (0)" button is still shown (disabled)
  And no error toast appears
```

### E2E-VIP-005 — Auth gate (missing Vips.View)

```gherkin
Scenario: Signed-in admin without Vips.View is denied the page
  Given a signed-in admin whose role does NOT grant Vips.View (and is not Administrator "*")
  When they navigate to /admin/vips
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/vips/list request fires
  And the "Module.Vips" nav item is hidden for that user (CpNavigation RequiredPermission = Vips.View)
```

### E2E-VIP-006 — Auth gate (has View, lacks Notify)

```gherkin
Scenario: An admin who can view but not notify is blocked at send
  Given a signed-in admin granted Vips.View but NOT Vips.Notify
  When they open /admin/vips
  Then the VIP list loads normally (POST /account/api/admin/vips/list returns 200)
  When they select a row and click "Send" in the notify modal
  Then POST /account/api/admin/vips/notify returns HTTP 403 (policy Vips.Notify)
  And a red toast surfaces the bilingual MessageForCurrentCulture()
  And the modal stays open
```

### E2E-VIP-007 — Validation failure (blank title / body)

```gherkin
Scenario: Empty title or body is rejected with the bilingual length message
  Given the "Notify VIPs" modal is open with one VIP selected
  When the administrator leaves Title (English) blank
  And fills the other three fields
  And clicks "Send"
  Then the API returns HTTP 400 with ApiResult.Error.Code = "InvitationInvalid"
  And the error message reads
    "Message title (EN + AR) must be between 1 and 200 characters each." /
    "يجب أن يكون عنوان الرسالة (إنجليزي + عربي) بين 1 و 200 حرفاً."
  And the modal stays open
  And no notification rows are created
  # Body has the same guard: 1–2000 chars EN + AR, else InvitationInvalid with the body message.
```

### E2E-VIP-008 — Empty selection guard

```gherkin
Scenario: Notifying with an empty id list returns VIP_NOTIFY_EMPTY
  Given the client (or a replayed request) posts /admin/vips/notify with UserProfileIds=[]
  When the request reaches the API
  Then it returns HTTP 400 with ApiResult.Error.Code = "VIP_NOTIFY_EMPTY"
  And the bilingual message reads "Select at least one VIP." / "اختر مستلماً واحداً على الأقل."
  # In the UI this path is normally blocked by the disabled button (E2E-VIP-002);
  # this scenario asserts the server-side guard directly.
```

### E2E-VIP-009 — Over-batch guard

```gherkin
Scenario: More than 500 recipients is rejected
  Given a request posts /admin/vips/notify with 501 distinct UserProfileIds
  When the request reaches the API
  Then it returns HTTP 400 with ApiResult.Error.Code = "VIP_NOTIFY_TOO_LARGE"
  And the bilingual message reads
    "Cannot dispatch to more than 500 VIPs in one batch." /
    "لا يمكن الإرسال إلى أكثر من 500 ضيف في دفعة واحدة."
```

### E2E-VIP-010 — Non-VIP id is skipped, not failed

```gherkin
Scenario: A non-VIP id in the selection is silently skipped and reported
  Given a valid VIP profile "vipProfile" (ProfileType "VVIP")
  And a non-VIP profile "generalProfile" (ProfileType "general")
  When the administrator posts /admin/vips/notify with UserProfileIds=[vipProfile, generalProfile]
  And a valid title + body
  Then the API returns HTTP 200
  And ApiResult.Data.Dispatched = 1 (only the VIP got an in-app row)
  And ApiResult.Data.SkippedProfileIds contains generalProfile.Id
  And the audit Detail records "skipped=1"
```

### E2E-VIP-011 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is configured to return 500 on /admin/vips/list (e.g. DB down)
  When the administrator opens /admin/vips
  Then the loading text "Loading VIPs…" / "جارٍ تحميل القائمة…" shows first
  And then a red toast appears reading
    "The VIP list could not be loaded." / "تعذّر تحميل قائمة كبار الضيوف."
  And no rows render
  And the "Notify selected (0)" button is shown (disabled)
```

### E2E-VIP-012 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the page + notify modal
  Given the administrator is on /admin/vips in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "كبار الضيوف"
  And the column headers read "الاسم", "المسمّى الوظيفي", "نوع الملف", "البريد الإلكتروني"
  And the Name + Profile type columns show the Arabic projection (ArabicName / ProfileTypeNameArabic)
  And the toolbar button reads "إشعار المحدّدين (0)"

  When they tick a row and open the notify modal
  Then the modal title reads "إشعار كبار الضيوف"
  And the field labels read "العنوان (الإنجليزية)", "العنوان (العربية)", "النص (الإنجليزية)", "النص (العربية)"
  And the footer buttons are "إرسال" (Send) and "إلغاء" (Cancel)
```

### E2E-VIP-013 — Excel export (D-356)

```gherkin
Scenario: Export the VIP list to an XLSX workbook (selected rows or the whole filtered grid)
  Given the administrator is on /admin/vips with at least two VIP rows present
  And they hold the Vips.Export permission
  And the grid toolbar shows the "Export" action (no Import — the VIP list is export-only)
  When they click "Export" with no rows selected
  Then the BFF forwards POST /account/api/admin/vips/export
  And the request body is AdminGridExportRequest with an empty Ids list and the current Query (the whole filtered grid)
  And the API returns HTTP 200 with an .xlsx body and a download named "simf-vips-{timestamp}.xlsx"
  And the workbook's "VIPs" sheet header row reads
    """
    EnglishName | ArabicName | JobTitle | ProfileType | ProfileTypeArabic | Email
    """
  And the sheet contains one data row per VIP in the filtered set

  When they instead tick the rows "HRH Faisal Al Saud" and "Adm. Turki Al Maliki" then click "Export"
  Then the request body carries Ids = [those two UserProfileIds] and Query = null
  And the downloaded workbook contains exactly those two rows
  # The export endpoint (AdminGridExportEndpoint<AdminVipSummary>, gated by Vips.Export)
  # caps the export at 5000 rows; an admin who lacks Vips.Export gets HTTP 403 with the
  # bilingual MessageForCurrentCulture() and no file is produced.
```

**Evidence captured:**
- Screenshot: `docs/screenshots/cp-admin-vips-export.png` (toolbar Export clicked, two rows ticked)
- Network: `POST /account/api/admin/vips/export` returns 200 with `Content-Type` `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- File: saved workbook opened, "VIPs" sheet header row matches the six columns above
- Console errors: 0 expected

### E2E-VIP-014 — New VIP navigates to the registration page

```gherkin
Scenario: The toolbar Add button opens the VVIP/VIP registration page
  Given an Administrator holding Visitors.RegisterOnsite has signed in
  And they have landed on /admin/vips
  Then the grid toolbar shows an "New VIP" (plus-icon) button
  When they click "New VIP"
  Then the browser navigates to /admin/visitors/vip
  And the VVIP/VIP registration form renders (picker restricted to VVIP/VIP + Mawj welcome fields + VIP photo)
```

### E2E-VIP-015 — Row Edit changes name / email / tier

```gherkin
Scenario: Edit a VIP's core account fields
  Given an Administrator holding Visitors.Edit is on /admin/vips
  And a VIP row "Adm. Turki Al Maliki" (Profile type "VIP") is listed
  When they click the row Edit (pencil) icon on that row
  Then an "Edit VIP" modal opens hosting the shared account edit form
  And it is pre-filled with the VIP's email, display name, and current tier
  When they change the Display name to "Adm. Turki Al Maliki (Chief of Staff)"
  And they change the Profile type to "VVIP"
  And they click "Save"
  Then the BFF forwards PUT /account/api/admin/visitors/{UserId} with the new display name + ProfileTypeId
  And the API returns HTTP 200
  And the modal closes
  And a green toast reads "VIP updated." / "تم تحديث بيانات الشخصية البارزة."
  And the grid reloads and the row now shows Profile type "VVIP"
```

### E2E-VIP-016 — Row Edit replaces the photos

```gherkin
Scenario: Change a VIP's profile photo and welcome photo
  Given the "Edit VIP" modal is open for a VIP (scope visitors, ShowVipPhoto=true)
  And the "Photo & ID" section shows Profile photo, ID document, and VIP welcome photo inputs
  When they pick a new PNG (< 2 MB) for "Profile photo"
  And they pick a new JPEG (< 2 MB) for "VIP welcome photo"
  And they click "Save"
  Then PUT /account/api/admin/visitors/{UserId} fires first (core fields)
  And then POST /account/api/admin/visitors/{UserId}/avatar (multipart "file") returns 200
  And then POST /account/api/admin/visitors/{UserId}/vip-photo (multipart "file") returns 200
  And the modal closes with the "VIP updated." toast
  # Unpicked image inputs are not uploaded — the current images are kept.
```

### E2E-VIP-017 — Row Edit ID face-gate validation

```gherkin
Scenario: An ID image with no human face is rejected, core fields already saved
  Given the "Edit VIP" modal is open for a VIP
  When they change the Display name to a new value
  And they pick a landscape photo (no face) for "ID document"
  And they click "Save"
  Then PUT /account/api/admin/visitors/{UserId} returns 200 (the display name is saved)
  And POST /account/api/admin/visitors/{UserId}/id-document returns HTTP 400 with Error.Code = "VISITOR_ID_IMAGE_NO_FACE"
  And the modal STAYS OPEN showing the bilingual message
    "No human face was detected in the photo — retake a clear photo of the face." /
    "لم يتم التعرف على وجه بشري في الصورة — أعد التقاط صورة واضحة للوجه."
  When they clear the ID picker and click "Save" again
  Then no id-document upload fires and the modal closes with the success toast
```

### E2E-VIP-018 — Add/Edit affordances are permission-gated

```gherkin
Scenario: A Vips.View-only admin sees no New VIP or Edit controls
  Given a signed-in admin granted Vips.View but NOT Visitors.Edit and NOT Visitors.RegisterOnsite
  When they open /admin/vips
  Then the VIP list loads normally (POST /account/api/admin/vips/list returns 200)
  And the toolbar does NOT show the "New VIP" button
  And the rows do NOT show a per-row Edit (pencil) icon
  And the bulk-notify flow is unaffected (still available if they hold Vips.Notify)
  # SimfDataGrid renders Add/Edit only when the callback HasDelegate; the page wires
  # them only when Authz.AuthorizeAsync succeeds for the respective policy.
```

### E2E-VIP-019 — RTL render of the Edit modal

```gherkin
Scenario: Arabic toggle mirrors the Edit VIP modal
  Given the administrator is on /admin/vips in العربية and opens a row's Edit modal
  Then the modal title reads "تعديل بيانات الشخصية البارزة"
  And the field labels read "البريد الإلكتروني", "الاسم الظاهر", "نوع الملف"
  And the "الصورة والهوية" section shows "الصورة الشخصية", "صورة الهوية", "صورة ترحيب كبار الشخصيات"
  And the modal renders under <html dir="rtl" lang="ar"> with no horizontal overflow
```

---

## Implementation notes

- **Derived list + notify + New VIP + row Edit.** The VIP membership is derived from
  `ProfileType.Name ∈ {VVIP, VIP, Gold}` (server-side `VipProfileTypes.All`), so there
  is no create/delete of the *membership* here — a visitor becomes a VIP by having a
  VIP tier (set on registration via `/admin/visitors/vip`, or by the row Edit's tier
  dropdown). The mutating flows are the bulk broadcast, **New VIP** (navigate to the
  registration page), and **row Edit** (the shared `EditAccountForm` reused with
  `Scope="visitors"`, `IsVisitorScope=true`, `ShowVipPhoto=true`). Edit reuses the
  existing account-id-keyed admin endpoints — `PUT /admin/visitors/{id}` (email + name
  + tier), `POST /admin/visitors/{id}/avatar`, `/id-document`, `/vip-photo` — all gated
  by `Visitors.Edit`; no new permission or endpoint was added.
- **Two permissions back the page.** `Vips.View` (page + `/admin/vips/list`) and
  `Vips.Notify` (`/admin/vips/notify`); both are `PublicRelations`/`AdminOnly` baseline.
  The CP page carries `[RequirePermission(PermissionCatalog.Vips.View)]`; the API
  endpoints carry `Policies(PermissionCatalog.PolicyFor(...), RequireApprovedAccount)`.
  The notify endpoint additionally requires the `"auth"` rate-limiter.
- **API integration tests (lower layer).** `tests/SIMF.Api.Tests/AdminInvitationsTests.cs`
  covers the same surface without a browser:
  - `Vip_list_returns_only_VVIP_VIP_Gold_profiles` (matrix maps to E2E-VIP-001/004 grounding),
  - `NotifyVips_dispatches_and_records_skips_for_non_VIP_ids` (E2E-VIP-010),
  - `NotifyVips_with_empty_ids_is_400_VIP_NOTIFY_EMPTY` (E2E-VIP-008).
  When the matching E2E scenario is automated you can usually retire the duplicate
  `Api.Tests` case — but keep both during the transition.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin scenario into
  a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created) plus a
  step-definition class. The Gherkin here is already runner-agnostic.

---

_Last reviewed:_ 2026-07-21 by Claude (VIP edit — added New VIP nav + row Edit via the shared EditAccountForm with photo/ID/VIP-photo upload; E2E-VIP-014..019).
_Prior:_ 2026-06-10 (D-356 Phase 5 — Excel + toggle; added E2E-VIP-013 export; D-256/D-257 grid affordances reconciled).
