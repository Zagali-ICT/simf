# E2E test catalogue — Sponsors CRUD (`/admin/sponsors`)

| | |
|--|--|
| **Page** | [`cp/admin-sponsors.md`](../../pages/cp/admin-sponsors.md) _(reference doc not yet authored — grounded directly in `SponsorsList.razor`)_ |
| **Route** | `/admin/sponsors` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Page permission:** the page is gated by `@attribute [RequirePermission(PermissionCatalog.Sponsors.View)]`.
> The action buttons (Add / Edit / Delete) are **not** wrapped in `<AuthorizedAction>` on
> this page, so any admin who can open it sees all three buttons — but the BFF/API enforce
> the finer-grained `Sponsors.Create` / `Sponsors.Edit` / `Sponsors.Delete` policies on the
> underlying endpoints (`POST /admin/sponsors`, `PUT /admin/sponsors/{id}`, `DELETE
> /admin/sponsors/{id}`). E2E-SPN-009 covers the per-action API gate.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-SPN-001 | Full CRUD round-trip — Add → Edit → Delete (deactivate) | happy | P0 | _to author_ |
| E2E-SPN-002 | Add a sponsor (create-only, all fields incl. logo/url/order) | happy | P1 | _to author_ |
| E2E-SPN-003 | Edit a sponsor (change tier + toggle Active off) | happy | P1 | _to author_ |
| E2E-SPN-004 | Delete (soft-deactivate) with the native confirm dialog | happy | P1 | _to author_ |
| E2E-SPN-005 | Cancel delete from the confirm dialog (no-op) | happy | P2 | _to author_ |
| E2E-SPN-006 | Tier dropdown carries all four tiers + grid ordering | happy | P2 | _to author_ |
| E2E-SPN-007 | Empty list renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-SPN-008 | Auth gate (page) — admin lacking `Sponsors.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-SPN-009 | Auth gate (action) — admin with View but not Create → POST 403 | auth | P1 | _to author_ |
| E2E-SPN-010 | Validation — blank name(s) → client-side bilingual toast, no POST | error | P1 | _to author_ |
| E2E-SPN-011 | Validation — server length/tier/order rejection (400 `SponsorInvalid`) | error | P1 | _to author_ |
| E2E-SPN-012 | Conflict — duplicate active NameAr in same tier → 409 `SponsorDuplicate` | error | P1 | _to author_ |
| E2E-SPN-013 | Server 500 on `/list` → bilingual fallback toast | resilience | P2 | _to author_ |
| E2E-SPN-014 | Modal Cancel discards edits | happy | P2 | _to author_ |
| E2E-SPN-015 | RTL/Arabic render — page + modal mirror | i18n | P1 | _to author_ |

## Scenarios

### E2E-SPN-001 — Full CRUD round-trip

```gherkin
Feature: Sponsors CRUD round-trip
  As an Administrator
  I want to manage the public sponsors list (logos grouped by tier)
  So that the website sponsors screen (Mockup page 23) stays accurate

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator with the Sponsors.View/Create/Edit/Delete permissions has
      signed in via /login + /login/totp (TOTP from the Get-Totp helper)
  And they have landed on /admin/sponsors
  And the grid is rendered (or the SimfEmptyState if there are no sponsors)

Scenario: Create, edit, then delete one sponsor
  Given the grid currently shows {N} rows
  When the administrator clicks "Add sponsor"
  Then the "Add sponsor" modal opens with fields:
       Name (English), Name (Arabic), Tier (select), Link, Logo path,
       Display order (number), and an "Active" checkbox (ticked)
  When they fill Name (English) = "Lockheed Martin"
  And they fill Name (Arabic) = "لوكهيد مارتن"
  And they select Tier = "Platinum"
  And they fill Link = "https://www.lockheedmartin.com"
  And they fill Logo path = "sponsors/lockheed.png"
  And they fill Display order = "10"
  And they click "Save"
  Then the BFF fires POST /account/api/admin/sponsors and the API returns HTTP 200
  And the modal closes
  And a green toast reads "Sponsor saved." / "تم حفظ الراعي."
  And a row exists with Name (English) = "Lockheed Martin",
      Tier = "Platinum", Logo path = "sponsors/lockheed.png",
      Link = "https://www.lockheedmartin.com", Display order = 10, Active = "✓"
  And the grid summary reads "Showing 1–{N+1} of {N+1}"

  When the administrator clicks "Edit" on that row
  Then the "Edit sponsor" modal opens with the row's values pre-filled
  And the "Active" checkbox is visible and ticked
  When they change Display order to "20"
  And they click "Save"
  Then the BFF fires PUT /account/api/admin/sponsors/{id} and the API returns HTTP 200
  And the modal closes
  And a green toast reads "Sponsor saved." / "تم حفظ الراعي."
  And the row's Display order column now reads "20"

  When the administrator clicks "Delete" on that row
  Then a native browser confirm dialog appears reading
       "Delete this sponsor? It will be removed from the public list immediately."
       / "هل تريد حذف هذا الراعي؟ سيُزال من القائمة العامة فورًا."
  When they accept the confirm dialog
  Then the BFF fires DELETE /account/api/admin/sponsors/{id} and the API returns HTTP 200
  And a green toast reads "Sponsor deleted." / "تم حذف الراعي."
  And the grid reloads
  And the "Lockheed Martin" row no longer appears (it was soft-deactivated; the
      list query orders by Tier, DisplayOrder, NameAr and re-renders without it
      only when an isActive filter excludes inactive rows — without that filter the
      row still shows with Active = "—")
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-sponsors-001-before.png`
- Screenshot after create: `docs/screenshots/cp-admin-sponsors-001-after-create.png`
- Screenshot after edit: `docs/screenshots/cp-admin-sponsors-001-after-edit.png`
- Screenshot after delete: `docs/screenshots/cp-admin-sponsors-001-after-delete.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/sponsors/*` call returns 200
- Audit rows: `OperationLog` rows with `Event = 'Sponsor.Created'`, `'Sponsor.Updated'`,
  and `'Sponsor.Deactivated'`, each carrying the actor's id (the
  `superadmin@zagali-ict.com` user id)

### E2E-SPN-002 — Add a sponsor (create-only)

```gherkin
Scenario: Create a sponsor with every field populated
  Given the administrator is on /admin/sponsors
  When they click "Add sponsor"
  And they fill Name (English) = "Saab"
  And they fill Name (Arabic) = "ساب"
  And they select Tier = "Gold"
  And they fill Link = "https://www.saab.com"
  And they fill Logo path = "sponsors/saab.svg"
  And they fill Display order = "5"
  And they leave the "Active" checkbox ticked
  And they click "Save"
  Then POST /account/api/admin/sponsors returns HTTP 200
  And the modal closes
  And a green toast reads "Sponsor saved." / "تم حفظ الراعي."
  And a new grid row shows NameEn="Saab", Tier="Gold", Display order=5, Active="✓"
```

### E2E-SPN-003 — Edit a sponsor (change tier + deactivate)

```gherkin
Scenario: Re-tier and deactivate via the Edit modal
  Given a sponsor "Saab" exists in tier "Gold" and is Active
  When the administrator clicks "Edit" on the "Saab" row
  Then the modal is titled "Edit sponsor" and the fields are pre-filled
  When they change Tier = "Silver"
  And they untick the "Active" checkbox
  And they click "Save"
  Then PUT /account/api/admin/sponsors/{id} returns HTTP 200
  And a green toast reads "Sponsor saved." / "تم حفظ الراعي."
  And the row now shows Tier="Silver" and Active="—"
  And an OperationLog row with Event='Sponsor.Updated' records the actor id
```

### E2E-SPN-004 — Delete (soft-deactivate) confirmed

```gherkin
Scenario: Delete a sponsor and accept the confirm dialog
  Given a sponsor "Saab" exists and is Active
  When the administrator clicks "Delete" on the "Saab" row
  Then a native confirm dialog appears with the bilingual delete-confirm copy
  When they accept the dialog
  Then DELETE /account/api/admin/sponsors/{id} returns HTTP 200
  And a green toast reads "Sponsor deleted." / "تم حذف الراعي."
  And the grid reloads with the row's Active column now "—"
  And an OperationLog row with Event='Sponsor.Deactivated' records the actor id
```

### E2E-SPN-005 — Cancel delete (no-op)

```gherkin
Scenario: Dismiss the delete confirm dialog
  Given a sponsor "Saab" exists and is Active
  When the administrator clicks "Delete" on the "Saab" row
  And they dismiss (cancel) the native confirm dialog
  Then no DELETE request fires
  And no toast appears
  And the "Saab" row is unchanged (still Active="✓")
```

### E2E-SPN-006 — Tier dropdown + grid ordering

```gherkin
Scenario: Tier picker offers all four tiers and the grid orders by tier
  Given the administrator opens the "Add sponsor" modal
  Then the Tier select lists exactly: "Platinum", "Gold", "Silver", "Bronze"
  And "Platinum" is the default selection
  When sponsors exist across more than one tier
  Then the grid renders them ordered by Tier (Platinum→Gold→Silver→Bronze),
       then Display order ascending, then Name (Arabic) ascending
       (the API ListAllAsync OrderBy Tier, DisplayOrder, NameAr)
```

### E2E-SPN-007 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no Sponsor rows
  When the administrator opens /admin/sponsors
  Then the grid body renders the SimfEmptyState component
  And the empty state title reads "No sponsors yet." / "لا يوجد رعاة بعد."
  And the toolbar still shows the "Add sponsor" button
  And no error toast appears
```

### E2E-SPN-008 — Auth gate (page level)

```gherkin
Scenario: Admin lacking Sponsors.View is denied the page
  Given a signed-in admin whose role does NOT include the Sponsors.View permission
        (and is not the Administrator wildcard "*")
  When they navigate to /admin/sponsors
  Then they land on /not-permitted with HTTP 200
  And no /account/api/admin/sponsors/list request fires
```

### E2E-SPN-009 — Auth gate (action level)

```gherkin
Scenario: Admin with View but without Create cannot create
  Given a signed-in admin whose role includes Sponsors.View but NOT Sponsors.Create
  And they have opened /admin/sponsors (the page renders, "Add sponsor" is visible
      because the button is not individually gated in the CP UI)
  When they fill the Add modal and click "Save"
  Then the BFF forwards POST /admin/sponsors
  And the API rejects it with HTTP 403 (the Sponsors.Create policy is not satisfied)
  And the modal stays open with the bilingual error toast surfaced from the envelope
```

### E2E-SPN-010 — Client-side name validation

```gherkin
Scenario: Blank name shows a bilingual toast and suppresses the POST
  Given the "Add sponsor" modal is open
  When the administrator leaves Name (English) and/or Name (Arabic) blank
  And clicks "Save"
  Then a SimfAlert error toast appears reading
       "Both the English and Arabic names are required."
       / "الاسم بالإنجليزية والعربية مطلوبان."
  And the modal stays open
  And NO POST /account/api/admin/sponsors request fires (guarded client-side in SaveAsync)
```

### E2E-SPN-011 — Server-side validation rejection

```gherkin
Scenario: Over-length / bad-tier / negative-order is rejected by the API with 400
  Given the "Add sponsor" modal is open with a valid Name (English) and Name (Arabic)
  When the administrator submits a value the API rejects, e.g.:
       a Logo path longer than 256 characters, or
       a Link longer than 512 characters, or
       a Display order below 0
  And clicks "Save"
  Then POST /account/api/admin/sponsors returns HTTP 400
  And ApiResult.Error.Code = "SponsorInvalid"
  And the modal stays open
  And the error toast surfaces the bilingual MessageForCurrentCulture(), e.g.
      "Logo path must be 256 characters or fewer." /
      "يجب أن يكون مسار الشعار 256 حرفاً أو أقل."
```

### E2E-SPN-012 — Duplicate (conflict)

```gherkin
Scenario: Duplicate active Arabic name in the same tier returns 409
  Given an active sponsor with Name (Arabic) = "ساب" exists in tier "Gold"
  When the administrator opens "Add sponsor"
  And fills Name (English) = "Saab Duplicate"
  And fills Name (Arabic) = "ساب"
  And selects Tier = "Gold"
  And clicks "Save"
  Then the BFF forwards POST /admin/sponsors
  And the API returns HTTP 409 with ApiResult.Error.Code = "SponsorDuplicate"
  And the modal stays open
  And the error toast surfaces the bilingual message, e.g.
      "An active sponsor named 'ساب' already exists in this tier." /
      "يوجد راعٍ نشط بالاسم 'ساب' في هذه الفئة بالفعل."
  And the same Arabic name in a DIFFERENT tier (e.g. "Silver") would NOT conflict
```

### E2E-SPN-013 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is configured to return 500 on /admin/sponsors/list (e.g. DB down)
  When the administrator opens /admin/sponsors
  Then the page first shows "Loading sponsors…" / "جارٍ تحميل الرعاة…"
  And then a red toast appears reading
       "Could not load sponsors. Please try again." /
       "تعذّر تحميل الرعاة. يُرجى المحاولة مرة أخرى."
  And no rows render
```

### E2E-SPN-014 — Modal Cancel discards edits

```gherkin
Scenario: Cancel closes the modal without persisting
  Given the administrator has opened the "Edit sponsor" modal for "Saab"
  When they change Display order to "99"
  And they click "Cancel"
  Then the modal closes
  And NO PUT request fires
  And the "Saab" row's Display order is unchanged in the grid
```

### E2E-SPN-015 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the Add modal
  Given the administrator is on /admin/sponsors in English
  When they switch the UI language to Arabic ("العربية")
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "الرعاة"
  And the table headers read
      "الاسم (إنجليزي)", "الاسم (عربي)", "الفئة", "مسار الشعار",
      "الرابط", "ترتيب العرض", "نشط"
  And the toolbar button reads "إضافة راعٍ"

  When they click "إضافة راعٍ"
  Then the Add modal opens in RTL with title "إضافة راعٍ"
  And the field labels read "الاسم (إنجليزي)", "الاسم (عربي)", "الفئة",
      "الرابط", "مسار الشعار", "ترتيب العرض", "نشط"
  And the footer buttons read "إلغاء" (Cancel) and "حفظ" (Save)
```

---

## Implementation notes

- **Manual smoke is canonical today.** Until Playwright is adopted, the canonical
  "run" of these scenarios is a Chrome DevTools MCP session — sign in via the
  Background steps, walk each scenario, and capture screenshots into
  `docs/screenshots/cp-admin-sponsors-*.png`.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin scenario
  into a `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created) plus
  a step-definition class. The Gherkin shape is already runner-agnostic.
- **API integration tests** at `tests/SIMF.Api.Tests/SponsorsTests.cs` cover the
  same surface at a lower layer (no browser):
  - `Admin_creates_sponsor_and_public_endpoint_returns_it_grouped`
  - `Public_list_groups_highest_tier_first`
  - `Deactivated_sponsor_drops_off_public_list`
  - `Duplicate_active_name_in_same_tier_returns_409` (E2E-SPN-012 at API layer)
  - `Non_admin_caller_is_forbidden_on_create` (E2E-SPN-009 at API layer)
  When an E2E scenario reliably covers one of these, the matching `Api.Tests` case
  can usually be retired — but keep both during the transition.
- **Backing endpoints / error codes** (grounded in
  `src/Backend/SIMF.Api/Endpoints/Sponsors/SponsorEndpoints.cs` +
  `src/Backend/SIMF.Infrastructure/Sponsors/AdminSponsorService.cs`):
  - `POST /admin/sponsors/list` — policy `Sponsors.View`
  - `POST /admin/sponsors` — policy `Sponsors.Create`, rate-limited "auth"
  - `PUT /admin/sponsors/{id}` — policy `Sponsors.Edit`, rate-limited "auth"
  - `DELETE /admin/sponsors/{id}` — policy `Sponsors.Delete` (soft-deactivate), rate-limited "auth"
  - Error codes: `SponsorInvalid` (400), `SponsorDuplicate` (409), `SponsorNotFound` (404)
  - Tier values: 10=Platinum, 20=Gold, 30=Silver, 40=Bronze
  - Field limits: NameEn/NameAr 1–256, LogoRelativePath ≤256, Url ≤512, DisplayOrder ≥0
  - Audit events: `Sponsor.Created`, `Sponsor.Updated`, `Sponsor.Deactivated`
- **CP page note.** The page uses the native `window.confirm` for delete (not a
  `SimfModal`), so the delete scenarios must handle a browser dialog
  (`handle_dialog` in Chrome DevTools MCP). The "Details" read-only modal that the
  Interests page has does NOT exist here — Sponsors has only Add/Edit + Delete.
  Action buttons are not individually `<AuthorizedAction>`-gated; per-action
  enforcement is API-side only (see E2E-SPN-009).

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
