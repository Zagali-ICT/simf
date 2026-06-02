# E2E test catalogue — AI prompts catalogue (`/admin/ai/prompts`)

| | |
|--|--|
| **Page** | [`cp/admin-ai-prompts.md`](../../pages/cp/admin-ai-prompts.md) _(reference doc not yet authored — grounded directly in `AiPromptsList.razor`)_ |
| **Route** | `/admin/ai/prompts` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Page permission:** `@attribute [RequirePermission(PermissionCatalog.AiPrompts.View)]`.
> Each row action is gated by its own permission at the API:
> `AiPrompts.Create` (POST), `AiPrompts.Edit` (PUT), `AiPrompts.Delete`
> (DELETE / "Deactivate"), `AiPrompts.Test` (POST `/test`). All are
> `AdminOnly` baseline (`Administrator = "*"` satisfies every one).
> The Create / Update / Delete / Test endpoints also require the
> `RequireApprovedAccount` policy, and Create/Update/Delete sit behind the
> per-IP `auth` rate-limit while Test sits behind the per-admin `ai-test`
> limiter (D-179).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-AIP-001 | Golden round-trip — New → Edit → Test → Deactivate one prompt | happy | P0 | _to author_ |
| E2E-AIP-002 | Empty list renders `SimfEmptyState` ("No AI prompts yet.") | happy | P1 | _to author_ |
| E2E-AIP-003 | Auth gate: signed-in admin lacking `AiPrompts.View` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-AIP-004 | Create: invalid `Key` (not kebab-case) → 400 `AI_PROMPT_INVALID` bilingual toast | error | P1 | _to author_ |
| E2E-AIP-005 | Create: blank `Display name` → 400 `AI_PROMPT_INVALID` bilingual toast | error | P1 | _to author_ |
| E2E-AIP-006 | Create: `Temperature` out of range (3.0) → 400 `AI_PROMPT_INVALID` | error | P2 | _to author_ |
| E2E-AIP-007 | Duplicate `Key` → 409 `AI_PROMPT_KEY_DUPLICATE` bilingual toast | error | P1 | _to author_ |
| E2E-AIP-008 | Edit: `Key` field disabled (immutable), pre-filled values, toggle `Active` off | happy | P1 | _to author_ |
| E2E-AIP-009 | Test (Echo provider): `key=value` inputs → output + latency + tokens | happy | P0 | _to author_ |
| E2E-AIP-010 | Test (OpenAi provider, no key) → 503 `AI_PROVIDER_NOT_CONFIGURED` toast | resilience | P2 | _to author_ |
| E2E-AIP-011 | Deactivate ("Delete" button) → soft-deactivate, row pill flips to "—" | happy | P1 | _to author_ |
| E2E-AIP-012 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-AIP-013 | RTL render: Arabic toggle mirrors page + Add/Test modals | i18n | P1 | _to author_ |
| E2E-AIP-014 | Pager summary line reads "Showing X–Y of Z" | happy | P2 | _to author_ |

## Scenarios

### E2E-AIP-001 — Golden round-trip (New → Edit → Test → Deactivate)

```gherkin
Feature: AI prompt catalogue round-trip
  As an Administrator
  I want to create, edit, dry-run and deactivate an AI prompt from one place
  So that all AI behaviour on SIMF is managed dynamically without a redeploy

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp (Get-Totp helper)
  And they have landed on /admin/ai/prompts
  And the page issued POST /account/api/admin/ai/prompts/list and rendered the grid

Scenario: Create, edit, test and deactivate one prompt
  Given the grid currently shows {N} rows
  When the administrator clicks "New prompt"
  Then the Create modal opens titled "Create AI prompt"
  And the modal shows: Key, Feature (select), Display name (English),
      Display name (Arabic), Provider (select), Model, System prompt (textarea),
      User prompt template (textarea), Temperature (number), Max output tokens
      (number), and an "Active" checkbox
  And the "Key" field is enabled (it is only disabled in edit mode)
  And the Provider select defaults to "Echo" and Model defaults to "echo"
  And Temperature defaults to "0.2" and Max output tokens defaults to "512"

  When they fill Key="welcome-greeting"
  And they select Feature="Assistance"
  And they fill Display name (English)="Welcome greeting"
  And they fill Display name (Arabic)="رسالة الترحيب"
  And they leave Provider="Echo" and Model="echo"
  And they fill System prompt="You are a friendly SIMF concierge."
  And they fill User prompt template="Greet the visitor named {name}."
  And they leave Temperature="0.2" and Max output tokens="512"
  And they click "Save"
  Then the BFF forwards POST /account/api/admin/ai/prompts and the API returns 200
  And the modal closes
  And a green SimfAlert reads "Prompt saved."
  And the grid reloads (POST /account/api/admin/ai/prompts/list) showing {N + 1} rows
  And a row exists with Key="welcome-greeting", Feature="Assistance",
      Name="Welcome greeting", Provider="Echo", Model="echo", Version="v1",
      and the Active column shows "✓"

  When the administrator clicks "Edit" on that row
  Then GET /account/api/admin/ai/prompts/{id} returns 200
  And the Edit modal opens titled "Edit AI prompt" with values pre-filled
  And the "Key" field is now disabled (Key is immutable once written)
  When they change System prompt="You are a concise SIMF concierge."
  And they click "Save"
  Then PUT /account/api/admin/ai/prompts/{id} returns 200
  And a green SimfAlert reads "Prompt saved."
  And the row's Version column now reads "v2"

  When the administrator clicks "Test" on that row
  Then the Test modal opens titled "Test prompt" showing the key "welcome-greeting"
  When they type inputs (one per line) "name=Captain Ahmad"
  And they click "Run test"
  Then POST /account/api/admin/ai/prompts/{id}/test returns 200
  And a description list shows Output, Latency (e.g. "3ms") and Tokens
  And because Provider="Echo" the Output deterministically echoes the rendered prompt
  When they click "Cancel"
  Then the Test modal closes

  When the administrator clicks "Deactivate" on that row
  Then DELETE /account/api/admin/ai/prompts/{id} returns 200
  And a green SimfAlert reads "Prompt deactivated."
  And the grid reloads and the row's Active column now shows "—"
```

**Evidence captured:**
- Screenshots: `docs/screenshots/cp-admin-ai-prompts-{grid,create-modal,edit-modal,test-modal,after-deactivate}.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/ai/prompts/*` call returns 200
  (`/list` POST, create POST, `{id}` GET, `{id}` PUT, `{id}/test` POST, `{id}` DELETE)
- Audit rows: `OperationLog` (or audit sink) rows with `Event = 'AiPrompt.Created'`,
  `'AiPrompt.Updated'` (carrying `contentHashOld`/`contentHashNew`/`contentChanged`),
  and `'AiPrompt.Deactivated'`, each with the actor's id. The edit also writes an
  `AiPromptHistory` snapshot of the pre-mutation v1 content.

### E2E-AIP-002 — Empty list

```gherkin
Scenario: Empty list renders SimfEmptyState
  Given the database has no active AiPrompt rows
  When the administrator opens /admin/ai/prompts
  Then POST /account/api/admin/ai/prompts/list returns 200 with Total = 0
  And the grid body renders the SimfEmptyState component
  And the empty state title reads "No AI prompts yet." / "لا توجد محفّزات بعد."
  And the "New prompt" button is still visible in the toolbar
  And no summary line is rendered
```

### E2E-AIP-003 — Auth gate

```gherkin
Scenario: Signed-in admin lacking AiPrompts.View is denied
  Given a signed-in Control Panel user whose role does NOT grant AiPrompts.View
      (i.e. not Administrator and without that permission baked into the JWT)
  When they navigate to /admin/ai/prompts
  Then the [RequirePermission(PermissionCatalog.AiPrompts.View)] gate redirects
      them to /not-permitted with HTTP 200
  And no POST /account/api/admin/ai/prompts/list request fires
  And the "AI prompts" nav item is hidden for them (CpNavigation RequiredPermission
      = AiPrompts.View)
```

### E2E-AIP-004 — Invalid Key (not kebab-case)

```gherkin
Scenario: Non-kebab Key returns 400 AI_PROMPT_INVALID
  Given the Create modal is open
  When the administrator fills Key="Welcome Greeting" (spaces + capitals)
  And fills the remaining required fields with valid values
  And clicks "Save"
  Then the BFF forwards POST /account/api/admin/ai/prompts
  And the API returns HTTP 400 with ApiResult.Error.Code = "AI_PROMPT_INVALID"
  And the error toast surfaces the bilingual MessageForCurrentCulture()
      "Key must be 2–64 chars, kebab-case (a-z, 0-9, -)." /
      "يجب أن يكون المفتاح بين 2 و 64 محرفاً، بصيغة kebab."
  And the modal stays open with the field values intact
```

### E2E-AIP-005 — Blank Display name

```gherkin
Scenario: Blank Display name returns 400 AI_PROMPT_INVALID
  Given the Create modal is open
  When the administrator fills Key="blank-name-test"
  And leaves Display name (English) blank
  And fills the other required fields
  And clicks "Save"
  Then POST /account/api/admin/ai/prompts returns HTTP 400
  And ApiResult.Error.Code = "AI_PROMPT_INVALID"
  And the error toast reads
      "DisplayName must be between 1 and 128 characters." /
      "يجب أن يتراوح طول DisplayName بين 1 و 128 محرفاً."
  And the modal stays open
```

### E2E-AIP-006 — Temperature out of range

```gherkin
Scenario: Temperature above 2.0 returns 400 AI_PROMPT_INVALID
  Given the Create modal is open
  And all text fields hold valid values (Key="temp-range-test")
  When the administrator sets Temperature="3.0"
  And clicks "Save"
  Then POST /account/api/admin/ai/prompts returns HTTP 400
  And ApiResult.Error.Code = "AI_PROMPT_INVALID"
  And the error toast reads
      "Temperature must be between 0 and 2." /
      "يجب أن تكون درجة الحرارة بين 0 و 2."
  And the modal stays open
```

### E2E-AIP-007 — Duplicate Key

```gherkin
Scenario: Duplicate Key returns 409 AI_PROMPT_KEY_DUPLICATE
  Given an active AiPrompt with Key="welcome-greeting" already exists
  When the administrator opens the Create modal
  And fills Key="welcome-greeting" plus all other valid fields
  And clicks "Save"
  Then POST /account/api/admin/ai/prompts returns HTTP 409
  And ApiResult.Error.Code = "AI_PROMPT_KEY_DUPLICATE"
  And the error toast surfaces the bilingual server message
      "AI prompt key 'welcome-greeting' is already in use." /
      "مفتاح المحفّز 'welcome-greeting' مستخدم بالفعل."
  And the modal stays open
```

### E2E-AIP-008 — Edit modal: Key immutable + Active toggle

```gherkin
Scenario: Edit pre-fills values, disables Key, and toggles Active off
  Given an active prompt "welcome-greeting" exists in the grid
  When the administrator clicks "Edit" on its row
  Then GET /account/api/admin/ai/prompts/{id} returns 200
  And the Edit modal opens with every field pre-filled from the detail payload
  And the "Key" field is disabled (immutable; the update request has no Key field)
  And the "Active" checkbox is ticked
  When they untick "Active"
  And click "Save"
  Then PUT /account/api/admin/ai/prompts/{id} returns 200 with IsActive=false
  And a green SimfAlert reads "Prompt saved."
  And on reload the row's Active column shows "—" and Version increments by 1
```

### E2E-AIP-009 — Test prompt against Echo provider

```gherkin
Scenario: Dry-run an Echo prompt returns a deterministic output
  Given an active prompt "welcome-greeting" with Provider="Echo" exists
  When the administrator clicks "Test" on its row
  Then the Test modal opens titled "Test prompt" and shows the key "welcome-greeting"
  And the inputs textarea label reads "Inputs (one per line: key=value)"
  When they type "name=Captain Ahmad"
  And click "Run test"
  Then POST /account/api/admin/ai/prompts/{id}/test returns 200 (ApiResult success)
  And a description list renders Output, Latency ("{N}ms") and Tokens ("in / out")
  And the Echo provider's Output deterministically reflects the rendered template
  And one AiInvocation row is recorded (CallerKind="Admin") visible later under
      /admin/ai/invocations
```

### E2E-AIP-010 — Test against an unconfigured provider

```gherkin
Scenario: Testing an OpenAi prompt with no API key returns 503
  Given an active prompt "live-assist" with Provider="OpenAi" exists
  And the API has no OpenAi key configured (default dev posture)
  When the administrator clicks "Test" on its row, enters any inputs, and clicks "Run test"
  Then POST /account/api/admin/ai/prompts/{id}/test returns HTTP 503
  And ApiResult.Error.Code = "AI_PROVIDER_NOT_CONFIGURED"
  And a red error toast surfaces the bilingual MessageForCurrentCulture()
  And the Test modal stays open with no Output description list
```

### E2E-AIP-011 — Deactivate is a soft delete

```gherkin
Scenario: "Deactivate" soft-deactivates and is idempotent
  Given an active prompt "welcome-greeting" exists in the grid
  When the administrator clicks "Deactivate" on its row
  Then DELETE /account/api/admin/ai/prompts/{id} returns 200 (ApiResult<bool> = true)
  And a green SimfAlert reads "Prompt deactivated." / "تمّ تعطيل المحفّز."
  And the grid reloads and the row's Active column shows "—" (row not removed)
  And an audit row 'AiPrompt.Deactivated' is written with the actor id
  When the administrator clicks "Deactivate" again on the now-inactive row
  Then the API returns 200 with no further audit row (the service early-returns
      when IsActive is already false)
```

### E2E-AIP-012 — Server 500 on /list

```gherkin
Scenario: API 500 on /list shows the bilingual fallback toast
  Given the API is configured to return 500 on /admin/ai/prompts/list (e.g. DB down)
  When the administrator opens /admin/ai/prompts
  Then the page first shows the "Loading…" text
  And then a red SimfAlert appears reading
      "Could not load AI prompts." / "تعذّر تحميل قائمة المحفّزات."
  And no rows render and no summary line appears
```

### E2E-AIP-013 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the page, Create modal and Test modal
  Given the administrator is on /admin/ai/prompts in English
  When they switch the UI culture to العربية from the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "محفّزات الذكاء الاصطناعي"
  And the toolbar button reads "محفّز جديد"
  And the table column headers read المفتاح / الميزة / الاسم / الموفّر / النموذج /
      الإصدار / مفعّل
  And the nav rail mirrors with Arabic labels

  When they click "محفّز جديد"
  Then the Create modal opens in RTL titled "إنشاء محفّز"
  And the field labels render in Arabic (e.g. "المفتاح (kebab-case، غير قابل للتعديل)",
      "محفّز النظام", "درجة الحرارة (0.0–2.0)")
  And the footer buttons read "حفظ" and "إلغاء" in reverse order

  When they cancel and click "اختبار" on a row
  Then the Test modal opens in RTL titled "اختبار المحفّز"
  And the inputs label reads "المدخلات (سطر لكلٍّ: key=value)"
  And the action button reads "تشغيل الاختبار"
```

### E2E-AIP-014 — Pager summary line

```gherkin
Scenario: Summary line reflects the current page window
  Given the database has more than 25 active prompts (Top defaults to 25)
  When the administrator opens /admin/ai/prompts
  Then POST /account/api/admin/ai/prompts/list returns Skip=0, the first 25 items, Total=Z
  And the summary line under the table reads "Showing 1–25 of {Z}" /
      "عرض 1–25 من {Z}"
```

---

## Implementation notes

- **Manual smoke is canonical today.** Until a Playwright project exists, the
  canonical run is a Chrome DevTools MCP session: sign in via the Auth setup,
  walk each scenario, capture screenshots into
  `docs/screenshots/cp-admin-ai-prompts-{scenario}.png`. The Gherkin is
  runner-agnostic and converts 1:1 into `.feature` files under a future
  `tests/SIMF.E2E.Tests/`.
- **Lower-layer API integration tests** that cover this same surface without a
  browser live at `tests/SIMF.Api.Tests/AiModuleTests.cs` (CRUD + Echo dry-run +
  invocations log) and `tests/SIMF.Api.Tests/AiHardeningTests.cs` (D-179 input
  caps, the per-admin `ai-test` rate-limit, audit-detail redaction, and the
  provider-not-configured path). The `// Tests:` header on
  `AiPromptAdminEndpoints.cs` still references a file named `AiAdminTests.cs`,
  which does not exist on disk under that name — the equivalent coverage is in
  `AiModuleTests.cs` + `AiHardeningTests.cs`. Worth reconciling that header in a
  separate change.
- **Permission gates** are enforced twice: the API endpoint `Policies(...)` and
  the CP page `[RequirePermission]`. `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs`
  and `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` fail the build if a
  gate is missing, so E2E-AIP-003 has a build-time backstop for the page gate.
- **Sibling page:** the AI invocations log lives at `/admin/ai/invocations`
  (`AiInvocations.View`); it is out of scope here and has its own catalogue.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
