// Tests: SIMF.Api.Tests/DelegatesAndBulkBadgesTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Excel;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// D-209 (A2 split): the bulk + duplicate + export + import surface of
/// <see cref="AdminAccountService"/> (D-113 / D-164 / D-045). Bulk-approve
/// delegates to the approval workers; duplicate delegates to the create
/// workers; export re-runs the list query. Split into its own partial-class
/// file for navigability; behaviour and DI are unchanged.
/// </summary>
internal sealed partial class AdminAccountService
{
    public Task<AdminBulkApprovalResponse> BulkApproveVisitorsAsync(
        Guid actorUserId, AdminBulkApprovalRequest request,
        CancellationToken cancellationToken = default) =>
        BulkApproveAsync(actorUserId, request, ApprovalScope.AudienceVisitor, cancellationToken);

    public Task<AdminBulkApprovalResponse> BulkApproveOthersAsync(
        Guid actorUserId, AdminBulkApprovalRequest request,
        CancellationToken cancellationToken = default) =>
        BulkApproveAsync(actorUserId, request, ApprovalScope.PartnerOther, cancellationToken);

    public Task<AdminBulkApprovalResponse> BulkApproveAdminsAsync(
        Guid actorUserId, AdminBulkApprovalRequest request,
        CancellationToken cancellationToken = default) =>
        BulkApproveAsync(actorUserId, request, ApprovalScope.Admin, cancellationToken);

    private async Task<AdminBulkApprovalResponse> BulkApproveAsync(
        Guid actorUserId, AdminBulkApprovalRequest request, ApprovalScope scope,
        CancellationToken cancellationToken)
    {
        // D-164 — distinct ids; cap at 500 per request so the batch fits
        // inside one reasonable transaction window without dragging the
        // hot path. The endpoint rejects empty arrays at the validator.
        var ids = request.Ids.Distinct().Take(500).ToList();
        var approved = 0;
        var failures = new List<AdminBulkApprovalFailure>();
        foreach (var subjectId in ids)
        {
            try
            {
                await ApproveAsync(actorUserId, subjectId, scope, cancellationToken);
                approved++;
            }
            catch (ApiException ex)
            {
                // Each subject's failure is bilingual + typed so the CP
                // can render the inline error list next to each row.
                var email = await accounts
                    .FindByIdAsync(subjectId, cancellationToken);
                failures.Add(new AdminBulkApprovalFailure(
                    subjectId,
                    email?.Email,
                    ex.Code,
                    ex.Message,
                    ex.MessageArabic));
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Bulk-approve subject {SubjectId} failed for actor {ActorId}",
                    subjectId, actorUserId);
                failures.Add(new AdminBulkApprovalFailure(
                    subjectId, null, ErrorCodes.InternalError,
                    "An unexpected error prevented this approval.",
                    "حدث خطأ غير متوقع أثناء اعتماد هذا المستخدم."));
            }
        }
        return new AdminBulkApprovalResponse(approved, failures.Count, failures);
    }

    public Task<AdminBulkRejectResponse> BulkRejectVisitorsAsync(
        Guid actorUserId, AdminBulkRejectRequest request,
        CancellationToken cancellationToken = default) =>
        BulkRejectAsync(actorUserId, request, ApprovalScope.AudienceVisitor, cancellationToken);

    public Task<AdminBulkRejectResponse> BulkRejectOthersAsync(
        Guid actorUserId, AdminBulkRejectRequest request,
        CancellationToken cancellationToken = default) =>
        BulkRejectAsync(actorUserId, request, ApprovalScope.PartnerOther, cancellationToken);

    public Task<AdminBulkRejectResponse> BulkRejectAdminsAsync(
        Guid actorUserId, AdminBulkRejectRequest request,
        CancellationToken cancellationToken = default) =>
        BulkRejectAsync(actorUserId, request, ApprovalScope.Admin, cancellationToken);

    private async Task<AdminBulkRejectResponse> BulkRejectAsync(
        Guid actorUserId, AdminBulkRejectRequest request, ApprovalScope scope,
        CancellationToken cancellationToken)
    {
        // D-209 — mirror of BulkApproveAsync. Distinct ids, capped at 500;
        // each subject is rejected in its own step via the single-reject
        // worker (which owns the scope guard + state flip + token revoke +
        // audit + notification), so per-subject behaviour is identical to a
        // single reject. The shared reason is applied to every subject.
        var ids = request.Ids.Distinct().Take(500).ToList();
        var rejectRequest = new AdminRejectRequest { Reason = request.Reason };
        var rejected = 0;
        var failures = new List<AdminBulkApprovalFailure>();
        foreach (var subjectId in ids)
        {
            try
            {
                await RejectAsync(actorUserId, subjectId, rejectRequest, scope, cancellationToken);
                rejected++;
            }
            catch (ApiException ex)
            {
                var subject = await accounts.FindByIdAsync(subjectId, cancellationToken);
                failures.Add(new AdminBulkApprovalFailure(
                    subjectId,
                    subject?.Email,
                    ex.Code,
                    ex.Message,
                    ex.MessageArabic));
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Bulk-reject subject {SubjectId} failed for actor {ActorId}",
                    subjectId, actorUserId);
                failures.Add(new AdminBulkApprovalFailure(
                    subjectId, null, ErrorCodes.InternalError,
                    "An unexpected error prevented this rejection.",
                    "حدث خطأ غير متوقع أثناء رفض هذا المستخدم."));
            }
        }
        return new AdminBulkRejectResponse(rejected, failures.Count, failures);
    }

    // D-186 — duplicate helper that inspects the source's linked
    // ProfileType to decide whether the duplicate lands on the
    // audience queue or the partner queue. Partner duplicates require
    // the source to have an explicit ProfileTypeId.
    private async Task<AdminCreateUserResponse> DuplicateVisitorScopedAsync(
        Guid actorUserId, SimfUser source, string newEmail,
        Guid? sourceProfileTypeId, CancellationToken cancellationToken)
    {
        var isPartnerSource = sourceProfileTypeId is not null
            && await appDbContext.ProfileTypes
                .AsNoTracking()
                .AnyAsync(p => p.Id == sourceProfileTypeId.Value
                            && p.IsForVisitor == false,
                          cancellationToken);

        if (isPartnerSource)
        {
            return await CreateOtherAsync(actorUserId,
                new AdminCreateOtherRequest
                {
                    Email = newEmail,
                    DisplayName = source.DisplayName,
                    ProfileTypeId = sourceProfileTypeId!.Value,
                },
                cancellationToken);
        }
        return await CreateVisitorAsync(actorUserId,
            new AdminCreateVisitorRequest
            {
                Email = newEmail,
                DisplayName = source.DisplayName,
                ProfileTypeId = sourceProfileTypeId,
            },
            cancellationToken);
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

            var target = await accounts.FindByIdAsync(targetId, cancellationToken);
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
                || await accounts.IsInRoleAsync(target, AdministratorRole))
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
                    var updateResult = await accounts.UpdateAsync(target);
                    if (!updateResult.Succeeded)
                    {
                        failureDetail = string.Join("; ",
                            updateResult.Errors.Select(error => error.Description));
                        return;
                    }
                    await accounts.UpdateSecurityStampAsync(target);
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
        var source = await accounts.FindByIdAsync(request.SourceId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AdminUserNotFound, 404,
                "The source account was not found.",
                "لم يتم العثور على الحساب المصدر.");

        // P7c — the duplicate keeps the source's UserType + role-membership
        // shape: an Admin source duplicates as an Admin (with the same roles);
        // an Other / Visitor source duplicates as the same UserType. P8 — the
        // source's ProfileTypeId now lives on the source's UserProfile row;
        // look it up and pass it through.
        var sourceRoles = await accounts.GetRolesAsync(source);
        var sourceProfileTypeId = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == source.Id)
            .Select(p => p.ProfileTypeId)
            .SingleOrDefaultAsync(cancellationToken);
        // D-186: source UserType is now Admin or Visitor. For Visitor
        // sources we check the linked ProfileType.IsVisitor to decide
        // whether to route through CreateOther (partner scope) or
        // CreateVisitor (audience scope) so the duplicate inherits the
        // source's queue.
        var created = source.UserType == UserType.Admin
            ? await CreateAdminAsync(actorUserId,
                new AdminCreateAdminRequest
                {
                    Email = request.NewEmail,
                    DisplayName = source.DisplayName,
                    Roles = sourceRoles.ToList(),
                },
                cancellationToken)
            : await DuplicateVisitorScopedAsync(
                actorUserId, source, request.NewEmail,
                sourceProfileTypeId, cancellationToken);

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

    // The whole-grid user export is bounded to this many rows so an accidental
    // "export everything" never loads the entire table into memory. Each page is
    // built via GridExportPaging.Page (a fresh GridQuery, so the caller's own live
    // query is never mutated — D-045 H1, the CP page passes its own `_query` in).
    private const int ExportRowCap = 5_000;

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
            var projected = await dbContext.Users
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
            // Whole-result-set path — page through the same query the grid used
            // (ListAdminsAsync clamps Top to its 200-row page size) until the whole
            // set is collected or the export cap is reached, so a >200-row grid is
            // not silently truncated to the first page (D-642). Bounded to
            // ExportRowCap rows so an accidental "export everything" never loads the
            // entire table into memory.
            //
            // P7c — export operates on the Admin family today (the /admin/admins
            // grid is the only consumer that triggers it). When the Other / Visitor
            // grids grow their own export, this branches on a request-side
            // `UserType` filter.
            var source = request.Query ?? new GridQuery();
            rows = await GridExportPaging.CollectAllAsync(
                async skip => (await ListAdminsAsync(
                    GridExportPaging.Page(source, skip, ExportRowCap), cancellationToken)).Items,
                ExportRowCap);
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
                // P7c — the XLSX import is the Admin-family bulk-create
                // path. The `IsAdministrator` flag on the imported row
                // chooses whether the new admin gets the Administrator
                // RBAC role; the UserType is always Admin here.
                await CreateAdminAsync(actorUserId,
                    new AdminCreateAdminRequest
                    {
                        Email = row.Email,
                        DisplayName = row.DisplayName,
                        Roles = row.IsAdministrator
                            ? new List<string> { AppRoles.Administrator }
                            : new List<string>(),
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

    // -- D-113 — type-scoped bulk operations for /admin/visitors/* and
    //            /admin/others/*. Each method narrows the existing helper
    //            by SimfUser.UserType so the Admin grid surface above
    //            stays bit-for-bit unchanged.

    public async Task<AdminBulkDeleteResponse> BulkDeleteUsersByKindAsync(
        Guid actorUserId,
        UserType kind,
        bool? requirePartnerScope,
        AdminBulkDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var deleted = 0;
        var skipped = 0;

        foreach (var targetId in request.Ids)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var target = await accounts.FindByIdAsync(targetId, cancellationToken);
            var scopeOk = target is not null
                && target.UserType == kind
                && (requirePartnerScope is null
                    || await SubjectMatchesProfileScopeAsync(
                        target.Id, !requirePartnerScope.Value, cancellationToken));
            if (target is null || !scopeOk)
            {
                // Wrong-type or missing — audited as the same "not found"
                // code so a probing admin cannot tell the two apart.
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
                || await accounts.IsInRoleAsync(target, AdministratorRole))
            {
                // Self-delete and Administrator-vs-Administrator are blocked
                // silently per target — identical guard to the Admin-grid
                // path so a stray Administrator-roled Visitor / Other in
                // the batch still can't be deleted via this surface.
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

            var success = false;
            string? failureDetail = null;
            try
            {
                await transactionRunner.ExecuteAsync(async (innerCt) =>
                {
                    target.AccountState = AccountState.Disabled;
                    target.UpdatedAt = now;
                    var updateResult = await accounts.UpdateAsync(target);
                    if (!updateResult.Succeeded)
                    {
                        failureDetail = string.Join("; ",
                            updateResult.Errors.Select(error => error.Description));
                        return;
                    }
                    await accounts.UpdateSecurityStampAsync(target);
                    await refreshTokenRepository.RevokeAllForUserAsync(
                        target.Id, now, innerCt);
                    await auditLog.WriteAsync(new AuditEntry
                    {
                        EventType = AuditEvents.AdminUserDeleted,
                        Outcome = AuditOutcome.Success,
                        SubjectEmail = target.Email,
                        SubjectUserId = target.Id,
                        ActorUserId = actorUserId,
                        Detail = $"kind={kind}; {request.Reason}",
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
            "Admin {ActorId} bulk-deleted {Deleted} {Kind} (skipped {Skipped})",
            actorUserId, deleted, kind, skipped);
        return new AdminBulkDeleteResponse(deleted, skipped);
    }

    public async Task<AdminCreateUserResponse> DuplicateUserByKindAsync(
        Guid actorUserId,
        UserType kind,
        bool? requirePartnerScope,
        AdminDuplicateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var source = await accounts.FindByIdAsync(request.SourceId, cancellationToken);
        var scopeOk = source is not null
            && source.UserType == kind
            && (requirePartnerScope is null
                || await SubjectMatchesProfileScopeAsync(
                    source.Id, !requirePartnerScope.Value, cancellationToken));
        if (source is null || !scopeOk)
        {
            // 404 for either branch — never reveal "the id exists but is
            // the wrong type / wrong scope" to the duplicating admin.
            throw new ApiException(
                ErrorCodes.AdminUserNotFound, 404,
                "The source account was not found.",
                "لم يتم العثور على الحساب المصدر.");
        }
        // Source UserType is already proven == kind, so DuplicateUserAsync's
        // switch lands on the matching CreateXxxAsync without any extra
        // guarding. Reuses the canonical implementation.
        return await DuplicateUserAsync(actorUserId, request, cancellationToken);
    }

    public async Task<byte[]> ExportUsersByKindAsync(
        Guid actorUserId,
        UserType kind,
        bool? requirePartnerScope,
        AdminExportUsersRequest request,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AdminUserSummary> rows;
        // D-186: convert the new partner-scope flag to the audience-vs-
        // partner profileScope notion ListAccountsAsync uses.
        bool? profileScope = requirePartnerScope switch
        {
            true => false,    // partner side → ProfileType.IsVisitor=false
            false => true,    // audience side → ProfileType.IsVisitor=true (or no profile)
            null => null,
        };
        if (request.Ids.Count > 0)
        {
            // Selected-ids path — narrow by both id AND UserType so a
            // smuggled wrong-type id never appears in the workbook.
            // D-186: also narrow by the profile-scope id set so a
            // wrong-scope id (e.g. an audience-side visitor smuggled
            // into the Others export) does not leak in either.
            var idSet = request.Ids.ToHashSet();
            var scopedIds = await ResolveProfileScopedUserIdsAsync(
                profileScope, cancellationToken);
            if (scopedIds is not null)
            {
                idSet.IntersectWith(scopedIds);
            }
            var adminRoleId = await GetAdministratorRoleIdAsync(cancellationToken);
            var projected = await dbContext.Users
                .Where(u => idSet.Contains(u.Id) && u.UserType == kind)
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
            // Whole-result-set path — page through the matching kind (see
            // ExportUsersAsync) until the whole set is collected or the export cap
            // is reached, so a >200-row grid is not truncated to the first page.
            var source = request.Query ?? new GridQuery();
            rows = await GridExportPaging.CollectAllAsync(
                async skip => (await ListAccountsAsync(
                    GridExportPaging.Page(source, skip, ExportRowCap), kind, profileScope, cancellationToken)).Items,
                ExportRowCap);
        }

        var bytes = excel.Export(rows);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminUsersExported,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"kind={kind}; count={rows.Count}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} exported {Count} {Kind} to XLSX",
            actorUserId, rows.Count, kind);
        return bytes;
    }

    // D-186 — bulk-import always creates Visitor-typed accounts;
    // partnerScope=true additionally requires a ProfileTypeId per row
    // and the chosen ProfileType.IsVisitor must be false.
    public async Task<AdminImportUsersResponse> ImportUsersByKindAsync(
        Guid actorUserId,
        bool partnerScope,
        byte[] xlsx,
        CancellationToken cancellationToken = default)
    {
        var kind = UserType.Visitor;
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
            // D-186: partner-side imports require a parseable
            // ProfileTypeId (matches AdminCreateOtherRequest validator).
            // Audience-side imports accept null (tier is optional).
            if (partnerScope && row.ProfileTypeId is null)
            {
                errors.Add(new AdminImportError(row.RowNumber, row.Email,
                    "A ProfileTypeId is required for a partner-kind import."));
                skipped++;
                continue;
            }

            try
            {
                if (partnerScope)
                {
                    // ProfileTypeId is non-null per the guard above.
                    await CreateOtherAsync(actorUserId,
                        new AdminCreateOtherRequest
                        {
                            Email = row.Email,
                            DisplayName = row.DisplayName,
                            ProfileTypeId = row.ProfileTypeId!.Value,
                        },
                        cancellationToken);
                }
                else
                {
                    await CreateVisitorAsync(actorUserId,
                        new AdminCreateVisitorRequest
                        {
                            Email = row.Email,
                            DisplayName = row.DisplayName,
                            ProfileTypeId = row.ProfileTypeId,
                        },
                        cancellationToken);
                }
                created++;
            }
            catch (ApiException exception)
                when (exception.Code == ErrorCodes.AdminEmailAlreadyRegistered)
            {
                errors.Add(new AdminImportError(row.RowNumber, row.Email,
                    "An account with this email already exists."));
                skipped++;
            }
            catch (ApiException exception)
                when (exception.Code == ErrorCodes.AdminProfileTypeInvalid)
            {
                errors.Add(new AdminImportError(row.RowNumber, row.Email,
                    "The selected profile type is not valid for this user type."));
                skipped++;
            }
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminUsersImported,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"kind={kind}; created={created}; skipped={skipped}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} imported {Created} {Kind} from XLSX (skipped {Skipped})",
            actorUserId, created, kind, skipped);
        return new AdminImportUsersResponse(created, skipped, errors);
    }

    public async Task<AdminBulkGenerateBadgesResponse> BulkGenerateBadgesAsync(
        Guid actorUserId, UserType kind, AdminBulkGenerateBadgesRequest request,
        CancellationToken cancellationToken = default)
    {
        // D-473 (#10) — bounded so a typo can't generate a runaway number of rows.
        const int MaxPerRequest = 1000;

        var batches = (request.Batches ?? new List<BulkBadgeBatch>())
            .Where(b => b.Count > 0)
            .ToList();
        if (batches.Count == 0)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                "Provide at least one batch with a positive count.",
                "أدخل دفعة واحدة على الأقل بعدد موجب.");
        }
        if (batches.Sum(b => (long)b.Count) > MaxPerRequest)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                $"At most {MaxPerRequest} badges can be generated per request.",
                $"يمكن توليد {MaxPerRequest} شارة كحدّ أقصى في الطلب الواحد.");
        }

        var now = timeProvider.GetUtcNow();
        var created = 0;

        // Pre-validate EVERY batch's profile type BEFORE creating any account, so an
        // invalid later batch is a clean 400 with nothing persisted (mirrors the
        // up-front empty / cap checks above). Without this pass an invalid Nth batch
        // would 400 while earlier batches' Approved badges were already committed —
        // and a 4xx must have no side effects (SIMF-API-001). No cross-DB transaction
        // (D-157): this only reads the App DB up front; nothing is written until every
        // batch has passed.
        var plan = new List<(BulkBadgeBatch Batch, UserProfileType ProfileType)>(batches.Count);
        foreach (var batch in batches)
        {
            var profileType = await appDbContext.ProfileTypes
                .AsNoTracking()
                .SingleOrDefaultAsync(p => p.Id == batch.ProfileTypeId && p.IsActive, cancellationToken)
                ?? throw new ApiException(
                    ErrorCodes.AdminProfileTypeInvalid, 400,
                    "The selected profile type is not valid.",
                    "نوع الملف الشخصي المحدّد غير صالح.");

            // Bulk badges are audience tiers (VIP / Normal / …). Refuse partner /
            // elevated-role types — a bulk Approved badge of an elevated MobileAppRole
            // would hand out QR-accessible elevated authority (least-privilege).
            if (!profileType.IsForVisitor)
            {
                throw new ApiException(
                    ErrorCodes.AdminProfileTypeInvalid, 400,
                    "Bulk-generate is only available for audience (visitor) profile types.",
                    "توليد الشارات بالجملة متاح فقط لأنواع ملفات الجمهور (الزوار).");
            }

            plan.Add((batch, profileType));
        }

        foreach (var (batch, profileType) in plan)
        {
            // NOTE: each badge writes a SimfUser (Identity DB) then its UserProfile
            // (App DB) with no distributed transaction (D-157). A mid-loop failure
            // can leave the last user without a profile — the established walk-in
            // trade-off; the already-created badges stay valid.
            for (var i = 0; i < batch.Count; i++)
            {
                // Synthesized login (no real email / password) — the QR is the
                // access key, exactly like the walk-in desk's no-email path.
                var email = $"badge-{Guid.NewGuid():N}@simf.local";
                var displayName = $"{profileType.Name} #{created + 1}";
                var user = new SimfUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    DisplayName = displayName,
                    // A pre-generated badge is ready to hand out — Approved with a QR.
                    AccountState = AccountState.Approved,
                    UserType = kind,
                    PasswordChangeRequired = false,
                    CreatedAt = now,
                    StateChangedAt = now,
                    StateChangedByUserId = actorUserId,
                };
                var createResult = await accounts.CreateAsync(user);
                if (!createResult.Succeeded)
                {
                    throw new ApiException(
                        ErrorCodes.InternalError, 500,
                        "A badge account could not be created.",
                        "تعذّر إنشاء حساب الشارة.");
                }

                var profile = new UserProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    ProfileTypeId = profileType.Id,
                    Name = displayName,
                    NameArabic = profileType.NameArabic,
                    // Placeholder default data — filled in when the badge is assigned.
                    NationalityId = 0,
                    IsDelegate = request.IsDelegate,
                    CreatedAt = now,
                };
                // Mint + save per badge so the QR-uniqueness check sees prior rows.
                await qrIdMinter.MintIfMissingAsync(profile, cancellationToken);
                appDbContext.UserProfiles.Add(profile);
                await appDbContext.SaveChangesAsync(cancellationToken);
                created++;
            }
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminBulkBadgesGenerated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"created={created}; isDelegate={request.IsDelegate}",
        }, cancellationToken);
        logger.LogInformation(
            "Admin {ActorId} bulk-generated {Created} badges (isDelegate={IsDelegate}).",
            actorUserId, created, request.IsDelegate);

        return new AdminBulkGenerateBadgesResponse(created);
    }
}
