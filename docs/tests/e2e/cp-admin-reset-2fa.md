# E2E test catalogue — Reset user 2FA (`/admin/reset-2fa`)

| | |
|--|--|
| **Page** | [`cp/admin-reset-2fa.md`](../../pages/cp/admin-reset-2fa.md) |
| **Route** | `/admin/reset-2fa` |
| **Surface** | Control Panel |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | `superadmin@simrsnf.com` + TOTP via the `Get-Totp` helper |
| **Last reviewed** | 2026-06-02 |

> **Page shape.** This is **not** a grid page. `ResetTwoFactor.razor` is a single
> form card (`simf-card simf-page-card`) with a supporting paragraph, two fields
> (**User email** + **Reason (audited)**), and one submit button (**Reset 2FA**).
> There is no search, no result list, no per-row action — the older reference doc
> describes a search-grid design the current page does **not** implement.
> Submit fires a native JS `confirm()` dialog, then `POST /account/api/admin/reset-2fa`.
> `RequiredPermission` = `PermissionCatalog.Admins.ResetTwoFactor` (`"Admins.ResetTwoFactor"`).

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-R2F-001 | Golden path — reset a normal user's 2FA → confirm → green success alert | happy | P0 | _to author_ |
| E2E-R2F-002 | Field clearing — typing in a field clears its inline error + the success alert | happy | P2 | _to author_ |
| E2E-R2F-003 | Confirm dialog — cancelling the `confirm()` aborts (no POST fires) | happy | P1 | _to author_ |
| E2E-R2F-004 | Auth gate — signed-in admin lacking `Admins.ResetTwoFactor` → `/not-permitted` | auth | P0 | _to author_ |
| E2E-R2F-005 | Validation — blank email → inline "Enter the user's email address." | error | P1 | _to author_ |
| E2E-R2F-006 | Validation — email without `@` → inline "Enter a valid email address." | error | P1 | _to author_ |
| E2E-R2F-007 | Validation — reason < 10 chars → inline "Enter a reason (10–500 characters)." | error | P1 | _to author_ |
| E2E-R2F-008 | Server guard — unknown email → 404 `AuthAccountNotFound` → red alert | error | P1 | _to author_ |
| E2E-R2F-009 | Server guard — target is the actor → 400 `AdminCannotResetSelf` → red alert | error | P0 | _to author_ |
| E2E-R2F-010 | Server guard — target is another Administrator → 400 `AdminCannotResetAdministrator` → red alert | error | P0 | _to author_ |
| E2E-R2F-011 | Server 500 on `/reset-2fa` → bilingual fallback alert | resilience | P2 | _to author_ |
| E2E-R2F-012 | RTL render — Arabic toggle mirrors the form card | i18n | P1 | _to author_ |
| E2E-R2F-ELS-001 | Element inventory — every control the page wires is present, accessibly named, and correctly gated (no selection: selection-gated buttons present **and disabled**; one row selected: they enable). Asserted in **LTR and RTL**, expected-vs-actual against `tools/qa/predicted_inventory.py`. | element | P1 | _to author_ |
| E2E-R2F-ELS-002 | Element health — no dead control, no broken image, and every same-origin link and asset returns < 400. Console reports zero errors and `scrollWidth == clientWidth` (no horizontal overflow). | element | P1 | _to author_ |

## Scenarios

### E2E-R2F-001 — Golden path

```gherkin
Feature: Reset another user's 2FA — golden path
  As an Administrator
  I want to wipe a user's authenticator, recovery codes and sessions
  So that a user who lost their phone can re-pair 2FA at next sign-in

Background:
  Given the API is reachable on http://localhost:5175
  And the Control Panel is reachable on http://localhost:5158
  And an Administrator with the "Admins.ResetTwoFactor" permission has signed in
    via /login + /login/totp (superadmin@simrsnf.com + Get-Totp helper)
  And they have landed on /admin/reset-2fa

Scenario: Reset a normal user's 2FA from email + reason
  Given a non-admin user "visitor.2fa@simf.test" exists with 2FA enrolled
    (authenticator key paired + recovery codes minted)
  And the page shows the SimfBanner title "Reset another user's 2FA"
  And the supporting paragraph reads "Use this only after verifying the user's identity out of band. Wipes the user's authenticator, recovery codes and active sessions, and forces them to set up 2FA again at their next sign-in."
  When the administrator fills "User email" = "visitor.2fa@simf.test"
  And they fill "Reason (audited)" = "User reported lost phone on a call from a known number 0555-123-456."
  And they click "Reset 2FA"
  Then a native confirm() dialog appears reading "This signs the user out of every session and forces them to set up 2FA again. Continue?"
  When they accept the confirm() dialog
  Then the button shows its loading label "Resetting"
  And the BFF forwards POST /account/api/admin/reset-2fa with { Email, Reason }
  And the API returns HTTP 200 with ApiResult.Success = true and Data = true
  And a green SimfAlert appears reading "2FA reset. The user has been notified by email and must set up 2FA again on next sign-in."
  And both fields are cleared (Email = "", Reason = "")
  And the target's TwoFactorEnabled is now false, authenticator key removed, recovery codes count = 0, security stamp rolled, refresh tokens revoked
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/cp-admin-reset-2fa-golden-before.png` (empty form)
- Screenshot after: `docs/screenshots/cp-admin-reset-2fa-golden-after.png` (green success alert + cleared fields)
- Console errors: 0 expected
- Network: the `/account/api/admin/reset-2fa` call returns 200; every supporting `/account/api/...` call returns 200
- Audit row: `OperationLog` row with `EventType = 'Admin.TwoFactorReset'`, `Outcome = Success`, `SubjectEmail = 'visitor.2fa@simf.test'`, the actor's `ActorUserId` (≠ subject), and `Detail` = the typed reason
- Side effect: target receives a `NotificationKind.AccountTwoFactorReset` in-app row (Severity = Warning) + an out-of-band email

### E2E-R2F-002 — Field clearing resets inline error + success

```gherkin
Scenario: Editing a field clears its own validation message and the success alert
  Given the administrator just completed a successful reset (green alert visible)
  When they start typing in the "User email" field
  Then the green success alert disappears
  And no error alert appears
  # ClearFieldError clears the touched field's messages + resets _success on every OnFieldChanged
```

### E2E-R2F-003 — Cancel the confirm dialog

```gherkin
Scenario: Cancelling the confirm() dialog aborts the reset
  Given the administrator has filled valid Email + Reason
  When they click "Reset 2FA"
  And they DISMISS the native confirm() dialog
  Then no POST /account/api/admin/reset-2fa request fires
  And the button is not in the loading state
  And the form keeps the entered values (Email + Reason unchanged)
  And no success or error alert appears
```

### E2E-R2F-004 — Auth gate

```gherkin
Scenario: Signed-in admin lacking the permission is denied
  Given a signed-in Control Panel user whose role does NOT grant "Admins.ResetTwoFactor"
    (and is not the wildcard Administrator)
  When they navigate to /admin/reset-2fa
  Then they land on /not-permitted with HTTP 200
  And the page form never renders
  And no POST /account/api/admin/reset-2fa request fires
```

### E2E-R2F-005 — Blank email

```gherkin
Scenario: Blank email shows the inline required error
  Given the form is empty
  When the administrator leaves "User email" blank
  And fills "Reason (audited)" = "Verified the caller against the staff directory."
  And clicks "Reset 2FA"
  Then an inline validation message appears under the email field reading "Enter the user's email address."
  And no confirm() dialog appears
  And no POST /account/api/admin/reset-2fa request fires
```

### E2E-R2F-006 — Email without @

```gherkin
Scenario: Malformed email shows the inline invalid error
  Given the form is empty
  When the administrator fills "User email" = "not-an-email"
  And fills "Reason (audited)" = "Verified identity in person at the desk."
  And clicks "Reset 2FA"
  Then an inline validation message appears under the email field reading "Enter a valid email address."
  And no confirm() dialog appears
  And no POST request fires
  # Client guard: _model.Email must contain '@' (StringComparison.Ordinal)
```

### E2E-R2F-007 — Reason too short

```gherkin
Scenario: A reason under 10 characters shows the inline required error
  Given the form is empty
  When the administrator fills "User email" = "visitor.2fa@simf.test"
  And fills "Reason (audited)" = "lost it"   # 7 chars
  And clicks "Reset 2FA"
  Then an inline validation message appears under the reason field reading "Enter a reason (10–500 characters)."
  And no confirm() dialog appears
  And no POST request fires
  # Client guard: Reason.Length must be 10..500. The API validator mirrors this (NotEmpty + MinimumLength(10) + MaximumLength(500)).
```

### E2E-R2F-008 — Unknown email (404)

```gherkin
Scenario: An email with no matching account returns 404 and a red alert
  Given no account exists for "ghost-9f3a@example.com"
  When the administrator fills "User email" = "ghost-9f3a@example.com"
  And fills "Reason (audited)" = "User reported lost phone, verified against records."
  And clicks "Reset 2FA"
  And accepts the confirm() dialog
  Then the BFF forwards POST /account/api/admin/reset-2fa
  And the API returns HTTP 404 with ApiResult.Error.Code = "AuthAccountNotFound"
  And a red SimfAlert surfaces the bilingual MessageForCurrentCulture()
    reading "No account was found for this email address." / "لم يتم العثور على حساب بهذا البريد الإلكتروني."
  And the fields keep their values (no clear on failure)
  And an OperationLog row is written with EventType = 'Admin.TwoFactorResetFailed', Outcome = Failure, ErrorCode = 'AuthAccountNotFound'
```

### E2E-R2F-009 — Self-reset rejected (400)

```gherkin
Scenario: Resetting your own account is rejected with AdminCannotResetSelf
  Given the signed-in administrator's own email is "superadmin@simrsnf.com"
  When they fill "User email" = "superadmin@simrsnf.com"
  And fill "Reason (audited)" = "Trying to reset my own account from this page."
  And click "Reset 2FA"
  And accept the confirm() dialog
  Then the API returns HTTP 400 with ApiResult.Error.Code = "AdminCannotResetSelf"
  And a red SimfAlert surfaces the bilingual message
    reading "An administrator cannot reset their own 2FA from this page. Use the profile page or the operator-level reset." / "لا يمكن للمسؤول إعادة تعيين المصادقة الثنائية الخاصة به من هنا. استخدم صفحة الملف الشخصي أو إعادة التعيين على مستوى المشغّل."
  And an OperationLog 'Admin.TwoFactorResetFailed' / Failure row is written with ErrorCode = 'AdminCannotResetSelf'
```

### E2E-R2F-010 — Admin-vs-admin rejected (400)

```gherkin
Scenario: Resetting another Administrator is rejected with AdminCannotResetAdministrator
  Given a second Administrator "other.admin@simf.test" exists (holds the Administrator role)
  When the signed-in administrator fills "User email" = "other.admin@simf.test"
  And fills "Reason (audited)" = "Colleague locked out, attempting admin-vs-admin reset."
  And clicks "Reset 2FA"
  And accepts the confirm() dialog
  Then the API returns HTTP 400 with ApiResult.Error.Code = "AdminCannotResetAdministrator"
  And a red SimfAlert surfaces the bilingual message
    reading "An administrator's 2FA cannot be reset by another administrator. The super-administrator's secret is re-paired through configuration." / "لا يمكن إعادة تعيين المصادقة الثنائية لمسؤول آخر من خلال هذه الصفحة. يتم إعادة ربط سرّ المسؤول الأعلى عبر الإعدادات."
  And an OperationLog 'Admin.TwoFactorResetFailed' / Failure row is written with ErrorCode = 'AdminCannotResetAdministrator'
```

### E2E-R2F-011 — Server 500 resilience

```gherkin
Scenario: API 500 on /reset-2fa shows the bilingual fallback alert
  Given the API is configured to return HTTP 500 on /api/v1/admin/admins/reset-two-factor (e.g. DB down)
  When the administrator fills valid Email + Reason
  And clicks "Reset 2FA"
  And accepts the confirm() dialog
  Then the envelope is not Success
  And because the error message is empty, a red SimfAlert shows the fallback
    "The reset could not be completed. Please try again." / "تعذّر إكمال إعادة التعيين. حاول مرة أخرى."
  And the button returns from its loading state (the finally block clears _busy)
  And no Console error escapes the page (the failure is rendered, not thrown)
```

### E2E-R2F-012 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the form card
  Given the administrator is on /admin/reset-2fa in English
  When they switch the UI language to "العربية"
  Then the page reloads with <html dir="rtl" lang="ar">
  And the SimfBanner title reads "إعادة تعيين المصادقة الثنائية لمستخدم آخر"
  And the supporting paragraph reads "استخدم هذه الخطوة بعد التحقق من هوية المستخدم بقناة خارجية. سيتم محو تطبيق المصادقة ورموز الاسترداد وجميع جلسات المستخدم، وسيُطلب منه إعداد المصادقة الثنائية من جديد عند تسجيل الدخول التالي."
  And the field labels read "البريد الإلكتروني للمستخدم" and "السبب (يُسجَّل في السجل)"
  And the reason helper reads "كيف تحققت من هوية المستخدم؟ من أبلغ عن فقدان الجهاز؟"
  And the submit button reads "إعادة تعيين المصادقة الثنائية"
  And the form card content is right-aligned (mirrored)
```

---

## Implementation notes

- **API integration tests at a lower layer.** `tests/SIMF.Api.Tests/AdminResetTwoFactorTests.cs`
  covers this surface without a browser (`POST /api/v1/admin/admins/reset-two-factor`):
  - `An_administrator_can_reset_another_users_2FA` — golden path + verifies the
    wipe (TwoFactorEnabled false, authenticator key null, recovery codes = 0).
  - `A_non_administrator_caller_is_forbidden` — non-admin token → HTTP 403 (the
    API policy gate behind E2E-R2F-004).
  - `An_administrator_cannot_reset_their_own_2FA` → 400 `AdminCannotResetSelf` (E2E-R2F-009).
  - `An_administrator_cannot_reset_another_administrators_2FA` → 400 `AdminCannotResetAdministrator` (E2E-R2F-010).
  - `Resetting_a_user_writes_an_audit_row_with_actor_and_subject` → `OperationLog`
    row `EventType = 'Admin.TwoFactorReset'` with distinct actor/subject (E2E-R2F-001 audit assertion).
  - `An_unknown_target_email_returns_404` → 404 `AuthAccountNotFound` (E2E-R2F-008).
  The FluentValidation rules (`AdminResetTwoFactorRequestValidator`) cover the
  server-side mirror of E2E-R2F-005..007 (Email NotEmpty/EmailAddress/Max256,
  Reason NotEmpty/Min10/Max500).
- **Client vs server validation.** The .razor performs its own pre-submit guard
  (email present + contains `@`; reason length 10..500) and only POSTs when clean,
  so E2E-R2F-005..007 assert the inline message + that **no POST fires**. The API
  validator is the defence-in-depth duplicate, exercised by the integration tests.
- **BFF route.** The CP calls the local BFF `POST /account/api/admin/reset-2fa`
  (`AccountEndpoints.cs`), which attaches the bearer token and forwards to the API
  `POST /api/v1/admin/admins/reset-two-factor` via `SimfAdminClient.ResetTwoFactorAsync`.
- **Manual smoke is canonical today.** Until Playwright is adopted, run these via a
  Chrome DevTools MCP session per the [SIMF smoke template](../../dev/SIMF_TABLE_PATTERN.md),
  walking each scenario and capturing screenshots into `docs/screenshots/cp-admin-reset-2fa-{scenario}.png`.
- **Convert to Playwright** when the runner lands: copy each Gherkin scenario into a
  `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created) + step
  definitions. The Gherkin shape is already runner-agnostic. Note: the native
  `confirm()` dialog must be handled by the dialog handler (auto-accept / dismiss
  per scenario), not by a DOM click.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
