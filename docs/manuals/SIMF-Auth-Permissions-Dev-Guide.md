# SIMF — Authentication & Permissions Developer Guide

This guide explains how authentication and authorization work end-to-end in SIMF: how a sign-in becomes a token, how that token carries permissions, how the API and Control Panel enforce them, how an operator configures access, and the mandatory steps for adding a new permission. Authorization is **roles-only and claim-based**: a user holds roles, a role holds permission codes, those codes are minted into the JWT once at sign-in, and every gate checks the claims already in the token. The single source of truth for the permission catalogue is `src/Shared/SIMF.Common/PermissionCatalog.cs`.

## Table of contents

1. [Overview & core concepts](#1-overview--core-concepts)
2. [Authentication flow (sign-in → token → CP cookie)](#2-authentication-flow-sign-in--token--cp-cookie)
3. [The permission catalogue & seeding](#3-the-permission-catalogue--seeding)
4. [API enforcement](#4-api-enforcement)
5. [Control Panel enforcement](#5-control-panel-enforcement)
6. [Configuring access in the CP (admin UI)](#6-configuring-access-in-the-cp-admin-ui)
7. [Playbook — adding a NEW page or action (MANDATORY: add its permission)](#7-playbook--adding-a-new-page-or-action-mandatory-add-its-permission)

---

## 1. Overview & core concepts

SIMF authorization is **roles-only**. A user holds roles; a role holds permissions; there are no per-user permission grants. The domain confirms this — the only join entity is `RolePermission` (`src/Backend/SIMF.Domain/IdentityAccess/RolePermission.cs`), keyed on `(RoleId, PermissionId)`. There is no `UserPermission` type. To change what one user can do, you change their role membership or that role's grants.

The three entities:

- **`Permission`** (`Permission.cs`) — one action on one page. Fields: `Id`, `Page`, `Action`, `DisplayName`, `Code`, and a `RolePermissions` back-collection. `Code` is the stable identifier used in authorization checks; `Page`/`Action`/`DisplayName` are metadata for the assignment UI.
- **`SimfRole`** (`SimfRole.cs`) — extends ASP.NET Core Identity's `IdentityRole<Guid>`. Roles are dynamic (an admin creates them and assigns permissions). `IsBaseline` marks built-in roles that cannot be deleted.
- **`RolePermission`** (`RolePermission.cs`) — grants one `Permission` to one `SimfRole`. Composite PK `{ RoleId, PermissionId }`, both FKs cascade-delete (`RolePermissionConfiguration.cs`).

### A permission is a (Page, Action) code

Codes follow `Page.Action`, e.g. `Sessions.Edit`, `Gates.Operate`, `Vips.Notify`. The full catalogue is the single source of truth in `src/Shared/SIMF.Common/PermissionCatalog.cs` — nested static classes expose the codes as constants (`PermissionCatalog.Sessions.Edit == "Sessions.Edit"`), and `PermissionCatalog.All` is a `List<PermissionDef>` (one `PermissionDef(Code, Page, Action, DisplayName, BaselineRoles)` per permission) that the seeder inserts as `Permission` rows. The catalogue doc-comment states how it is consumed: endpoints gate via `PolicyFor(code)`, CP pages via `[RequirePermission(code)]`, the side menu filters by `RequiredPermission`, and the seeder inserts one row per `All` entry.

### Administrator = wildcard `"*"`

`PermissionCatalog.Wildcard = "*"`. The Administrator role is never expanded into individual codes. `PermissionResolver.ResolveForRolesAsync` (`src/Backend/SIMF.Application/IdentityAccess/PermissionResolver.cs`) short-circuits:

```csharp
if (roleNames.Contains(AppRoles.Administrator))
{
    return [PermissionCatalog.Wildcard];
}
return await permissions.GetCodesForRolesAsync(roleNames, cancellationToken);
```

The comment explains the intent: keeps the token small and the super-admin "un-lockout-able." A permission check passes when the principal holds either the requested code **or** the wildcard. In `PermissionAuthorizationHandler.HandleRequirementAsync` (`src/Backend/SIMF.Api/Authorization/PermissionAuthorization.cs`):

```csharp
if (claim.Value == PermissionCatalog.Wildcard || claim.Value == requirement.Code)
{
    context.Succeed(requirement);
    break;
}
```

`AppRoles.CpRoles` lists today's three CP-side RBAC roles: `Administrator`, `GateOperator`, `PublicRelations` (`src/Shared/SIMF.Common/AppRoles.cs`). RBAC roles apply only to `UserType = Admin` users; Visitors/Others never carry an RBAC role. Non-admin baseline grants are seeded from each `PermissionDef.BaselineRoles` — only `GateOperator` (the two `Gates` codes) and `PublicRelations` (the `News`, `Invitations`, `Vips` codes) get any; everything else is `AdminOnly` (empty list = Administrator-only via the wildcard).

### Permissions are baked into the JWT at sign-in — no per-request DB lookup

The resolved codes are minted as claims when the token is issued, then every request gates against the claims already in the token. `JwtTokenService.CreateAccessToken` (`src/Backend/SIMF.Infrastructure/Identity/JwtTokenService.cs`) adds one `perm` claim per code (for an Administrator, the single `"*"`):

```csharp
claims.AddRange(permissions.Select(code => new Claim(PermissionCatalog.ClaimType, code)));
```

The flow is `ResolveForRolesAsync` → `CreateAccessToken`, called from three sign-in/refresh paths: `SignInService` (`SignInService.cs:574-576`), `SessionService` (refresh, `SessionService.cs:143-145`), and `DeviceKeyService` (`DeviceKeyService.cs:296-299`). The per-request handler reads only `context.User.FindAll(PermissionCatalog.ClaimType)` — it never touches the DB. **Practical consequence: a role's grant change does not take effect for an active user until their token is re-minted (next sign-in or refresh).**

The DB lookup for non-admin roles lives in `PermissionRepository.GetCodesForRolesAsync` (`src/Backend/SIMF.Infrastructure/Persistence/Repositories/PermissionRepository.cs`), a single `RolePermission`-join-`Roles`-join-`Permissions` query filtered by role name, returning `Distinct()` codes.

### Vocabulary (constants, all in `PermissionCatalog`)

- **Code format**: `Page.Action` (e.g. `Sessions.Edit`).
- **Claim type**: `PermissionCatalog.ClaimType = "perm"` — the JWT/cookie claim carrying each code.
- **Wildcard**: `PermissionCatalog.Wildcard = "*"` — the Administrator's single claim.
- **Policy prefix**: `PermissionCatalog.PolicyPrefix = "perm:"`. `PolicyFor(code)` builds `perm:Sessions.Edit`; `IsPermissionPolicy` / `CodeFromPolicy` parse it. `PermissionPolicyProvider` (`SIMF.Api/Authorization/PermissionAuthorization.cs`) materialises these ~130 policies on demand (`RequireAuthenticatedUser()` + a `PermissionRequirement`) so each catalogue code needs no pre-registered named policy; non-`perm:` names fall through to the default provider.

### No schema change — the tables pre-exist

`Permission` and `RolePermission` are part of `SimfIdentityDbContext` and were captured in the `InitialModel`/`InitialCreate` migration baseline that CLAUDE.md's D-110 freeze pins (`SimfIdentityDbContextModelSnapshot.cs`, `20260529150421_InitialModel.cs`). Adding a new permission therefore means inserting catalogue rows via the seeder, not a schema migration.

---

## 2. Authentication flow (sign-in → token → CP cookie)

The sign-in pipeline lives in `SIMF.Application.IdentityAccess.SignInService` (the API), mints JWTs in `SIMF.Infrastructure.Identity.JwtTokenService`, validates them in `SIMF.Api.Authentication.JwtBearerSetup`, and is exchanged for a CP cookie in `SIMF.ControlPanel.Endpoints.AuthEndpoints`. The token is the single carrier of identity + authorization state; the CP cookie is a verbatim copy of the JWT's claims.

### Step 1 — Password + account gates (`SignInService.SignInAsync`)

In order, before any token is minted:

1. **Lockout** → 423 `AUTH_ACCOUNT_LOCKED` (`accounts.IsLockedOutAsync`).
2. **Password** → `accounts.CheckPasswordAsync`; failure calls `AccessFailedAsync` and returns one generic 401 `AUTH_INVALID_CREDENTIALS` (no email-existence oracle).
3. **Account state** (`CheckAccountState`) — only `Registered` (403 `AUTH_EMAIL_NOT_VERIFIED`) and `Disabled` (403 `AUTH_ACCOUNT_DISABLED`) hard-block. `PendingApproval` / `Rejected` / `EmailVerified` sign in successfully and surface an `AccountStateInfo`.
4. **Audience gate** (`EnforceAudienceAsync`) — `UserType.Admin` ⇒ CP only; non-admin ⇒ Web/App only. Mismatch → 403 `AUTH_WRONG_SURFACE_CP` / `AUTH_WRONG_SURFACE_WEB`.
5. **PasswordChangeRequired** — for the CP audience it returns a single-use `SecondFactorKind.PasswordChange` ticket; every other audience gets 403 `AUTH_PASSWORD_CHANGE_REQUIRED`.

### Step 2 — Second factor

- If `!user.TwoFactorEnabled`, the password step *is* the sign-in → straight to `IssueTokensAsync`.
- Otherwise the factor kind is the user's own choice: an enrolled authenticator key **or** any role ⇒ `SecondFactorKind.Totp`; else `SecondFactorKind.EmailOtp`. A 5-minute opaque ticket (`SecondFactorToken`, hashed via `OpaqueToken.Hash`) is stored and returned. Verification happens in `VerifyTotpAsync` (with RFC-6238 replay rejection via `LastUsedTotpTimestep`), `VerifyRecoveryCodeAsync`, or `VerifyOtpAsync` — all of which re-run `EnsureNotLockedOutAsync` + `RequirePasswordChangeNotRequired`, then call `IssueTokensAsync`.

### Step 3 — Token minting (`SignInService.IssueTokensAsync`)

```csharp
var roles = await accounts.GetRolesAsync(user);
var permissions = await permissionResolver.ResolveForRolesAsync(roles, cancellationToken);
var mobileAppRole = await userProfiles.ResolveMobileAppRoleAsync(user.Id, cancellationToken);
var accessToken = jwtTokenService.CreateAccessToken(user, roles, permissions, mobileAppRole);
```

`PermissionResolver.ResolveForRolesAsync` returns the single wildcard `PermissionCatalog.Wildcard` (`"*"`) for `AppRoles.Administrator` (never expanded), otherwise the per-role codes from `IPermissionRepository.GetCodesForRolesAsync`. A 30-day opaque `RefreshToken` (hashed) is also persisted, and `AuthTokens(access, refresh, "Bearer", expiresIn, AuthUser)` is returned.

`JwtTokenService.CreateAccessToken` (HMAC-SHA256, `JwtOptions.SigningKey`) builds the claim set:

- `sub` (user id), `email`, `jti`, `display_name`
- `security_stamp` — `user.SecurityStamp`, the revocation anchor
- `account_state` = `user.AccountState.ToString()`, `user_type` = `user.UserType.ToString()`
- `mobile_app_role` = `mobileAppRole.ToString()`
- one `ClaimTypes.Role` per role, and one `perm` claim per permission code (`PermissionCatalog.ClaimType = "perm"`, multi-valued — `"*"` for Administrator)

### Step 4 — API validation (`JwtBearerSetup.Configure`)

`options.MapInboundClaims = false` so claim types stay as minted (`sub`, `perm`, etc. are not remapped to legacy URIs). `TokenValidationParameters` validate issuer/audience/lifetime/signing-key with `ClockSkew` 30s and **`ValidAlgorithms = [HmacSha256]`** (blocks alg-confusion / `alg:none`). `OnTokenValidatedAsync` enforces revocation: it requires a non-empty `security_stamp` claim, loads the user via `IUserAccountRepository.FindByIdAsync`, requires a non-empty DB stamp, and compares the two with `CryptographicOperations.FixedTimeEquals` — any miss calls `context.Fail(...)`. So sign-out / password-change (which call `accounts.UpdateSecurityStampAsync`) revoke live access tokens before expiry. Rejections audit `AccessTokenRejected` (per-IP throttled to 10 DB writes/60s via `IMemoryCache`); `OnChallengeAsync` returns the standard `ApiResult<object>.Fail(...)` 401 envelope.

### Step 5 — CP cookie exchange (`AuthEndpoints` → `GET /auth/complete`)

The interactive login redeems a one-time `SignInTicketStore` reference (anonymous endpoint; the short-lived single-use ticket is the control), then builds a `ClaimsIdentity` for `CookieAuthenticationDefaults.AuthenticationScheme`. It reads the JWT **without signature validation** (the API already verified it) via three helpers using `JwtSecurityTokenHandler.ReadJwtToken`, each returning empty on a malformed token (fail-closed):

- `ExtractRoleClaims` → copies roles (matches `ClaimTypes.Role` or `"role"`) into cookie `ClaimTypes.Role` claims, so `[Authorize(Roles=…)]` works.
- `ExtractPermissionClaims` → copies every `perm` claim across, so CP `[RequirePermission]` page gates + side-menu filtering read permissions with no API round-trip.
- `ExtractJwtClaims` → copies `account_state` and `user_type`.

`account_state` is taken from `payload.AccountState?.State` first, then the JWT claim, defaulting to `"Approved"`; the bilingual `rejection_reason` / `rejection_reason_ar` ride on the cookie from the sign-in response (not the JWT). The raw API tokens + `expires_at` are stored in the encrypted cookie via `SimfCookieRefreshHandler.StoreTokens` for module pages and the refresh hook. After `SignInAsync`, non-Approved users are redirected to `/auth/pending` or `/auth/rejected`, else `/`.

### Re-mint paths also carry `perm`

`perm` (and roles + `mobile_app_role`) are re-resolved on every mint, not just at sign-in:

- **Refresh** — `SessionService.RefreshAsync` (around line 142) re-runs `GetRolesAsync` → `ResolveForRolesAsync` → `ResolveMobileAppRoleAsync` → `CreateAccessToken`, after gating on `user.PasswordChangeRequired` (403 `AUTH_PASSWORD_CHANGE_REQUIRED`) and rotating the refresh token (reuse triggers `UpdateSecurityStampAsync`).
- **Device key** — `DeviceKeyService` (around line 295) does the same three-call resolve before `CreateAccessToken`.

So a role/permission change takes effect on the next refresh or device-key mint without a new password sign-in.

Files: `src/Backend/SIMF.Application/IdentityAccess/SignInService.cs`, `src/Backend/SIMF.Infrastructure/Identity/JwtTokenService.cs`, `src/Backend/SIMF.Api/Authentication/JwtBearerSetup.cs`, `src/ControlPanel/SIMF.ControlPanel/Endpoints/AuthEndpoints.cs`, `src/Backend/SIMF.Application/IdentityAccess/PermissionResolver.cs`, `src/Backend/SIMF.Application/IdentityAccess/SessionService.cs`, `src/Backend/SIMF.Infrastructure/IdentityAccess/DeviceKeyService.cs`, `src/Shared/SIMF.Common/PermissionCatalog.cs`.

---

## 3. The permission catalogue & seeding

The catalogue is one static class — `PermissionCatalog` in `src/Shared/SIMF.Common/PermissionCatalog.cs` — that is the single source of truth for every page-and-action permission. Endpoints gate with `Policies(PermissionCatalog.PolicyFor(code))`, CP pages gate with `[RequirePermission(code)]`, the side menu filters on `RequiredPermission`, and the seeder inserts one row per entry.

### How codes are declared

Each permission code is a `const string` on a nested static class, grouped by area (`Admins`, `Visitors`, `Sessions`, `Gates`, `Invitations`, `Vips`, …). Codes follow the `Page.Action` convention:

```csharp
public static class Sessions
{
    public const string View   = "Sessions.View";
    public const string Create = "Sessions.Create";
    public const string Edit   = "Sessions.Edit";
    public const string Delete = "Sessions.Delete";
}
```

The flat catalogue is the `public static readonly IReadOnlyList<PermissionDef> All`, built by `BuildAll()`. Each entry is a `PermissionDef` record — `(string Code, string Page, string Action, string DisplayName, IReadOnlyList<string> BaselineRoles)` — e.g. `new(Sessions.View, "Sessions", "View", "View sessions", AdminOnly)`. `All` is what the seeder iterates; the nested consts are what call sites reference.

### Policy name helpers

Three helpers convert between a code and an authorization-policy name (consumed by the dynamic policy provider):
- `PolicyFor(code)` → `PolicyPrefix + code`, so `Sessions.Edit` becomes `perm:Sessions.Edit`.
- `IsPermissionPolicy(policyName)` → true when the name starts with `perm:` (`PolicyPrefix`).
- `CodeFromPolicy(policyName)` → strips the prefix back to `Sessions.Edit`.

The permission claim itself rides under `ClaimType = "perm"`, and the wildcard is `Wildcard = "*"`.

### Baseline-role grants

The fourth `PermissionDef` field, `BaselineRoles`, is the list of built-in non-Administrator roles that get this permission as a seeded grant. Only three list values exist (declared as `private static readonly`):

```csharp
private static readonly IReadOnlyList<string> AdminOnly        = [];
private static readonly IReadOnlyList<string> GateOperator     = [AppRoles.GateOperator];
private static readonly IReadOnlyList<string> PublicRelations  = [AppRoles.PublicRelations];
```

- **`AdminOnly` (empty list)** — the default for almost every entry. No baseline role is granted; only Administrator (via wildcard) holds it.
- **`GateOperator`** — granted to `Gates.Operate` and `Gates.ViewOwnReports`. Note `Gates.Manage` is `AdminOnly`, so a gate operator can run a gate and see its own reports but cannot manage gates.
- **`PublicRelations`** — granted to the four `News.*` codes, the two `Invitations.*` codes, and the two `Vips.*` codes. The News grant is explicitly there to preserve prior behaviour: the admin News endpoints were gated by `PublicRelationsAccess` before this catalogue existed, so PR keeps News as a seeded grant. The Gate triad (D-148) and the PR/VIP triad (D-168) are the six codes that pre-date the catalogue and keep their exact strings/pages/actions/display-names so existing seeded rows and grants match on re-seed.

The role-name strings come from `AppRoles` (`src/Shared/SIMF.Common/AppRoles.cs`): `AppRoles.CpRoles = [Administrator, GateOperator, PublicRelations]`. Per the P7 model, RBAC roles apply only to `UserType = Admin` users.

### Administrator is never seeded per-code (wildcard)

There is no `Administrator` value in any `BaselineRoles` list. Administrator is resolved to `PermissionCatalog.Wildcard` (`"*"`) at token-mint time, so it holds every permission implicitly. A permission check passes when the principal holds the requested code **or** the wildcard. Seeding therefore never writes Administrator→permission grants.

### Static-init ordering note

The three baseline lists are deliberately declared **before** `All` (lines 391-397). C# static field initializers run in textual order and `BuildAll()` reads `AdminOnly`/`GateOperator`/`PublicRelations`. If they were declared after `All`, `BuildAll()` would capture their null defaults — seeding every entry with a null `BaselineRoles` and then NRE-ing the seeder's `foreach (var roleName in grantToRoles)` loop. The inline comment documents this exactly.

### Seeding loop + `EnsurePermissionAsync` (idempotent by Code, data-only)

`IdentitySeeder.SeedAsync` (`src/Backend/SIMF.Infrastructure/Identity/IdentitySeeder.cs`) first ensures the CP roles exist (`foreach (var role in AppRoles.CpRoles) await EnsureRoleAsync(role)`), then drives the catalogue:

```csharp
foreach (var permission in PermissionCatalog.All)
{
    await EnsurePermissionAsync(
        permission.Code, permission.Page, permission.Action,
        permission.DisplayName, permission.BaselineRoles, cancellationToken);
}
```

`EnsurePermissionAsync` is idempotent **by `Code`**:
1. `dbContext.Permissions.SingleOrDefaultAsync(p => p.Code == code)`. If null, insert a new `Permission` (fresh `Guid.NewGuid()` Id, plus Page/Action/DisplayName) and save. If it already exists, the existing row is reused — Page/Action/DisplayName are **not** re-written on an existing row.
2. For each role name in `grantToRoles`: `FindByNameAsync(roleName)` (skip with `continue` if the role is missing), then check `RolePermissions.AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id)` and only add a `RolePermission` if the grant does not already exist.

Both the row insert and each grant are conditional, so re-running on every boot is a no-op once seeded. The work is pure data — `INSERT`s into `Permission` and `RolePermission` — with no DDL, so it is **freeze-safe**: it does not touch the frozen EF schema or the enum contract, and adding catalogue entries only adds data rows.

One caveat worth flagging: because step 1 reuses an existing row without updating it, changing a `DisplayName`/`Page`/`Action` in the catalogue for a code that is already seeded will **not** propagate to an existing database — only brand-new codes get the new metadata. (Codes are matched solely on the `Code` string.)

Relevant files:
- `src/Shared/SIMF.Common/PermissionCatalog.cs`
- `src/Shared/SIMF.Common/AppRoles.cs`
- `src/Backend/SIMF.Infrastructure/Identity/IdentitySeeder.cs`

---

## 4. API enforcement

Authorization is **claim-based, computed once at token-mint time**. The chain is: resolve a user's roles to permission codes → bake them into `perm` claims on the JWT → gate each endpoint with a dynamically-materialised `perm:<code>` policy that a handler checks against those claims. No per-request DB lookup happens during authorization.

### Step 1 — Resolving roles to permission codes

`PermissionResolver` (`src/Backend/SIMF.Application/IdentityAccess/PermissionResolver.cs`, implementing `IPermissionResolver`) has one method, `ResolveForRolesAsync`. It short-circuits Administrators to a single wildcard rather than expanding ~130 codes:

```csharp
if (roleNames.Contains(AppRoles.Administrator))
{
    return [PermissionCatalog.Wildcard];   // "*"
}
return await permissions.GetCodesForRolesAsync(roleNames, cancellationToken);
```

The comment notes this keeps the token small and the super-admin "un-lockout-able". For every other role it delegates to `PermissionRepository.GetCodesForRolesAsync` (`src/Backend/SIMF.Infrastructure/Persistence/Repositories/PermissionRepository.cs`), which is a three-way EF join over the identity DB context returning distinct codes:

```csharp
from rolePermission in dbContext.RolePermissions
join role in dbContext.Roles on rolePermission.RoleId equals role.Id
join permission in dbContext.Permissions on rolePermission.PermissionId equals permission.Id
where role.Name != null && names.Contains(role.Name)
select permission.Code
```

Empty role set returns `[]` early. Both are wired in `src/Backend/SIMF.Infrastructure/DependencyInjection.cs` (`IPermissionRepository` and `IPermissionResolver` as scoped).

### Step 2 — From codes to `perm` claims

The resolved list is minted into claims by `JwtTokenService` (`src/Backend/SIMF.Infrastructure/Identity/JwtTokenService.cs:53`):

```csharp
claims.AddRange(permissions.Select(code => new Claim(PermissionCatalog.ClaimType, code)));
```

`PermissionCatalog.ClaimType` is `"perm"`. The resolver is invoked at every token-issuing path — `SignInService`, `SessionService`, and `DeviceKeyService` all call `ResolveForRolesAsync` before minting. So an Administrator carries one `perm=*` claim; everyone else carries one `perm` claim per granted code.

### Step 3 — Codes become policies via the dynamic provider

Endpoints reference a policy name `perm:<code>`, built by `PermissionCatalog.PolicyFor` (`src/Shared/SIMF.Common/PermissionCatalog.cs`):

```csharp
public static string PolicyFor(string code) => PolicyPrefix + code;   // "perm:" + code
```

Rather than register ~130 named policies, `PermissionPolicyProvider` (`src/Backend/SIMF.Api/Authorization/PermissionAuthorization.cs`) materialises them on demand. In `GetPolicyAsync`, if the name starts with `perm:` (`PermissionCatalog.IsPermissionPolicy`), it builds a policy requiring an authenticated user plus a `PermissionRequirement` carrying the code (extracted via `CodeFromPolicy`); any other name (`AdministratorOnly`, `RequireApprovedAccount`, the gate / PR policies) falls through to the `DefaultAuthorizationPolicyProvider`.

`PermissionAuthorizationHandler` satisfies the requirement by scanning the principal's `perm` claims — the **wildcard or an exact code match** succeeds:

```csharp
foreach (var claim in context.User.FindAll(PermissionCatalog.ClaimType))
{
    if (claim.Value == PermissionCatalog.Wildcard || claim.Value == requirement.Code)
    {
        context.Succeed(requirement);
        break;
    }
}
```

Registered in `src/Backend/SIMF.Api/Program.cs:245-246` as singletons, *after* `AddAuthorizationBuilder().AddSimfAuthorization()` so the custom provider wins for `perm:` names:

```csharp
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
```

The non-permission policies are defined in the `AuthorizationPolicies` static class + `AddSimfAuthorization` extension (located in `src/Backend/SIMF.Api/Endpoints/Admin/ResetTwoFactorEndpoint.cs`). `RequireApprovedAccount` is a claim check, not a role check:

```csharp
builder.AddPolicy(RequireApprovedAccount, policy =>
    policy.RequireClaim("account_state", "Approved"));
```

### Step 4 — How an endpoint gates

A sample gated endpoint, `ListThemesEndpoint` in `src/Backend/SIMF.Api/Endpoints/Admin/ThemeEndpoints.cs`, stacks two policies in `Configure()`:

```csharp
Post("/admin/themes/list");
Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Themes.View),
         nameof(AuthorizationPolicies.RequireApprovedAccount));
```

FastEndpoints applies **all** listed policies (AND). The caller must hold `perm=Themes.View` (or `perm=*`) **and** an `account_state=Approved` claim. The CRUD siblings gate on distinct codes — `CreateThemeEndpoint` uses `Themes.Create`, `UpdateThemeEndpoint` `Themes.Edit`, `DeactivateThemeEndpoint` `Themes.Delete` — so View access does not imply write access.

### Step 5 — The 403 path

If the required `perm` claim is absent (and the principal isn't an Administrator with the wildcard), `PermissionAuthorizationHandler` never calls `context.Succeed`, the policy is unmet, and the authenticated-but-unauthorized request yields **HTTP 403 Forbidden** (vs. 401 when unauthenticated, since the materialised policy also `RequireAuthenticatedUser()`). Likewise a non-approved account fails `RequireApprovedAccount` and gets 403. Because the codes live in the JWT, this is enforced without touching the database — and a revoked role/permission for a non-Administrator user keeps working until the token is refreshed or re-minted (the join in `GetCodesForRolesAsync` is consulted only at mint time, not per request).

---

## 5. Control Panel enforcement

The Control Panel is a Blazor Server app that enforces permissions at three layers — page gating, side-menu filtering, and per-action gating — all keyed off `perm` claims the cookie carries. None of these is the security boundary: the SIMF API re-checks the identical permission on every BFF call, so the CP layer is purely UX ("don't show what you can't do").

### The permission spine (intentional API mirror)

`src/ControlPanel/SIMF.ControlPanel/Authorization/PermissionAuthorization.cs` is a deliberate duplicate of the API's policy spine — the two are separate processes that can't share an authorization assembly. It defines three types plus an attribute:

- `PermissionRequirement(string code)` — an `IAuthorizationRequirement` holding one code.
- `PermissionAuthorizationHandler` — succeeds if any of the principal's `perm` claims equals the required code **or** the wildcard:
  ```csharp
  foreach (var claim in context.User.FindAll(PermissionCatalog.ClaimType))
      if (claim.Value == PermissionCatalog.Wildcard || claim.Value == requirement.Code)
          context.Succeed(requirement);
  ```
- `PermissionPolicyProvider` — an `IAuthorizationPolicyProvider` that materialises a `perm:<code>` policy on demand (`RequireAuthenticatedUser()` + a `PermissionRequirement`); any non-`perm:` policy name falls through to `DefaultAuthorizationPolicyProvider`.
- `RequirePermissionAttribute : AuthorizeAttribute` — sets `Policy = PermissionCatalog.PolicyFor(code)` so a code (a compile-time constant) can be used in a Razor `@attribute`.

The shared constants live in `src/Shared/SIMF.Common/PermissionCatalog.cs`: `ClaimType = "perm"`, `Wildcard = "*"`, `PolicyPrefix = "perm:"`, with `PolicyFor("Themes.View") → "perm:Themes.View"` and the reverse `CodeFromPolicy`.

### Registration

`src/ControlPanel/SIMF.ControlPanel/Program.cs` registers the provider as a singleton **after** `AddAuthorization()` so it wins for `perm:` names, and the handler as scoped:

```csharp
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddCascadingAuthenticationState();
```

The cookie (`simf.cp.auth`) is what carries the `perm` claims — copied from the JWT at sign-in. On access-denied the cookie pipeline redirects to `/not-permitted` and logs via `AuthLog`.

### Layer 1 — Page gating: `@attribute [RequirePermission(code)]`

Each admin page declares its required code at the top. From `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ThemesList.razor`:

```razor
@page "/admin/themes"
@layout CpShellLayout
@attribute [RequirePermission(PermissionCatalog.Themes.View)]
```

This routes through Blazor's `AuthorizeRouteView` against the `perm:Themes.View` policy; a user lacking the code (and without the wildcard) is bounced to the cookie's `AccessDeniedPath` (`/not-permitted`).

### Layer 2 — Side-menu filter: `CanSee` in `CpShellLayout`

`src/ControlPanel/SIMF.ControlPanel/CpNavigation.cs` models the menu as `NavGroup`/`NavItem` records, where each `NavItem` carries an optional `string? RequiredPermission` (and a `bool IsStub`). Real pages set it to a catalog code, e.g.:

```csharp
new("Module.Themes", "/admin/themes", RequiredPermission: PermissionCatalog.Themes.View),
```

Stubs that resolve to a placeholder leave it `null`:

```csharp
new("Module.RegistrationRequests", "/m/registration-requests", IsStub: true),
```

`src/ControlPanel/SIMF.ControlPanel/Components/Layout/CpShellLayout.razor` snapshots the claims in `OnInitializedAsync` into a `HashSet<string> _permissions` (ordinal) plus a `_hasAllPermissions` wildcard flag, then filters each group:

```csharp
private bool CanSee(CpNavigation.NavItem item) =>
    item.RequiredPermission is null
    || _hasAllPermissions
    || _permissions.Contains(item.RequiredPermission);
```

Behaviour: an item the user lacks is hidden; if a group ends up with zero visible items the whole `SimfNavGroup` heading is skipped (`visibleItems.Count > 0`); an item with `RequiredPermission == null` (dashboard and the not-yet-built `IsStub` entries) is always shown; an Administrator with the `*` claim sees every item. The set defaults to empty so the brief pre-auth render is fail-closed. This same layout also enforces account state — a `PendingApproval`/`Rejected` user is redirected to `/auth/pending` or `/auth/rejected` before module content renders.

### Layer 3 — Action gating: `<AuthorizedAction Permission="...">`

`src/ControlPanel/SIMF.ControlPanel/Components/AuthorizedAction.razor` wraps a single create/edit/delete control so it disappears for users lacking the code. It is a thin wrapper over `AuthorizeView` against the same per-code policy:

```razor
<AuthorizeView Policy="@_policy">
    <Authorized>@ChildContent</Authorized>
</AuthorizeView>
@code {
    [Parameter, EditorRequired] public string Permission { get; set; } = "";
    private string _policy => PermissionCatalog.PolicyFor(Permission);
}
```

Same wildcard/exact-code semantics as page gating, since both resolve through `PermissionPolicyProvider` → `PermissionAuthorizationHandler`.

### Why this is UX, not security

The CP gating only controls what renders in the browser circuit. Actual data access goes through JS-interop BFF calls — e.g. `ThemesList` posts to `/account/api/admin/themes/list` and `DELETE`s `/account/api/admin/themes/{id}` — which forward the cookie's access token to the SIMF API, where the **same** `perm:<code>` policy is enforced server-side. The file headers state this explicitly (`PermissionAuthorization.cs` calls itself "the Control Panel mirror of the API's identical requirement"; `AuthorizedAction.razor` notes "the API still enforces the same permission, so this is a UX layer ... not the security boundary"). Hiding a button or menu item never grants or denies access on its own.

Relevant files:
- `src/ControlPanel/SIMF.ControlPanel/Authorization/PermissionAuthorization.cs`
- `src/ControlPanel/SIMF.ControlPanel/Program.cs`
- `src/ControlPanel/SIMF.ControlPanel/CpNavigation.cs`
- `src/ControlPanel/SIMF.ControlPanel/Components/Layout/CpShellLayout.razor`
- `src/ControlPanel/SIMF.ControlPanel/Components/AuthorizedAction.razor`
- `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ThemesList.razor`

---

## 6. Configuring access in the CP (admin UI)

Access in SIMF is RBAC: a **role** owns a set of **permission codes**, and a user holds zero or more roles. Two operator tasks cover the whole flow — granting permissions to a role, and assigning roles to a user. Both are Administrator-area screens that call the `/account/api/admin/...` endpoints through the `simfAccount` JS interop helpers (`postJson` / `getJson` / `putJson` / `deleteJson`).

### Task 1 — Assign permissions to a role

**Where:** `RolesList.razor` (`/admin/roles`) → the per-row **Permissions** action → `RolePermissionsEditor.razor` (`/admin/roles/{RoleId:guid}/permissions`).

In `RolesList.razor`, every row renders a Permissions link inside an `<AuthorizedAction Permission="@PermissionCatalog.Roles.AssignPermissions">` (lines 85–90), so the action only appears for operators who hold `Roles.AssignPermissions`. The Details modal exposes the same jump via `OpenPermissionEditor` (line 241), which navigates to the editor page.

The editor (`RolePermissionsEditor.razor`) is itself gated by `@attribute [RequirePermission(PermissionCatalog.Roles.AssignPermissions)]` (line 8). On load it GETs the role's current grants:

```
GET /account/api/admin/roles/{RoleId}/permissions  →  AdminRolePermissionsResponse
```

It then renders the **full catalogue** grouped by page — `_groups` is `PermissionCatalog.All.GroupBy(p => p.Page)` (lines 77–78) — with one `SimfCheckbox` per permission, pre-checked from `_role.GrantedCodes` (the `_selected` `HashSet<string>`). `Toggle` adds/removes a code in `_selected`. Save sends the **complete** selected set (it replaces, not merges):

```
PUT /account/api/admin/roles/{RoleId}/permissions   body: { Codes: [...] }
```

**Baseline roles are read-only.** When `_role.IsBaseline` is true the editor shows `Admin.RolePermissions.BaselineNotice`, every checkbox is `Disabled`, and the Save button is not rendered (lines 30–61); `SaveAsync` also returns early if `_role.IsBaseline` (line 129). This is enforced server-side too: `AdminRoleService.SetPermissionsAsync` throws `ApiException(ErrorCodes.RoleIsBaseline, 409)` for any baseline role (lines 317–325) — the comment notes Administrator is the wildcard while GateOperator/PublicRelations are seeded.

Server behaviour (`AdminRoleService.SetPermissionsAsync`, lines 305–390):
- Validates every requested code against `PermissionCatalog.All`; unknown codes → `ValidationFailed` 400 (lines 327–338).
- Computes the diff against existing `RolePermission` rows and only `RemoveRange`/`Add`s the delta (lines 349–377) — an already-granted permission is not deleted and re-inserted (avoids tripping the EF change tracker on the composite key).
- Writes an `AuditEvents.RolePermissionsUpdated` audit entry.

Endpoint wiring (`RoleEndpoints.cs`): `GetRolePermissionsEndpoint` is gated by `Roles.View` (line 137); `SetRolePermissionsEndpoint` (`PUT`) by `Roles.AssignPermissions` plus `RequireApprovedAccount`, with `auth` rate-limiting (lines 167–175).

### Task 2 — Assign roles to a user

Two entry points, both Administrator-only and both backed by `AdminAccountService`.

**(a) At create time — `CreateAdminForm.razor` multi-select.** Hosted by the Add-user modal in `UsersList.razor` (lines 136–145) or the `/admin/admins/new` fallback page. On init it loads assignable roles via `POST /account/api/admin/roles/list` (`Top=200`, `Sort=name`) and renders a `SimfCheckbox` per role name (lines 39–46). The selection (`_selectedRoles`) is posted with the new account:

```
POST /account/api/admin/admins   body: { Email, DisplayName, Roles: [...] }
```

If the caller lacks `Roles.View` the list comes back empty and the form still creates the user with no roles — it degrades gracefully (lines 97–114). Server-side, `CreateAdminAsync` routes to `CreateAccountAsync` with `UserType.Admin`; roles are applied only for Admin-typed users (`AdminAccountService.cs` lines 528–538) — each role is added via `AddToRoleAsync` only if `RoleExistsAsync`.

**(b) After create — `UsersList.razor` Edit-roles modal.** `/admin/admins` (gated `[RequirePermission(PermissionCatalog.Admins.View)]`). The grid's Edit action opens `OnEditAsync` (lines 376–408), which loads the role catalogue (`/admin/roles/list`) **and** the user's current roles:

```
GET /account/api/admin/admins/{id}/roles   →  AdminUserRolesResponse
```

then renders a checkbox per role pre-checked from the user's current set (`_editSelectedRoles`). Save sends the complete desired set:

```
PUT /account/api/admin/admins/{id}/roles   body: { Roles: [...] }
```

**`SetUserRolesAsync` guards** (`AdminAccountService.cs` lines 2059–2150) — this is a replace operation that diffs current vs requested:

1. **Non-admin target rejected.** If `user.UserType != UserType.Admin` → `ApiException(ErrorCodes.AdminRolesTargetNotAdmin, 409)` "Only admin accounts can hold roles." (lines 2072–2078). RBAC roles apply only to Admin-typed users.
2. **Unknown role rejected.** Each requested name must pass `RoleManager.RoleExistsAsync`, else `RoleNotFound` 400 (lines 2086–2095).
3. **Last-admin protection.** If the diff would remove `Administrator` (`toRemove.Contains(AdministratorRole)`), it counts `UserRoles` for the Administrator role id; if `administratorCount <= 1` it throws `ApiException(ErrorCodes.AdminCannotRemoveLastAdministrator, 409)` (lines 2104–2118) — you cannot strip the role from the last administrator.
4. **Security stamp rolled.** When anything actually changed (`toRemove.Count > 0 || toAdd.Count > 0`), it calls `accounts.UpdateSecurityStampAsync(user)` (lines 2129–2135) so the user's live access tokens carrying the old roles/permissions are rejected and the new grants take effect on next sign-in/refresh.

Both server paths write audit entries (`AuditEvents.UserRolesUpdated` for the edit; `AdminUserCreated` for create).

### Key files
- `src/Backend/SIMF.Infrastructure/Identity/AdminRoleService.cs` — `GetPermissionsAsync` / `SetPermissionsAsync` (baseline guard, code validation, diff apply)
- `src/Backend/SIMF.Api/Endpoints/Admin/RoleEndpoints.cs` — `GetRolePermissionsEndpoint` (`Roles.View`), `SetRolePermissionsEndpoint` (`Roles.AssignPermissions`)
- `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/RolePermissionsEditor.razor` — grouped-checkbox editor, baseline read-only
- `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/RolesList.razor` — row Permissions action + Details "Edit permissions"
- `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/CreateAdminForm.razor` — role multi-select at create
- `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/UsersList.razor` — Edit-roles modal (`OnEditAsync` / `SaveRolesAsync`)
- `src/Backend/SIMF.Infrastructure/Identity/AdminAccountService.cs` — `GetUserRolesAsync` / `SetUserRolesAsync` (non-admin reject, last-admin guard, stamp roll)

---

## 7. Playbook — adding a NEW page or action (MANDATORY: add its permission)

**Golden rule:** a new admin page or action is not done until its permission code exists, is seeded, and gates BOTH the API and the CP. Access control in SIMF flows from one source of truth — `src/Shared/SIMF.Common/PermissionCatalog.cs`. The API endpoint, the CP page, the side-menu item, and the action button all reference the same `const` code; the seeder turns each catalogue entry into a `Permission` row; the two bypass test suites fail the build if you skip a step.

Follow these steps in order.

### Step 1 — Add the const code(s) to the right nested class in `PermissionCatalog`

Codes follow the `Page.Action` convention. Add (or extend) a nested static class grouped under the matching section comment (`// ── Programme ──`, etc.). Reuse an existing page name if the feature belongs to one.

```csharp
public static class Awards
{
    public const string View = "Awards.View";
    public const string Create = "Awards.Create";
    public const string Edit = "Awards.Edit";
    public const string Delete = "Awards.Delete";
}
```

A read-only page needs only `View`. Standard CRUD gets `View/Create/Edit/Delete` (Themes is the canonical four-action shape).

### Step 2 — Add `new(...)` entries to `PermissionCatalog.All`

Append one `PermissionDef` per code inside `BuildAll()`, in display order, in the matching section. The `BaselineRoles` argument is almost always `AdminOnly` — Administrator is never listed per-code (it carries the `Wildcard` `"*"` minted into its token). Only use `GateOperator` / `PublicRelations` if the feature must be a seeded grant for that built-in role (e.g. `News.*` ships to `PublicRelations` to preserve prior behaviour).

```csharp
// Programme  (or the right section)
new(Awards.View,   "Awards", "View",   "View awards",   AdminOnly),
new(Awards.Create, "Awards", "Create", "Create awards", AdminOnly),
new(Awards.Edit,   "Awards", "Edit",   "Edit awards",   AdminOnly),
new(Awards.Delete, "Awards", "Delete", "Delete awards", AdminOnly),
```

Note: `AdminOnly`, `GateOperator`, `PublicRelations` are declared as `private static readonly` fields **before** `All` on purpose (static initializers run in textual order). If you add a new baseline-role list, declare it above `All` too.

### Step 3 — Seeding is automatic — no migration needed

`IdentitySeeder.SeedAsync` loops `PermissionCatalog.All` and calls `EnsurePermissionAsync`, which inserts a `Permission` row only when `Code` is absent and adds each `BaselineRoles` grant only when missing. It is idempotent on every boot. The `Permission` / `RolePermission` tables already exist, so **a new catalogue entry needs NO EF migration** — this is the one place the D-110 schema freeze does not bite. (Do not confuse this with the entity tables your new feature itself needs; those are separate and follow the D-199 freeze-lift rules.)

### Step 4 — Gate the API endpoint(s) with `Policies(PermissionCatalog.PolicyFor(...))`

In each FastEndpoint `Configure()`, pass the policy name built from the code. Match the action to the verb (`View` on list/get, `Create` on POST, `Edit` on PUT, `Delete` on DELETE), and keep `RequireApprovedAccount` alongside it — see `ThemeEndpoints.cs`:

```csharp
public override void Configure()
{
    Post("/admin/awards/list");
    Policies(PermissionCatalog.PolicyFor(PermissionCatalog.Awards.View),
             nameof(AuthorizationPolicies.RequireApprovedAccount));
    Tags("Admin");
}
```

This is the real security boundary. The `PermissionPolicyProvider` materialises the `perm:Awards.View` policy on demand; the handler passes when the principal holds that `perm` claim or the wildcard.

### Step 5 — Gate the CP page with `@attribute [RequirePermission(...)]`

At the top of the `.razor` page (see `ThemesList.razor`):

```razor
@page "/admin/awards"
@layout CpShellLayout
@attribute [RequirePermission(PermissionCatalog.Awards.View)]
```

`RequirePermissionAttribute` (in `Authorization/PermissionAuthorization.cs`) is an `AuthorizeAttribute` whose `Policy` is `PolicyFor(code)`. The CP mirrors the API's requirement in its own process — same `perm` claim, copied from the JWT into the auth cookie at sign-in.

### Step 6 — Add the nav item with `RequiredPermission`

In `CpNavigation.cs`, add a `NavItem` to the right `NavGroup`. Set `RequiredPermission` to the page's `View` code so the shell hides it from users who lack it. Use `null` ONLY for the dashboard (`/`) or `IsStub: true` placeholders.

```csharp
new("Module.Awards", "/admin/awards",
    RequiredPermission: PermissionCatalog.Awards.View),
```

Add the matching `Module.Awards` resource key to `Strings` (the `LabelKey`).

### Step 7 — Gate every action control, in whichever of the two places renders it

A page has two kinds of action control and they are gated differently. Miss the
second and the page looks gated while half of its buttons are not — that is
exactly what happened on 37 pages before D-830.

**7a — buttons the page writes: wrap them.**

```razor
<AuthorizedAction Permission="@PermissionCatalog.Awards.Create">
    <SimfButton OnClick="OnAddAsync">@L["Admin.Awards.Action.Add"]</SimfButton>
</AuthorizedAction>
```

This includes anything inside `<RowActions>` and inside a modal.

**7b — buttons `SimfDataGrid` renders for you: name the permission (D-830).**

The grid draws its own Add / Edit / Delete / Duplicate / Paste / Import / Export /
Approve / Reject controls from the callbacks you wire, in the toolbar, at the row
end and in the right-click menu. `<AuthorizedAction>` cannot reach them, so the
grid takes a code per action instead:

```razor
<SimfDataGrid TItem="AdminAwardSummary"
              OnAdd="OnAddAsync" OnEditOne="OnEditAsync" OnDeleteOne="OnDeleteAsync"
              OnImport="OnImportAsync" OnExport="OnExportAsync"
              AddPermission="@PermissionCatalog.Awards.Create"
              EditPermission="@PermissionCatalog.Awards.Edit"
              DeletePermission="@PermissionCatalog.Awards.Delete"
              ImportPermission="@PermissionCatalog.Awards.Import"
              ExportPermission="@PermissionCatalog.Awards.Export">
```

Null is the default and means ungated, so an un-opted grid renders as before.
Seven parameters, not nine: one code covers every render site of its action, so
`DeletePermission` gates the bulk toolbar button, the row bin and the context-menu
entry together, and `AddPermission` also covers Duplicate and Paste (all three
create; the catalogue has no Duplicate or Paste code, and the API gates
`POST /admin/admins/duplicate` on the same `Admins.Create` as plain create).
**Export is gated like the rest** — it takes a spreadsheet of the whole result set
off the premises, and every list has its own `X.Export` code on that endpoint.
Select all, Copy and Details are the only ungated affordances.

**Use the code that gates the ENDPOINT the button calls, not a name that looks
right.** The two are often not the same word, and every one of these is real:

- `UsersList`'s Edit opens the roles form: `Admins.AssignRoles`, because there is
  no `Admins.Edit` and no `PUT /admin/admins/{id}`.
- `ContentBlocks` Add is an upsert: `ContentBlocks.Edit`.
- Invitations Add/Edit/Delete are all `Invitations.Manage`.
- `SessionModerators` Add is `.Assign`; `BusinessMeetings` Add is `.Schedule`.
- **A page can host two grids over two resources.** `MeetingTablesList`'s second
  grid is hall allocations, so its Add/Delete are `HallAllocations.Edit`, not the
  `MeetingTables.Edit` of the grid above it.
- **The same action can split by scope.** On `/admin/others/pending` the bulk
  Approve/Reject are `Others.Approve` / `Others.Reject` while the per-row ones are
  `Admins.Approve` / `Admins.Reject`, because `ApproveStaffEndpoint` serves both
  the admins and the others single-row routes.
- **The page's own gate is not a shortcut.** `/admin/gates` is gated on
  `Gates.Manage`, but its Import needs `Gates.Import` and its Export
  `Gates.Export`.

`tests/SIMF.ControlPanel.Tests/ActionPermissionGuardRatchetTests.cs` fails the
build on **any** permission-gated page that wires a grid action with no matching
code (not just View-gated ones — that filter is what let `/admin/gates` through),
on a code that is merely the page's own `View` (which gates nothing), on a NEW
grid callback nobody has classified, and on a new grid parameter no rule requires.

This is UX only ("don't show what you can't do") — the API gate from Step 4 is
still the enforcement.

### Step 8 — (Optional) grant to a baseline or custom role

`AdminOnly` already covers Administrator via the wildcard. Grant a non-admin baseline role only by listing it in the Step-2 `BaselineRoles` (the seeder picks it up). Custom roles get grants at runtime through the Roles page (`Roles.AssignPermissions`).

### Step 9 — Confirm the tests stay green

Two suites enforce completeness — run them:

- `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs`
  - `Every_nav_required_permission_is_a_real_catalogue_code` — your nav item's `RequiredPermission` must exist in `PermissionCatalog.All` (fails if you did Step 6 but not Steps 1–2).
  - `Every_real_admin_nav_item_is_permission_gated` — any non-stub `/admin` nav item with `RequiredPermission is null` fails the build (the dashboard `/` is the only allowed exception).
- `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs` — proves the API gate cannot be bypassed: a custom role granted only `Sessions.View` gets `200` on `/api/v1/admin/sessions/list` and `403` on `/api/v1/admin/themes/list`; an Administrator gets `200` everywhere via the wildcard; a role-less admin gets `403`. Add an analogous assertion for a new high-value endpoint if it introduces a new gating shape.

### Quick checklist

1. `const` code(s) in the right nested class — `PermissionCatalog.cs`
2. `new(...)` rows in `PermissionCatalog.All` with `BaselineRoles` (usually `AdminOnly`)
3. Seeder picks them up automatically, idempotent — **no migration**
4. API endpoint(s): `Policies(PermissionCatalog.PolicyFor(...))` + `RequireApprovedAccount`
5. CP page: `@attribute [RequirePermission(...)]`
6. Nav item: `RequiredPermission:` (or `null`/`IsStub` only for dashboard/placeholders) + a `Module.*` resource key
7. Action buttons: page-written ones wrapped in `<AuthorizedAction Permission="...">`; `SimfDataGrid`-rendered ones named via `AddPermission` (also covers Duplicate + Paste) / `EditPermission` / `DeletePermission` / `ImportPermission` / `ExportPermission` / `ApprovePermission` / `RejectPermission` — each set to the code that gates the endpoint it calls
8. (Optional) baseline-role grant via Step-2 `BaselineRoles`; custom roles via the Roles page
9. Run `CpNavigationPermissionTests` + `PermissionEnforcementTests` — both green

Relevant files (all under `d:/SIMF/System/V1.0.0`):
- `src/Shared/SIMF.Common/PermissionCatalog.cs`
- `src/Backend/SIMF.Infrastructure/Identity/IdentitySeeder.cs` (`EnsurePermissionAsync`)
- `src/Backend/SIMF.Api/Endpoints/Admin/ThemeEndpoints.cs`
- `src/ControlPanel/SIMF.ControlPanel/Components/Pages/Admin/ThemesList.razor`
- `src/ControlPanel/SIMF.ControlPanel/CpNavigation.cs`
- `src/ControlPanel/SIMF.ControlPanel/Components/AuthorizedAction.razor`
- `src/Shared/SIMF.Components/Forms/SimfActionGate.razor` (the one policy decision; `AuthorizedAction` is an alias over it)
- `src/Shared/SIMF.Components/Forms/SimfDataGrid.razor` (the eight action-permission parameters)
- `src/ControlPanel/SIMF.ControlPanel/Authorization/PermissionAuthorization.cs` (`RequirePermissionAttribute`, `PermissionPolicyProvider`)
- `tests/SIMF.Api.Tests/PermissionEnforcementTests.cs`
- `tests/SIMF.ControlPanel.Tests/CpNavigationPermissionTests.cs`
- `tests/SIMF.ControlPanel.Tests/ActionPermissionGuardRatchetTests.cs` (markup ratchet, both kinds of control)
- `tests/SIMF.ControlPanel.Tests/GridActionPermissionRenderTests.cs` (the gate asserted by rendering)

---

**Golden rule:** one source of truth — every new admin page or action is incomplete until its `PermissionCatalog` code is seeded and gates both the API (the real boundary) and the CP (the UX mirror).
