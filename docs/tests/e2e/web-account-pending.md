# E2E test catalogue — Pending approval (`/account/pending`)

| | |
|--|--|
| **Page** | [`web/account-pending.md`](../../pages/web/account-pending.md) |
| **Route** | `/account/pending` |
| **Surface** | Website |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | A visitor account whose `AccountState = PendingApproval`, signed in via the Website `/login` flow. (Admin `superadmin@zagali-ict.com` + TOTP via the `Get-Totp` helper is used only to *create / approve / reject* the subject visitor from the Control Panel.) |
| **Last reviewed** | 2026-06-02 |

> **What this page is.** A pure state-banner page. There is **no API call**
> and **no CRUD** on this page — it reads the `account_state` cookie claim
> set at sign-in, shows a friendly "waiting for approval" message + the
> account email, and offers one action: a POST `/auth/sign-out` button.
> Its only "functions" are: (1) render the banner + email line, (2) the
> sign-out form, and (3) three claim-driven self-guards that bounce the
> user to `/account/profile` (Approved), `/account/rejected` (Rejected),
> or stay (PendingApproval / unknown). Coverage below exercises every one.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WPN-001 | Golden path — pending visitor lands, sees banner + email, signs out → `/login` | happy | P0 | _to author_ |
| E2E-WPN-002 | Banner content + page title render from resx (`Account.Pending.*`) | happy | P1 | _to author_ |
| E2E-WPN-003 | Email line hidden when the email claim is absent | happy | P2 | _to author_ |
| E2E-WPN-004 | Self-guard: `account_state=Approved` → redirect `/account/profile` (forceLoad) | happy | P0 | _to author_ |
| E2E-WPN-005 | Self-guard: `account_state=Rejected` → redirect `/account/rejected` | happy | P1 | _to author_ |
| E2E-WPN-006 | Auth gate: unauthenticated visit → cookie challenge → `/login` | auth | P0 | _to author_ |
| E2E-WPN-007 | Approval round-trip: admin approves the subject in CP → subject's next visit routes past pending to `/account/profile` | happy | P0 | _to author_ |
| E2E-WPN-008 | Sign-out resilience: API `SignOutAsync` fails but cookie still cleared → `/login` | resilience | P2 | _to author_ |
| E2E-WPN-009 | Forged cross-site POST to `/auth/sign-out` (no SameSite cookie) is rejected | error | P1 | _to author_ |
| E2E-WPN-010 | RTL / Arabic render mirrors the banner + sign-out button | i18n | P1 | _to author_ |

## Scenarios

### E2E-WPN-001 — Golden path

```gherkin
Feature: Pending-approval state banner — golden path
  As a visitor whose registration is awaiting admin approval
  I want a clear "you are pending" page with a sign-out button
  So that I understand why I cannot use SIMF yet and can leave cleanly

Background:
  Given the API is reachable on http://localhost:5175
  And the Website is reachable on http://localhost:5115
  And a visitor account exists with AccountState = PendingApproval
    (created from the CP by an Administrator, or a fresh sign-up whose
     email is verified but not yet approved)
  And that visitor has signed in via the Website /login flow
  And the sign-in redirect has landed them on /account/pending

Scenario: Pending visitor sees the banner and signs out
  Given the page has rendered through MainLayout
  Then the <h1 id="account-pending-title"> reads "Your account is waiting for approval"
  And a supporting paragraph reads
    "An administrator must approve your account before you can use SIMF. We will email you when the decision is made."
  And a line reads "Account: <the visitor's email>" (label from Account.Pending.AccountEmail)
  And a single primary button labelled "Sign out" is the only action on the page
  And no /api/... request fired during render (this page calls no API)

  When the visitor clicks "Sign out"
  Then the browser POSTs the <form action="/auth/sign-out">
  And the Website ends the API session via SimfAuthClient.SignOutAsync
  And the auth cookie is cleared (SignOutAsync on the Cookie scheme)
  And the browser is redirected to /login with HTTP 200
  And re-navigating to /account/pending now bounces to /login (cookie gone)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/web-account-pending-golden-before.png` (banner + email line + Sign out button)
- Screenshot after: `docs/screenshots/web-account-pending-golden-after.png` (the `/login` page after sign-out)
- Console errors: 0 expected
- Network: page render fires **no** `/api/...` call; the sign-out click fires one `POST /auth/sign-out` (302 → `/login`) and a server-side `POST /api/v1/auth/sign-out` to the API
- Audit row: API-side sign-out is logged (`Website sign-out for {UserId}.`); no `RowAudit` on this page (no write to a tracked entity)

### E2E-WPN-002 — Banner content + page title from resx

```gherkin
Scenario: Title and banner copy come from the Account.Pending.* resources
  Given a pending visitor is on /account/pending in English
  Then the browser tab title is "Your account is waiting for approval" (Account.Pending.Title via <PageTitle>)
  And the <h1> uses the same Account.Pending.Title string
  And the supporting copy uses Account.Pending.Supporting
  And the email-line label uses Account.Pending.AccountEmail ("Account")
  And the action button uses Account.Pending.SignOut ("Sign out")
  And none of these strings are hardcoded literals (all flow through IStringLocalizer<Strings>)
```

### E2E-WPN-003 — Email line hidden when claim absent

```gherkin
Scenario: The account email line is conditional on the email claim
  Given a pending visitor whose cookie carries no ClaimTypes.Email value
  When they open /account/pending
  Then the title + supporting paragraph still render
  And the "Account: ..." line is NOT rendered
    (the @if (!string.IsNullOrEmpty(_email)) block is skipped)
  And the "Sign out" button still renders
  And no console error is logged for the missing claim
```

### E2E-WPN-004 — Self-guard: Approved → /account/profile

```gherkin
Scenario: An Approved account does not get stuck on the pending page
  Given an account whose AccountState is now Approved
  And whose cookie carries account_state = "Approved"
  When they navigate directly to /account/pending
  Then OnInitializedAsync reads the "Approved" claim
  And the page immediately calls Nav.NavigateTo("/account/profile", forceLoad: true)
  And the browser fully reloads onto /account/profile
  And the pending banner never becomes interactive
```

### E2E-WPN-005 — Self-guard: Rejected → /account/rejected

```gherkin
Scenario: A Rejected account is bounced to the rejected page
  Given an account whose cookie carries account_state = "Rejected"
  When they navigate directly to /account/pending
  Then OnInitializedAsync reads the "Rejected" claim
  And the page calls Nav.NavigateTo("/account/rejected", forceLoad: false)
  And the browser lands on /account/rejected
  And the pending banner is not shown
```

### E2E-WPN-006 — Auth gate (unauthenticated)

```gherkin
Scenario: An anonymous visitor cannot view the pending page
  Given no SIMF auth cookie is present (signed out / fresh browser)
  When the browser navigates to /account/pending
  Then the [Authorize] attribute triggers the cookie challenge
  And the cookie scheme's LoginPath redirects to /login
  And the visitor lands on /login with HTTP 200
  And the pending banner is never rendered
```

> Auth note: this is a **Website** page guarded by plain `[Authorize]`
> (cookie auth), not a CP page with a `PermissionCatalog` gate — so the
> "non-permitted" path here is the unauthenticated cookie challenge to
> `/login` (`options.LoginPath = "/login"` in `Program.cs`), not a
> `/not-permitted` landing.

### E2E-WPN-007 — Approval round-trip (cross-surface)

```gherkin
Feature: Approval lifts the pending visitor past the banner
  As an Administrator
  I want approving a pending visitor to let them reach their profile
  So that the pending page is a transient state, not a dead end

Background:
  Given a visitor is in AccountState = PendingApproval and is sitting on /account/pending
  And an Administrator is signed in to the Control Panel (http://localhost:5158)
    via /login + /login/totp using superadmin@zagali-ict.com + Get-Totp

Scenario: Admin approves, visitor re-authenticates past pending
  When the Administrator opens /admin/visitors/pending in the CP
  And approves the subject visitor (POST /api/v1/admin/visitors/{id}/approve → 200)
  Then the subject's AccountState becomes Approved and a QrId is minted
  When the subject visitor signs out and signs back in on the Website
  Then the /auth/sign-in-callback redirect maps account_state="Approved" to /account/profile
  And the visitor no longer sees /account/pending
  And even a direct visit to /account/pending now self-guards to /account/profile (see E2E-WPN-004)
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-account-pending-approval-before.png` (visitor on pending) and `docs/screenshots/web-account-pending-approval-after.png` (visitor on `/account/profile`)
- Network: CP `POST /api/v1/admin/visitors/{id}/approve` returns 200
- Audit row: approval writes a `RowAudit`/state-change row for the subject on the API side (lower-layer coverage in `VisitorLifecycleTests`)

### E2E-WPN-008 — Sign-out resilience (API session end fails)

```gherkin
Scenario: Sign-out clears the cookie even if the API call fails
  Given a pending visitor is on /account/pending
  And the API /api/v1/auth/sign-out is made to fail (e.g. API down / returns non-success)
  When the visitor clicks "Sign out"
  Then the Website logs a warning
    "Website sign-out: the API session could not be ended for {UserId}."
  And it STILL clears the local auth cookie (http.SignOutAsync on the Cookie scheme)
  And the visitor is still redirected to /login with HTTP 200
  And the visitor is no longer authenticated on the Website
```

### E2E-WPN-009 — Forged cross-site POST rejected

```gherkin
Scenario: A cross-site forged POST to /auth/sign-out is not honoured
  Given a pending visitor has a valid SIMF auth cookie (SameSite=Lax)
  When a cross-site page POSTs to http://localhost:5115/auth/sign-out
  Then the SameSite=Lax policy means the auth cookie is NOT sent on the cross-site POST
  And the endpoint's RequireAuthorization() rejects the unauthenticated request
  And the visitor's session is unaffected (they remain signed in on /account/pending)
```

> Implementation note carried from `AuthEndpoints.cs`: the endpoint is
> `POST` + `RequireAuthorization()` + `DisableAntiforgery()` — the comment
> states the **SameSite policy** is the gate, not antiforgery, because a
> forged cross-site POST carries no cookie.

### E2E-WPN-010 — RTL / Arabic render

```gherkin
Scenario: Arabic culture mirrors the banner and the sign-out button
  Given a pending visitor is on /account/pending in English
  When they switch the Website to Arabic (العربية)
  Then the page reloads with <html dir="rtl" lang="ar">
  And the <h1> reads "حسابك بانتظار الموافقة" (Account.Pending.Title, ar)
  And the supporting paragraph reads
    "يجب أن يوافق المسؤول على حسابك قبل استخدام SIMF. ستصلك رسالة بريد إلكتروني فور اتخاذ القرار."
  And the email-line label reads "الحساب" (Account.Pending.AccountEmail, ar)
  And the action button reads "تسجيل الخروج" (Account.Pending.SignOut, ar)
  And the card content is right-aligned (RTL)
```

---

## Implementation notes

- **No page-level API surface.** `/account/pending` calls no API during
  render (it reads the `account_state` cookie claim only). The single
  outbound action is the `POST /auth/sign-out` form handled by
  `src/Website/SIMF.Web/Endpoints/AuthEndpoints.cs` (ends the API session
  via `SimfAuthClient.SignOutAsync`, clears the cookie, redirects
  `/login`). There is therefore no `tests/SIMF.Api.Tests/*PendingPage*`
  integration test — the page logic is claim-driven UI.
- **Lower-layer coverage of the surrounding lifecycle:**
  - `tests/SIMF.Api.Tests/VisitorLifecycleTests.cs` —
    `Visitor_lifecycle_signup_through_approved_app_sign_in` walks
    Registered → EmailVerified → PendingApproval → Approved (the exact
    state transition that controls whether a visitor lands on, or is
    lifted off, this page). Covers E2E-WPN-004/-007 at the API layer.
  - `tests/SIMF.Api.Tests/PendingProfileReadTests.cs` — the admin-side
    pending-profile read endpoints used to drive the approve / reject
    decision (the action that flips this page's claim).
- **Self-guard logic under test** lives in `PendingApproval.razor`
  `OnInitializedAsync`: `Approved` → `/account/profile` (`forceLoad: true`),
  `Rejected` → `/account/rejected` (`forceLoad: false`), otherwise render.
  Its sibling pages `UserProfile.razor` and `Rejected.razor` carry the
  mirror guards (a `PendingApproval` claim on either bounces back here),
  so E2E-WPN-004/-005 should be smoke-tested alongside those pages.
- **Convert to Playwright** when adopted: copy each Gherkin scenario into a
  `.feature` file under `tests/SIMF.E2E.Tests/` (project to be created) +
  step definitions. The Gherkin is already runner-agnostic.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
