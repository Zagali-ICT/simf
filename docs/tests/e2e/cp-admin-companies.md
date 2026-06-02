# E2E test catalogue — Companies CRUD + account provisioning (`/admin/companies`)

| | |
|--|--|
| **Page** | [`cp/companies.md`](../../pages/cp/companies.md) _(page reference doc not yet authored)_ |
| **Route** | `/admin/companies` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (canonical SIMF browser smoke). Convertible to Playwright later — keep scenario steps tool-agnostic. |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Permission gate.** The page carries `@attribute [RequirePermission(PermissionCatalog.Companies.View)]`
> (`"Companies.View"`). Each API action is gated independently:
> list / detail / accounts-list = `Companies.View`, create + provision-account =
> `Companies.Create`, edit = `Companies.Edit`, delete = `Companies.Delete`. The
> `CpNavigation` item `Module.Companies` → `/admin/companies` requires
> `Companies.View`. `Administrator = "*"` satisfies all four.
>
> **Surface map (grounded in `CompaniesList.razor`).** Toolbar: one **Add company**
> button. Grid columns: Name (English), Name (Arabic), Type, Accounts, Active,
> actions. Each row carries three buttons: **Edit**, **Accounts**, **Delete**.
> The **Add/Edit modal** has fields NameEn, NameAr, Type (`<select>`:
> Exhibitor=0 / Sponsor=1), Contact email, Contact phone, Website, plus an
> **Active** checkbox shown on edit only. The **Accounts modal** lists provisioned
> accounts (Contact name, Email, Role, Active) and has a "Provision an account"
> sub-form (Contact name, Email, Role label). There is **no search box, no
> filters, and no pager controls on the page itself** — `_query` is fixed at
> `Top = 50` and the grid simply shows a `Showing {0}–{1} of {2}` summary line.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-CMP-001 | Golden CRUD round-trip — Add → Edit → Delete (soft-delete) | happy | P0 | _to author_ |
| E2E-CMP-002 | Provision an account on a company (Accounts modal sub-flow) | happy | P0 | _to author_ |
| E2E-CMP-003 | Add company — modal opens with empty defaults (Exhibitor, no Active checkbox) | happy | P1 | _to author_ |
| E2E-CMP-004 | Edit company — full detail pre-fill incl. Active checkbox + GET-by-id round-trip | happy | P1 | _to author_ |
| E2E-CMP-005 | Delete company — confirm() dialog cancel path fires no DELETE | happy | P1 | _to author_ |
| E2E-CMP-006 | Accounts modal — list provisioned accounts + empty state | happy | P1 | _to author_ |
| E2E-CMP-007 | Empty list renders `SimfEmptyState` ("No companies yet.") | happy | P1 | _to author_ |
| E2E-CMP-008 | Auth gate — signed-in admin lacking `Companies.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-CMP-009 | Validation — blank EN/AR name → client guard toast, no POST | error | P1 | _to author_ |
| E2E-CMP-010 | Validation — server length rule (name > 256) → `COMPANY_INVALID` 400 | error | P1 | _to author_ |
| E2E-CMP-011 | Provision conflict — duplicate email → `ADMIN_EMAIL_ALREADY_REGISTERED` 409 | error | P1 | _to author_ |
| E2E-CMP-012 | Provision on inactive company → `COMPANY_INACTIVE` 409 | error | P1 | _to author_ |
| E2E-CMP-013 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-CMP-014 | RTL / Arabic render — page + both modals mirror | i18n | P1 | _to author_ |

## Scenarios

### E2E-CMP-001 — Golden CRUD round-trip

```gherkin
Feature: Companies CRUD round-trip
  As an Administrator
  I want to manage exhibitor / sponsor companies
  So that the public exhibitor and sponsor surfaces stay accurate

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp (TOTP from Get-Totp)
  And they have landed on /admin/companies
  And the page issued POST /account/api/admin/companies/list and rendered the grid

Scenario: Create, edit, then soft-delete one company
  Given the grid currently shows {N} companies (or the SimfEmptyState if {N}=0)
  When the administrator clicks "Add company"
  Then a modal titled "Add company" opens
  And it shows fields: Name (English), Name (Arabic), Type (defaulting to "Exhibitor"),
      Contact email, Contact phone, Website
  And the "Active" checkbox is NOT shown (it appears on edit only)

  When they fill Name (English)="Lockheed Maritime Systems"
  And they fill Name (Arabic)="لوكهيد للأنظمة البحرية"
  And they select Type="Sponsor"
  And they fill Contact email="exhibits@lmaritime.example"
  And they fill Contact phone="+966512345678"
  And they fill Website="https://lmaritime.example"
  And they click "Save"
  Then the BFF forwards POST /account/api/admin/companies and the API returns HTTP 200
  And the modal closes
  And a green SimfAlert reads "Company saved." / "تم حفظ الشركة."
  And the grid reloads (a fresh POST /account/api/admin/companies/list fires)
  And a row exists with Name (English)="Lockheed Maritime Systems", Type="Sponsor",
      Accounts="0", and the Active column shows "✓"

  When the administrator clicks "Edit" on that row
  Then the BFF issues GET /account/api/admin/companies/{id} (full detail fetch) returning HTTP 200
  And a modal titled "Edit company" opens with every field pre-filled from the detail
  And the "Active" checkbox is now visible and ticked
  When they change Name (English)="Lockheed Maritime Systems Intl"
  And they click "Save"
  Then the BFF forwards PUT /account/api/admin/companies/{id} returning HTTP 200
  And the modal closes
  And a green SimfAlert reads "Company saved." / "تم حفظ الشركة."
  And the row's Name (English) column now reads "Lockheed Maritime Systems Intl"

  When the administrator clicks "Delete" on that row
  Then a browser confirm() dialog asks
      "Delete this company? It will be removed from the public exhibitor / sponsor list immediately."
  When they accept the dialog
  Then the BFF forwards DELETE /account/api/admin/companies/{id} returning HTTP 200
  And a green SimfAlert reads "Company deleted." / "تم حذف الشركة."
  And the grid reloads; the row's Active column now shows "—" (soft-delete, IsActive=false)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-companies-golden-before.png`
- Screenshots: `docs/screenshots/cp-admin-companies-{add-modal,edit-modal,delete-confirm,after}.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/companies/*` call returns 200 (POST list, POST create, GET by-id, PUT, DELETE)
- Audit rows: `OperationLog` rows with `Event = 'Company.Created'`, `'Company.Updated'`,
  and `'Company.Deactivated'`, each carrying the actor's id (= the signed-in superadmin's `sub`)

### E2E-CMP-002 — Provision an account

```gherkin
Scenario: Provision a least-privilege account tagged to a company
  Given an active company "Lockheed Maritime Systems Intl" exists with Accounts="0"
  When the administrator clicks "Accounts" on that row
  Then a modal titled "Accounts — Lockheed Maritime Systems Intl" opens
  And the BFF issues GET /account/api/admin/companies/{id}/accounts returning HTTP 200
  And an info SimfAlert reads
      "A provisioned account is a pending-approval app login tagged to this company."
      / "الحساب المُنشأ هو تسجيل دخول للتطبيق قيد الموافقة مرتبط بهذه الشركة."
  And a "Provision an account" sub-form shows Contact name, Email, Role label fields
  When they fill Contact name="Sara Al-Otaibi"
  And they fill Email="sara.otaibi@lmaritime.example"
  And they fill Role label="Booth manager"
  And they click "Provision account"
  Then the BFF forwards POST /account/api/admin/companies/{id}/accounts returning HTTP 200
  And a green SimfAlert reads
      "Account provisioned. It is pending approval." / "تم إنشاء الحساب. وهو قيد الموافقة."
  And the accounts table reloads and shows a row Contact name="Sara Al-Otaibi",
      Email="sara.otaibi@lmaritime.example", Role="Booth manager", Active="✓"
  And the provision sub-form is cleared
  When they click "Close"
  Then the Accounts modal closes
  And the grid row's Accounts column for that company now reads "1"
      (the page also re-runs POST /account/api/admin/companies/list)
```

**Evidence captured:**
- Screenshots: `docs/screenshots/cp-admin-companies-accounts-{empty,provisioned}.png`
- Console errors: 0 expected
- Network: GET `/accounts` (200), POST `/accounts` (200), POST `/list` refresh (200)
- Audit row: `OperationLog` row with `Event = 'Company.AccountProvisioned'`, the actor's id,
  and `SubjectUserId` = the new Visitor account id (the account is created in the pending-approval state)

### E2E-CMP-003 — Add modal defaults

```gherkin
Scenario: Add company modal opens with empty defaults
  When the administrator clicks "Add company"
  Then the modal title reads "Add company"
  And Name (English) and Name (Arabic) are empty
  And the Type <select> defaults to "Exhibitor" (value 0)
  And Contact email, Contact phone, Website are empty
  And no "Active" checkbox is rendered
  And no toast / alert is shown above the form
  When they click "Cancel"
  Then the modal closes and no network request fires
```

### E2E-CMP-004 — Edit pre-fill via GET-by-id

```gherkin
Scenario: Edit fetches full detail before opening
  Given an active Exhibitor company "Saab Maritime" exists
  When the administrator clicks "Edit" on its row
  Then a GET /account/api/admin/companies/{id} request fires and returns HTTP 200
  And the "Edit company" modal opens with NameEn, NameAr, Type, Contact email,
      Contact phone, Website all populated from the AdminCompanyDetail response
  And the "Active" checkbox is visible and reflects the company's IsActive value
  When they untick "Active"
  And they click "Save"
  Then PUT /account/api/admin/companies/{id} fires with IsActive=false and returns HTTP 200
  And a green "Company saved." toast appears
  And the row's Active column now shows "—"
```

### E2E-CMP-005 — Delete confirm cancel path

```gherkin
Scenario: Cancelling the delete confirm() fires no request
  Given an active company row is visible
  When the administrator clicks "Delete" on that row
  Then a browser confirm() dialog appears with the bilingual delete-confirm text
  When they dismiss / cancel the dialog
  Then NO DELETE /account/api/admin/companies/{id} request fires
  And no toast appears
  And the row stays unchanged (Active still "✓")
```

### E2E-CMP-006 — Accounts list + empty state

```gherkin
Scenario: Accounts modal shows existing accounts or its empty state
  Given a company with zero provisioned accounts
  When the administrator clicks "Accounts" on its row
  Then the Accounts modal opens and the accounts area renders the SimfEmptyState
  And the empty state reads "No accounts provisioned yet." / "لم يتم إنشاء أي حسابات بعد."
  And the "Provision an account" sub-form is still shown beneath it

  Given a company with two provisioned accounts
  When the administrator clicks "Accounts" on its row
  Then the accounts table renders both rows with Contact name, Email, Role, Active columns
  And a missing Role label renders as "—"
```

### E2E-CMP-007 — Empty list

```gherkin
Scenario: Empty company list renders SimfEmptyState
  Given the database has no Company rows
  When the administrator opens /admin/companies
  Then POST /account/api/admin/companies/list returns Total=0
  And the grid body renders the SimfEmptyState component
  And the empty state reads "No companies yet." / "لا توجد شركات بعد."
  And the "Add company" toolbar button is still shown
  And no error toast appears
```

### E2E-CMP-008 — Auth gate

```gherkin
Scenario: Signed-in admin lacking Companies.View is denied
  Given a signed-in admin whose role does NOT grant "Companies.View"
      (and is not Administrator "*")
  When they navigate to /admin/companies
  Then the [RequirePermission(PermissionCatalog.Companies.View)] attribute denies access
  And they land on /not-permitted with HTTP 200
  And no /account/api/admin/companies/list request fires
  And the "Companies" item is absent from the CP nav rail (RequiredPermission gate)
```

### E2E-CMP-009 — Client-side name validation

```gherkin
Scenario: Blank English/Arabic name is blocked client-side
  Given the Add company modal is open
  When the administrator leaves Name (English) blank
  And clicks "Save"
  Then the client guard in SaveAsync short-circuits
  And a red SimfAlert reads
      "Both the English and Arabic names are required." / "الاسم بالإنجليزية والعربية كلاهما مطلوب."
  And the modal stays open
  And NO POST /account/api/admin/companies request fires
```

### E2E-CMP-010 — Server length validation

```gherkin
Scenario: Over-length name returns COMPANY_INVALID from the API
  Given the Add company modal is open
  And the SimfTextField MaxLength=256 guard is bypassed (e.g. paste a 300-char name
      via DevTools fill) so the value reaches the server over the 256 limit
  When the administrator fills both names and clicks "Save"
  Then POST /account/api/admin/companies returns HTTP 400
  And ApiResult.Error.Code = "COMPANY_INVALID"
  And the modal stays open
  And the error toast surfaces the bilingual MessageForCurrentCulture()
      "Company name (EN + AR) must be between 1 and 256 characters."
      / "يجب أن يتراوح طول اسم الشركة (إنجليزي + عربي) بين 1 و 256 حرفاً."
```

### E2E-CMP-011 — Provision duplicate email

```gherkin
Scenario: Provisioning an already-registered email returns 409
  Given a company "Saab Maritime" is active
  And the email "existing.user@simf.example" is already registered as a SIMF account
  When the administrator opens the Accounts modal
  And fills Contact name="Dup User" + Email="existing.user@simf.example"
  And clicks "Provision account"
  Then POST /account/api/admin/companies/{id}/accounts returns HTTP 409
  And ApiResult.Error.Code = "ADMIN_EMAIL_ALREADY_REGISTERED"
      (raised by the reused IAdminUserProvisioningService.CreateVisitorAsync pipeline)
  And a red SimfAlert surfaces the bilingual server message
  And no CompanyMembership row is created
```

### E2E-CMP-012 — Provision on inactive company

```gherkin
Scenario: Provisioning on a deactivated company returns COMPANY_INACTIVE
  Given a company that was previously soft-deleted (IsActive=false)
  And it has somehow been re-opened in the Accounts modal (e.g. via a stale grid)
  When the administrator fills the provision sub-form with a fresh email
  And clicks "Provision account"
  Then POST /account/api/admin/companies/{id}/accounts returns HTTP 409
  And ApiResult.Error.Code = "COMPANY_INACTIVE"
  And the error toast reads
      "The company is not active; reactivate it before adding accounts."
      / "الشركة غير نشطة؛ يرجى إعادة تفعيلها قبل إضافة الحسابات."
```

### E2E-CMP-013 — Server 500 on list

```gherkin
Scenario: API 500 on /list shows the fallback bilingual toast
  Given the API is configured to return 500 on /admin/companies/list (e.g. DB down)
  When the administrator opens /admin/companies
  Then the page shows "Loading companies…" / "جارٍ تحميل الشركات…" first
  And then a red SimfAlert appears reading
      "Could not load companies. Please try again." / "تعذر تحميل الشركات. حاول مرة أخرى."
  And no company rows render (the grid stays empty, no SimfEmptyState/table)
```

### E2E-CMP-014 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and both modals
  Given the administrator is on /admin/companies in English
  When they switch the UI to Arabic via the header language toggle
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "الشركات"
  And the "Add company" button reads "إضافة شركة"
  And the grid headers read الاسم (بالإنجليزية) / الاسم (بالعربية) / النوع / الحسابات / نشط
  And the row action buttons read تعديل / الحسابات / حذف

  When they click "إضافة شركة"
  Then the Add modal opens in RTL with Arabic field labels
  And the Type <select> options read "عارض" (Exhibitor) and "راعٍ" (Sponsor)
  And the footer buttons read إلغاء / حفظ in reverse order

  When they open the Accounts modal for any company
  Then it renders RTL with the title "الحسابات — {company}"
  And the provision sub-form labels read اسم جهة الاتصال / البريد الإلكتروني / مسمى الدور
  And the submit button reads "إنشاء الحساب"
```

---

## Implementation notes

- **Manual smoke is the canonical "run" today.** Until Playwright is adopted, the
  canonical execution is a Chrome DevTools MCP session: sign in per the Background,
  then walk each scenario, capturing screenshots into
  `docs/screenshots/cp-admin-companies-{scenario}.png`.
- **Convert to Playwright** later by copying each Gherkin scenario into a `.feature`
  file under `tests/SIMF.E2E.Tests/` (project to be created) plus a step-definition
  class. The Gherkin shape is already runner-agnostic.
- **No search / filter / pager on the page.** `CompaniesList.razor` fixes `_query`
  at `Top = 50` with no UI to change `Skip`, `Search`, or `Filters`. The service
  (`AdminCompanyService.ListAllAsync`) *supports* `search`, `isActive`, and `type`
  filters, but they are not wired to any control on this page — so there are no
  search/filter/pager scenarios to author. If a search box is added later, add a
  matching `E2E-CMP` row.
- **API-layer coverage gap.** `CompanyEndpoints.cs` and `AdminCompanyService.cs`
  both carry a `// Tests: SIMF.Api.Tests/CompaniesTests.cs` header, but **that test
  file does not exist** in `tests/SIMF.Api.Tests/` as of 2026-06-02 — the only
  company-adjacent API test is `tests/SIMF.Api.Tests/AdminBoothsTests.cs` (booths
  reference a company). The lower-layer xUnit coverage these E2E scenarios mirror
  is therefore **missing** and should be raised separately; do not assume the named
  file backs this surface.
- **Error codes referenced** (from `src/Shared/SIMF.Common/ErrorCodes.cs`):
  `COMPANY_INVALID` (400), `COMPANY_NOT_FOUND` (404), `COMPANY_INACTIVE` (409),
  `COMPANY_ACCOUNT_INVALID` (400), and `ADMIN_EMAIL_ALREADY_REGISTERED` (409) from
  the reused provisioning pipeline.
- **Audit event keys** (from `src/Backend/SIMF.Application/Auditing/AuditEvents.cs`):
  `Company.Created`, `Company.Updated`, `Company.Deactivated`,
  `Company.AccountProvisioned`.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
