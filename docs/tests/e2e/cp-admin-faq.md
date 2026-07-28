# E2E test catalogue — FAQ management (`/admin/faq`)

| | |
|--|--|
| **Page** | [`cp/admin-faq.md`](../../pages/cp/admin-faq.md) |
| **Route** | `/admin/faq` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-07 (D-338 — converted to SimfDataGrid) |

> **Page shape (D-338).** Two-level master-detail using **two `SimfDataGrid`s**: a
> **groups** grid at the top and, once a group's **Manage entries** (`list-tree`)
> row action is clicked, its **entries** grid below. Both are **server-paged** via
> `/account/api/admin/faq/groups/list` + `/groups/{id}/entries/list` with select-all,
> numbered pager and per-row Edit/Deactivate icon actions; columns sort on
> name/question/display-order (no per-column filter — the service filters only by
> global search + `isActive`). CRUD runs through two `SimfModal`s (group modal + entry
> modal). Soft-delete only: **Deactivate** flips `IsActive` to false; rows stay
> visible (the admin list returns every row, active and inactive). The required
> permission for the page is `PermissionCatalog.Faq.View`; **Create/Edit/Delete are
> enforced by the API** (the converted grid pattern moves the per-action gate to the
> API, as the other canonical grid pages do; previously these were individually
> gated in the UI by `Faq.Create` / `Faq.Edit` / `Faq.Delete`).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-FAQ-001 | Group CRUD round-trip — Add group → Edit → Deactivate | happy | P0 | _to author_ |
| E2E-FAQ-002 | Entry CRUD round-trip — Manage entries → Add entry → Edit → Deactivate | happy | P0 | _to author_ |
| E2E-FAQ-003 | `Manage entries` selects a group + loads its entries table | happy | P1 | _to author_ |
| E2E-FAQ-004 | Empty groups state renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-FAQ-005 | Empty entries state renders `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-FAQ-006 | Entry count column updates after add/deactivate | happy | P2 | _to author_ |
| E2E-FAQ-007 | Auth gate — signed-in admin lacking `Faq.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-FAQ-008 | Action gate — admin with `Faq.View` but not Create/Edit/Delete sees no action buttons | auth | P1 | _to author_ |
| E2E-FAQ-009 | Group validation — blank English name → bilingual error **inside the dialog** (BUG-004) | error | P1 | _to author_ |
| E2E-FAQ-010 | Group validation — negative display order → bilingual error **inside the dialog** | error | P1 | _to author_ |
| E2E-FAQ-011 | Entry validation — blank answer / over-length question → bilingual error **inside the dialog** | error | P1 | _to author_ |
| E2E-FAQ-012 | Not-found — edit a group/entry deactivated in another tab → 404 toast | error | P2 | _to author_ |
| E2E-FAQ-013 | Server 500 on groups `/list` → bilingual fallback toast | resilience | P2 | _to author_ |
| E2E-FAQ-014 | Idempotent deactivate — re-deactivate an already-hidden group is a no-op success | resilience | P2 | _to author_ |
| E2E-FAQ-015 | RTL / Arabic render — page + both modals mirror | i18n | P1 | _to author_ |
| E2E-FAQ-016 | Client validation — empty submit on either dialog → bilingual error **inside the dialog**, no POST (BUG-004) | error | P1 | _to author_ |

## Scenarios

### E2E-FAQ-001 — Group CRUD round-trip

```gherkin
Feature: FAQ group CRUD round-trip
  As an Administrator
  I want to manage the FAQ groups that organise the visitor-facing FAQ
  So that questions are grouped accurately for the event programme

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator with the Faq.View/Create/Edit/Delete permissions has signed in
    via /login + /login/totp (TOTP from the Get-Totp helper)
  And they have landed on /admin/faq
  And the page has fired POST /account/api/admin/faq/groups/list and rendered the groups table

Scenario: Create, edit and deactivate one FAQ group
  Given the groups table currently shows {N} rows
  When the administrator clicks "Add group"
  Then the group modal opens titled "Add FAQ group"
  And it shows three fields: "Name (English)", "Name (Arabic)", "Display order"
  And no "Active (visible)" checkbox is shown (it only appears in Edit)
  When they fill Name (English)="Registration & badges"
  And they fill Name (Arabic)="التسجيل والشارات"
  And they fill Display order="10"
  And they click "Save"
  Then POST /account/api/admin/faq/groups is sent with { NameEn, NameAr, DisplayOrder: 10 }
  And the API returns HTTP 200 with ApiResult.Success = true
  And the modal closes
  And a green toast reads "Saved."
  And the groups table reloads and shows {N + 1} rows
  And a row exists with Name (EN)="Registration & badges", Order=10, Entries=0 and the green "Active" pill

  When the administrator clicks "Edit" on that row
  Then the group modal opens titled "Edit FAQ group" with the row's values pre-filled
  And the "Active (visible)" checkbox is now visible and ticked
  When they change Display order to "0"
  And they click "Save"
  Then PUT /account/api/admin/faq/groups/{id} is sent with IsActive: true and DisplayOrder: 0
  And the modal closes
  And a green toast reads "Saved."
  And the row's Order column now reads "0"

  When the administrator clicks "Deactivate" on that row
  Then DELETE /account/api/admin/faq/groups/{id} is sent
  And the API returns HTTP 200 with ApiResult<bool>.Data = true
  And a green toast reads "Deactivated."
  And the row remains visible but its pill changes to the "Hidden" pill
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-faq-group-crud-before.png`
- Screenshot after (add modal / edited row / deactivated pill): `docs/screenshots/cp-admin-faq-group-crud-{add-modal,edited,deactivated}.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/faq/groups/*` call returns 200
- Audit rows: `OperationLog` rows with `EventType = 'faq.group.created'`, `'faq.group.updated'`,
  `'faq.group.deactivated'` and the actor's id (`AuditOutcome.Success`)

### E2E-FAQ-002 — Entry CRUD round-trip

```gherkin
Feature: FAQ entry CRUD round-trip
  As an Administrator
  I want to manage the question/answer entries inside a FAQ group
  So that visitors see accurate, bilingual answers

Background:
  Given an Administrator with the Faq.View/Create/Edit/Delete permissions is on /admin/faq
  And a group "Registration & badges" exists in the groups table

Scenario: Add, edit and deactivate one entry inside a group
  When the administrator clicks "Manage entries" on the "Registration & badges" row
  Then POST /account/api/admin/faq/groups/{groupId}/entries/list is sent
  And the group row gains the "is-selected" highlight
  And the entries heading reads "Entries in “Registration & badges”"
  And the entries section shows an "Add entry" button

  When the administrator clicks "Add entry"
  Then the entry modal opens titled "Add FAQ entry"
  And it shows fields: "Question (English)", "Question (Arabic)", "Answer (English)",
    "Answer (Arabic)", "Display order"
  And the Answer fields are multi-line textareas (rows=4, maxlength 4000)
  And no "Active (visible)" checkbox is shown (Add only)
  When they fill Question (English)="When does badge collection open?"
  And they fill Question (Arabic)="متى يبدأ استلام الشارات؟"
  And they fill Answer (English)="Badge desks open at 08:00 each day."
  And they fill Answer (Arabic)="تفتح مكاتب الشارات الساعة 08:00 كل يوم."
  And they fill Display order="0"
  And they click "Save"
  Then POST /account/api/admin/faq/entries is sent with FaqGroupId={groupId} and the four texts
  And the API returns HTTP 200
  And the modal closes
  And a green toast reads "Saved."
  And the entries table shows the new question with the green "Active" pill
  And the group's Entries count increments by 1 (the page also reloads the groups list)

  When the administrator clicks "Edit" on that entry
  Then the entry modal opens titled "Edit FAQ entry" with all five fields pre-filled
  And the "Active (visible)" checkbox is visible and ticked
  When they change Answer (English)="Badge desks open at 07:30 each day."
  And they click "Save"
  Then PUT /account/api/admin/faq/entries/{id} is sent with the updated AnswerEn and IsActive: true
  And the modal closes
  And a green toast reads "Saved."

  When the administrator clicks "Deactivate" on that entry
  Then DELETE /account/api/admin/faq/entries/{id} is sent
  And a green toast reads "Deactivated."
  And the entry's pill changes to "Hidden"
  And the group's Entries count (active only) decrements by 1
```

**Evidence captured:**
- Screenshot before / after: `docs/screenshots/cp-admin-faq-entry-crud-{before,add-modal,edited,deactivated}.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/faq/entries/*` and `.../entries/list` call returns 200
- Audit rows: `OperationLog` rows with `EventType = 'faq.entry.created'`, `'faq.entry.updated'`,
  `'faq.entry.deactivated'` and the actor's id

### E2E-FAQ-003 — Manage entries selects a group

```gherkin
Scenario: Selecting a different group reloads its entries
  Given two groups "Registration & badges" and "Venue & parking" exist
  And the administrator is viewing the entries of "Registration & badges"
  When they click "Manage entries" on the "Venue & parking" row
  Then POST /account/api/admin/faq/groups/{venueGroupId}/entries/list is sent
  And the "is-selected" highlight moves to the "Venue & parking" row
  And the entries heading now reads "Entries in “Venue & parking”"
  And the entries table shows only that group's entries
```

### E2E-FAQ-004 — Empty groups state

```gherkin
Scenario: No groups renders SimfEmptyState
  Given the database has no FaqGroup rows
  When the administrator opens /admin/faq
  Then POST /account/api/admin/faq/groups/list returns an empty page (Items = [])
  And the groups area renders the SimfEmptyState component
  And the empty state title reads "No FAQ groups yet." / "لا توجد مجموعات أسئلة بعد."
  And the "Add group" button is still shown in the toolbar
  And no entries section is rendered (no group is selected)
```

### E2E-FAQ-005 — Empty entries state

```gherkin
Scenario: A group with no entries renders SimfEmptyState
  Given a group "Press" exists with zero entries
  When the administrator clicks "Manage entries" on the "Press" row
  Then the entries heading reads "Entries in “Press”"
  And the entries area renders the SimfEmptyState component
  And the empty state title reads "No entries in this group yet." / "لا توجد أسئلة في هذه المجموعة بعد."
  And the "Add entry" button is still shown
```

### E2E-FAQ-006 — Entry count column reflects active entries

```gherkin
Scenario: The group Entries column counts only active entries
  Given a group "Catering" with one active entry exists (Entries column reads "1")
  When the administrator adds a second entry to "Catering" and saves
  Then after the groups list reloads the "Catering" Entries column reads "2"
  When the administrator deactivates one of those entries
  Then after the reload the "Catering" Entries column reads "1"
```

### E2E-FAQ-007 — Auth gate (page permission)

```gherkin
Scenario: Signed-in admin lacking Faq.View is denied the page
  Given a signed-in admin whose roles grant no Faq.View permission
    (the page is gated by [RequirePermission(PermissionCatalog.Faq.View)])
  When they navigate to /admin/faq
  Then they land on /not-permitted with HTTP 200
  And no POST /account/api/admin/faq/groups/list request fires
  And the "FAQ management" / "إدارة الأسئلة الشائعة" nav item is not shown for them
```

### E2E-FAQ-008 — Action gate (read-only admin)

```gherkin
Scenario: Admin with Faq.View only sees no Create/Edit/Delete buttons
  Given a signed-in admin granted Faq.View but not Faq.Create / Faq.Edit / Faq.Delete
  When they open /admin/faq
  Then the groups table renders and rows are listed
  And the "Add group" button is hidden (wrapped in AuthorizedAction Faq.Create)
  And on each group row the "Edit" and "Deactivate" buttons are hidden
  And the "Manage entries" button is still shown (it is not permission-gated)
  When they click "Manage entries"
  Then the entries table renders read-only with no "Add entry"/"Edit"/"Deactivate" buttons
```

### E2E-FAQ-009 — Group validation: blank name

```gherkin
Scenario: Blank English name returns 400 with bilingual message
  Given the "Add FAQ group" modal is open
  When the administrator leaves Name (English) blank
  And fills Name (Arabic)="عام" and Display order="0"
  And clicks "Save"
  Then POST /account/api/admin/faq/groups is sent
  And the API returns HTTP 400 with ApiResult.Error.Code = "FAQ_INVALID"
  And the error message is "FAQ English name is required." / "الاسم الإنجليزي مطلوب."
  And the modal stays open
  And a red SimfAlert surfaces the bilingual MessageForCurrentCulture() INSIDE the
      dialog body (.simf-modal__body), not on the page behind the backdrop
```

> **BUG-004 (as-built).** The page-level toast is rendered inside
> `.simf-surface`, which sits under the modal backdrop
> (`.simf-modal { position: fixed; inset: 0; z-index: 100 }`), so a rejected save
> was invisible while the dialog was open and Save read as a dead button. Both
> dialogs now render a dedicated `_error` in the dialog body, and a blank
> required field is caught client-side before the request goes out — the same
> shape the canonical CRUD forms (e.g. `SessionCategoriesAddEdit`) use.

### E2E-FAQ-010 — Group validation: negative display order

```gherkin
Scenario: Negative display order returns 400 with bilingual message
  Given the "Add FAQ group" modal is open
  When the administrator fills Name (English)="General" + Name (Arabic)="عام"
  And fills Display order="-1"
  And clicks "Save"
  Then POST /account/api/admin/faq/groups returns HTTP 400 with Error.Code = "FAQ_INVALID"
  And the message is "Display order must be zero or a positive integer."
    / "يجب أن يكون ترتيب العرض صفراً أو عدداً صحيحاً موجباً."
  And the modal stays open and the red SimfAlert in the dialog body shows the bilingual text
```

### E2E-FAQ-011 — Entry validation: blank / over-length text

```gherkin
Scenario: Blank answer returns 400 with bilingual message
  Given a group is selected and the "Add FAQ entry" modal is open
  When the administrator fills Question (English)="Q?" + Question (Arabic)="س؟"
  And fills Answer (English)="Some answer" but leaves Answer (Arabic) blank
  And clicks "Save"
  Then POST /account/api/admin/faq/entries returns HTTP 400 with Error.Code = "FAQ_INVALID"
  And the message is "FAQ Arabic answer is required." / "الإجابة العربية مطلوب."
  And the modal stays open and the red SimfAlert in the dialog body shows the bilingual text

Scenario: Question over 512 characters returns 400
  Given the "Add FAQ entry" modal is open with all other fields valid
  When the administrator pastes a 513-character Question (English)
  And clicks "Save"
  Then the API returns HTTP 400 with Error.Code = "FAQ_INVALID"
  And the message is "FAQ English question must be 512 characters or fewer."
    / "يجب ألا يتجاوز السؤال الإنجليزي 512 حرفاً."
```

### E2E-FAQ-012 — Not-found on stale edit

```gherkin
Scenario: Editing a group deactivated elsewhere still resolves (soft-delete keeps the row)
  Given group "Sponsors" is visible in the administrator's groups table
  And in another session the same group's row is deactivated (IsActive=false, row retained)
  When the administrator clicks "Edit" on "Sponsors" and clicks "Save"
  Then PUT /account/api/admin/faq/groups/{id} returns HTTP 200 (the row still exists)
  And the save succeeds

Scenario: Editing an entry hard-removed from the DB returns 404
  Given an entry row is open in the Edit modal
  And the entry no longer exists in the database
  When the administrator clicks "Save"
  Then PUT /account/api/admin/faq/entries/{id} returns HTTP 404 with Error.Code = "FAQ_ENTRY_NOT_FOUND"
  And the message is "The FAQ entry was not found." / "لم يتم العثور على السؤال."
  And a red toast surfaces the bilingual message and the modal stays open
```

### E2E-FAQ-013 — Server 500 on groups list

```gherkin
Scenario: API 500 on /groups/list shows the fallback bilingual toast
  Given the API is configured to return 500 on /admin/faq/groups/list (e.g. DB down)
  When the administrator opens /admin/faq
  Then the page fires POST /account/api/admin/faq/groups/list
  And the envelope is not Success
  And a red toast reads "Could not complete the request. Please try again."
    / "تعذّر إكمال الطلب. حاول مرة أخرى."
  And no group rows render (the SimfEmptyState does not show because _loading guards it)
```

### E2E-FAQ-014 — Idempotent deactivate

```gherkin
Scenario: Re-deactivating an already-hidden group is a no-op success
  Given a group "Archived topic" already has the "Hidden" pill (IsActive=false)
  When the administrator clicks "Deactivate" on that row
  Then DELETE /account/api/admin/faq/groups/{id} returns HTTP 200 with Data = true
  And a green toast reads "Deactivated."
  And the row is unchanged (still Hidden)
  And no second 'faq.group.deactivated' audit row is written (service returns early when already inactive)
```

### E2E-FAQ-015 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and both modals
  Given the administrator is on /admin/faq in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "إدارة الأسئلة الشائعة"
  And the groups heading reads "مجموعات الأسئلة" and the "Add group" button reads "إضافة مجموعة"
  And the table headers read "الاسم (إنجليزي)", "الاسم (عربي)", "الترتيب", "الأسئلة", "نشط", "إجراءات"
  And the row pills read "ظاهر" (active) / "مخفي" (hidden)

  When they click "إضافة مجموعة"
  Then the group modal opens in RTL titled "إضافة مجموعة أسئلة"
  And the field labels read "الاسم (الإنجليزية)", "الاسم (العربية)", "ترتيب العرض"
  And the footer buttons read "حفظ" and "إلغاء" in reverse order

  When they select a group and click "إضافة سؤال"
  Then the entry modal opens in RTL titled "إضافة سؤال"
  And the answer textareas are right-aligned
```

### E2E-FAQ-016 — Client validation (empty submit)

```gherkin
Scenario: Saving the Add-group dialog empty reports, and creates nothing
  Given the "Add FAQ group" modal is open with every field empty
  When the administrator clicks "Save" without typing anything
  Then a red SimfAlert renders INSIDE the dialog body (.simf-modal__body) reading
      "An English and an Arabic group name are both required." /
      "اسم المجموعة بالإنجليزية والعربية مطلوبان معاً."
  And the modal stays open
  And no POST /account/api/admin/faq/groups request fires
  And the groups grid row count is unchanged
  And closing and re-opening the dialog clears the message

Scenario: Saving the Add-entry dialog empty reports, and creates nothing
  Given a group is selected and the "Add FAQ entry" modal is open with every field empty
  When the administrator clicks "Save" without typing anything
  Then a red SimfAlert renders INSIDE the dialog body reading
      "The question and the answer are required in both English and Arabic." /
      "السؤال والإجابة مطلوبان بالإنجليزية والعربية."
  And the modal stays open
  And no POST /account/api/admin/faq/entries request fires
```

---

## Implementation notes

- **Manual smoke is the canonical run today.** Until Playwright is adopted, the
  canonical execution of these scenarios is a Chrome DevTools MCP session: sign in
  per the Auth setup, walk each scenario, capture screenshots into
  `docs/screenshots/cp-admin-faq-*.png`. Keep the Gherkin runner-agnostic.
- **Two-level page, no `SimfDataGrid`.** Unlike `/admin/interests`, this page is a
  hand-rolled two-table layout (groups → entries) mirroring `SessionSeatPlan`. There
  is no pager, search box, or column sort surfaced in the UI — both lists request
  `Top = 200`. The backing service *does* support search / `isActive` filter / sort
  via `GridQuery`, but the page does not expose them, so no UI scenario covers them.
- **Soft-delete semantics.** Deactivate is `IsActive = false`; rows stay in the admin
  list (the service deliberately returns inactive rows so editors can manage them).
  There is no hard delete and no restore button — re-activation is done via the Edit
  modal's "Active (visible)" checkbox.
- **No duplicate-name constraint.** The FAQ service has no uniqueness check on group
  names, so there is no 409/`*NameNotUnique` scenario (unlike Interests). The only
  error codes are `FAQ_INVALID` (400 validation), `FAQ_GROUP_NOT_FOUND` and
  `FAQ_ENTRY_NOT_FOUND` (404).
- **API integration tests** at `tests/SIMF.Api.Tests/FaqTests.cs` cover the same
  group/entry CRUD + validation surface at a lower layer (no browser). During the
  transition keep both; once an E2E scenario reliably covers a case you may retire
  the matching `Api.Tests` case.
- **Permission gates** (HARD RULE): page `[RequirePermission(PermissionCatalog.Faq.View)]`;
  each API endpoint `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Faq.{View|Create|Edit|Delete}), nameof(AuthorizationPolicies.RequireApprovedAccount))`;
  action buttons wrapped in `AuthorizedAction`. `CpNavigation` item `Module.Faq` →
  `RequiredPermission: PermissionCatalog.Faq.View`. `CpNavigationPermissionTests` and
  `PermissionEnforcementTests` fail the build if a gate is missing.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
_Last reviewed:_ 2026-07-26 by Claude (BUG-004): both dialogs' validation messages now render inside the dialog body instead of behind the backdrop; reworded E2E-FAQ-009/010/011 and added E2E-FAQ-016 (empty submit).
