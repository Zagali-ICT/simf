# E2E test catalogue — Rating configuration (`/admin/rating-config`)

| | |
|--|--|
| **Page** | [`cp/admin-rating-config.md`](../../pages/cp/admin-rating-config.md) |
| **Route** | `/admin/rating-config` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-25 (D-496 — dynamic ratings) |

> **Page shape (D-496).** Three-level master-detail using `SimfDataGrid`s (mirrors
> `FaqManager`): a **rating-types** grid at the top and, once a type's **Manage**
> (`list-tree`) row action is clicked, its **question-groups** grid + **questions**
> grid below. All server-paged via `/account/api/admin/ratings/types/list`,
> `/types/{id}/groups/list`, `/types/{id}/questions/list`. CRUD runs through three
> `SimfModal`s (type / group / question). Soft-delete only (Deactivate flips
> `IsActive`). The page is gated by `PermissionCatalog.RatingConfig.View`;
> **Create/Edit/Delete are enforced by the API** (`RatingConfig.{Create,Edit,Delete}`).
> **System types** (`App`, `Session`) are seeded, un-deletable (API returns 400) and
> their `Code`/`Scope` are locked in the edit modal.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-RCFG-001 | Type CRUD round-trip — Add type → Edit → Deactivate | happy | P0 | _to author_ |
| E2E-RCFG-002 | Group CRUD round-trip inside a selected type | happy | P0 | _to author_ |
| E2E-RCFG-003 | Question CRUD round-trip (with/without a group, required flag) | happy | P0 | _to author_ |
| E2E-RCFG-004 | `Manage` selects a type + loads its groups + questions grids | happy | P1 | _to author_ |
| E2E-RCFG-005 | Child counts (Groups/Questions/Responses) update after CRUD | happy | P2 | _to author_ |
| E2E-RCFG-006 | System type — App/Session show "Built-in = Yes"; Code+Scope locked in Edit | happy | P1 | _to author_ |
| E2E-RCFG-007 | Deleting a system type → 400 bilingual toast (RATING_TYPE_IS_SYSTEM) | error | P0 | _to author_ |
| E2E-RCFG-008 | Duplicate Code on Create → 409 bilingual toast (RATING_TYPE_CODE_TAKEN) | error | P1 | _to author_ |
| E2E-RCFG-009 | Empty types / groups / questions render `SimfEmptyState` | happy | P1 | _to author_ |
| E2E-RCFG-010 | Auth gate — admin lacking `RatingConfig.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-RCFG-011 | Action gate — `RatingConfig.View` only → no Add/Edit/Delete buttons | auth | P1 | _to author_ |
| E2E-RCFG-012 | Validation — blank name / negative order / over-length text → bilingual error **inside the dialog** (BUG-004) | error | P1 | _to author_ |
| E2E-RCFG-013 | Group delete leaves its questions flat (SetNull, not cascade) | resilience | P2 | _to author_ |
| E2E-RCFG-014 | Server 500 on `/types/list` → bilingual fallback toast | resilience | P2 | _to author_ |
| E2E-RCFG-015 | RTL / Arabic render — page + three modals mirror | i18n | P1 | _to author_ |
| E2E-RCFG-016 | Client validation — empty submit on any of the three dialogs → bilingual error **inside the dialog**, no POST (BUG-004) | error | P1 | _to author_ |
| E2E-RCFG-017 | Details on all three grids for a `RatingConfig.View`-only admin — type (Arabic name + comment settings), group and question (D-835) | auth | P0 | _to author_ |
| E2E-RCFG-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-RCFG-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-RCFG-001 — Type CRUD round-trip

```gherkin
Feature: Rating-type CRUD round-trip
  As an Administrator
  I want to define rating types beyond the built-in App and Session
  So that any aspect of the event can be rated dynamically

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator with RatingConfig.View/Create/Edit/Delete has signed in
    via /login + /login/totp (TOTP from the Get-Totp helper)
  And they have landed on /admin/rating-config
  And the page has fired POST /account/api/admin/ratings/types/list and rendered the types table
  And the table already shows the seeded "App" and "Session" rows

Scenario: Create, edit and deactivate one rating type
  When the administrator clicks "Add"
  Then the type modal opens titled "Add rating type"
  And it shows fields: Code (slug), Name (English), Name (Arabic), Scope,
    "Show overall star rating", "Allow a comment", Comment label (EN/AR), Display order
  When they fill Code="Exhibition", Name (English)="Exhibition", Name (Arabic)="المعرض"
  And they choose Scope="Global (once per user)"
  And they tick "Show overall star rating" and "Allow a comment"
  And they fill Display order="2"
  And they click "Save"
  Then POST /account/api/admin/ratings/types is sent with { Code:"Exhibition", Scope:0, HasOverallStars:true, AllowComment:true, DisplayOrder:2 }
  And the API returns HTTP 200 with ApiResult.Success = true
  And the modal closes, a green "Saved." toast shows
  And the types table reloads and shows a row Code="Exhibition", Scope="Global (once per user)", Built-in="No", Groups=0, Questions=0, Responses=0, "Active"

  When the administrator clicks "Edit" on that row
  Then the type modal opens titled "Edit rating type" with the values pre-filled
  And the Code field is disabled (immutable after create) and Scope is disabled
  And the "Active" checkbox is visible and ticked
  When they untick "Allow a comment" and click "Save"
  Then PUT /account/api/admin/ratings/types/{id} is sent with AllowComment:false
  And a green "Saved." toast shows

  When the administrator clicks "Deactivate" on that row
  Then DELETE /account/api/admin/ratings/types/{id} returns HTTP 200 with Data=true
  And a green "Deactivated." toast shows and the row's pill changes to "Inactive"
```

**Evidence captured:**
- Screenshots: `docs/screenshots/cp-admin-rating-config-type-{add-modal,edited,deactivated}.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/ratings/types/*` call returns 200
- Audit rows: `OperationLog` with `EventType = 'RatingType.Created'`, `'RatingType.Updated'`, `'RatingType.Deactivated'`

### E2E-RCFG-002 — Group CRUD inside a selected type

```gherkin
Scenario: Add, edit and deactivate a question group under the Session type
  Given the administrator clicks "Manage" on the "Session" row
  Then POST /account/api/admin/ratings/types/{sessionId}/groups/list is sent
  And the groups heading reads "Question groups — Session"
  When they click "Add group", fill Name (English)="Audio-visual", Name (Arabic)="الصوتيات والمرئيات", Order="0"
  And click "Save"
  Then POST /account/api/admin/ratings/groups is sent with { RatingTypeId:{sessionId}, Name, NameArabic, DisplayOrder:0 }
  And a green "Saved." toast shows and the groups table shows the new row with Questions=0
  When they Edit the group and Deactivate it
  Then PUT then DELETE /account/api/admin/ratings/groups/{id} each return HTTP 200
```

### E2E-RCFG-003 — Question CRUD round-trip

```gherkin
Scenario: Add a required question, optionally in a group
  Given the "Session" type is selected and its groups grid lists "Audio-visual"
  When the administrator clicks "Add question" on the questions grid
  Then the question modal opens with fields: Question (English), Question (Arabic),
    Group (a select defaulting to "(no group)"), "Required to submit", Display order
  When they fill Question (English)="Microphone clarity", Question (Arabic)="وضوح الميكروفون"
  And they pick Group="Audio-visual" and tick "Required to submit" and Order="0"
  And they click "Save"
  Then POST /account/api/admin/ratings/questions is sent with { RatingTypeId:{sessionId}, RatingQuestionGroupId:{groupId}, IsRequired:true }
  And a green "Saved." toast shows and the questions grid shows Group="Audio-visual", Required="Yes"
  When they Edit the question, change Group to "(no group)" and Save
  Then PUT /account/api/admin/ratings/questions/{id} is sent with RatingQuestionGroupId:null
  And the Group column now reads "—"
  When they Deactivate the question
  Then DELETE /account/api/admin/ratings/questions/{id} returns HTTP 200
```

### E2E-RCFG-007 — System type cannot be deleted

```gherkin
Scenario: Deactivating the built-in App type is blocked
  Given the types table shows the seeded "App" row with Built-in="Yes"
  When the administrator selects it and clicks "Deactivate"
  Then DELETE /account/api/admin/ratings/types/{appId} returns HTTP 400 with Error.Code = "RATING_TYPE_IS_SYSTEM"
  And the message is "Built-in rating types can't be deleted." / "لا يمكن حذف أنواع التقييم المدمجة."
  And a red toast surfaces the bilingual message and the row is unchanged
```

### E2E-RCFG-008 — Duplicate code

```gherkin
Scenario: Creating a type with an existing code returns 409
  Given a type with Code="App" already exists (seeded)
  When the administrator opens "Add", fills Code="App" + names, and clicks "Save"
  Then POST /account/api/admin/ratings/types returns HTTP 409 with Error.Code = "RATING_TYPE_CODE_TAKEN"
  And the message is "A rating type with this code already exists." / "يوجد نوع تقييم بهذا الرمز بالفعل."
  And the modal stays open with a red toast
```

### E2E-RCFG-010 — Auth gate

```gherkin
Scenario: Signed-in admin lacking RatingConfig.View is denied the page
  Given a signed-in admin whose roles grant no RatingConfig.View permission
  When they navigate to /admin/rating-config
  Then they land on /not-permitted with HTTP 200
  And no POST /account/api/admin/ratings/types/list request fires
  And the "Rating configuration" / "إعداد التقييمات" nav item is not shown for them
```

### E2E-RCFG-015 — RTL / Arabic render

```gherkin
Scenario: Arabic toggle mirrors the page and the three modals
  Given the administrator is on /admin/rating-config in English
  When they switch the UI language to العربية
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "إعداد التقييمات"
  And the types headers read "الرمز", "الاسم", "النطاق", "مدمج", "المجموعات", "الأسئلة", "الردود", "نشط", "إجراءات"
  And opening "Add" shows the type modal in RTL with the Scope options
    "عام (مرة واحدة لكل مستخدم)" / "لكل جلسة"
```

### E2E-RCFG-016 — Client validation (empty submit)

> **BUG-004 (as-built).** The page-level toast is rendered inside
> `.simf-surface`, which sits under the modal backdrop
> (`.simf-modal { position: fixed; inset: 0; z-index: 100 }`), so a rejected save
> was invisible while a dialog was open and Save read as a dead button. All three
> dialogs now render a dedicated `_error` in the dialog body, and a blank required
> field is caught client-side before the request goes out. `Code` is required on
> Create only — it is locked (disabled) in Edit.

```gherkin
Scenario: Saving the Add-type dialog empty reports, and creates nothing
  Given the "Add rating type" modal is open with every field empty
  When the administrator clicks "Save" without typing anything
  Then a red SimfAlert renders INSIDE the dialog body (.simf-modal__body) reading
      "A code and both names (English and Arabic) are required." /
      "الرمز والاسمان (الإنجليزي والعربي) مطلوبة."
  And the modal stays open
  And no POST /account/api/admin/ratings/types request fires
  And the types grid row count is unchanged
  And closing and re-opening the dialog clears the message

Scenario: Saving the Add-group dialog empty reports, and creates nothing
  Given a type is selected via "Manage" and the "Add group" modal is open, all fields empty
  When the administrator clicks "Save"
  Then a red SimfAlert renders INSIDE the dialog body reading
      "Both group names (English and Arabic) are required." /
      "اسما المجموعة (الإنجليزي والعربي) مطلوبان."
  And no POST /account/api/admin/ratings/groups request fires

Scenario: Saving the Add-question dialog empty reports, and creates nothing
  Given a type is selected and the "Add question" modal is open, all fields empty
  When the administrator clicks "Save"
  Then a red SimfAlert renders INSIDE the dialog body reading
      "Both question texts (English and Arabic) are required." /
      "نصّا السؤال (الإنجليزي والعربي) مطلوبان."
  And no POST /account/api/admin/ratings/questions request fires
```

---

### E2E-RCFG-017 — Details on the group and question grids (D-835)

```gherkin
Scenario: A read-only admin reads one question group and one question
  Given a signed-in admin holding RatingConfig.View but not RatingConfig.Edit
  And the rating type "Session feedback" is selected
  And it holds the group "Delivery" / "الإلقاء" at display order 2 with 3 questions
  When they click "Details" on the "Delivery" row
  Then a read-only dialog opens titled "Delivery"
  And it shows Name, Name (AR), Display order, Questions, Created
        and the active/inactive pill
  And the Created date is visible here and in no grid column
  And it renders in Saudi local time, 12-hour, as dd-MM-yyyy hh:mm tt

  When they close it and click "Details" on the question
        "Was the session clear?" / "هل كانت الجلسة واضحة؟"
  Then a read-only dialog opens showing Question, Question (AR), Display order,
        Required (Yes/No), Created and the active/inactive pill
  And NO request fires for either dialog - both render from the rows the grids hold
  And neither dialog offers a Save or Delete control
  # Before D-835 both inner grids rendered an empty actions column for this admin.

Scenario: Details on the rating type itself reveals what no column shows
  Given the same read-only admin on /admin/rating-config
  And the rating type "Session feedback" allows a comment labelled
        "Anything else?" / "هل من شيء آخر؟" and shows overall stars
  Then the types grid shows no Add, Edit or Delete
  And it still shows the ungated "Manage" row action
  When they click "Details" on the "Session feedback" row
  Then a read-only dialog shows Code, Name, Name (AR), Scope,
        Overall star rating, Comment allowed, Comment label, Comment label (AR),
        Display order, Groups, Questions, Responses, Created and the status pill
  And the Arabic name and both comment labels are visible here and in no column
  # The types grid was first treated as needing no Details, on the grounds that
  # "Manage" already opens the row. The review pass rejected that: opening is not
  # reading - seven fields on AdminRatingTypeSummary have no column at all.
```

## Implementation notes

- **Manual smoke is the canonical run today** (Chrome DevTools MCP). Keep the Gherkin
  runner-agnostic.
- **System types** are seeded by `RatingSeeder` (App / Event / Exhibition = Global
  stars+comment; Session = PerSession with default non-required Speaker/Sound/Light
  questions; **Day = PerDay**, D-679). They can't be deleted and their `Code`/`Scope`
  are locked.
- **Scope options (D-679):** the type modal's Scope select now offers three values —
  `Global (once per user)`, `Per session`, and **`Per programme day`** (`RatingScope.PerDay`)
  — and the types grid's Scope column labels each via a switch (a PerDay type no longer
  mislabels as "Global").
- **Group delete is `SetNull`** — deactivating/deleting a group leaves its questions as
  flat (ungrouped), never cascade-deletes them.
- **API integration tests** at `tests/SIMF.Api.Tests/RatingConfigTests.cs` cover the
  same type/group/question CRUD + the system-delete-block + duplicate-code + the
  permission gate at a lower layer.
- **Permission gates** (HARD RULE): page `[RequirePermission(PermissionCatalog.RatingConfig.View)]`;
  each API endpoint `Policies(PermissionCatalog.PolicyFor(PermissionCatalog.RatingConfig.{View|Create|Edit|Delete}), …RequireApprovedAccount)`;
  `CpNavigation` item `Module.RatingConfig` → `RequiredPermission: RatingConfig.View`.

---

_Last reviewed:_ 2026-06-25 by Claude (D-496 dynamic ratings).
_Last reviewed:_ 2026-07-26 by Claude (BUG-004): all three dialogs' validation messages now render inside the dialog body instead of behind the backdrop; reworded E2E-RCFG-012 and added E2E-RCFG-016 (empty submit).
