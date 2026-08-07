# E2E test catalogue — Badge self-claim profile capture (`POST /api/v1/app/auth/badge-activation/complete`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7). Registry row in
> [`README.md`](README.md).

| | |
|--|--|
| **Page** | [`mobile-badge-activation.md`](mobile-badge-activation.md) (the app screen that drives it) |
| **Route** | `POST /api/v1/app/auth/badge-activation/complete` |
| **Surface** | Public auth API (anonymous — this runs before any token exists) |
| **Test runner** | xUnit + `SimfApiFactory` (`tests/SIMF.Api.Tests/BadgeSelfClaimProfileTests.cs`) |
| **Auth setup** | None. The badge QR + control of an emailed code are the two factors. |
| **Last reviewed** | 2026-07-31 |

## What changed and why

`#10-phase4`. A bulk badge run mints a **placeholder** profile: a generated display
name ("VIP #3"), `NationalityId = 0`, no interests. `BadgeActivationCompleteRequest`
carried only `QrId`, `Code`, `NewPassword`, `ConfirmPassword`, and
`CompleteActivationAsync` verified the code, set the first password and attached the
stashed email — but never touched that placeholder row. A claimed badge therefore
kept its generated name and a zero nationality forever.

The request now also carries `EnglishName`, `ArabicName`, `NationalityCode` and
`InterestIds` — every one optional and appended with a default, so a client that
sends none activates exactly as before (D-219 append-only). Self-claim is the one
moment the real holder is at the keyboard, so it is where the capture belongs.

**No Identity schema change.** The profile lives on `SIMF_App` and the account on
`SIMF_Identity`; they are written as two separate units of work (D-157 forbids a
transaction spanning both).

## Ordering — profile first, password second

The profile write happens **before** the password/email transaction. If the password
step then fails (a policy rejection, an email race), the badge is still unactivated
and the holder simply retries — the profile write is idempotent and the retry
overwrites it. The reverse order would leave an activated account whose retry is
refused by `EnsureNotAlreadyActivated`, with the placeholder name never filled.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-BSC-001 | Self-claim fills name, nationality and interests | happy | P0 | automated |
| E2E-BSC-002 | Placeholder display name is promoted on the account | happy | P0 | automated |
| E2E-BSC-003 | No profile fields sent — still activates, placeholder untouched | happy | P0 | automated |
| E2E-BSC-004 | Unknown nationality code → 400, badge stays unactivated | error | P0 | automated |
| E2E-BSC-005 | Deactivated interest → 400 `INTEREST_INVALID` | error | P0 | automated |
| E2E-BSC-006 | More than 10 interests → 400 (validator) | error | P1 | automated |
| E2E-BSC-007 | Wrong code → 400, no profile write | error | P0 | automated |
| E2E-BSC-008 | Already-activated badge → 409 `BADGE_ALREADY_ACTIVATED` | error | P0 | automated |
| E2E-BSC-009 | RTL — the Arabic name round-trips unmangled | i18n | P1 | automated |

## Scenarios

### E2E-BSC-001 — Self-claim fills name, nationality and interests

```gherkin
Feature: Badge self-claim captures the claimer
  As the holder of a bulk-generated badge
  I want to give my real details while I set my password
  So that my profile is not a generated placeholder forever

Background:
  Given an Approved, passwordless badge account with a placeholder profile
    And its display name is "VIP #3"
    And its profile NationalityId is 0 with no interests
  And activation has been started and a 6-digit code emailed

Scenario: The placeholder is filled
  When the holder POSTs /api/v1/app/auth/badge-activation/complete with
    | qrId            | {the 12-char QR id}          |
    | code            | {the emailed 6-digit code}   |
    | newPassword     | Zx9#mKp2!                    |
    | confirmPassword | Zx9#mKp2!                    |
    | englishName     | Khalid Al Otaibi             |
    | arabicName      | خالد العتيبي                  |
    | nationalityCode | SA                           |
    | interestIds     | [{maritime-security}, {port-logistics}] |
  Then the response is 200 with data.activated = true
  And the UserProfile row for that account has Name "Khalid Al Otaibi"
  And NameArabic "خالد العتيبي"
  And NationalityId resolved from the ISO code "SA"
  And exactly those two interests
```

**Evidence captured:** `BadgeSelfClaimProfileTests.Self_claim_fills_the_placeholder_name_nationality_and_interests`.

### E2E-BSC-002 — Display name is promoted

```gherkin
Scenario: The app stops greeting the holder as "VIP #3"
  When the holder completes activation supplying an English name
  Then the account's DisplayName is that name
```

The display name lives on `SimfUser` (Identity), so it is written inside the same
Identity transaction as the password — not as a second cross-database write.

**Evidence captured:** the tail of
`BadgeSelfClaimProfileTests.Self_claim_fills_the_placeholder_name_nationality_and_interests`.

### E2E-BSC-003 — No profile fields sent

```gherkin
Scenario: An older client is unaffected
  When the holder completes activation with only qrId, code and the two passwords
  Then the response is 200
  And the profile still carries its generated placeholder name
  And NationalityId is still 0
```

The service returns before touching the row when nothing was captured, so a
no-fields request performs no profile write at all.

**Evidence captured:** `BadgeSelfClaimProfileTests.Self_claim_without_profile_fields_still_activates_and_leaves_the_placeholder`.

### E2E-BSC-004 — Unknown nationality code

```gherkin
Scenario: A bad lookup fails before any write
  When the holder completes activation with nationalityCode "ZZ"
  Then the response is 400
  And error.code is "PROFILE_NATIONALITY_UNKNOWN"
  And the account still has NO password
  And the profile still carries its placeholder name
```

The lookups are resolved and validated up front, alongside the existing
pending-email re-check, so a bad payload can never half-activate a badge.

**Evidence captured:** `BadgeSelfClaimProfileTests.Unknown_nationality_code_is_rejected_and_the_badge_stays_unactivated`.

### E2E-BSC-005 — Deactivated interest

```gherkin
Scenario: A retired interest cannot be picked
  Given an interest row with IsActive = false
  When the holder completes activation picking it
  Then the response is 400
  And error.code is "INTEREST_INVALID"
```

Same rule and same error code as the ordinary profile upsert — one behaviour, two
entry points.

**Evidence captured:** `BadgeSelfClaimProfileTests.Deactivated_interest_is_rejected`.

### E2E-BSC-006 — More than 10 interests

```gherkin
Scenario: The 1-10 interest cap is enforced on this path too
  When the holder completes activation with 11 interest ids
  Then the response is 400
```

Shape is checked by `BadgeActivationCompleteRequestValidator`; existence is checked
against the live lookup in the service — the same split the profile upsert uses.

**Evidence captured:** `BadgeSelfClaimProfileTests.More_than_ten_interests_is_rejected_by_the_validator`.

### E2E-BSC-007 / E2E-BSC-008 — Unchanged guards

The wrong-code, expired-code, attempt-cap and already-activated guards are
untouched by this change and stay covered by `BadgeAuthTests`
(`Activation_complete_with_wrong_code_is_rejected`,
`Activation_start_on_account_with_password_returns_409`).

### E2E-BSC-009 — RTL round-trip

```gherkin
Scenario: The Arabic name survives the round-trip byte-for-byte
  When the holder submits arabicName "خالد العتيبي"
  Then the stored NameArabic equals "خالد العتيبي"
  And the app profile screen renders it right-to-left
```

## Follow-up outside this change

The app screen `badge_activation_screen.dart` still routes straight to `signIn`
after activation with no capture step — the Flutter half is Track D's. Until it
ships, the API accepts the fields and the app simply sends none, which is the
E2E-BSC-003 path. See `docs/_pending/C1.md`.
