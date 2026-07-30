// Tests: SIMF.Api.Tests/AdminChangeAccountTypeTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Domain.Auditing;
using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// D-728 (owner item 9) — flip an existing account between the audience
/// (Visitor) and partner (Other) scope. This is <c>UpdateAccountAsync</c>
/// (<see cref="AdminAccountService.UpdateAccountAsync"/>) minus the two
/// same-scope guards, plus the requirement that the new type is in the
/// OPPOSITE scope. It reuses the proven edit-time pieces:
/// <see cref="AdminAccountService.ResolveEditProfileTypeAsync"/> (exists +
/// active + <c>IsForVisitor == expected</c>) and
/// <see cref="AdminAccountService.UpsertProfileTypeAsync"/> (the App-DB write).
/// A type flip is ALWAYS a privilege change — the new type's
/// <c>MobileAppRole</c> re-sources the app's operational permission claims
/// (D-563) — so it unconditionally rolls the security stamp and revokes the
/// subject's sessions. Approval state is left unchanged (owner decision): an
/// approved account stays approved under the new type and simply re-issues a
/// token carrying the new perms.
/// </summary>
internal sealed partial class AdminAccountService
{
    public async Task ChangeAccountTypeAsync(
        Guid actorUserId, Guid userId, Guid newProfileTypeId,
        CancellationToken cancellationToken = default)
    {
        // Load the subject. Only non-admin accounts (UserType.Visitor covers
        // both audience and partner post-D-186) have an audience/partner scope;
        // an Admin or missing id is reported as the same 404 so the desk cannot
        // probe. Mirrors the edit path's not-found shape.
        var target = await accounts.FindByIdAsync(userId, cancellationToken);
        if (target is null || target.UserType != UserType.Visitor)
        {
            await AuditFailure(
                AuditEvents.AdminUserUpdateFailed, actorUserId,
                target?.Email ?? string.Empty, userId,
                ErrorCodes.AdminUserNotFound, cancellationToken);
            throw new ApiException(
                ErrorCodes.AdminUserNotFound, 404,
                "The account was not found.",
                "لم يتم العثور على الحساب.");
        }

        // The account's current scope: no profile type ⇒ audience (Visitor).
        var currentProfileTypeId = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.UserId == target.Id)
            .Select(profile => profile.ProfileTypeId)
            .SingleOrDefaultAsync(cancellationToken);
        var currentIsVisitor = currentProfileTypeId is null
            || await appDbContext.ProfileTypes
                .AsNoTracking()
                .Where(type => type.Id == currentProfileTypeId)
                .Select(type => type.IsForVisitor)
                .SingleAsync(cancellationToken);

        // A type change always flips the scope, so the new type must be in the
        // OPPOSITE scope. ResolveEditProfileTypeAsync enforces exists + active +
        // IsForVisitor == expected, so passing the opposite scope both validates
        // the type and rejects a same-scope pick (that path is an edit, not a
        // type change) with AdminProfileTypeInvalid (400).
        var targetIsVisitor = !currentIsVisitor;
        var resolvedProfileTypeId = await ResolveEditProfileTypeAsync(
            actorUserId, target.Email ?? string.Empty, target.Id,
            newProfileTypeId, expectedIsVisitor: targetIsVisitor,
            profileTypeRequired: true, cancellationToken);

        // Bi-Meeting rework — a scope flip must NOT clobber the two admin-assigned
        // meeting-eligibility flags; read the current values so the upsert below
        // preserves them (defaults false when the subject has no profile row yet).
        var currentFlags = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == target.Id)
            .Select(p => new { p.AllowsSpeakerMeeting, p.AllowsDelegationMeeting })
            .SingleOrDefaultAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();

        // A type flip is ALWAYS a privilege change (the new type's MobileAppRole
        // re-sources the app 'perm' claims), so kill the subject's live sessions
        // FIRST — roll the security stamp + revoke the refresh tokens in one
        // Identity transaction — BEFORE the App-DB profile flip. Because the two
        // databases cannot share a transaction (D-157), ordering the session
        // kill first makes a partial failure fail-CLOSED: if the flip below
        // throws, the account is already signed out and keeps its old type, so a
        // live token can never outlive a privilege escalation. (The reused edit
        // path tolerates the reverse order because an edit is only *sometimes* a
        // privilege change; a type flip always is — D-728 review finding.)
        await transactionRunner.ExecuteAsync(async (innerCt) =>
        {
            await accounts.UpdateSecurityStampAsync(target, innerCt);
            await refreshTokenRepository.RevokeAllForUserAsync(
                target.Id, now, innerCt);
        }, cancellationToken);

        // Then apply the scope flip on the App DB (no cross-DB transaction). Preserve
        // the two meeting-eligibility flags read above.
        await UpsertProfileTypeAsync(
            target.Id, resolvedProfileTypeId,
            currentFlags?.AllowsSpeakerMeeting ?? false,
            currentFlags?.AllowsDelegationMeeting ?? false,
            // B22 — a scope flip never touches nationality (null = leave it alone).
            nationalityId: null,
            // FR-PHN-002 — nor the mobile numbers (same "null = no change" rule).
            saudiMobile: null, internationalMobile: null,
            now, cancellationToken);

        // Audit the completed flip — after it is durable, so the trail reflects
        // reality; a retry after a mid-way failure is idempotent (the stamp roll
        // and revoke re-apply harmlessly and the flip is a no-op once applied).
        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.AdminUserTypeChanged,
            Outcome = AuditOutcome.Success,
            SubjectEmail = target.Email ?? string.Empty,
            SubjectUserId = target.Id,
            ActorUserId = actorUserId,
            Detail = $"from={(currentIsVisitor ? "visitor" : "other")}; "
                + $"to={(targetIsVisitor ? "visitor" : "other")}; "
                + $"fromProfileType={currentProfileTypeId}; "
                + $"toProfileType={resolvedProfileTypeId}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} changed account {SubjectId} type {From} -> {To}",
            actorUserId, target.Id,
            currentIsVisitor ? "visitor" : "other",
            targetIsVisitor ? "visitor" : "other");
    }
}
