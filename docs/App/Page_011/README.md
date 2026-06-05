# Page 011 — حالة التسجيل · Registration status

Per-page documentation folder. Everything about this app page lives here.

| Aspect | Document | What it holds |
|--------|----------|---------------|
| Function | [Page_011_Function.md](Page_011_Function.md) | What the page does — elements, user actions, navigation, acceptance criteria |
| Logic | [Page_011_Logic.md](Page_011_Logic.md) | Business rules — status mapping, state transitions, polling, edge cases, dependencies |
| API | [Page_011_API.md](Page_011_API.md) | The backend endpoint + DTO that serves this page (authoritative contract) |
| Design | [Page_011_Design.md](Page_011_Design.md) | Flutter screen design — layout, states, RTL, localization |

## Identity
| | |
|---|---|
| Mockup page | **11** (`Mockup.html`, owner page 011) |
| Route | `RouteNames.registrationStatus` → `/registration/status` |
| Titles | AR **حالة التسجيل** · EN **Registration status** |
| Section | 1 — Onboarding / account lifecycle |
| Nature | **Approval-process indicator** (read-only status screen) |
| App privilege | **Signed-in, profile ready, NOT yet approved** (pending account) |
| Status | **🟢 Screen built** (D-292) — Flutter `RegistrationStatusScreen`; backing route `GET /app/users/me` **BUILT** (`CurrentUserEndpoint`, D-249) |

## As built (Flutter, D-292)
`RegistrationStatusScreen` (route `registrationStatus` → `/registration/status`,
**auth-gated** — Page_011 L-1) reads `GET /app/users/me` via the new
`AuthController.refreshCurrentUser` (which surfaces failures, unlike the
best-effort `reloadCurrentUser`, and updates the global session). It renders the
state for `registrationStatus`: **Pending** (hourglass + under-review copy +
**Re-check** + the four-step stages tracker with step 3 current), **Approved**
(check + **Continue** → home + all stages complete), **Rejected** (declined copy,
no Continue). A wire failure shows the **Error** state with retry; a session-expired
failure flips auth to signed-out and the router's auth gate redirects to sign-in.
**Sign out** is always available (→ sign-in). The approval reference/date are
**decoration only (D11)** and are not rendered. Note: the client coerces an unknown
`registrationStatus` to Pending at the DTO layer (`RegistrationStatus.fromJson`); the
server only emits the three valid values, so a true unknown does not occur on the wire.

## Purpose
A waiting / status screen shown to a user who has signed in and completed their
profile but whose account is **not yet approved** by the Control Panel. It reads the
current registration status (`Approved` / `Pending` / `Rejected`) from
`GET /app/users/me` and renders the matching state so the user knows where they stand
in the approval process. When the status becomes `Approved` the app moves the user on
to the main experience; while `Pending` the screen lets the user re-check; on
`Rejected` it explains the outcome.

## Owner reference
- Owner page **011** in the mockup set. Screen key `registrationStatus`, path
  `/registration/status`.
- **D11** — an approval **reference number + date** on this screen is **decoration
  only** (not built / not backed by the API). See [Page_011_Logic.md](Page_011_Logic.md)
  L-5 and [Page_011_Design.md](Page_011_Design.md).

## Sources of truth
`Mockup.html` (visual, page 011) · `SIMF_Screen_Guide_and_User_Journey` (narrative,
Screen 11) · SIMF-MOB-API-001 (shared API conventions + auth) · SIMF-MAA-001 (mobile
architecture).
