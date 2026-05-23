// Tests: SIMF.Api.Tests/AdminResetTwoFactorTests.cs,
//        SIMF.Api.Tests/AdminCreateUserTests.cs
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.Excel;
using SIMF.Application.IdentityAccess;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// Admin-driven user-management use cases (decisions D-041, D-042). First
/// slice of the User Management module from <c>myComment</c> #33 — reset
/// 2FA, create a new CP user, list every account.
/// </summary>
internal sealed class AdminAccountService(
    UserManager<SimfUser> userManager,
    IRefreshTokenRepository refreshTokenRepository,
    IRecoveryCodeService recoveryCodes,
    IAccountCodeRepository accountCodeRepository,
    IEmailQueue emailQueue,
    IAuditLog auditLog,
    IUserExcelService excel,
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

    public async Task<AdminCreateUserResponse> CreateUserAsync(
        Guid actorUserId,
        AdminCreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (await userManager.FindByEmailAsync(request.Email) is not null)
        {
            await AuditFailure(
                AuditEvents.AdminUserCreateFailed, actorUserId, request.Email, null,
                ErrorCodes.AdminEmailAlreadyRegistered, cancellationToken);
            throw new ApiException(
                ErrorCodes.AdminEmailAlreadyRegistered, 409,
                "An account with this email address already exists.",
                "يوجد حساب مسجّل بهذا البريد الإلكتروني بالفعل.");
        }

        var now = timeProvider.GetUtcNow();
        var user = new SimfUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            DisplayName = request.DisplayName,
            AccountState = AccountState.Approved,
            PasswordChangeRequired = false,
            CreatedAt = now,
        };
        // No password yet — the new user sets it via the invite link.
        var createResult = await userManager.CreateAsync(user);
        if (!createResult.Succeeded)
        {
            await AuditFailure(
                AuditEvents.AdminUserCreateFailed, actorUserId, request.Email, null,
                ErrorCodes.InternalError, cancellationToken,
                detail: string.Join("; ", createResult.Errors.Select(error => error.Description)));
            throw new ApiException(
                ErrorCodes.InternalError, 500,
                "The account could not be created.",
                "تعذّر إنشاء الحساب.");
        }

        if (request.GrantAdministratorRole)
        {
            await userManager.AddToRoleAsync(user, AdministratorRole);
        }

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
                Detail = request.GrantAdministratorRole ? "role=Administrator" : null,
            },
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created user {Email} (Administrator={IsAdmin})",
            actorUserId, user.Email, request.GrantAdministratorRole);
        return new AdminCreateUserResponse(
            user.Id, user.Email!, (int)inviteLifetime.TotalSeconds);
    }

    public async Task<GridPage<AdminUserSummary>> ListUsersAsync(
        GridQuery query,
        CancellationToken cancellationToken = default)
    {
        // Normalise: clamp Top to [1..200], clamp Skip to [0..). The grid
        // contract (SIMF.Common.GridQuery) says the endpoint owns the clamp.
        var skip = query.Skip < 0 ? 0 : query.Skip;
        var top = query.Top switch { < 1 => 20, > 200 => 200, _ => query.Top };

        // Build the query over UserManager.Users so the EF tracker stays out.
        var users = userManager.Users.AsQueryable();

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
        var page = await users.Skip(skip).Take(top).ToListAsync(cancellationToken);

        // The role flag needs UserManager.IsInRoleAsync (not a join the
        // store guarantees). Cost: one query per row — acceptable at the
        // bounded page size; if it ever isn't, fold the role into the
        // projection by joining AspNetUserRoles directly.
        var summaries = new List<AdminUserSummary>(page.Count);
        foreach (var user in page)
        {
            var isAdmin = await userManager.IsInRoleAsync(user, AdministratorRole);
            summaries.Add(new AdminUserSummary(
                user.Id,
                user.Email ?? string.Empty,
                user.DisplayName,
                user.AccountState.ToString(),
                user.TwoFactorEnabled,
                isAdmin,
                user.CreatedAt));
        }
        return GridPage<AdminUserSummary>.Of(summaries, total,
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
            var target = await userManager.FindByIdAsync(targetId.ToString());
            if (target is null)
            {
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

            target.AccountState = AccountState.Disabled;
            target.UpdatedAt = now;
            await userManager.UpdateAsync(target);
            await userManager.UpdateSecurityStampAsync(target);
            await refreshTokenRepository.RevokeAllForUserAsync(target.Id, now, cancellationToken);

            await auditLog.WriteAsync(new AuditEntry
            {
                EventType = AuditEvents.AdminUserDeleted,
                Outcome = AuditOutcome.Success,
                SubjectEmail = target.Email,
                SubjectUserId = target.Id,
                ActorUserId = actorUserId,
                Detail = request.Reason,
            }, cancellationToken);
            deleted++;
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
        var created = await CreateUserAsync(actorUserId,
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
            // Selected-ids path — pull and order to match the on-screen sort.
            var idSet = request.Ids.ToHashSet();
            var users = await userManager.Users
                .Where(u => idSet.Contains(u.Id))
                .ToListAsync(cancellationToken);
            var built = new List<AdminUserSummary>(users.Count);
            foreach (var user in users)
            {
                var isAdmin = await userManager.IsInRoleAsync(user, AdministratorRole);
                built.Add(new AdminUserSummary(
                    user.Id, user.Email ?? string.Empty, user.DisplayName,
                    user.AccountState.ToString(), user.TwoFactorEnabled, isAdmin,
                    user.CreatedAt));
            }
            rows = built;
        }
        else
        {
            // Whole-result-set path — re-run the same query the grid used.
            // Bound to 5 000 rows so an accidental "export everything"
            // doesn't load the entire database into RAM.
            var query = request.Query ?? new GridQuery();
            query.Skip = 0;
            query.Top = 5_000;
            var page = await ListUsersAsync(query, cancellationToken);
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
                await CreateUserAsync(actorUserId,
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
