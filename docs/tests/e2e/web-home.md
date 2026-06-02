# E2E test catalogue — Website home (`/account`)

| | |
|--|--|
| **Page** | [`web/home.md`](../../pages/web/home.md) |
| **Route** | `/account` |
| **Surface** | Website |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | An **Approved Visitor** account on the Website (`api/v1/auth/sign-in` rejects Administrator-role accounts on the Website audience — they must use the CP). Where TOTP is paired, codes come from the `Get-Totp` helper; visitor accounts on the OTP path read the emailed code from `SIMF_Identity.AccountCodes` (plaintext in dev). |
| **Last reviewed** | 2026-06-02 |

> **What this page is.** `/account` (`Home.razor`) is the Website's
> post-sign-in **landing placeholder** — the full forum shell is a later
> increment. It is `@rendermode InteractiveServerNoPrerender` and reads the
> in-circuit `SimfAuthSession` (not the cookie). When signed in it renders
> the `SimfSignedInLanding` card (wordmark + gold hairline + heading +
> supporting line + two actions); when **not** signed in `OnInitialized`
> immediately routes to `/login`. The only live action on the page is
> **Sign out**; **Continue** is intentionally `Disabled` until the forum
> experience ships.
>
> **Auth model (Website, not CP).** This is a Website page, so the auth gate
> is the **unauthenticated → `/login`** redirect — there is **no**
> `RequirePermission` / `/not-permitted` gate here (that is the Control-Panel
> pattern). The cookie-backed `/auth/complete` endpoint lands an Approved
> visitor on `/account/profile`, a `PendingApproval` visitor on
> `/account/pending`, and a `Rejected` visitor on `/account/rejected`;
> `/account` itself is reached in-circuit and self-guards only on
> `Session.IsSignedIn`.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WHM-001 | Golden round-trip — signed-in visitor lands on `/account`, renders the landing card, signs out back to `/login` | happy | P0 | _to author_ |
| E2E-WHM-002 | Landing card content — wordmark, gold rule, "You're signed in" heading, supporting line, Continue (disabled) + Sign out | happy | P1 | _to author_ |
| E2E-WHM-003 | Continue button is disabled (no navigation, no network) | happy | P2 | _to author_ |
| E2E-WHM-004 | Sign out — button shows "Signing out" loading state, calls the API, clears the session, lands on `/login` | happy | P0 | _to author_ |
| E2E-WHM-005 | Sign out is re-entrancy guarded (double-click fires one API call) | error | P2 | _to author_ |
| E2E-WHM-006 | Auth gate — unauthenticated direct hit to `/account` redirects to `/login` (no landing card flash) | auth | P0 | _to author_ |
| E2E-WHM-007 | State routing — Approved cookie lands `/account/profile`, Pending `/account/pending`, Rejected `/account/rejected` | auth | P1 | _to author_ |
| E2E-WHM-008 | Resilience — API `/sign-out` returns 500: cookie/session still cleared, user still lands on `/login` | resilience | P2 | _to author_ |
| E2E-WHM-009 | Session expiry — circuit refreshes after the tab idles; an in-circuit-only session loses its token and the page sends to `/login` | resilience | P2 | _to author_ |
| E2E-WHM-010 | RTL / Arabic render — landing card mirrors, heading + button labels are Arabic | i18n | P1 | _to author_ |

## Scenarios

### E2E-WHM-001 — Golden round-trip

```gherkin
Feature: Website home landing round-trip
  As an Approved visitor
  I want a confirmation that I am signed in and a way to sign out
  So that I trust the session before the full forum shell ships

Background:
  Given the API is reachable on http://localhost:5175
  And the Website is reachable on http://localhost:5115
  And an Approved Visitor V1 with a paired second factor exists
  And V1 has signed in via the Website /login + the second-factor step

Scenario: Sign in, view the landing card, sign out
  When V1 navigates the circuit to /account
  Then the SimfSignedInLanding card renders
  And the heading reads "You're signed in"
  And the supporting line reads "Your SIMF account is ready. The forum experience is being prepared."
  And a disabled "Continue" button is visible
  And an enabled "Sign out" secondary button is visible
  And the page title is "Saudi International Maritime Forum"

  When V1 clicks "Sign out"
  Then the button shows the "Signing out" loading state
  And a POST to /api/v1/auth/sign-out fires with V1's bearer token and returns 200 with ApiResult.Data.SignedOut = true
  And the in-circuit session is cleared
  And the browser lands on /login
  And re-navigating to /account immediately redirects to /login (the session is gone)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/web-home-landing-before.png` (landing card visible)
- Screenshot after: `docs/screenshots/web-home-signed-out-after.png` (on `/login`)
- Console errors: 0 expected
- Network: the `POST /api/v1/auth/sign-out` call returns 200 (ApiResult envelope, `Data.SignedOut = true`); no other `/api/v1/...` call fires from this page
- Audit row: the API session-revocation path runs server-side via `SessionService.SignOutAsync` for V1's user id

### E2E-WHM-002 — Landing card content

```gherkin
Scenario: The landing card shows the full SimfSignedInLanding composition
  Given V1 is signed in and on /account
  Then the SimfWordmark renders at the top of the card
  And a gold hairline rule (aria-hidden) sits below the wordmark
  And the heading element shows "You're signed in"
  And the supporting paragraph shows "Your SIMF account is ready. The forum experience is being prepared."
  And the actions row shows exactly two buttons in order: "Continue" then "Sign out"
```

### E2E-WHM-003 — Continue is disabled

```gherkin
Scenario: Continue button does nothing
  Given V1 is signed in and on /account
  When V1 clicks "Continue"
  Then the route stays on /account
  And no network request fires
  And no console error is logged
  And the button reports its disabled state to assistive tech
```

### E2E-WHM-004 — Sign out happy path

```gherkin
Scenario: Sign out ends the session and returns to /login
  Given V1 is signed in and on /account
  When V1 clicks "Sign out"
  Then the "Sign out" button enters the Loading state with label "Signing out"
  And the page reads the in-circuit access token and calls Api.SignOutAsync(accessToken)
  And the API returns ApiResult.Ok(SignOutResponse(true))
  And SimfAuthSession.Clear() runs (Tokens, PendingEmail, PendingMfaToken, PendingOtpToken all null)
  And the browser navigates to /login
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/web-home-signout-loading.png` ("Signing out" state)
- Screenshot after: `docs/screenshots/web-home-signed-out-after.png`
- Network: one `POST /api/v1/auth/sign-out` → 200
- Console errors: 0 expected

### E2E-WHM-005 — Sign out re-entrancy guard

```gherkin
Scenario: Double-clicking Sign out fires the API once
  Given V1 is signed in and on /account
  When V1 clicks "Sign out" twice in quick succession
  Then the _signingOut guard short-circuits the second invocation
  And exactly one POST /api/v1/auth/sign-out request is observed on the network panel
  And the browser still lands on /login
```

### E2E-WHM-006 — Auth gate (unauthenticated → /login)

```gherkin
Scenario: An unauthenticated direct hit is redirected to /login
  Given there is no signed-in session (a fresh browser / no auth cookie / not signed in)
  When the user opens /account directly
  Then OnInitialized sees Session.IsSignedIn = false
  And navigates to /login before the SimfSignedInLanding card paints
  And no "You're signed in" heading is ever visible (no flash)
  And no /api/v1/auth/sign-out request fires
```

> **Note (Website, not CP).** Unlike a Control-Panel page, `/account` has **no**
> per-page permission and never routes to `/not-permitted`. The gate is the
> unauthenticated → `/login` redirect above.

### E2E-WHM-007 — Account-state routing

```gherkin
Scenario Outline: The sign-in completion routes by account state
  Given visitor V is in account state <state>
  When V completes sign-in and the /auth/complete endpoint redeems the ticket
  Then the browser lands on <landing>

  Examples:
    | state           | landing            |
    | Approved        | /account/profile   |
    | PendingApproval | /account/pending   |
    | Rejected        | /account/rejected  |

Scenario: A Rejected visitor sees the bilingual reason
  Given V is Rejected with a stored rejection reason (EN + AR)
  When V signs in
  Then V lands on /account/rejected
  And the rejection_reason / rejection_reason_ar cookie claims surface the bilingual reason
```

### E2E-WHM-008 — Resilience: API 500 on sign-out

```gherkin
Scenario: Sign out still completes when the API call fails
  Given V1 is signed in and on /account
  And the API is configured so POST /api/v1/auth/sign-out returns HTTP 500
  When V1 clicks "Sign out"
  Then SimfAuthClient maps the failure to a failed ApiResult envelope (no exception thrown)
  And the page proceeds to Session.Clear() regardless of the API result
  And the browser still lands on /login
  And no unhandled exception reaches the browser console
```

> The page's `SignOutAsync` does not branch on the API result — it always
> clears the in-circuit session and routes to `/login`. The Website BFF
> `/auth/sign-out` endpoint mirrors this: it logs a warning when the API
> session could not be ended but still clears the cookie.

### E2E-WHM-009 — Session expiry (in-circuit hold)

```gherkin
Scenario: A lost in-circuit session routes to /login
  Given V1 is signed in and on /account
  And the in-circuit SimfAuthSession is the only token hold (scoped to the circuit)
  When the circuit is replaced (e.g. a full page reload that starts a new circuit) and the session is no longer signed in
  When V1 re-opens /account
  Then OnInitialized sees Session.IsSignedIn = false
  And V1 is sent to /login
```

### E2E-WHM-010 — RTL / Arabic render

```gherkin
Scenario: The landing card mirrors in Arabic
  Given V1 is signed in and on /account
  When the UI culture is Arabic (<html dir="rtl" lang="ar">)
  Then the SimfSignedInLanding card is mirrored right-to-left
  And the heading and supporting line render their Arabic strings
  And the two action buttons appear in reverse visual order
  And the gold hairline rule remains centred under the wordmark
  And no Latin text leaks into the Arabic layout
```

---

## Implementation notes

- **No CRUD, no admin permission.** `/account` is a read-only landing
  placeholder — the only mutating action is **Sign out**. It is a Website
  page, so the auth model is the unauthenticated → `/login` redirect, not a
  CP `RequirePermission` / `/not-permitted` gate. Author the matrix against
  the real composition (`SimfSignedInLanding` + the two buttons) and do not
  invent grid / filter / modal scenarios that the page does not have.
- **In-circuit session vs cookie.** `Home.razor` reads `SimfAuthSession`
  (scoped to the Blazor circuit; the token never reaches the browser). The
  persistent landing after sign-in is actually `/account/profile` via the
  cookie-backed `/auth/complete` endpoint — `/account` is the in-circuit
  placeholder. Keep E2E-WHM-007 and E2E-WHM-009 honest about that split.
- **Sign-out is resilient by design.** Both `SignOutAsync` in `Home.razor`
  and the Website BFF `POST /auth/sign-out` clear local state even when the
  API call fails, and `SimfAuthClient` never throws (transport failures map
  to a failed `ApiResult` with `ErrorCodes.InternalError`). E2E-WHM-008
  asserts that contract.
- **Lower-layer coverage.** The API `POST /api/v1/auth/sign-out`
  (`SignOutEndpoint` → `ISessionService.SignOutAsync`) is the backing call.
  No dedicated `tests/SIMF.Api.Tests/*SignOut*` integration test was found at
  the time of this review; if one is added later, cross-link it here and the
  E2E sign-out scenarios can lean on it for the server-side revocation
  assertion.
- **Convert to Playwright** when the runner is adopted: copy each Gherkin
  scenario into a `.feature` under `tests/SIMF.E2E.Tests/` (project TBD) with
  a step-definition class. The steps are already runner-agnostic.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
