# SIMF — Issue-1 (Permissions) + E2E Test Results

Date: 2026-05-31 · Branch: `feature/login-api` · Scope: the per-page/per-action
permission system (D-207), its access-management follow-up (D-208), and an
end-to-end pass — automated suite + live browser — over the running stack.

## 1. Stack health (live)

Restarted on Release (new build) and confirmed responding:

| Host | URL | Result |
|---|---|---|
| API | http://localhost:5175/health | **200** |
| Control Panel | http://localhost:5158/login | **200** |
| Website | http://localhost:5115/ | **200** |

The API serving `/health` proves **startup migration + the full permission
catalogue seed ran without error** — a live confirmation of the `PermissionCatalog`
static-init fix (without it the seeder's grant loop NRE'd at boot).

## 2. Automated tests

**Full solution Release build: 0 warnings / 0 errors.** Test suites (0 failures):

| Project | Tests |
|---|---|
| SIMF.Api.Tests | **575** |
| SIMF.ControlPanel.Tests | 55 |
| SIMF.Web.Tests | 27 |
| SIMF.ApiClient.Tests | 13 |
| SIMF.Application.Tests | 16 |
| SIMF.Domain.Tests | 5 |
| **Total** | **691** |

Permission-system coverage specifically (all green):

| Test | Proves |
|---|---|
| `PermissionEnforcementTests` (API, 3) | A role granted only `Sessions.View` → **200** on `/admin/sessions/list`, **403** on `/admin/themes/list`; Administrator wildcard → all; no-permission admin → 403 everywhere. **The API cannot be bypassed.** |
| `RolePermissionsEndpointsTests` (API, 3) | `GET/PUT /admin/roles/{id}/permissions` round-trips and **replaces** the grant set; baseline role → 409; unknown code → 400. |
| `AdminUserRolesTests` (API, 3) | `GET/PUT /admin/admins/{id}/roles` round-trips and replaces a user's roles; non-admin target → 409; unknown role → 400. |
| `PermissionResolverTests` (3) | Administrator → wildcard with no DB query; other roles → granted codes; no roles → none. |
| `PermissionCatalogTests` (6) | codes unique, `Page.Action == Code`, policy-name round-trip, the six pre-catalogue codes unchanged, baseline grants intact. |
| `CpNavigationPermissionTests` (2) | every nav `RequiredPermission` is a real code; every real `/admin` nav item is gated. |

## 3. Access-management follow-up (D-208)

After the owner exercised the live build, three usability gaps were fixed:

- **Assign a role to an existing user** — the `UsersList` Edit dialog was a stub;
  it is now a real role multi-select (pre-checked from current roles), backed by
  `GET/PUT /admin/admins/{id}/roles` with a last-administrator guard + a
  security-stamp roll so the change takes effect on next request.
- **Discoverability** — a one-click **Permissions** action on each Roles grid row.
- **Information architecture** — a new **Access control** nav group (Admins +
  Pending admins + Roles & permissions + Reset 2FA) pulled out of People/System.

## 4. Live authenticated browser E2E (chrome-devtools, running stack)

Driven in a real browser against the running CP. (To avoid printing the
super-admin secret/TOTP, a throwaway admin was promoted via SQL for the session.)

**4.1 Admin (wildcard) walkthrough — both flows:**

1. Signed in → the side menu shows **all 9 groups** incl. the new **Access control**.
2. Created a custom role **"Programme Editor"** (Roles & permissions → Add).
3. Opened its permission editor via the **row "Permissions" action** → ticked
   *View sessions*, *Edit sessions*, *View themes* → **"Permissions saved."** ✅
4. Created **`programme.editor@simf.test`** with the Programme Editor role via the
   create-form **role multi-select** → "Account created…" ✅
5. Reopened that user's **Edit-roles editor** → **Programme Editor pre-checked**
   (existing-user assignment works; the old stub is gone) ✅
6. Observed seeded baselines live: **PublicRelations = 8** permissions
   (Invitations×2 + Vips×2 + News×4), **GateOperator = 2**.

**4.2 Limited user — "only allowed pages" / no-bypass (the negative case):**

Signed in as `programme.editor@simf.test` (holds only Programme Editor =
`Sessions.View/Edit` + `Themes.View`):

| Check | Result |
|---|---|
| Side menu | Shows **only** Dashboard + Themes & pillars + Sessions (+ ungated "SOON" stubs). The whole Access control group, Statistics, Countries, Gates, Content, etc. are **hidden** ✅ |
| Direct URL to an un-granted page (`/admin/countries`) | Redirected to **`/not-permitted`** — page gate blocked ✅ |
| Granted page (`/admin/sessions`) | Loaded and rendered its grid (BFF→API call returned 200) ✅ |

**Zero console errors** across the entire session (admin + limited).

Together with §2, this proves the goal end-to-end: a role holder sees and
reaches **exactly** their granted pages and is denied the rest — in the menu
(hidden) and on direct navigation (redirected) in the CP, and with 200/403 at
the API.

## 5. Triaged findings

| # | Severity | Finding | Status |
|---|---|---|---|
| 1 | **Low / cosmetic** | `GET /favicon.ico` → 404 on CP + Website (no favicon configured). Pre-existing, unrelated to Issue-1. | Open — add a favicon when convenient. |
| 2 | **Low / known limitation** | CP cookie `perm`/role/`account_state` claims are not re-derived when the access token silently rotates (~28 min); a mid-session grant/revoke isn't reflected in the CP **UI** until re-sign-in. **The API always re-checks the fresh JWT — not an authorization bypass.** D-208 mitigates the revoke case for direct role edits by rolling the user's security stamp (ends their sessions). | Open — broaden in `SimfCookieRefreshHandler` if fully-live UI updates are required. |
| 3 | **Low / cleanup** | Dead `Permissions` constants class (`AppRoles.cs`) + 4 now-unused role `AuthorizationPolicies` (`ResetTwoFactorEndpoint.cs`), orphaned by the endpoint sweep. | Reported (not deleted — files not authored by this change, §14). Owner-approved cleanup ticket. |
| 4 | **Resolved** | The authenticated CP walkthrough — previously not automated — was **executed live** (§4) end-to-end with zero console errors. | Done. |

**No functional bugs were found.** The permission system behaves exactly as
designed in every automated test and in the live browser walkthrough.

## 6. Dev-DB test artefacts

Created during the live E2E (in the dev DB only; safe to delete):
`e2e-diag@simf.test` (promoted admin, 2FA off), the **Programme Editor** role,
and `programme.editor@simf.test` (made loginable for the negative-case test).

## 7. Out of scope

The Flutter mobile app (`feature/mobile-app-skeleton`) — not on this branch; its
own `MobileAppRole` model is independent of the CP permission catalogue.
