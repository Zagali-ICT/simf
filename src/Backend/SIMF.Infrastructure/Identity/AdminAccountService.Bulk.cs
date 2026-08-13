// Tests: SIMF.Api.Tests/DelegatesAndBulkBadgesTests.cs
using System.Globalization;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QRCoder;
using QuestPDF.Fluent;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.Excel;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.Badges;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// The bulk + duplicate + export + import surface of
/// <see cref="AdminAccountService"/>. Bulk-approve
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
        // Distinct ids; cap at 500 per request so the batch fits
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
        // Mirror of BulkApproveAsync. Distinct ids, capped at 500;
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

    // Duplicate helper that inspects the source's linked
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
        var now = timeProvider.SimfNow();
        var deleted = 0;
        var skipped = 0;

        foreach (var targetId in request.Ids)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var target = await accounts.FindByIdAsync(targetId, cancellationToken);
            if (target is null)
            {
                // Unknown-id is now audited (an enumeration probe
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

            // Every subject wipes state + revokes sessions inside
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

        // The duplicate keeps the source's UserType + role-membership
        // shape: an Admin source duplicates as an Admin (with the same roles);
        // an Other / Visitor source duplicates as the same UserType. The
        // source's ProfileTypeId lives on the source's UserProfile row;
        // look it up and pass it through.
        var sourceRoles = await accounts.GetRolesAsync(source);
        var sourceProfileTypeId = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == source.Id)
            .Select(p => p.ProfileTypeId)
            .SingleOrDefaultAsync(cancellationToken);
        // Source UserType is now Admin or Visitor. For Visitor
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
    // query is never mutated — the CP page passes its own `_query` in).
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
            // query, which kills the per-row IsInRoleAsync N+1.
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
            // not silently truncated to the first page. Bounded to
            // ExportRowCap rows so an accidental "export everything" never loads the
            // entire table into memory.
            //
            // Export operates on the Admin family today (the /admin/admins
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

        await auditLog.WriteSuccessAsync(
            AuditEvents.AdminUsersExported, actorUserId, $"count={rows.Count}", cancellationToken);

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
                // The XLSX import is the Admin-family bulk-create
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

        await auditLog.WriteSuccessAsync(
            AuditEvents.AdminUsersImported,
            actorUserId,
            $"created={created}, skipped={skipped}",
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} imported {Created} users from XLSX (skipped {Skipped})",
            actorUserId, created, skipped);
        return new AdminImportUsersResponse(created, skipped, errors);
    }

    // -- Type-scoped bulk operations for /admin/visitors/* and
    //    /admin/others/*. Each method narrows the existing helper
    //    by SimfUser.UserType so the Admin grid surface above
    //    stays bit-for-bit unchanged.

    public async Task<AdminBulkDeleteResponse> BulkDeleteUsersByKindAsync(
        Guid actorUserId,
        UserType kind,
        bool? requirePartnerScope,
        AdminBulkDeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.SimfNow();
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
        // Convert the new partner-scope flag to the audience-vs-
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
            // Also narrow by the profile-scope id set so a
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

        await auditLog.WriteSuccessAsync(
            AuditEvents.AdminUsersExported,
            actorUserId,
            $"kind={kind}; count={rows.Count}",
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} exported {Count} {Kind} to XLSX",
            actorUserId, rows.Count, kind);
        return bytes;
    }

    // Bulk-import always creates Visitor-typed accounts;
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
            // Partner-side imports require a parseable
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

        await auditLog.WriteSuccessAsync(
            AuditEvents.AdminUsersImported,
            actorUserId,
            $"kind={kind}; created={created}; skipped={skipped}",
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} imported {Created} {Kind} from XLSX (skipped {Skipped})",
            actorUserId, created, kind, skipped);
        return new AdminImportUsersResponse(created, skipped, errors);
    }

    public async Task<AdminBulkGenerateBadgesResponse> BulkGenerateBadgesAsync(
        Guid actorUserId, UserType kind, AdminBulkGenerateBadgesRequest request,
        CancellationToken cancellationToken = default)
    {
        // Bounded so a typo can't generate a runaway number of rows.
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

        // Validate the optional organiser recipient in the SAME
        // pre-write pass as the empty / cap / profile-type checks below, so an
        // invalid address is a clean 400 with zero accounts created (a 4xx must
        // have no side effects). Empty / whitespace = no email.
        const int MaxRecipientEmailLength = 256;
        string? recipient = null;
        if (!string.IsNullOrWhiteSpace(request.RecipientEmail))
        {
            recipient = request.RecipientEmail.Trim();
            if (recipient.Length > MaxRecipientEmailLength
                || !System.Net.Mail.MailAddress.TryCreate(recipient, out var parsed)
                || !parsed.Host.Contains('.'))
            {
                throw new ApiException(
                    ErrorCodes.ValidationFailed, 400,
                    "The organiser email address is not valid.",
                    "عنوان البريد الإلكتروني للمنظّم غير صالح.");
            }
        }

        var now = timeProvider.SimfNow();
        var created = 0;

        // Pre-validate EVERY batch's profile type BEFORE creating any account, so an
        // invalid later batch is a clean 400 with nothing persisted (mirrors the
        // up-front empty / cap checks above). Without this pass an invalid Nth batch
        // would 400 while earlier batches' Approved badges were already committed —
        // and a 4xx must have no side effects. There is no transaction spanning the
        // two databases to roll that back, so this pass only READS the App DB up
        // front; nothing is written until every batch has passed.
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

        // Persist the batch so this generated set can be
        // re-emailed / revoked together later. Created + saved BEFORE the badge loop
        // so each profile's BadgeBatchId FK resolves. CountsSummary is built
        // invariant-culture (an ar-SA request culture would otherwise store
        // Arabic-Indic digits). × is the multiplication sign, kept per the house
        // doc rule. Same App DB only — no cross-DB write.
        var badgeBatch = new BadgeBatch
        {
            Id = Guid.NewGuid(),
            CountsSummary = string.Join(" + ", plan.Select(p =>
                $"{p.ProfileType.Name} × {p.Batch.Count.ToString(CultureInfo.InvariantCulture)}")),
            TotalCount = plan.Sum(p => p.Batch.Count),
            IsDelegate = request.IsDelegate,
            RecipientEmail = recipient,
            CreatedAt = now,
            CreatedBy = actorUserId,
        };
        appDbContext.BadgeBatches.Add(badgeBatch);
        await appDbContext.SaveChangesAsync(cancellationToken);

        // Collected only when an organiser recipient is supplied; drives the
        // ZIP-of-PNGs built after the badges commit. (profile-type name, 1-based seq,
        // minted QR id) per badge.
        var badgeArtifacts = recipient is null
            ? null
            : new List<(string ProfileTypeName, int Seq, string QrId)>(batches.Sum(b => b.Count));

        foreach (var (batch, profileType) in plan)
        {
            // NOTE: each badge writes a SimfUser (Identity DB) then its UserProfile
            // (App DB) with no distributed transaction — the two databases are
            // physically separate. A mid-loop failure
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
                    // Back-reference to the persisted batch.
                    BadgeBatchId = badgeBatch.Id,
                    // The badge is printed ready to use, and every gate reads
                    // admission HERE rather than on the account. Leaving it at
                    // the pending default would print a box of badges that scan
                    // correctly and are refused at every door.
                    AdmissionState = AccountState.Approved,
                    StateChangedAt = now,
                    StateChangedByUserId = actorUserId,
                    CreatedAt = now,
                };
                // Mint + save per badge so the QR-uniqueness check sees prior rows.
                var qrId = await qrIdMinter.MintIfMissingAsync(profile, cancellationToken);
                appDbContext.UserProfiles.Add(profile);
                await appDbContext.SaveChangesAsync(cancellationToken);
                badgeArtifacts?.Add((profileType.Name, created + 1, qrId));
                created++;
            }
        }

        await auditLog.WriteSuccessAsync(
            AuditEvents.AdminBulkBadgesGenerated,
            actorUserId,
            $"created={created}; isDelegate={request.IsDelegate}; batchId={badgeBatch.Id}",
            cancellationToken);
        logger.LogInformation(
            "Admin {ActorId} bulk-generated {Created} badges (isDelegate={IsDelegate}).",
            actorUserId, created, request.IsDelegate);

        // When an organiser recipient was supplied, render the QR pack and email
        // it. The badges are already committed; a mail-side failure must NOT roll them
        // back (the helper dispatches through the swallow-and-audit path).
        var emailQueued = false;
        if (recipient is not null && badgeArtifacts is { Count: > 0 })
        {
            await EnqueueBadgePackEmailAsync(
                recipient, badgeArtifacts, created, now, actorUserId, cancellationToken);
            emailQueued = true;
        }

        return new AdminBulkGenerateBadgesResponse(created, emailQueued);
    }

    public async Task<GridPage<AdminBadgeBatchSummary>> ListBadgeBatchesAsync(
        Guid actorUserId, GridQuery query, CancellationToken cancellationToken = default)
    {
        // Admin-only list (no per-actor scope). Newest-first; a revoked batch keeps its
        // row with IsActive=false so the history stays visible.
        var (skip, top) = query.ClampPage();
        var total = await appDbContext.BadgeBatches.CountAsync(cancellationToken);
        var rows = await appDbContext.BadgeBatches
            .AsNoTracking()
            .OrderByDescending(batch => batch.CreatedAt)
            .Skip(skip).Take(top)
            .Select(batch => new AdminBadgeBatchSummary(
                batch.Id, batch.CountsSummary, batch.TotalCount, batch.IsDelegate,
                batch.RecipientEmail, batch.CreatedAt, batch.IsActive))
            .ToListAsync(cancellationToken);
        return GridPage<AdminBadgeBatchSummary>.Of(rows, total, skip, top);
    }

    public async Task<AdminReEmailBadgeBatchResponse> ReEmailBadgeBatchAsync(
        Guid actorUserId, AdminReEmailBadgeBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var batch = await appDbContext.BadgeBatches
            .SingleOrDefaultAsync(b => b.Id == request.BatchId, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AdminUserNotFound, 404,
                "The badge batch was not found.",
                "لم يتم العثور على دفعة الشارات.");

        // Validate the organiser address the same way BulkGenerateBadgesAsync does — a
        // 400 must leave nothing sent.
        var recipient = (request.RecipientEmail ?? string.Empty).Trim();
        if (recipient.Length == 0 || recipient.Length > 256
            || !System.Net.Mail.MailAddress.TryCreate(recipient, out var parsed)
            || !parsed.Host.Contains('.'))
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                "The organiser email address is not valid.",
                "عنوان البريد الإلكتروني للمنظّم غير صالح.");
        }

        // Re-materialise the pack from the batch's minted badges (tier name + QR id),
        // ordered stably so the re-issued sequence numbers match the original run.
        var members = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.BadgeBatchId == batch.Id && profile.QrId != null)
            .OrderBy(profile => profile.CreatedAt).ThenBy(profile => profile.Id)
            .Join(appDbContext.ProfileTypes,
                profile => profile.ProfileTypeId, type => type.Id,
                (profile, type) => new { type.Name, profile.QrId })
            .ToListAsync(cancellationToken);
        if (members.Count == 0)
        {
            throw new ApiException(
                ErrorCodes.ValidationFailed, 400,
                "This batch has no badges to email.",
                "لا توجد شارات في هذه الدفعة لإرسالها.");
        }

        var now = timeProvider.SimfNow();
        var badgeArtifacts = members
            .Select((member, index) => (member.Name, Seq: index + 1, QrId: member.QrId!))
            .ToList();
        await EnqueueBadgePackEmailAsync(
            recipient, badgeArtifacts, badgeArtifacts.Count, now, actorUserId, cancellationToken);

        // Remember the last recipient so a future re-email pre-fills it.
        batch.RecipientEmail = recipient;
        batch.UpdatedAt = now;
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.AdminBulkBadgesGenerated,
            actorUserId,
            $"re-email batchId={batch.Id}; count={badgeArtifacts.Count}; to={recipient}",
            cancellationToken);
        return new AdminReEmailBadgeBatchResponse(badgeArtifacts.Count, true);
    }

    public async Task<AdminRevokeBadgeBatchResponse> RevokeBadgeBatchAsync(
        Guid actorUserId, AdminRevokeBadgeBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var batch = await appDbContext.BadgeBatches
            .SingleOrDefaultAsync(b => b.Id == request.BatchId && b.IsActive, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.AdminUserNotFound, 404,
                "The badge batch was not found or is already revoked.",
                "لم يتم العثور على دفعة الشارات أو أنها ملغاة بالفعل.");

        // Disable every account the batch minted, reusing the type-scoped bulk-delete
        // path (audience Visitors) so each account's disable + token-revoke + audit is
        // identical to a manual bulk delete. This crosses databases: these SimfUsers
        // live in the Identity DB — disabled first, THEN the App-DB batch is
        // deactivated as a separate unit of work (no distributed transaction).
        var memberIds = await appDbContext.UserProfiles
            .AsNoTracking()
            // Only members that HAVE an account, because what follows deletes
            // accounts. A batch member without one has nothing to delete here;
            // revoking its badge is a profile-side concern.
            .Where(profile => profile.BadgeBatchId == batch.Id
                && profile.UserId != null)
            .Select(profile => profile.UserId!.Value)
            .ToListAsync(cancellationToken);

        var revoked = 0;
        if (memberIds.Count > 0)
        {
            var deleteResult = await BulkDeleteUsersByKindAsync(
                actorUserId, UserType.Visitor, requirePartnerScope: false,
                new AdminBulkDeleteRequest
                {
                    Ids = memberIds,
                    Reason = $"Badge batch {batch.Id} revoked",
                },
                cancellationToken);
            revoked = deleteResult.Deleted;
        }

        batch.Deactivate();
        batch.UpdatedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.AdminBulkBadgesGenerated,
            actorUserId,
            $"revoke batchId={batch.Id}; disabled={revoked}",
            cancellationToken);
        return new AdminRevokeBadgeBatchResponse(revoked);
    }

    // Render the batch's QR pack and enqueue it to the organiser as a
    // single email. Extracted so bulk-generate and the re-email build the
    // identical pack. Invariant formatting throughout — an ar-SA request culture would
    // otherwise render the filename / body tokens with a Hijri year + Arabic-Indic
    // digits. `attachments` is a list so a second file could be added beside the ZIP
    // without touching either caller — which is how the printable contact-sheet PDF
    // came to sit next to the ZIP of individual QR PNGs.
    private async Task EnqueueBadgePackEmailAsync(
        string recipient,
        IReadOnlyList<(string ProfileTypeName, int Seq, string QrId)> badgeArtifacts,
        int count, DateTime generatedAt, Guid actorUserId, CancellationToken cancellationToken)
    {
        var stamp = generatedAt.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        var attachments = new List<EmailAttachment>
        {
            new($"badges-{stamp}.zip", "application/zip", BuildBadgeZip(badgeArtifacts)),
            // A printable contact-sheet PDF beside the ZIP of
            // individual PNGs, so the organiser can print all badges on one page.
            new($"badges-{stamp}.pdf", "application/pdf", BuildBadgeSheetPdf(badgeArtifacts)),
        };
        var tokens = new Dictionary<string, string>
        {
            ["Count"] = count.ToString(CultureInfo.InvariantCulture),
            ["GeneratedAt"] = generatedAt.FormatSaudi(),
        };
        var cover = await emailTemplates.RenderAsync(
            EmailTemplateType.BulkBadgeDelivery, recipient, tokens, cancellationToken);
        await emailQueue.TryEnqueueAsync(
            cover with { Attachments = attachments.ToArray() },
            "BulkBadgeDelivery", recipient, actorUserId, auditLog, logger, cancellationToken);
        logger.LogInformation(
            "Admin {ActorId} emailed {Count} badges to {Recipient}.", actorUserId, count, recipient);
    }

    // One ZIP entry per badge (an individual QR PNG). No System.Drawing
    // dependency; PngByteQRCode emits raw PNG bytes.
    private static byte[] BuildBadgeZip(
        IReadOnlyList<(string ProfileTypeName, int Seq, string QrId)> badges)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var badge in badges)
            {
                var png = RenderQrPng(badge.QrId);
                var entryName = $"{SafeSegment(badge.ProfileTypeName)}-{badge.Seq}-{badge.QrId}.png";
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                entryStream.Write(png, 0, png.Length);
            }
        }
        return buffer.ToArray();
    }

    // A printable contact-sheet PDF: a 3-column grid of badge
    // cards (QR + tier + #N + QR id) via QuestPDF, reusing the same QRCoder PNGs as the
    // ZIP. LICENCE (owner-accepted follow-up): QuestPDF's free Community licence covers
    // organisations under ~$1M revenue; a paid QuestPDF licence is required for the
    // production customer (RSNF). Set here (idempotent) so it is in force before any
    // GeneratePdf call regardless of host.
    private static byte[] BuildBadgeSheetPdf(
        IReadOnlyList<(string ProfileTypeName, int Seq, string QrId)> badges)
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(QuestPDF.Helpers.PageSizes.A4);
                page.Margin(1, QuestPDF.Infrastructure.Unit.Centimetre);
                page.DefaultTextStyle(text => text.FontSize(9));
                page.Header().PaddingBottom(8).Text("SIMF — Badge sheet").SemiBold().FontSize(14);
                // 3-up grid via Column-of-Rows (QuestPDF's Grid element is deprecated).
                // The outer Column paginates across pages automatically.
                const int perRow = 3;
                page.Content().Column(rows =>
                {
                    rows.Spacing(8);
                    for (var start = 0; start < badges.Count; start += perRow)
                    {
                        rows.Item().Row(row =>
                        {
                            row.Spacing(8);
                            for (var offset = 0; offset < perRow; offset++)
                            {
                                var index = start + offset;
                                if (index >= badges.Count)
                                {
                                    row.RelativeItem(); // filler so cells keep equal width
                                    continue;
                                }
                                var badge = badges[index];
                                row.RelativeItem().Border(1).Padding(6).Column(cell =>
                                {
                                    cell.Item().AlignCenter().Width(110).Image(RenderQrPng(badge.QrId));
                                    cell.Item().PaddingTop(4).AlignCenter()
                                        .Text($"{badge.ProfileTypeName} #{badge.Seq}").SemiBold();
                                    cell.Item().AlignCenter()
                                        .Text(badge.QrId).FontSize(7)
                                        .FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                                });
                            }
                        });
                    }
                });
                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("SIMF ").FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });
        return document.GeneratePdf();
    }

    // One QR PNG (ECC level Q, same as PrintBag), shared by the ZIP
    // entries and the PDF sheet cells.
    private static byte[] RenderQrPng(string qrId)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrData = qrGenerator.CreateQrCode(qrId, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(qrData).GetGraphic(pixelsPerModule: 10);
    }

    // Keeps a profile-type name safe + readable inside a ZIP entry / file name.
    private static string SafeSegment(string value)
    {
        var mapped = new string(value.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray())
            .Trim('-');
        return string.IsNullOrEmpty(mapped) ? "badge" : mapped;
    }
}
