// Tests: SIMF.Api.Tests/AdminApprovalTests.cs
// Tests: SIMF.Api.Tests/GateRevokedBadgeTests.cs (admission is written on the
//        profile, so a refused or withdrawn holder is denied at a gate)
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Auditing;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Notifications;
using SIMF.Domain.Profiles;
using SIMF.Common.Enums;

namespace SIMF.Infrastructure.Identity;

/// <summary>
/// The approve / reject workers of
/// <see cref="AdminAccountService"/>,
/// plus their shared helpers (<c>LoadPendingSubjectAsync</c>,
/// <c>EnsureUserProfileAsync</c>, scope checks). The public per-scope
/// dispatchers + the bulk-approve loop stay in the main / bulk parts and
/// call into these. Split into its own partial-class file for navigability;
/// behaviour and DI are unchanged.
/// </summary>
internal sealed partial class AdminAccountService
{
    /// <param name="sendApprovalEmail">
    /// Whether the approval notification also sends an email. Defaults
    /// to true, so every existing caller is unchanged. The walk-in desk passes
    /// false when it synthesized a placeholder address: at an event with a large
    /// crowd that would queue one dead send per registration.
    /// </param>
    private async Task ApproveAsync(
        Guid actorUserId, Guid subjectUserId, ApprovalScope scope,
        CancellationToken cancellationToken, Guid? profileTypeId = null,
        bool sendApprovalEmail = true)
    {
        var subject = await LoadPendingSubjectAsync(
            actorUserId, subjectUserId, scope, cancellationToken);
        var now = timeProvider.SimfNow();
        subject.AccountState = AccountState.Approved;
        subject.UpdatedAt = now;
        subject.StateChangedAt = now;
        subject.StateChangedByUserId = actorUserId;

        // QR + rejection text live on UserProfile now. Ensure the
        // profile row exists (it usually does — the visitor filled in
        // their form — but an admin-created Visitor / Other might be
        // approved before any profile data is captured). Clear any
        // prior rejection text (the reconsider path) and mint the QR.
        var profile = await EnsureUserProfileAsync(subject.Id, now, cancellationToken);
        profile.RejectionReason = null;
        profile.RejectionReasonArabic = null;

        // Admission lives on the PROFILE, because an attendee need not have an
        // account and the gate must still be able to decide. The user row is
        // updated too, but for a different question: its state governs sign-in.
        // The two are not mirrors of one fact — they are two facts that happen to
        // change together on this path — and only this one is read at a gate.
        profile.AdmissionState = AccountState.Approved;
        profile.StateChangedAt = now;
        profile.StateChangedByUserId = actorUserId;

        await qrIdMinter.MintIfMissingAsync(profile, cancellationToken);

        // Optional tier assignment on approve. Only the
        // AudienceVisitor dispatcher passes a non-null id; the Other /
        // Admin dispatchers always pass null. A supplied id must be an
        // active, audience-side (IsForVisitor=true) ProfileType — the
        // same shape CreateAccountAsync enforces — else the approval is
        // rejected before any save. Runs inside the same unit of work so
        // the tier lands atomically with the QR + state flip.
        Guid? assignedProfileTypeId = null;
        if (profileTypeId is { } chosenTypeId)
        {
            var chosenType = await appDbContext.ProfileTypes
                .SingleOrDefaultAsync(p => p.Id == chosenTypeId, cancellationToken);
            if (chosenType is null || !chosenType.IsActive || !chosenType.IsForVisitor)
            {
                throw new ApiException(
                    ErrorCodes.AdminProfileTypeInvalid, 400,
                    "The selected profile type is not valid for a visitor.",
                    "نوع الملف الشخصي المحدّد غير صالح للزائر.");
            }
            profile.ProfileTypeId = chosenTypeId;
            assignedProfileTypeId = chosenTypeId;
        }

        // Cross-DB ordering: there is
        // NO distributed transaction spanning the two databases. Persist the
        // App-DB unit of work (the minted QR + the cleared rejection text +
        // the optional tier) FIRST, then flip the Identity account to Approved.
        // A transient App-save failure now leaves the account PendingApproval
        // — retryable, because the approve path re-runs and MintIfMissingAsync
        // is idempotent — instead of orphaning an Approved visitor with no QR
        // (which LoadPendingSubjectAsync would then 409 on every retry, leaving
        // the visitor permanently un-scannable). The reverse window (a minted
        // QR on a still-Pending account) is fail-closed: a gate scan denies it
        // as HolderNotApproved (GateOperatorService), so it grants no access.
        // UserProfile lives on the App DB, saved separately from the
        // Identity-side user-state flip.
        await appDbContext.SaveChangesAsync(cancellationToken);
        await accounts.UpdateAsync(subject).EnsureSuccessAsync();
        await dbContext.SaveChangesAsync(cancellationToken);

        // Revoke every refresh token so the subject's next API
        // call gets a fresh access token with account_state=Approved.
        // Without this, a previously-pending session could keep using
        // its stale token for ≤ 15 minutes.
        await refreshTokenRepository.RevokeAllForUserAsync(
            subject.Id, now, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = ApprovalEventType(scope, approved: true),
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            SubjectUserId = subject.Id,
            SubjectEmail = subject.Email,
            // Record the assigned tier when one was set so the
            // approve-time tier assignment is auditable alongside the QR id.
            Detail = assignedProfileTypeId is { } tierId
                ? $"{profile.QrId}; profileType={tierId}"
                : profile.QrId,
        }, cancellationToken);

        // Notify the approved user (with their QR id) +
        // email.
        var approvedTokens = new Dictionary<string, string>
        {
            ["DisplayName"] = subject.DisplayName,
            ["QrId"] = profile.QrId ?? string.Empty,
        };
        await notifications.DispatchAsync(new NotificationRequest
        {
            UserId = subject.Id,
            Kind = NotificationKind.AccountApproved,
            Title = "Your SIMF account is approved",
            TitleArabic = "تم اعتماد حسابك في SIMF",
            Body = $"Your event QR id is {profile.QrId}. Sign in to view it on your profile.",
            BodyArabic = $"رمز QR الخاص بك للفعالية هو {profile.QrId}. سجّل الدخول لعرضه في ملفك الشخصي.",
            Severity = NotificationSeverity.Success,
            SendEmail = sendApprovalEmail,
            PreRenderedEmailHtml = NotificationEmailTemplates.Render(
                NotificationKind.AccountApproved, "en", approvedTokens),
        }, cancellationToken);
    }

    private async Task RejectAsync(
        Guid actorUserId, Guid subjectUserId, AdminRejectRequest request,
        ApprovalScope scope, CancellationToken cancellationToken)
    {
        var subject = await LoadPendingSubjectAsync(
            actorUserId, subjectUserId, scope, cancellationToken);
        var now = timeProvider.SimfNow();
        subject.AccountState = AccountState.Rejected;
        subject.UpdatedAt = now;
        subject.StateChangedAt = now;
        subject.StateChangedByUserId = actorUserId;

        // Persist the rejection text on UserProfile (it used to live on the
        // user row). EN-only admin input mirrors to the Arabic
        // column as a graceful fallback.
        var profile = await EnsureUserProfileAsync(subject.Id, now, cancellationToken);
        profile.RejectionReason = request.Reason;
        profile.RejectionReasonArabic = request.Reason;

        // Refusing admission is a decision about the attendee, so it is recorded
        // on the profile — the row a gate reads. See the note on the approve path.
        profile.AdmissionState = AccountState.Rejected;
        profile.StateChangedAt = now;
        profile.StateChangedByUserId = actorUserId;

        // Same cross-DB ordering as the approve path, for the same reason: no
        // transaction spans the two databases, so the App-DB unit of work is
        // persisted FIRST. A failure there leaves the account PendingApproval and
        // the whole rejection retryable. The reverse order could leave a Rejected
        // account whose profile still reads Approved, and the profile is the row a
        // gate reads, so that window would admit someone who had just been
        // refused. Written the safe way round rather than leaning on the
        // pending-only guard in LoadPendingSubjectAsync to keep it shut.
        await appDbContext.SaveChangesAsync(cancellationToken);
        await accounts.UpdateAsync(subject).EnsureSuccessAsync();
        await dbContext.SaveChangesAsync(cancellationToken);

        // Revoke every refresh token so the subject's next API
        // call mints a token with account_state=Rejected (and the
        // authorization handler then routes them to the rejected page).
        await refreshTokenRepository.RevokeAllForUserAsync(
            subject.Id, now, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = ApprovalEventType(scope, approved: false),
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            SubjectUserId = subject.Id,
            SubjectEmail = subject.Email,
            Detail = request.Reason,
        }, cancellationToken);

        // Notify the rejected user (with the reason) +
        // email.
        var rejectedTokens = new Dictionary<string, string>
        {
            ["DisplayName"] = subject.DisplayName,
            ["Reason"] = request.Reason,
        };
        await notifications.DispatchAsync(new NotificationRequest
        {
            UserId = subject.Id,
            Kind = NotificationKind.AccountRejected,
            Title = "Your SIMF account was not approved",
            TitleArabic = "لم يتم اعتماد حسابك في SIMF",
            Body = $"Reason: {request.Reason}",
            BodyArabic = $"السبب: {request.Reason}",
            Severity = NotificationSeverity.Error,
            SendEmail = true,
            PreRenderedEmailHtml = NotificationEmailTemplates.Render(
                NotificationKind.AccountRejected, "en", rejectedTokens),
        }, cancellationToken);
    }

    /// <summary>Maps an approval scope + outcome to the right
    /// audit event name. The discriminator is ApprovalScope (AudienceVisitor /
    /// PartnerOther / Admin) rather than UserType, so the audit-event names stay the same
    /// even though Other accounts are now Visitor-typed under the
    /// hood.</summary>
    private static string ApprovalEventType(ApprovalScope scope, bool approved) => scope switch
    {
        ApprovalScope.Admin when approved => AuditEvents.AdminStaffApproved,
        ApprovalScope.Admin => AuditEvents.AdminStaffRejected,
        ApprovalScope.PartnerOther when approved => AuditEvents.AdminOtherApproved,
        ApprovalScope.PartnerOther => AuditEvents.AdminOtherRejected,
        _ when approved => AuditEvents.AdminVisitorApproved,
        _ => AuditEvents.AdminVisitorRejected,
    };

    /// <summary>Loads a user that must currently be in PendingApproval
    /// **and** match the expected approval scope — any other
    /// state or a scope-mismatch throws <see cref="ApiException"/>.
    /// Shared by every approve/reject path; the scope check closes the
    /// "approve an admin via the visitor URL" hole AND the
    /// "approve a sponsor via the visitors URL" hole.</summary>
    private async Task<SimfUser> LoadPendingSubjectAsync(
        Guid actorUserId, Guid subjectUserId, ApprovalScope scope,
        CancellationToken cancellationToken)
    {
        var subject = await accounts.FindByIdAsync(subjectUserId, cancellationToken)
            ?? throw new ApiException(ErrorCodes.AdminUserNotFound, 404,
                "The target account was not found.",
                "تعذّر العثور على الحساب المستهدف.");
        if (subject.UserType != UserTypeOf(scope))
        {
            throw new ApiException(ErrorCodes.AdminUserNotFound, 404,
                "The target account is not of the expected type.",
                "نوع الحساب المستهدف لا يطابق المتوقع.");
        }
        // Within the Visitor scope, also enforce the
        // audience-vs-partner queue match via the linked ProfileType.
        // Emit a dedicated
        // audit row on the scope-mismatch branch so SOC rule
        // m-004-approval-scope-probe can fire on a probe pattern
        // (the 404 itself is indistinguishable from a missing id).
        var requireProfileScope = ProfileScopeOf(scope);
        if (requireProfileScope is not null
            && !await SubjectMatchesProfileScopeAsync(
                subject.Id, requireProfileScope.Value, cancellationToken))
        {
            await auditLog.WriteAsync(new AuditEntry
            {
                EventType = AuditEvents.AdminApprovalScopeMismatch,
                Outcome = AuditOutcome.Failure,
                ActorUserId = actorUserId,
                SubjectUserId = subject.Id,
                SubjectEmail = subject.Email,
                ErrorCode = ErrorCodes.AdminUserNotFound,
                Detail = $"expectedScope={scope}; "
                    + $"expectedIsVisitor={requireProfileScope.Value}",
            }, cancellationToken);
            throw new ApiException(ErrorCodes.AdminUserNotFound, 404,
                "The target account is not in the expected approval queue.",
                "الحساب المستهدف ليس في قائمة الاعتماد المتوقعة.");
        }
        if (subject.AccountState != AccountState.PendingApproval)
        {
            throw new ApiException(ErrorCodes.AdminUserNotPending, 409,
                "The target account is not pending approval.",
                "الحساب المستهدف ليس في انتظار الموافقة.");
        }
        return subject;
    }

    // Checks the subject's linked ProfileType.IsVisitor matches
    // the expected scope flag. Audience scope (true) accepts a missing
    // ProfileType (a self-signed-up visitor without an admin-assigned
    // type still lands on the audience queue); partner scope (false)
    // rejects a missing ProfileType — a user without a partner-side
    // type is not a partner.
    private async Task<bool> SubjectMatchesProfileScopeAsync(
        Guid subjectUserId, bool requireIsVisitor,
        CancellationToken cancellationToken)
    {
        var profileTypeId = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.UserId == subjectUserId)
            .Select(p => p.ProfileTypeId)
            .SingleOrDefaultAsync(cancellationToken);
        if (profileTypeId is null)
        {
            return requireIsVisitor;
        }
        var isVisitor = await appDbContext.ProfileTypes
            .AsNoTracking()
            .Where(p => p.Id == profileTypeId)
            .Select(p => (bool?)p.IsForVisitor)
            .SingleOrDefaultAsync(cancellationToken);
        return (isVisitor ?? true) == requireIsVisitor;
    }

    /// <summary>
    /// Returns the tracked <see cref="UserProfile"/> for the user,
    /// creating a stub row if none exists. The approve/reject flows need
    /// a profile row to write the QR / rejection text onto. Admin-typed
    /// users normally never reach approve/reject (Admins don't carry a
    /// profile today), but Visitor / Other accounts created by an admin
    /// can be approved before the user fills out the profile form —
    /// those rows get a minimal stub here so the QR has somewhere to
    /// land. The caller commits via SaveChangesAsync after the rest of
    /// the unit of work completes.
    /// </summary>
    private async Task<UserProfile> EnsureUserProfileAsync(
        Guid userId, DateTime now, CancellationToken cancellationToken)
    {
        var profile = await appDbContext.UserProfiles
            .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is not null) { return profile; }

        profile = new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = now,
        };
        appDbContext.UserProfiles.Add(profile);
        return profile;
    }
}
