# E2E test catalogue — Rejected state banner (Web) (`/account/rejected`)

| | |
|--|--|
| **Page** | [`web/account-rejected.md`](../../pages/web/account-rejected.md) |
| **Route** | `/account/rejected` |
| **Surface** | Website |
| **Test runner** | Chrome DevTools MCP + PowerShell `Get-Totp` helper (Playwright later — keep steps tool-agnostic) |
| **Auth setup** | A signed-in **visitor** whose `account_state` cookie claim is `Rejected` (see Background); admin TOTP via the `Get-Totp` helper is only needed to drive the reject action that creates the fixture |
| **Last reviewed** | 2026-06-02 |

> **What this page is.** A read-only *state-banner* page (P11 — D-052). It is
> not a CRUD screen — there is no API call on load. It reads three cookie
> claims that were copied from the API sign-in response at `/auth/complete`:
> `account_state`, `rejection_reason` (English) and `rejection_reason_ar`
> (Arabic). If `account_state` is `Approved` it redirects (force-load) to
> `/account/profile`; if it is `PendingApproval` it redirects (soft nav) to
> `/account/pending`. Otherwise it renders the rejection banner. The only
> action on the page is the **Sign out** button, which POSTs to
> `/auth/sign-out`.
>
> **Fixture creation.** A visitor lands in `Rejected` only after an
> Administrator rejects them in the Control Panel (`POST /admin/.../reject`
> with a mandatory **10–500 char** bilingual reason — see
> `RejectRouteRequestValidator`). The reason the page shows is exactly that
> admin-typed text, surfaced in the visitor's current culture.

## Coverage matrix

| ID | Scenario | Type | Priority | Status |
|----|----------|------|----------|--------|
| E2E-WRJ-001 | Golden path — rejected visitor signs in, sees the verbatim reason banner, signs out | happy | P0 | _to author_ |
| E2E-WRJ-002 | No-reason fallback — `rejection_reason` claim absent → warning alert, not error | happy | P1 | _to author_ |
| E2E-WRJ-003 | Auth gate — anonymous visitor hitting `/account/rejected` → redirect to `/login` | auth | P0 | _to author_ |
| E2E-WRJ-004 | State-guard redirect — `Approved` claim → force-load to `/account/profile` | redirect | P0 | _to author_ |
| E2E-WRJ-005 | State-guard redirect — `PendingApproval` claim → soft nav to `/account/pending` | redirect | P1 | _to author_ |
| E2E-WRJ-006 | Sign-out action — POST `/auth/sign-out` ends the session + clears the cookie | happy | P0 | _to author_ |
| E2E-WRJ-007 | Sign-out resilience — API `SignOutAsync` fails → cookie still cleared, user still lands on `/login` | resilience | P2 | _to author_ |
| E2E-WRJ-008 | Culture pick — Arabic culture shows `rejection_reason_ar`; English shows `rejection_reason` | i18n | P1 | _to author_ |
| E2E-WRJ-009 | RTL render — Arabic toggle mirrors the card, alert and Sign-out button | i18n | P1 | _to author_ |
| E2E-WRJ-010 | XSS-safety — a reason containing markup renders as inert text, not HTML | security | P2 | _to author_ |

## Scenarios

### E2E-WRJ-001 — Golden path

```gherkin
Feature: Rejected visitor sees the rejection reason and can sign out
  As a visitor whose registration was rejected
  I want to read why my account was not approved
  So that I can decide whether to contact the event coordinator

Background:
  Given the API is reachable on http://localhost:5175
  And the Website is reachable on http://localhost:5115
  And an Administrator has already rejected visitor "reem.alharbi@example.com"
      with the bilingual reason
        English="Submitted ID document was illegible; please re-apply with a clear scan."
        Arabic="صورة الهوية المرفقة غير واضحة؛ يُرجى إعادة التقديم بنسخة واضحة."
  And the visitor signs in at /login with that email + password + OTP
  And /auth/complete redirects them straight to /account/rejected
      (because account_state="Rejected")

Scenario: Rejected visitor reads the verbatim reason and signs out
  When the visitor lands on /account/rejected
  Then the document <title> is "Your account was not approved"
  And the page heading (h1#account-rejected-title) reads "Your account was not approved"
  And a "Reason:" label is shown (Account.Rejected.Reason)
  And a red SimfAlert (Variant="error") shows the exact admin text
      "Submitted ID document was illegible; please re-apply with a clear scan."
  And the supporting line reads
      "If you believe this is a mistake, please contact your event coordinator."
  And a primary "Sign out" button is visible inside a <form method="post" action="/auth/sign-out">
  And NO /account/api/... request fired on load (this page makes no API call)

  When the visitor clicks "Sign out"
  Then a POST /auth/sign-out is submitted
  And the response redirects to /login
  And re-navigating to /account/rejected now redirects to /login (cookie cleared)
```

**Evidence captured:**
- Screenshot before: `docs/screenshots/web-account-rejected-golden-before.png` (the red reason banner)
- Screenshot after: `docs/screenshots/web-account-rejected-golden-after.png` (the `/login` page after sign-out)
- Console errors: 0 expected
- Network: NO `/account/api/...` call on page load; the only request is the `POST /auth/sign-out` (302 → `/login`)
- Audit row: the *fixture* reject produced the audit row (admin-side, `AccountState` → `Rejected`); the page render itself writes no audit row.

### E2E-WRJ-002 — No-reason fallback

```gherkin
Scenario: No reason recorded → warning alert instead of error
  Given a Rejected visitor is signed in
  And neither the rejection_reason nor rejection_reason_ar cookie claim is present
      (e.g. the API sign-in response carried no reason)
  When the visitor lands on /account/rejected
  Then NO "Reason:" label and NO red error alert are rendered
  And a yellow SimfAlert (Variant="warning") reads
      "No reason was recorded for this decision." (Account.Rejected.NoReasonProvided)
  And the "If you believe this is a mistake..." supporting line is still shown
  And the "Sign out" button is still present
```

### E2E-WRJ-003 — Auth gate (anonymous)

```gherkin
Scenario: Anonymous visitor cannot reach the rejected page
  Given the browser has no authentication cookie (signed out / fresh session)
  When it navigates to /account/rejected
  Then the [Authorize] attribute denies access
  And the browser is redirected to /login (the Website auth-challenge target)
  And the rejection banner never renders
```

> **Note (Website auth gate ≠ CP /not-permitted).** This is a Website page
> protected only by `[Authorize]` — there is no per-page `PermissionCatalog`
> gate here (those apply to Control Panel admin pages). An unauthenticated
> request is challenged to `/login`; there is no `/not-permitted` redirect on
> this surface.

### E2E-WRJ-004 — State-guard redirect: Approved

```gherkin
Scenario: An Approved visitor is bounced to their profile
  Given a signed-in visitor whose account_state cookie claim is "Approved"
  When they manually navigate to /account/rejected
  Then OnInitializedAsync detects account_state="Approved"
  And the page force-loads (forceLoad: true) to /account/profile
  And the rejection banner never paints
```

### E2E-WRJ-005 — State-guard redirect: PendingApproval

```gherkin
Scenario: A PendingApproval visitor is bounced to the pending page
  Given a signed-in visitor whose account_state cookie claim is "PendingApproval"
  When they manually navigate to /account/rejected
  Then OnInitializedAsync detects account_state="PendingApproval"
  And the page soft-navigates (forceLoad: false) to /account/pending
  And the rejection banner never paints
```

### E2E-WRJ-006 — Sign-out action

```gherkin
Scenario: Sign out ends the API session and clears the cookie
  Given a Rejected visitor is on /account/rejected
  When they click "Sign out"
  Then the form POSTs to /auth/sign-out (authenticated, SameSite=Lax protects against CSRF)
  And the endpoint reads the stored access_token and calls SimfAuthClient.SignOutAsync
  And then signs out of the cookie scheme
  And redirects to /login
  And the auth cookie is no longer present on the next request
```

### E2E-WRJ-007 — Sign-out resilience (API failure)

```gherkin
Scenario: API sign-out fails but the cookie is still cleared
  Given a Rejected visitor is on /account/rejected
  And the API /auth/sign-out call will fail (API down / returns non-success)
  When they click "Sign out"
  Then the endpoint logs a warning "the API session could not be ended"
  And STILL signs out of the cookie scheme (the failure does not block local sign-out)
  And STILL redirects to /login
  And the visitor is locally signed out
```

### E2E-WRJ-008 — Culture pick (Arabic reason)

```gherkin
Scenario: The reason is shown in the visitor's current culture
  Given a Rejected visitor whose rejection_reason="<English text>"
      and rejection_reason_ar="<Arabic text>" are both present in the cookie
  When the current UI culture is Arabic (ar)
  Then the error alert shows the Arabic text (rejection_reason_ar)
  When the current UI culture is English (en)
  Then the error alert shows the English text (rejection_reason)

Scenario: Graceful fallback when only one language was recorded
  Given a Rejected Arabic-culture visitor whose rejection_reason_ar is absent
      but rejection_reason (English) is present
  Then the error alert falls back to the English text (so the visitor still sees a reason)
```

### E2E-WRJ-009 — RTL render

```gherkin
Scenario: Arabic toggle mirrors the whole card
  Given a Rejected visitor is on /account/rejected in English
  When they switch the site language to "العربية"
  Then the page reloads with <html dir="rtl" lang="ar">
  And the heading reads "تم رفض حسابك"
  And the "Reason" label reads "السبب"
  And the supporting line reads
      "إذا كنت تعتقد أن هذا خطأ، يُرجى التواصل مع منسّق الفعالية."
  And the error alert text is right-aligned and shows the Arabic reason
  And the "Sign out" button reads "تسجيل الخروج" and sits per RTL flow
```

### E2E-WRJ-010 — XSS-safety of the reason text

```gherkin
Scenario: A reason containing markup is rendered inert
  Given an Administrator rejected a visitor with the reason
      "<img src=x onerror=alert(1)> please re-apply"
      (10–500 chars, accepted by RejectRouteRequestValidator)
  And that visitor is signed in and on /account/rejected
  When the page renders the reason inside the SimfAlert
  Then the markup is shown as literal text (Razor @-binding HTML-encodes it)
  And NO alert dialog fires
  And NO <img> element is injected into the DOM
  And the console shows 0 errors
```

**Evidence captured:**
- Screenshot: `docs/screenshots/web-account-rejected-xss.png` (literal text, no injected element)
- Console errors: 0 expected; no `alert` dialog handled
- Network: 0 `/account/api/...` calls

---

## Implementation notes

- **Read-only page, no on-load API call.** Unlike the CRUD catalogues, this
  page issues no `/account/api/...` request when it renders. Its entire state
  comes from cookie claims set earlier at `/auth/complete`
  (`src/Website/SIMF.Web/Endpoints/AuthEndpoints.cs`). The only network call a
  scenario triggers is the `POST /auth/sign-out`.
- **Fixture is created via the admin reject flow.** To put a visitor into
  `Rejected`, an Administrator drives the Control Panel / admin API reject
  action. The reason is validated server-side at **10–500 chars, bilingual**
  (`src/Backend/SIMF.Api/Endpoints/Admin/Validators/RejectRouteRequestValidator.cs`
  and `AdminBulkRejectRequestValidator.cs`). E2E here asserts the *visitor-side
  consequence*; the *admin-side cause* is covered at the API layer by
  `tests/SIMF.Api.Tests/AdminBulkRejectTests.cs` (state transition + reason
  persistence) — so a missing E2E for the reject action itself is acceptable
  as long as those API tests stay green.
- **State-guard redirects are part of the contract.** Scenarios WRJ-004 and
  WRJ-005 assert the `OnInitializedAsync` guard in `Rejected.razor` — an
  Approved claim force-loads to `/account/profile`, a PendingApproval claim
  soft-navigates to `/account/pending`. These mirror the inverse guards in the
  sibling `PendingApproval.razor`.
- **Convert to Playwright** later by copying each Gherkin scenario into a
  `.feature` under `tests/SIMF.E2E.Tests/` (project to be created) plus
  step-definitions. The Gherkin is already runner-agnostic. Setting the
  fixture cookie claims (`account_state`, `rejection_reason*`) directly is the
  cheapest way to drive WRJ-002/004/005/008/010 without re-running the full
  admin reject + sign-in chain each time.

---

_Last reviewed:_ 2026-06-02 by Claude (E2E catalogue rebuild).
