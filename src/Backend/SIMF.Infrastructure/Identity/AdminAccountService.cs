// Tests: SIMF.Api.Tests/AdminResetTwoFactorTests.cs,
//        SIMF.Api.Tests/AdminCreateUserTests.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.Excel;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Admin-driven user-management use cases (decisions D-041, D-042). First
/// slice of the User Management module from <c>myComment</c> #33 — reset
/// 2FA, create a new CP user, list every account.
/// </summary>
internal sealed class AdminAccountService(
    UserManager<SimfUser> userManager,
    RoleManager<SimfRole> roleManager,
    IRefreshTokenRepository refreshTokenRepository,
    IRecoveryCodeService recoveryCodes,
    IAccountCodeRepository accountCodeRepository,
    IEmailQueue emailQueue,
    IAuditLog auditLog,
    IUserExcelService excel,
    IQrIdMinter qrIdMinter,
    ITransactionRunner transactionRunner,
    SimfIdentityDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<AdminAccountService> logger) : IAdminAccountService
{
    private const string AdministratorRole = "Administrator";
    private const string AuthenticatorProvider = "[AspNetUserStore]";
    private const string ActiveSecretTokenName = "AuthenticatorKey";
    private const string PendingSecretProvider = "[SIMF]";
    private const string PendingSecretTokenName = "PendingAuthenticatorKey";

    public async Task ResetTwoFactorAsync(
        Guid actorUserId,
        AdminResetTwoFactorRequest request,
        CancellationToken cancellationToken = default)
    {
        var actor = await userManager.FindByIdAsync(actorUserId.ToString())
            ?? throw new ApiException(
                ErrorCodes.AuthAccountNotFound, 404,
                "The acting account was not found.",
                "لم يتم العثور على حساب المسؤول.");

        var target = await userManager.FindByEmailAsync(request.Email);
        if (target is null)
        {
            await AuditFailure(actorUserId, request.Email, null,
                ErrorCodes.AuthAccountNotFound, cancellationToken);
            throw new ApiException(
                ErrorCodes.AuthAccountNotFound, 404,
                "No account was found for this email address.",
                "لم يتم العثور على حساب بهذا البريد الإلكتروني.");
        }

        // The user must use the self-service Disable on /account/profile —
        // that requires a current TOTP code as a sanity check, which the
        // admin reset is the very fallback FOR.
        if (target.Id == actorUserId)
        {
            await AuditFailure(actorUserId, request.Email, target.Id,
                ErrorCodes.AdminCannotResetSelf, cancellationToken);
            throw new ApiException(
                ErrorCodes.AdminCannotResetSelf, 400,
                "An administrator cannot reset their own 2FA from this page. "
                + "Use the profile page or the operator-level reset.",
                "لا يمكن للمسؤول إعادة تعيين المصادقة الثنائية الخاصة به من هنا. "
                + "استخدم صفحة الملف الشخصي أو إعادة التعيين على مستوى المشغّل.");
        }

        // Administrator vs Administrator is out of scope — those go through
        // the seeder re-pair path. Stops one admin from neutralising another.
        if (await userManager.IsInRoleAsync(target, AdministratorRole))
        {
            await AuditFailure(actorUserId, request.Email, target.Id,
                ErrorCodes.AdminCannotResetAdministrator, cancellationToken);
            throw new ApiException(
                ErrorCodes.AdminCannotResetAdministrator, 400,
                "An administrator's 2FA cannot be reset by another administrator. "
                + "The super-administrator's secret is re-paired through configuration.",
                "لا يمكن إعادة تعيين المصادقة الثنائية لمسؤول آخر من خلال هذه الصفحة. "
                + "يتم إعادة ربط سرّ المسؤول الأعلى عبر الإعدادات.");
        }

        var now = timeProvider.GetUtcNow();

        // The wipe — mirrors TotpEnrollmentService.DisableAsync but skips the
        // "you must prove a current code" gate, by design.
        await userManager.SetTwoFactorEnabledAsync(target, false);
        await userManager.RemoveAuthenticationTokenAsync(
            target, AuthenticatorProvider, ActiveSecretTokenName);
        await userManager.RemoveAuthenticationTokenAsync(
            target, PendingSecretProvider, PendingSecretTokenName);
        target.LastUsedTotpTimestep = null;
        target.UpdatedAt = now;
        await userManager.UpdateAsync(target);
        await recoveryCodes.RevokeAllAsync(target.Id, cancellationToken);

        // Kill every other session the target has open.
        await userManager.UpdateSecurityStampAsync(target);
        await refreshTokenRepository.RevokeAllForUserAsync(target.Id, now, cancellationToken);

        // Notify the target out-of-band so a silent admin abuse is visible
        // to them at once.
        EnqueueNotificationEmail(target.Email!, actor.Email ?? "an administrator", request.Reason);

        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.AdminTwoFactorReset,
                Outcome = AuditOutcome.Success,
                SubjectEmail = target.Email,
                SubjectUserId = target.Id,
                ActorUserId = actorUserId,
                Detail = request.Reason,
            },
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} reset 2FA on {TargetEmail} — reason: {Reason}",
            actorUserId, target.Email, request.Reason);
    }

    public Task<AdminCreateUserResponse> CreateStaffAsync(
        Guid actorUserId,
        AdminCreateUserRequest request,
        CancellationToken cancellationToken = default) =>
        CreateAccountAsync(actorUserId, request.Email, request.DisplayName,
            request.GrantAdministratorRole, cancellationToken);

    public Task<AdminCreateUserResponse> CreateVisitorAsync(
        Guid actorUserId,
        AdminCreateVisitorRequest request,
        CancellationToken cancellationToken = default) =>
        CreateAccountAsync(actorUserId, request.Email, request.DisplayName,
            grantAdministratorRole: false, cancellationToken);

    /// <summary>Shared back-end of <see cref="CreateStaffAsync"/> and
    /// <see cref="CreateVisitorAsync"/>. P3 extracted to keep the staff
    /// / visitor surfaces split at the contract layer without
    /// duplicating the audit + invite logic.</summary>
    private async Task<AdminCreateUserResponse> CreateAccountAsync(
        Guid actorUserId,
        string email,
        string displayName,
        bool grantAdministratorRole,
        CancellationToken cancellationToken)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            await AuditFailure(
                AuditEvents.AdminUserCreateFailed, actorUserId, email, null,
                ErrorCodes.AdminEmailAlreadyRegistered, cancellationToken);
            throw new ApiException(
                ErrorCodes.AdminEmailAlreadyRegistered, 409,
                "An account with this email address already exists.",
                "يوجد حساب مسجّل بهذا البريد الإلكتروني بالفعل.");
        }

        var now = timeProvider.GetUtcNow();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = displayName,
            // P4 — created users land in PendingApproval; the QR id is
            // minted in ApproveStaffAsync / ApproveVisitorAsync (D-046
            // QR-on-approval), not here.
            AccountState = AccountState.PendingApproval,
            PasswordChangeRequired = false,
            CreatedAt = now,
        };
        // No password yet — the new user sets it via the invite link.
        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            await AuditFailure(
                AuditEvents.AdminUserCreateFailed, actorUserId, email, null,
                ErrorCodes.InternalError, cancellationToken,
                detail: string.Join("; ", createResult.Errors.Select(error => error.Description)));
            throw new ApiException(
                ErrorCodes.InternalError, 500,
                "The account could not be created.",
                "تعذّر إنشاء الحساب.");
        }

        if (grantAdministratorRole)
        {
            await userManager.AddToRoleAsync(user, AdministratorRole);
        }

        // P4 — QR id mint moves from create-time to approve-time. See
        // ApproveStaffAsync / ApproveVisitorAsync below.

        // 7-day invite — longer than the 10-min self-service forgot-password
        // code, so the user can act on it without urgency (D-042).
        var inviteLifetime = TimeSpan.FromDays(7);
        var code = new AccountCode
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Purpose = AccountCodePurpose.PasswordReset,
            Code = VerificationCodeGenerator.Generate(),
            CreatedAt = now,
            ExpiresAt = now.Add(inviteLifetime),
        };
        await accountCodeRepository.AddAsync(code, cancellationToken);
        EnqueueInviteEmail(user.Email!, user.DisplayName, code.Code, inviteLifetime);

        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.AdminUserCreated,
                Outcome = AuditOutcome.Success,
                SubjectEmail = user.Email,
                SubjectUserId = user.Id,
                ActorUserId = actorUserId,
                Detail = grantAdministratorRole ? "role=Administrator" : null,
            },
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created user {Email} (Administrator={IsAdmin})",
            actorUserId, user.Email, grantAdministratorRole);
        return new AdminCreateUserResponse(
            user.Id, user.Email!, (int)inviteLifetime.TotalSeconds);
    }

    public Task<GridPage<AdminUserSummary>> ListStaffAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        ListAccountsAsync(query, staffOnly: true, cancellationToken);

    public Task<GridPage<AdminUserSummary>> ListVisitorsAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        ListAccountsAsync(query, staffOnly: false, cancellationToken);

    /// <summary>Shared back-end of <see cref="ListStaffAsync"/> and
    /// <see cref="ListVisitorsAsync"/>. P3 added the role-based filter:
    /// staff = users with the Administrator role today (Staff / Scientific
    /// / Security from P4); visitors = users with no CP role.</summary>
    private async Task<GridPage<AdminUserSummary>> ListAccountsAsync(
        GridQuery query, bool staffOnly, CancellationToken cancellationToken)
    {
        // Normalise: clamp Top to [1..200], clamp Skip to [0..). The grid
        // contract (SIMF.Common.GridQuery) says the endpoint owns the clamp.
        var skip = query.Skip < 0 ? 0 : query.Skip;
        var top = query.Top switch { < 1 => 20, > 200 => 200, _ => query.Top };

        // Resolve the Administrator role id once for the per-row projection
        // (the 'role' summary still shows Administrator membership only).
        var adminRoleId = await GetAdministratorRoleIdAsync(cancellationToken);

        // P4 — every CP role id (Administrator + Staff/Scientific/Security)
        // for the staff/visitor split. Staff = "user holds at least one of
        // these"; visitor = "user holds none of these".
        var cpRoleIds = await GetCpRoleIdsAsync(cancellationToken);

        // Build the query over UserManager.Users so the EF tracker stays out.
        var users = userManager.Users.AsQueryable();

        // The list is narrowed BEFORE any filter/sort/page so the totals are
        // correct.
        if (cpRoleIds.Count > 0)
        {
            users = staffOnly
                ? users.Where(u => dbContext.UserRoles.Any(ur =>
                    ur.UserId == u.Id && cpRoleIds.Contains(ur.RoleId)))
                : users.Where(u => !dbContext.UserRoles.Any(ur =>
                    ur.UserId == u.Id && cpRoleIds.Contains(ur.RoleId)));
        }
        else if (staffOnly)
        {
            // No CP roles exist yet — there are no staff users by definition,
            // so the staff page returns an empty result.
            users = users.Where(_ => false);
        }

        // -- Search ---------------------------------------------------------
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            users = users.Where(u =>
                (u.Email != null && EF.Functions.Like(u.Email, $"%{term}%"))
                || EF.Functions.Like(u.DisplayName, $"%{term}%"));
        }

        // -- Per-column filters --------------------------------------------
        if (query.Filters.TryGetValue("email", out var emailFilter)
            && !string.IsNullOrWhiteSpace(emailFilter))
        {
            users = users.Where(u =>
                u.Email != null && EF.Functions.Like(u.Email, $"%{emailFilter}%"));
        }
        if (query.Filters.TryGetValue("displayName", out var nameFilter)
            && !string.IsNullOrWhiteSpace(nameFilter))
        {
            users = users.Where(u => EF.Functions.Like(u.DisplayName, $"%{nameFilter}%"));
        }
        if (query.Filters.TryGetValue("state", out var stateFilter)
            && !string.IsNullOrWhiteSpace(stateFilter)
            && Enum.TryParse<AccountState>(stateFilter, ignoreCase: true, out var parsedState))
        {
            users = users.Where(u => u.AccountState == parsedState);
        }
        if (query.Filters.TryGetValue("twoFactor", out var twoFactorFilter)
            && bool.TryParse(twoFactorFilter, out var twoFactorOn))
        {
            users = users.Where(u => u.TwoFactorEnabled == twoFactorOn);
        }

        // -- Sort -----------------------------------------------------------
        users = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("email", true) => users.OrderByDescending(u => u.Email),
            ("email", false) => users.OrderBy(u => u.Email),
            ("displayname", true) => users.OrderByDescending(u => u.DisplayName),
            ("displayname", false) => users.OrderBy(u => u.DisplayName),
            ("state", true) => users.OrderByDescending(u => u.AccountState),
            ("state", false) => users.OrderBy(u => u.AccountState),
            ("createdat", false) => users.OrderBy(u => u.CreatedAt),
            // Natural order: newest first (matches the pre-D-044 behaviour).
            _ => users.OrderByDescending(u => u.CreatedAt),
        };

        var total = await users.CountAsync(cancellationToken);

        // Per-row role flag — projected inside the EF query (D-045 H1 N+1 fix).
        var rows = await users
            .Skip(skip)
            .Take(top)
            .Select(user => new
            {
                user.Id,
                user.Email,
                user.DisplayName,
                user.AccountState,
                user.TwoFactorEnabled,
                user.CreatedAt,
                IsAdmin = adminRoleId != null
                    && dbContext.UserRoles.Any(ur =>
                        ur.UserId == user.Id && ur.RoleId == adminRoleId),
            })
            .ToListAsync(cancellationToken);

        var summaries = rows
            .Select(row => new AdminUserSummary(
                row.Id,
                row.Email ?? string.Empty,
                row.DisplayName,
                row.AccountState.ToString(),
                row.TwoFactorEnabled,
                row.IsAdmin,
                row.CreatedAt))
            .ToList();

        return GridPage<AdminUserSummary>.Of(summaries, total,
            new GridQuery { Skip = skip, Top = top });
    }

    private async Task<Guid?> GetAdministratorRoleIdAsync(CancellationToken cancellationToken)
    {
        var role = await roleManager.FindByNameAsync(AdministratorRole);
        return role?.Id;
    }

    /// <summary>P4 — every CP role id present in the database. The set is
    /// (Administrator, Staff, Scientific, Security); missing roles are
    /// dropped silently so the seeder is the single source of role identity.</summary>
    private async Task<IReadOnlyList<Guid>> GetCpRoleIdsAsync(CancellationToken cancellationToken)
    {
        var ids = new List<Guid>(AppRoles.CpRoles.Count);
        foreach (var name in AppRoles.CpRoles)
        {
            var role = await roleManager.FindByNameAsync(name);
            if (role is not null) { ids.Add(role.Id); }
        }
        return ids;
    }

    // -- P4 — approval workflow ----------------------------------------------

    public Task ApproveStaffAsync(
        Guid actorUserId, Guid subjectUserId, CancellationToken cancellationToken = default) =>
        ApproveAsync(actorUserId, subjectUserId, isStaff: true, cancellationToken);

    public Task ApproveVisitorAsync(
        Guid actorUserId, Guid subjectUserId, CancellationToken cancellationToken = default) =>
        ApproveAsync(actorUserId, subjectUserId, isStaff: false, cancellationToken);

    public Task RejectStaffAsync(
        Guid actorUserId, Guid subjectUserId, AdminRejectRequest request,
        CancellationToken cancellationToken = default) =>
        RejectAsync(actorUserId, subjectUserId, request, isStaff: true, cancellationToken);

    public Task RejectVisitorAsync(
        Guid actorUserId, Guid subjectUserId, AdminRejectRequest request,
        CancellationToken cancellationToken = default) =>
        RejectAsync(actorUserId, subjectUserId, request, isStaff: false, cancellationToken);

    private async Task ApproveAsync(
        Guid actorUserId, Guid subjectUserId, bool isStaff, CancellationToken cancellationToken)
    {
        var subject = await LoadPendingSubjectAsync(subjectUserId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        subject.AccountState = AccountState.Approved;
        subject.UpdatedAt = now;

        // QR id mints at approval (D-046, P4) — idempotent.
        await qrIdMinter.MintIfMissingAsync(subject, cancellationToken);
        await userManager.UpdateAsync(subject);

        var eventType = isStaff
            ? AuditEvents.AdminStaffApproved
            : AuditEvents.AdminVisitorApproved;
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = eventType,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            SubjectUserId = subject.Id,
            SubjectEmail = subject.Email,
            Detail = subject.QrId,
        }, cancellationToken);
    }

    private async Task RejectAsync(
        Guid actorUserId, Guid subjectUserId, AdminRejectRequest request,
        bool isStaff, CancellationToken cancellationToken)
    {
        var subject = await LoadPendingSubjectAsync(subjectUserId, cancellationToken);
        var now = timeProvider.GetUtcNow();
        subject.AccountState = AccountState.Rejected;
        subject.UpdatedAt = now;
        await userManager.UpdateAsync(subject);

        var eventType = isStaff
            ? AuditEvents.AdminStaffRejected
            : AuditEvents.AdminVisitorRejected;
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = eventType,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            SubjectUserId = subject.Id,
            SubjectEmail = subject.Email,
            Detail = request.Reason,
        }, cancellationToken);
    }

    /// <summary>Loads a user that must currently be in PendingApproval —
    /// any other state throws <see cref="ApiException"/>. Shared by the
    /// four approve/reject helpers above.</summary>
    private async Task<SimfUser> LoadPendingSubjectAsync(
        Guid subjectUserId, CancellationToken cancellationToken)
    {
        var subject = await userManager.FindByIdAsync(subjectUserId.ToString())
            ?? throw new ApiException(ErrorCodes.AdminUserNotFound, 404,
                "The target account was not found.",
                "تعذّر العثور على الحساب المستهدف.");
        if (subject.AccountState != AccountState.PendingApproval)
        {
            throw new ApiException(ErrorCodes.AdminUserNotPending, 409,
                "The target account is not pending approval.",
                "الحساب المستهدف ليس في انتظار الموافقة.");
        }
        return subject;
    }

    public Task<GridPage<AdminPendingUserSummary>> ListPendingStaffAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        ListPendingAsync(query, staffOnly: true, cancellationToken);

    public Task<GridPage<AdminPendingUserSummary>> ListPendingVisitorsAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        ListPendingAsync(query, staffOnly: false, cancellationToken);

    /// <summary>P4 — pending-approval list. Same staff/visitor split as
    /// <see cref="ListAccountsAsync"/>, narrowed to PendingApproval rows.</summary>
    private async Task<GridPage<AdminPendingUserSummary>> ListPendingAsync(
        GridQuery query, bool staffOnly, CancellationToken cancellationToken)
    {
        var skip = query.Skip < 0 ? 0 : query.Skip;
        var top = query.Top switch { < 1 => 20, > 200 => 200, _ => query.Top };

        var cpRoleIds = await GetCpRoleIdsAsync(cancellationToken);
        var users = userManager.Users
            .Where(u => u.AccountState == AccountState.PendingApproval);
        if (cpRoleIds.Count > 0)
        {
            users = staffOnly
                ? users.Where(u => dbContext.UserRoles.Any(ur =>
                    ur.UserId == u.Id && cpRoleIds.Contains(ur.RoleId)))
                : users.Where(u => !dbContext.UserRoles.Any(ur =>
                    ur.UserId == u.Id && cpRoleIds.Contains(ur.RoleId)));
        }
        else if (staffOnly)
        {
            users = users.Where(_ => false);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            users = users.Where(u =>
                (u.Email != null && EF.Functions.Like(u.Email, $"%{term}%"))
                || EF.Functions.Like(u.DisplayName, $"%{term}%"));
        }

        var total = await users.CountAsync(cancellationToken);
        var page = await users
            .OrderByDescending(u => u.CreatedAt)
            .Skip(skip).Take(top)
            .Select(u => new AdminPendingUserSummary(
                u.Id, u.Email!, u.DisplayName, u.CreatedAt))
            .ToListAsync(cancellationToken);

        return GridPage<AdminPendingUserSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminBulkDeleteResponse> BulkDeleteUsersAsync(
        Guid actorUserId,
        AdminBulkDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var deleted = 0;
        var skipped = 0;

        foreach (var targetId in request.Ids)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var target = await userManager.FindByIdAsync(targetId.ToString());
            if (target is null)
            {
                // D-045 H1: unknown-id is now audited (an enumeration probe
                // against the user table is the exact signature of admin
                // abuse) — was a silent skip before.
                await auditLog.WriteAsync(new AuditEntry
                {
                    EventType = AuditEvents.AdminUserDeleteFailed,
                    Outcome = AuditOutcome.Failure,
                    SubjectUserId = targetId,
                    ActorUserId = actorUserId,
                    ErrorCode = ErrorCodes.AdminUserNotFound,
                    Detail = request.Reason,
                }, cancellationToken);
                skipped++;
                continue;
            }
            if (target.Id == actorUserId
                || await userManager.IsInRoleAsync(target, AdministratorRole))
            {
                // Self-delete and Administrator-vs-Administrator are blocked
                // silently per target — never explode the batch. The skipped
                // count tells the admin how many were left untouched.
                await auditLog.WriteAsync(new AuditEntry
                {
                    EventType = AuditEvents.AdminUserDeleteFailed,
                    Outcome = AuditOutcome.Failure,
                    SubjectEmail = target.Email,
                    SubjectUserId = target.Id,
                    ActorUserId = actorUserId,
                    ErrorCode = target.Id == actorUserId
                        ? ErrorCodes.AdminCannotResetSelf
                        : ErrorCodes.AdminCannotResetAdministrator,
                    Detail = request.Reason,
                }, cancellationToken);
                skipped++;
                continue;
            }

            // D-045 H1: every subject wipes state + revokes sessions inside
            // one transaction; check UpdateAsync.Succeeded so a silent
            // Identity error doesn't pretend success. The audit row is
            // committed in the same transaction as the state change so SOC
            // never sees a delete-without-audit pair.
            var success = false;
            string? failureDetail = null;
            try
            {
                await transactionRunner.ExecuteAsync(async (innerCt) =>
                {
                    target.AccountState = AccountState.Disabled;
                    target.UpdatedAt = now;
                    var updateResult = await userManager.UpdateAsync(target);
                    if (!updateResult.Succeeded)
                    {
                        failureDetail = string.Join("; ",
                            updateResult.Errors.Select(error => error.Description));
                        return;
                    }
                    await userManager.UpdateSecurityStampAsync(target);
                    await refreshTokenRepository.RevokeAllForUserAsync(
                        target.Id, now, innerCt);
                    await auditLog.WriteAsync(new AuditEntry
                    {
                        EventType = AuditEvents.AdminUserDeleted,
                        Outcome = AuditOutcome.Success,
                        SubjectEmail = target.Email,
                        SubjectUserId = target.Id,
                        ActorUserId = actorUserId,
                        Detail = request.Reason,
                    }, innerCt);
                    success = true;
                }, cancellationToken);
            }
            catch (Exception exception) when (
                exception is not OperationCanceledException)
            {
                failureDetail = exception.Message;
            }

            if (success)
            {
                deleted++;
            }
            else
            {
                // Outside the rolled-back transaction so the failure row
                // survives even when the work transaction did not.
                await auditLog.WriteAsync(new AuditEntry
                {
                    EventType = AuditEvents.AdminUserDeleteFailed,
                    Outcome = AuditOutcome.Failure,
                    SubjectEmail = target.Email,
                    SubjectUserId = target.Id,
                    ActorUserId = actorUserId,
                    ErrorCode = ErrorCodes.InternalError,
                    Detail = failureDetail ?? "Delete failed without a recorded reason.",
                }, cancellationToken);
                skipped++;
            }
        }

        logger.LogInformation(
            "Admin {ActorId} bulk-deleted {Deleted} users (skipped {Skipped})",
            actorUserId, deleted, skipped);
        return new AdminBulkDeleteResponse(deleted, skipped);
    }

    public async Task<AdminCreateUserResponse> DuplicateUserAsync(
        Guid actorUserId,
        AdminDuplicateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var source = await userManager.FindByIdAsync(request.SourceId.ToString())
            ?? throw new ApiException(
                ErrorCodes.AdminUserNotFound, 404,
                "The source account was not found.",
                "لم يتم العثور على الحساب المصدر.");

        var sourceIsAdmin = await userManager.IsInRoleAsync(source, AdministratorRole);
        // The duplicate keeps the source's role-membership shape: a staff
        // source duplicates as staff (admin role preserved); a visitor
        // source duplicates as a visitor (no role). One method handles both
        // via the staff-creation path with the role flag set appropriately.
        var created = await CreateStaffAsync(actorUserId,
            new AdminCreateUserRequest
            {
                Email = request.NewEmail,
                DisplayName = source.DisplayName,
                GrantAdministratorRole = sourceIsAdmin,
            },
            cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminUserDuplicated,
            Outcome = AuditOutcome.Success,
            SubjectEmail = request.NewEmail,
            SubjectUserId = created.UserId,
            ActorUserId = actorUserId,
            Detail = $"source={request.SourceId}",
        }, cancellationToken);

        return created;
    }

    public async Task<byte[]> ExportUsersAsync(
        Guid actorUserId,
        AdminExportUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AdminUserSummary> rows;
        if (request.Ids.Count > 0)
        {
            // Selected-ids path — pull, with the role flag projected in one
            // query (D-045 H1, kills the per-row IsInRoleAsync N+1).
            var idSet = request.Ids.ToHashSet();
            var adminRoleId = await GetAdministratorRoleIdAsync(cancellationToken);
            var projected = await userManager.Users
                .Where(u => idSet.Contains(u.Id))
                .Select(u => new
                {
                    u.Id, u.Email, u.DisplayName, u.AccountState,
                    u.TwoFactorEnabled, u.CreatedAt,
                    IsAdmin = adminRoleId != null
                        && dbContext.UserRoles.Any(ur =>
                            ur.UserId == u.Id && ur.RoleId == adminRoleId),
                })
                .ToListAsync(cancellationToken);
            rows = projected
                .Select(p => new AdminUserSummary(
                    p.Id, p.Email ?? string.Empty, p.DisplayName,
                    p.AccountState.ToString(), p.TwoFactorEnabled, p.IsAdmin,
                    p.CreatedAt))
                .ToList();
        }
        else
        {
            // Whole-result-set path — re-run the same query the grid used.
            // Bound to 5 000 rows so an accidental "export everything"
            // doesn't load the entire database into RAM.
            //
            // D-045 H1: clone the incoming query before mutating Skip/Top —
            // the caller may still be holding a reference to it (the CP page
            // does — UsersList.razor passes its own `_query` in), and
            // mutating it in place silently changed the page-size on the
            // next list call.
            var source = request.Query ?? new GridQuery();
            var query = new GridQuery
            {
                Skip = 0,
                Top = 5_000,
                Search = source.Search,
                Sort = source.Sort,
                SortDescending = source.SortDescending,
                Filters = new Dictionary<string, string>(source.Filters),
            };
            // Export operates on STAFF today (the /admin/staff grid is the
            // only consumer that triggers it). When the visitor grid grows
            // its own export, this branches.
            var page = await ListStaffAsync(query, cancellationToken);
            rows = page.Items;
        }

        var bytes = excel.Export(rows);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminUsersExported,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"count={rows.Count}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} exported {Count} users to XLSX",
            actorUserId, rows.Count);
        return bytes;
    }

    public async Task<AdminImportUsersResponse> ImportUsersAsync(
        Guid actorUserId,
        byte[] xlsx,
        CancellationToken cancellationToken = default)
    {
        if (xlsx is null || xlsx.Length == 0)
        {
            throw new ApiException(
                ErrorCodes.AdminImportEmpty, 400,
                "An Excel file is required.",
                "ملف Excel مطلوب.");
        }

        var rows = excel.Parse(xlsx);
        var errors = new List<AdminImportError>();
        var created = 0;
        var skipped = 0;

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Email) || !row.Email.Contains('@'))
            {
                errors.Add(new AdminImportError(row.RowNumber, row.Email,
                    "The email address is missing or invalid."));
                skipped++;
                continue;
            }
            if (string.IsNullOrWhiteSpace(row.DisplayName))
            {
                errors.Add(new AdminImportError(row.RowNumber, row.Email,
                    "The display name is missing."));
                skipped++;
                continue;
            }
            try
            {
                // The XLSX import is the staff bulk-create path (the visitor
                // import is a P4 follow-up).
                await CreateStaffAsync(actorUserId,
                    new AdminCreateUserRequest
                    {
                        Email = row.Email,
                        DisplayName = row.DisplayName,
                        GrantAdministratorRole = row.IsAdministrator,
                    },
                    cancellationToken);
                created++;
            }
            catch (ApiException exception)
                when (exception.Code == ErrorCodes.AdminEmailAlreadyRegistered)
            {
                errors.Add(new AdminImportError(row.RowNumber, row.Email,
                    "An account with this email already exists."));
                skipped++;
            }
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminUsersImported,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"created={created}, skipped={skipped}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} imported {Created} users from XLSX (skipped {Skipped})",
            actorUserId, created, skipped);
        return new AdminImportUsersResponse(created, skipped, errors);
    }

    private Task AuditFailure(
        Guid actorUserId, string email, Guid? targetUserId, string errorCode,
        CancellationToken cancellationToken) =>
        AuditFailure(AuditEvents.AdminTwoFactorResetFailed, actorUserId, email,
            targetUserId, errorCode, cancellationToken);

    private Task AuditFailure(
        string eventType, Guid actorUserId, string email, Guid? targetUserId,
        string errorCode, CancellationToken cancellationToken, string? detail = null) =>
        auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = eventType,
                Outcome = AuditOutcome.Failure,
                SubjectEmail = email,
                SubjectUserId = targetUserId,
                ActorUserId = actorUserId,
                ErrorCode = errorCode,
                Detail = detail,
            },
            cancellationToken);

    private void EnqueueInviteEmail(
        string targetEmail, string displayName, string code, TimeSpan lifetime)
    {
        var days = (int)lifetime.TotalDays;
        var body =
            $"<p>Hello {System.Net.WebUtility.HtmlEncode(displayName)},</p>"
            + "<p>A SIMF account has been created for you. To activate it, "
            + "open the Control Panel and set your password with the "
            + "verification code below.</p>"
            + $"<p><strong>Email:</strong> {System.Net.WebUtility.HtmlEncode(targetEmail)}<br/>"
            + $"<strong>Code:</strong> <strong>{code}</strong><br/>"
            + $"<strong>Valid for:</strong> {days} days.</p>"
            + "<p>If you did not expect this invitation, you can ignore this email.</p>";
        emailQueue.Enqueue(new EmailMessage(
            targetEmail, "SIMF — your account invitation", body));
    }

    private void EnqueueNotificationEmail(string targetEmail, string actorEmail, string reason)
    {
        var body =
            "<p>An administrator has reset the two-factor authentication on your SIMF account.</p>"
            + $"<p><strong>Performed by:</strong> {System.Net.WebUtility.HtmlEncode(actorEmail)}<br/>"
            + $"<strong>Reason:</strong> {System.Net.WebUtility.HtmlEncode(reason)}</p>"
            + "<p>Your authenticator app and any recovery codes are no longer valid. "
            + "Sign in with your password and set up two-factor authentication again "
            + "from your profile page.</p>"
            + "<p>If you did not request this, contact your security team immediately.</p>";
        emailQueue.Enqueue(new EmailMessage(
            targetEmail, "SIMF — your two-factor authentication was reset", body));
    }
}
