# E2E test catalogue — `Delete account — confirmation code` (`/account/delete-code`)

> **Authority:** SIMF E2E test catalogue template (D-133 slice 7).

| | |
|--|--|
| **Page** | [`mobile-my-area.md`](mobile-my-area.md) (the entry point and the erase contract) |
| **Route** | `RouteNames.deleteAccountCode` — an aux route, reached only by push |
| **Surface** | Mobile (Flutter, Android + iOS) |
| **Test runner** | `flutter test` widget suite + `dotnet test SIMF.Api.Tests` |
| **Auth setup** | Any signed-in account. **Deliberately NOT gated on approval** — see E2E-MOBDEL-009 |
| **Last reviewed** | `2026-09-06` |

## Why this screen exists

Erasure is irreversible and unauthenticated-by-possession: a phone left unlocked
on a desk was, until this screen, one tap and one dialog away from destroying an
account. It now takes a code emailed to the registered address, the same shape
the app already uses for enrolling a credential.

**The screen submits the deletion itself; it does not hand a verified code back
to the caller.** There is no verify-only endpoint — the code is checked inside
the `DELETE` — so popping back on every wrong digit would re-enter here and send
a *fresh* code. Five sends an hour is the cap, so four typos would lock the
holder out of deleting for an hour. E2E-MOBDEL-004 is the assertion that pins
this.

Every code refusal answers **403, never 401**. On a 401 the API client refreshes
the token and replays the request, so a single mistyped code would burn two of the
five attempts, and a failed refresh would sign the holder out mid-deletion.

(The send path does still answer 401 for `AUTH_ACCOUNT_NOT_FOUND` — an
authenticated token whose user row is gone. That is a genuinely invalid session
and signing the holder out is the right answer, so it is deliberately outside the
"never 401" rule, which is about code refusals.)

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-MOBDEL-001 | Golden path: code arrives, code entered, account erased | happy | P0 | authored ✓ (`test/features/account/delete_account_code_screen_test.dart`, `test/features/myarea/delete_account_tile_test.dart`) |
| E2E-MOBDEL-002 | A code is requested on open and the masked recipient is named | happy | P0 | authored ✓ (widget) |
| E2E-MOBDEL-003 | Abandoning the screen erases nothing | happy | P0 | authored ✓ (tile) |
| E2E-MOBDEL-004 | A wrong code keeps the user here and sends NO new code | error | P0 | authored ✓ (widget) |
| E2E-MOBDEL-005 | An expired code frees the resend immediately | error | P1 | authored ✓ (widget) |
| E2E-MOBDEL-006 | Five wrong attempts burn the code | error | P1 | authored ✓ (`AccountDeletionCodeTests`) |
| E2E-MOBDEL-007 | A sixth request in an hour is rate-limited, in the app's own copy | error | P1 | authored ✓ (widget + API) |
| E2E-MOBDEL-008 | Every code refusal is 403, never 401 | resilience | P0 | authored ✓ (`AccountDeletionCodeTests`) |
| E2E-MOBDEL-009 | A pending, rejected or disabled holder can still delete | auth | P0 | authored ✓ (API theory) |
| E2E-MOBDEL-010 | A restored route with no erase handed over refuses and sends nothing | resilience | P1 | authored ✓ (widget) |
| E2E-MOBDEL-011 | The newest code invalidates the previous one | error | P2 | authored ✓ (API) |
| E2E-MOBDEL-012 | RTL render — Arabic, the shared KSA OTP frame | i18n | P1 | authored ✓ (`test/golden/delete_account_code_golden_test.dart`) |

## Scenarios

### E2E-MOBDEL-001 — Golden path

```gherkin
Feature: Confirm an irreversible deletion with an emailed code
  As the holder of a SIMF account
  I want deletion to need a code sent to my email
  So that someone holding my unlocked phone cannot destroy my account

Scenario: The code deletes the account
  Given I am signed in as visitor "r***@xxx.sa"
  And I have tapped "Delete my account" and confirmed "حذف نهائي" / "Delete for ever"
  Then I am on the confirmation-code screen
  And the app has called POST /api/v1/app/account/delete/send-code
  And an email of type AccountDeletion has been queued to my registered address
  When I enter the six digits from that email
  And I tap "حذف نهائي" / "Delete for ever"
  Then the app calls DELETE /api/v1/app/account carrying the code
  And the server returns 200 with ApiResult.Ok(true)
  And the AccountCode row is consumed only AFTER the erasure commits
  And my device key is revoked before sign-out, so biometric sign-in cannot resume
  And I am signed out and land on the sign-in screen
```

**Evidence:** `delete_account_code_screen_test.dart` (submits the entered code
to the erase it was handed), `delete_account_tile_test.dart` (erases with the
code, then signs out and leaves the profile), `AccountDeletionCodeTests`.

### E2E-MOBDEL-002 — A code is requested on open

```gherkin
Scenario: Opening the screen sends the code, unprompted
  Given I have just confirmed the deletion dialog
  When the confirmation-code screen opens
  Then exactly one send request is made
  And the screen shows the masked recipient, e.g. "d***@simf.test"
  And a 60-second resend countdown starts at 01:00
  And the "Didn't get the code?" resend is inert until it reaches 00:00
```

### E2E-MOBDEL-003 — Abandoning erases nothing

```gherkin
Scenario: Backing out of the code screen
  Given I am on the confirmation-code screen
  When I use the header back control, or the system back gesture
  Then no DELETE request is sent
  And I am back on the screen I came from, still signed in
```

**Why it matters:** confirming the dialog is not the point of no return. The
dialog opens this screen; only the code erases.

### E2E-MOBDEL-004 — A wrong code sends no new code

```gherkin
Scenario: One mistyped digit costs one attempt, not a whole code
  Given I am on the confirmation-code screen and one code has been sent
  When I enter a wrong six-digit code and submit
  Then the server answers 403 ACCOUNT_DELETION_CODE_INVALID
  And I stay on this screen
  And the message reads "That confirmation code is not correct. Check it and try again."
  And that is the APP's string (deleteAccountCodeInvalid), substituted for the
  server's own wording by api_error_l10n
  And NO second send request is made
  And my account still exists
```

**This is the whole reason the screen submits rather than popping.** Popping back
would re-enter and re-send; at five sends an hour, four typos would lock deletion
out for an hour.

### E2E-MOBDEL-005 — An expired code frees the resend

```gherkin
Scenario: Expiry is not a typo
  Given the code was issued more than 10 minutes ago
  When I submit it
  Then the server answers 403 ACCOUNT_DELETION_CODE_EXPIRED
  And the message says the code expired
  And the countdown is zeroed immediately
  And "Didn't get the code?" is tappable at once and sends a new code
```

Zeroed deliberately: telling someone to request a new code while the resend is
still counting down is advice the screen refuses to act on.

### E2E-MOBDEL-006 — Five wrong attempts burn the code

```gherkin
Scenario: The code dies on the fifth failure
  Given a code has been issued
  When I submit a wrong code five times
  Then the fifth answer is 403
  And submitting the CORRECT code afterwards is still refused
  And requesting a new code succeeds and that new code works
```

### E2E-MOBDEL-007 — Rate limit

```gherkin
Scenario: A sixth code in an hour
  Given I have requested five codes within the hour
  When the screen requests a sixth
  Then the server answers 429 RATE_LIMIT_EXCEEDED
  And the screen shows "Too many codes requested…" — the app's copy, not the envelope
  And no code is emailed
```

### E2E-MOBDEL-008 — Refusals are 403, never 401

```gherkin
Scenario: A code refusal must not look like an expired session
  Given I am on the confirmation-code screen
  When a missing, wrong, expired or burned code is refused
  Then its HTTP status is 403
  And it is never 401
```

**Failure mode this prevents:** the mobile client refreshes and *replays* on 401,
so a single mistyped code would consume two attempts; and if the refresh failed,
`onSessionExpired` would sign the holder out in the middle of deleting.

### E2E-MOBDEL-009 — Approval is not required

```gherkin
Scenario Outline: Every account state can still delete
  Given I hold an account in state <state>
  When I request a deletion code
  Then it is sent
  And the code completes the deletion

Examples:
  | state           |
  | PendingApproval |
  | Rejected        |
  | Disabled        |
```

**Deliberate divergence from the biometric step-up**, which this endpoint is
otherwise modelled on. That one demands an approved, non-disabled account.
Copying it would have locked out exactly the people most likely to want their
account gone.

**Driving the `Disabled` row needs a token, not a sign-in.** `SignInService`
refuses `AccountState.Disabled` with `AUTH_ACCOUNT_DISABLED`, so that example is
only reachable with a bearer token minted before the account was disabled — which
is the real-world case: someone signed in, then an administrator disabled them.
The xUnit theory exercises it by user id. `PendingApproval` and `Rejected` sign in
normally and can be driven end to end.

### E2E-MOBDEL-010 — A restored route with nothing to erase

```gherkin
Scenario: Android process death drops the route's extra
  Given the process was killed while the confirmation-code screen was open
  When Android restores the route without its extra
  Then NO code is requested
  And the screen shows "Could not start account deletion…"
  And the code boxes and the delete button are disabled
```

Refusing beats guessing at a repository: a screen that silently rebuilt its own
erase call would make the hand-over decorative and untested.

### E2E-MOBDEL-011 — The newest code wins

```gherkin
Scenario: Requesting a second code invalidates the first
  Given a code was issued and I then tapped "Didn't get the code?"
  When I submit the FIRST code
  Then it is refused
  And the second code is accepted
```

### E2E-MOBDEL-012 — RTL render

```gherkin
Scenario: The Arabic render matches the shared KSA OTP frame
  Given the app locale is Arabic
  When the confirmation-code screen opens at 375x812
  Then the title reads "تأكيد حذف الحساب"
  And a trash mark sits above the heading
  And the masked recipient, six code boxes and the 01:00 countdown render right-to-left
  And the submit button reads "حذف نهائي"
```

**Evidence:** `test/golden/delete_account_code_golden_test.dart` →
`test/golden/goldens/delete_account_code.png`. Like the biometric step-up this
reuses the shared OTP frame with no dedicated Figma node, so the golden locks a
render regression rather than proving parity (D-554).

---

_Last reviewed:_ `2026-09-06` by `SIMF Team` — created with the emailed-code
confirmation (App Store 5.1.1(v) remediation).
