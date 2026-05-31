# SIMF — Issue-1 (Permissions) + E2E Test Results

Date: 2026-05-31 · Branch: `feature/login-api` · Scope: the per-page/per-action
permission system (D-207) plus an end-to-end health pass over the running stack.

## 1. Stack health (live)

Restarted on Release and confirmed responding:

| Host | URL | Result |
|---|---|---|
| API | http://localhost:5175/health | **200** |
| Control Panel | http://localhost:5158/login | **200** |
| Website | http://localhost:5115/ | **200** |

The API serving `/health` proves **startup migration + the full ~132-code permission
catalogue seed ran without error** — a live confirmation of the `PermissionCatalog`
static-init fix (without it the seeder's grant loop NRE'd at boot).

## 2. Automated tests

**Full solution suite, Release: 685 passing, 0 failing** (Api 569, ControlPanel 55,
Web 27, ApiClient 13, Application 16, Domain 5). After this pass the Api suite is **572**
(+3 role-permission-endpoint tests below).

Permission-system coverage specifically (all green):

| Test | Proves |
|---|---|
| `PermissionEnforcementTests` (API, 3) | A role granted only `Sessions.View` → **200** on `/admin/sessions/list`, **403** on `/admin/themes/list`; Administrator wildcard → all; no-permission admin → 403 everywhere. **The API cannot be bypassed.** |
| `RolePermissionsEndpointsTests` (API, 3) | `GET/PUT /admin/roles/{id}/permissions` round-trips and **replaces** the grant set; baseline role → 409; unknown code → 400. **The config surface works end-to-end.** |
| `PermissionResolverTests` (3) | Administrator → wildcard with no DB query; other roles → granted codes; no roles → none. |
| `PermissionCatalogTests` (6) | codes unique, `Page.Action == Code`, policy-name round-trip, the six pre-catalogue codes unchanged, baseline grants intact. |
| `CpNavigationPermissionTests` (2) | every nav `RequiredPermission` is a real code; every real `/admin` nav item is gated. |

## 3. Browser smoke (chrome-devtools, live stack)

| Page | Render | Console |
|---|---|---|
| CP `/login` | ✅ full form, language switch, theme toggle, branding; all CSS/JS assets 200 | only `favicon.ico` 404 |
| Website `/` | ✅ full landing page | only `favicon.ico` 404 |

## 4. Triaged findings

| # | Severity | Finding | Status |
|---|---|---|---|
| 1 | **Low / cosmetic** | `GET /favicon.ico` → 404 on CP + Website (no favicon configured). Pre-existing, unrelated to Issue-1. | Open — add a favicon when convenient. |
| 2 | **Low / known limitation** | CP cookie `perm`/role/`account_state` claims are not re-derived when the access token silently rotates (~28 min); a mid-session grant/revoke isn't reflected in the CP **UI** until re-sign-in. **The API always re-checks the fresh JWT — not an authorization bypass.** Pre-existing D-121 behaviour widened to `perm` by Issue-1. | Open — fix in `SimfCookieRefreshHandler` if live permission changes are required. |
| 3 | **Low / cleanup** | Dead `Permissions` constants class (`AppRoles.cs`) + 4 now-unused role `AuthorizationPolicies` (`ResetTwoFactorEndpoint.cs`), orphaned by the endpoint sweep. | Reported (not deleted — files I didn't author, §14). Owner-approved cleanup ticket. |
| 4 | **Info** | Authenticated CP browser walkthrough was **not automated** — it requires typing the super-admin credential + a TOTP code, and secrets must not be printed into tool calls. Covered instead by the automated bypass + endpoint tests; manual steps below. | By design. |

**No functional bugs were found.** The permission system behaves exactly as designed in
every automated and smoke check.

## 5. Manual verification walkthrough (authenticated CP)

Run these against the live stack (CP at http://localhost:5158) to eyeball the new
permission UI. Sign in as the super-admin (credentials from `SuperAdmin:*` config;
complete the forced-change + TOTP as prompted).

1. **Admin sees everything** — after sign-in the side menu shows all 8 groups (the
   Administrator wildcard). 
2. **Create a limited role** — *People → Roles → Add* → name it e.g. `Programme Editor`.
3. **Grant a subset** — on that role's row → *Details → Edit permissions* → tick only
   `Sessions.*` and `Themes.View` → *Save permissions*.
4. **Create an admin holding it** — *People → Admins → Add* → fill email/name → tick
   the `Programme Editor` role (the new multi-select) → create.
5. **Sign in as the limited admin** (new browser/incognito) → confirm: the side menu
   shows **only** Sessions + Themes (+ dashboard); navigating directly to
   `/admin/countries` is denied; the Sessions page loads and works.
6. **Confirm the API agrees** — the limited admin's calls to non-granted endpoints
   return 403 (already proven by `PermissionEnforcementTests`).

## 6. Out of scope

The Flutter mobile app (`feature/mobile-app-skeleton`) — not on this branch; its own
`MobileAppRole` model is independent of the CP permission catalogue.
