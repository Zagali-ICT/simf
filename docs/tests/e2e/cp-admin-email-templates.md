# E2E test catalogue — Email templates (`/admin/email/templates`)

| | |
|--|--|
| **Page** | [`cp/email-templates.md`](../../pages/cp/email-templates.md) |
| **Route** | `/admin/email/templates` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-07-10 (D-735 — Email templates admin editor) |

> **Page permission:** `@attribute [RequirePermission(PermissionCatalog.EmailTemplates.View)]`.
> The **list / detail / preview** reads are gated `EmailTemplates.View`; the
> **Save (PUT)** and **Reset** actions are gated `EmailTemplates.Edit`. Both are
> `AdminOnly` baseline (`Administrator = "*"` satisfies each). Every endpoint also
> requires the `RequireApprovedAccount` policy. All responses use the
> `ApiResult<T>` envelope; `{type}` in a route is an `EmailTemplateType` **name**
> (`SignInOtp`, `EmailVerification`, `AccountExists`, `PasswordReset`,
> `BadgeActivation`, `BiometricStepUp`).
>
> **Model.** The `EmailTemplate` table stores **overrides only**. A code
> **catalogue** supplies the built-in default subject + bilingual body for each of
> the six types, so the table starts **empty** and the resolver always falls back
> to the catalogue default when no override row exists. Editing writes/updates the
> override (and bumps `Version`); **Reset** deletes the override so the email
> reverts to the built-in copy. Tokens are single-brace placeholders — `{Code}`,
> `{ExpiryMinutes}` — and each type declares its own allowed token set in the
> catalogue.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-EMT-001 | Golden path — open SignInOtp → insert `{Code}` chip → preview renders `123456` → Save → `Version` bumps + grid shows "Customised" | happy | P0 | _to author_ |
| E2E-EMT-002 | List shows all six built-in templates with the `IsOverride` flag ("Default" when the override table is empty) | happy | P1 | _to author_ |
| E2E-EMT-003 | Auth gate: anonymous visitor → login redirect / 401 on the API | auth | P0 | _to author_ |
| E2E-EMT-004 | Auth gate: signed-in admin lacking `EmailTemplates.View` → `/not-permitted` (403) | auth | P0 | _to author_ |
| E2E-EMT-005 | Token chip: clicking the `{ExpiryMinutes}` chip inserts the token at the cursor of the focused body field | happy | P1 | _to author_ |
| E2E-EMT-006 | Live bilingual preview substitutes the sample values (`Code=123456`, `ExpiryMinutes=10`) into the EN + AR body | happy | P0 | _to author_ |
| E2E-EMT-007 | Save BLOCKED on an unknown `{Foo}` placeholder → 400 `EMAIL_TEMPLATE_INVALID` bilingual toast; preview reports it under `UnknownTokens` | error | P0 | _to author_ |
| E2E-EMT-008 | Save BLOCKED on an empty body → 400 `EMAIL_TEMPLATE_INVALID` bilingual toast | error | P1 | _to author_ |
| E2E-EMT-009 | Reset-to-default removes the override → the email reverts to the built-in copy, the grid flips to "Default", `Version` clears | happy | P0 | _to author_ |
| E2E-EMT-010 | Invalid `{type}` in the route → 404 `EMAIL_TEMPLATE_NOT_FOUND` | error | P1 | _to author_ |
| E2E-EMT-011 | Server 500 on `/list` → bilingual fallback toast, no rows | resilience | P2 | _to author_ |
| E2E-EMT-012 | RTL render: the Arabic body field + the live preview mirror to RTL | i18n | P1 | _to author_ |
| E2E-EMT-013 | Edit-permission gate: an admin with `EmailTemplates.View` but not `.Edit` sees a read-only editor — Save / Reset fire 403 | auth | P1 | _to author_ |

## Scenarios

### E2E-EMT-001 — Golden path (open SignInOtp → insert token → preview → Save)

```gherkin
Feature: Email template editor round-trip
  As an Administrator
  I want to override the built-in copy of a transactional identity email
  So that the six system emails read in our own words without a redeploy

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator has signed in via /login + /login/totp (Get-Totp helper)
  And they have landed on /admin/email/templates
  And the page issued POST /account/api/admin/email/templates/list and rendered
      the grid of the six template types

Scenario: Customise the sign-in OTP email and see the version bump
  Given the grid shows six rows and the "SignInOtp" row's Override column reads
      "Default" / "افتراضي" (no override exists yet)
  When the administrator opens the "SignInOtp" row's editor
  Then GET /account/api/admin/email/templates/SignInOtp returns 200 with the
      AdminEmailTemplateDetail (built-in Subject, bilingual Body, IsOverride=false,
      Version empty, and the allowed-token list {Code}, {ExpiryMinutes})
  And the editor shows the Subject (English) + Subject (Arabic), the Body (English)
      + Body (Arabic) fields, a row of token chips ({Code}, {ExpiryMinutes}), a live
      bilingual Preview panel, a "Save" button and a "Reset to default" button

  When they place the cursor in the Body (English) field and click the "{Code}" chip
  Then the token "{Code}" is inserted at the cursor position in the Body (English)
  And the Preview panel re-renders (POST /account/api/admin/email/templates/SignInOtp/preview)
      showing the sample value "123456" where "{Code}" appears

  When they edit Subject (English)="Your SIMF sign-in code"
  And they edit Body (English)="Your code is {Code}. It expires in {ExpiryMinutes} minutes."
  And they click "Save"
  Then PUT /account/api/admin/email/templates/SignInOtp returns 200 with the updated
      AdminEmailTemplateDetail (IsOverride=true, Version incremented)
  And a green SimfAlert reads "Template saved." / "تم حفظ القالب."
  And the grid reloads (POST /account/api/admin/email/templates/list) and the
      "SignInOtp" row's Override column now reads "Customised" / "مخصّص"
```

**Evidence captured:**
- Screenshots: `docs/screenshots/cp-admin-email-templates-{grid,editor,preview,after-save}.png`
- Console errors: 0 expected
- Network: every `/account/api/admin/email/templates/*` call returns 200
  (`/list` POST, `SignInOtp` GET, `SignInOtp/preview` POST, `SignInOtp` PUT)
- Audit row: an `OperationLog` row for the template override write with the actor's id.

### E2E-EMT-002 — List shows the six built-in templates with the override flag

```gherkin
Scenario: A fresh install lists six templates, all "Default"
  Given the EmailTemplate override table is empty
  When the administrator opens /admin/email/templates
  Then POST /account/api/admin/email/templates/list returns 200 with Total = 6
  And the grid renders exactly six rows — one per EmailTemplateType: SignInOtp,
      EmailVerification, AccountExists, PasswordReset, BadgeActivation, BiometricStepUp
  And every row's Override column reads "Default" / "افتراضي" (IsOverride=false)
  And no row can be created or deleted (the set is fixed — the toolbar exposes no
      Add/Delete; only per-row open-editor is offered)
  And no error toast appears
```

### E2E-EMT-003 — Auth gate (anonymous)

```gherkin
Scenario: An anonymous visitor cannot reach the page or the API
  Given no Control Panel session cookie is present
  When an anonymous client navigates to /admin/email/templates
  Then the CP redirects to /login (the page never renders the grid)
  And a direct POST /account/api/admin/email/templates/list without a session
      is rejected 401 (the BFF forwards no bearer)
```

### E2E-EMT-004 — Auth gate (signed-in admin lacking the permission)

```gherkin
Scenario: Signed-in admin without EmailTemplates.View is denied
  Given a signed-in Control Panel user whose role does NOT grant EmailTemplates.View
      (i.e. not Administrator and without that permission baked into the JWT)
  When they navigate to /admin/email/templates
  Then the [RequirePermission(PermissionCatalog.EmailTemplates.View)] gate redirects
      them to /not-permitted with HTTP 200
  And no POST /account/api/admin/email/templates/list request fires
  And the "Email templates" nav item is hidden for them (CpNavigation RequiredPermission
      = EmailTemplates.View)
```

### E2E-EMT-005 — Token chip insertion

```gherkin
Scenario: Clicking a token chip inserts the placeholder at the cursor
  Given the "PasswordReset" editor is open and its allowed tokens are {Code}, {ExpiryMinutes}
  When the administrator focuses the Body (English) field and positions the cursor
      after the word "in "
  And clicks the "{ExpiryMinutes}" chip
  Then the token "{ExpiryMinutes}" is spliced into the Body (English) at the cursor
      (not appended to the end, not into the Arabic field)
  And the Preview re-renders with the sample "10" where "{ExpiryMinutes}" appears
  And clicking the "{Code}" chip while the Body (Arabic) field is focused inserts
      "{Code}" into the Arabic field instead
```

### E2E-EMT-006 — Live bilingual preview

```gherkin
Scenario: The preview substitutes sample values into both languages
  Given the "EmailVerification" editor is open
  And the Body (English) reads "Confirm your email with {Code} (valid {ExpiryMinutes} min)."
  And the Body (Arabic) reads "أكّد بريدك بالرمز {Code} (صالح {ExpiryMinutes} دقيقة)."
  When the preview refreshes
  Then POST /account/api/admin/email/templates/EmailVerification/preview returns 200
      with EmailTemplatePreviewResult { Subject, HtmlBody, UnknownTokens = [] }
  And the English preview shows "Confirm your email with 123456 (valid 10 min)."
  And the Arabic preview shows "أكّد بريدك بالرمز 123456 (صالح 10 دقيقة)."
  And no unknown-token warning banner is shown (UnknownTokens is empty)
```

### E2E-EMT-007 — Save blocked on an unknown placeholder

```gherkin
Scenario: A body referencing {Foo} is refused with EMAIL_TEMPLATE_INVALID
  Given the "SignInOtp" editor is open (allowed tokens {Code}, {ExpiryMinutes})
  When the administrator types Body (English)="Your code is {Code}, ref {Foo}."
  Then the live preview reports UnknownTokens = ["Foo"] and shows the inline
      "unknown placeholder" warning, and the "Save" button is disabled client-side
  When they force the request anyway (or clear the client guard) and Save fires
  Then PUT /account/api/admin/email/templates/SignInOtp returns HTTP 400
  And ApiResult.Error.Code = "EMAIL_TEMPLATE_INVALID"
  And the error toast surfaces the bilingual MessageForCurrentCulture()
      "The template body references an unknown placeholder: {Foo}." /
      "يشير نص القالب إلى عنصر نائب غير معروف: {Foo}."
  And no override row is written (the grid still reads "Default" for SignInOtp)
  And the editor stays open with the field values intact
```

### E2E-EMT-008 — Save blocked on an empty body

```gherkin
Scenario: An empty body is refused with EMAIL_TEMPLATE_INVALID
  Given the "AccountExists" editor is open
  When the administrator clears the Body (English) field entirely
  And clicks "Save"
  Then PUT /account/api/admin/email/templates/AccountExists returns HTTP 400
  And ApiResult.Error.Code = "EMAIL_TEMPLATE_INVALID"
  And the error toast reads
      "The template body cannot be empty." /
      "لا يمكن أن يكون نص القالب فارغاً."
  And the editor stays open
```

### E2E-EMT-009 — Reset-to-default removes the override

```gherkin
Scenario: Reset deletes the override and reverts to the built-in copy
  Given the "SignInOtp" template has a saved override (from E2E-EMT-001) and the
      grid row reads "Customised" / "مخصّص" with Version incremented
  When the administrator opens the "SignInOtp" editor and clicks "Reset to default"
  Then a SimfConfirm dialog gates the action (confirm "Reset" / cancel "Cancel")
  When they confirm
  Then POST /account/api/admin/email/templates/SignInOtp/reset returns 200 with the
      AdminEmailTemplateDetail now carrying the built-in Subject + Body,
      IsOverride=false and an empty Version
  And a green SimfAlert reads "Reset to default." / "تمت الإعادة إلى الافتراضي."
  And the grid reloads and the "SignInOtp" row's Override column reads "Default"
      / "افتراضي" again
  And a subsequent GET returns the catalogue default copy (the override row is gone)
```

### E2E-EMT-010 — Invalid template type → 404

```gherkin
Scenario: An unrecognised {type} returns EMAIL_TEMPLATE_NOT_FOUND
  Given an Administrator is signed in
  When a GET /account/api/admin/email/templates/NotARealType is issued
      (e.g. a hand-edited deep link)
  Then the API returns HTTP 404 with ApiResult.Error.Code = "EMAIL_TEMPLATE_NOT_FOUND"
  And the CP shows the bilingual "not found" toast and returns to the grid
  And the same 404 is returned for a PUT / reset / preview against an unknown {type}
```

### E2E-EMT-011 — Server 500 on /list

```gherkin
Scenario: API 500 on /list shows the bilingual fallback toast
  Given the API is configured to return 500 on /admin/email/templates/list (e.g. DB down)
  When the administrator opens /admin/email/templates
  Then the page first shows the "Loading…" text
  And then a red SimfAlert appears reading
      "Could not load email templates." / "تعذّر تحميل قوالب البريد."
  And no rows render
```

### E2E-EMT-012 — RTL render

```gherkin
Scenario: The Arabic locale mirrors the page, the editor and the preview
  Given the administrator is on /admin/email/templates in English
  When they switch the UI culture to العربية from the header
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title, the grid column headers (Template / Override) and the
      nav rail render in Arabic and mirror to RTL
  When they open the "PasswordReset" editor
  Then the editor opens in RTL: the Body (Arabic) textarea is right-aligned and the
      Arabic preview renders right-to-left
  And the token chips ({Code}, {ExpiryMinutes}) still insert the single-brace
      literal placeholder unchanged regardless of locale
  And the "Save" / "Reset to default" buttons read "حفظ" / "إعادة إلى الافتراضي"
```

### E2E-EMT-013 — Edit-permission gate (View but not Edit)

```gherkin
Scenario: An admin with only EmailTemplates.View gets a read-only editor
  Given a signed-in Control Panel user whose role grants EmailTemplates.View but
      NOT EmailTemplates.Edit
  When they open /admin/email/templates
  Then the grid + editor load (the View-gated reads succeed) and the live preview works
  But the "Save" and "Reset to default" actions are hidden / disabled
      (each is wrapped in <AuthorizedAction Permission="EmailTemplates.Edit">)
  When a PUT /account/api/admin/email/templates/SignInOtp is forced anyway
  Then the API returns HTTP 403 (the Edit policy denies it)
  And a POST /account/api/admin/email/templates/SignInOtp/reset is likewise 403
```

---

## Implementation notes

- **Manual smoke is canonical today.** Until a Playwright project exists, the
  canonical run is a Chrome DevTools MCP session: sign in via the Auth setup, walk
  each scenario, capture screenshots into
  `docs/screenshots/cp-admin-email-templates-{scenario}.png`. The Gherkin is
  runner-agnostic and converts 1:1 into `.feature` files under a future
  `tests/SIMF.E2E.Tests/`.
- **Overrides-only + fallback.** The `EmailTemplate` table holds nothing on a fresh
  install; the resolver falls back to the code catalogue default for every type.
  That is why E2E-EMT-002 lists six rows over an empty table, and E2E-EMT-009 proves
  Reset restores the built-in copy by **deleting** the override rather than writing a
  copy of the default back in.
- **Unknown-token guard is enforced twice.** The editor disables Save when the
  preview's `UnknownTokens` is non-empty (client guard), and the PUT re-validates
  server-side (400 `EMAIL_TEMPLATE_INVALID`) — E2E-EMT-007 exercises the server path.
  The allowed token set is per-type from the catalogue, so `{Code}`/`{ExpiryMinutes}`
  are valid on the code-delivery templates but `{Foo}` never is.
- **Permission gates** are enforced twice: the API endpoint `Policies(...)` and the
  CP page `[RequirePermission]`. `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs`
  and `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` fail the build if a gate
  is missing, so E2E-EMT-004 (page gate) and E2E-EMT-013 (`.Edit` action gate) have a
  build-time backstop.
- **Lower-layer API integration tests** for this surface should cover: the six-row
  list over an empty table, GET fallback to the catalogue default, PUT version-bump +
  override persistence, PUT reject on unknown token + empty body, reset deletes the
  override, preview `UnknownTokens`, and the 404 on an invalid `{type}`.

---

_Last reviewed:_ 2026-07-10 by Claude (D-735 — Email templates admin editor): authored E2E-EMT-001..013 from the D-735 contract (six `EmailTemplateType` overrides, token chips + live bilingual preview + block-on-unknown-token + reset-to-default, `EmailTemplates.View`/`.Edit` split).
