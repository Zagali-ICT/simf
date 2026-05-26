# Hand-off — Prompt C — Scoped pending-profile read endpoints

| | |
|--|--|
| **Source** | `docs/SIMF-CP-Grid-Banner-Modal-Plan.md` §11C |
| **Owner** | Tech (tech@ammn.com.sa) |
| **Front-end consumer** | Plan §11.3 (PendingVisitors / PendingOthers approve-confirm modal) |
| **Decision-log ID to add on landing** | **D-122** (next free; D-121 is the cookie refresh fix) |
| **Status** | Ready to paste into a fresh backend agent session |

Paste everything below the line into a fresh Claude / Codex / other backend
agent session. The prompt is self-contained — it carries its own freeze-rule
reminders and acceptance criteria.

---

```
ROLE
You are a senior C# / FastEndpoints / EF Core backend engineer working on the
SIMF codebase at d:\SIMF\System\V1.0.0. You will add the two scoped pending-
profile-read endpoints that the Control Panel's approval flow needs to render
a full profile preview before an admin clicks Approve (UI plan
docs/SIMF-CP-Grid-Banner-Modal-Plan.md §11.3, Q-G).

WHAT EXISTS TODAY (verify before assuming)
- /admin/visitors/{id}/approve and /admin/others/{id}/approve already work and
  ship in:
    src/Backend/SIMF.Api/Endpoints/Admin/ApproveVisitorEndpoint.cs
    src/Backend/SIMF.Api/Endpoints/Admin/ApproveStaffEndpoint.cs
    (the equivalent for Others is in the same file family)
- The CP's PendingVisitors.razor / PendingOthers.razor currently approve
  on a single click with no preview and no confirm.
- UserProfileGetEndpoint at src/Backend/SIMF.Api/Endpoints/Account/UserProfileGetEndpoint.cs
  is SELF-only — the signed-in user reads their own profile. There is no
  admin-read variant today.
- The owner has explicitly REJECTED a general "admin can read any profile"
  endpoint. The read MUST be scoped to (a) the target is in PendingApproval
  state AND (b) the target's UserType matches the route's kind.

WHAT TO BUILD
Two new GET endpoints:

  1. GET /api/v1/admin/visitors/{id:guid}/profile-for-approval
       Response: ApiResult<PendingProfileResponse>
       Auth: AuthorizationPolicies.AdministratorOnly +
             RequireApprovedAccount
       Returns 404 (not 403) when:
         - the target id does not exist, OR
         - the target's AccountState is not PendingApproval, OR
         - the target's UserType is not Visitor.
       The single 404 for all three cases is load-bearing — it prevents an
       admin from enumerating approved users or cross-type ids by error-code
       diff. Same security stance D-113 took for its type-smuggling guards.

  2. GET /api/v1/admin/others/{id:guid}/profile-for-approval
       Same shape, restricted to UserType = Other.

  3. New DTO src/Shared/SIMF.Contracts/Admin/PendingProfileResponse.cs:
       record PendingProfileResponse(
           Guid Id,
           string Email,
           string DisplayName,
           string UserType,
           string? ProfileTypeName,
           string? Phone,
           string? Country,
           string? Organization,
           string? JobTitle,
           IReadOnlyList<string> Interests,
           bool HasIdDocument,
           string? IdDocumentMimeType,
           DateTimeOffset CreatedAt);
       (Only fields that already exist in UserProfile + the identity row.
       No schema change.)

  4. New service IAdminApprovalReadService at
     src/Backend/SIMF.Application/IdentityAccess/Abstractions/IAdminApprovalReadService.cs:
       - Task<PendingProfileResponse?> GetPendingVisitorProfileAsync(Guid id, CancellationToken ct);
       - Task<PendingProfileResponse?> GetPendingOtherProfileAsync(Guid id, CancellationToken ct);
     Returning null is the "not found / not eligible" signal that the
     endpoint translates to 404.

  5. New implementation
     src/Backend/SIMF.Infrastructure/Identity/AdminApprovalReadService.cs:
       - Reads from SimfIdentityDbContext (for SimfUser + UserType +
         AccountState) joined to UserProfile (for the rich fields), plus
         the Interests collection.
       - The "scope guard" is a SINGLE EF query that filters on both
         AccountState == PendingApproval AND UserType matching the method.
         If nothing comes back, return null.
       - DI registration in DependencyInjection.cs:
         services.AddScoped<IAdminApprovalReadService, AdminApprovalReadService>();

  6. Tests at tests/SIMF.Api.Tests/PendingProfileReadTests.cs covering:
       a. admin reads pending-visitor profile → 200 + DTO
       b. admin reads approved visitor → 404 (state guard)
       c. admin reads pending-visitor via /others/.../profile-for-approval → 404 (type guard)
       d. admin reads pending-Other via /visitors/.../profile-for-approval → 404 (type guard)
       e. non-admin caller → 403
       f. missing id → 404
       g. row-audit log captures the successful reads
          (D-109 SaveChanges interceptor auto-fires — assert one
          RowAuditOperation entry exists for the SimfUser row after the
          successful read).

CONVENTIONS YOU MUST FOLLOW
- FastEndpoints sealed Endpoint<TReq, TRes> with Configure() + HandleAsync().
- ApiResult<T> wrapper on every response.
- Policies(nameof(AuthorizationPolicies.AdministratorOnly),
           nameof(AuthorizationPolicies.RequireApprovedAccount)) on both endpoints.
- Tags("Admin"); Summary("...").
- // Tests: SIMF.Api.Tests/PendingProfileReadTests.cs header on each endpoint file.
- No new ErrorCodes needed — return 404 via Send.NotFoundAsync(ct) for the
  "not found / not eligible" cases.

WHAT YOU MUST NOT TOUCH (FREEZE — D-110, commit 67e2263)
- EF schema. The DTO must use only fields already on SimfUser / UserProfile /
  Interest at the InitialCreate baseline.
- Enum names / values.
- Migration history.
- appsettings.*.json.
- .csproj / Directory.Build.props / Directory.Packages.props.
- UserProfileGetEndpoint (self-only) — leave it as-is.

WHAT YOU MUST NOT BUILD
- A general "admin can read any profile" endpoint — the owner has explicitly
  rejected this. The scoped pending-only endpoints are the entire scope.
- Any write side. The endpoints are read-only; the existing approve / reject
  endpoints handle the writes.

ACCEPTANCE CRITERIA
- dotnet build -c Release : 0 warnings, 0 errors.
- dotnet test : all 7 new tests pass; the full pre-existing suite still
  passes (303 baseline; 310 after).
- Decision log entry D-122 added describing both routes, the scope guard,
  and the 404-for-all-three-cases security rationale.
- A "Frontend consumer" line in D-122 noting that the CP work is tracked
  in docs/SIMF-CP-Grid-Banner-Modal-Plan.md §11.3 and will be built next.

PROCESS
- Follow the §11 mandatory pre-approval format from ~/.claude/CLAUDE.md:
  read first, plan first, get owner approval, then code.
- Tests are part of "done". Do not declare complete without a passing
  dotnet test run quoted in the final report.
```
