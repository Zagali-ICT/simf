// Tests: SIMF.Api.Tests/AdminInvitationsTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Notifications;
using SIMF.Application.PublicRelations.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.PublicRelations;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.PublicRelations;

/// <summary>
/// D-168 (gap doc G5, PDF §2.7.3) — public-relations service. Real DB FK
/// links <see cref="Invitation"/> to <c>UserProfile</c> (both on App DB
/// since D-167); SimfUser lookups for sender display-name and recipient
/// email cross into the Identity DB via an in-memory merge — same
/// pattern as <c>AdminAttendeeService</c>.
/// </summary>
internal sealed class AdminInvitationService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    INotificationDispatcher notificationDispatcher,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminInvitationService> logger) : IAdminInvitationService
{
    public async Task<GridPage<AdminInvitationSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var invitations = appDbContext.Invitations.AsNoTracking().AsQueryable();

        if (query.Filters.TryGetValue("state", out var stateRaw)
            && Enum.TryParse<InvitationState>(stateRaw, ignoreCase: true, out var stateValue))
        {
            invitations = invitations.Where(row => row.State == stateValue);
        }
        if (query.Filters.TryGetValue("isActive", out var activeFilter)
            && bool.TryParse(activeFilter, out var isActive))
        {
            invitations = invitations.Where(row => row.IsActive == isActive);
        }

        invitations = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("createdat", true) => invitations.OrderByDescending(row => row.CreatedAt),
            ("createdat", false) => invitations.OrderBy(row => row.CreatedAt),
            ("state", true) => invitations.OrderByDescending(row => row.State),
            ("state", false) => invitations.OrderBy(row => row.State),
            _ => invitations.OrderByDescending(row => row.CreatedAt),
        };

        var total = await invitations.CountAsync(cancellationToken);
        var pageRows = await invitations
            .Skip(skip)
            .Take(top)
            .Select(row => new
            {
                row.Id,
                row.SentByUserId,
                row.SentToUserProfileId,
                row.State,
                row.Notes,
                row.CreatedAt,
                row.RespondedAt,
                row.IsActive,
            })
            .ToListAsync(cancellationToken);

        if (pageRows.Count == 0)
        {
            return GridPage<AdminInvitationSummary>.Of(
                Array.Empty<AdminInvitationSummary>(), total,
                new GridQuery { Skip = skip, Top = top });
        }

        var profileIds = pageRows.Select(row => row.SentToUserProfileId).Distinct().ToList();
        var profiles = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => profileIds.Contains(profile.Id))
            .Select(profile => new
            {
                profile.Id,
                profile.UserId,
                profile.Name,
                profile.NameArabic,
                ProfileTypeName = profile.ProfileType != null ? profile.ProfileType.Name : null,
            })
            .ToListAsync(cancellationToken);

        var recipientUserIds = profiles.Select(p => p.UserId).ToList();
        var senderUserIds = pageRows.Select(row => row.SentByUserId).Distinct().ToList();
        var allUserIds = recipientUserIds.Union(senderUserIds).ToList();

        var users = await identityDbContext.Users.AsNoTracking()
            .Where(user => allUserIds.Contains(user.Id))
            .Select(user => new { user.Id, user.Email, user.DisplayName })
            .ToListAsync(cancellationToken);

        var profileById = profiles.ToDictionary(p => p.Id);
        var userById = users.ToDictionary(u => u.Id);

        var items = pageRows.Select(row =>
        {
            profileById.TryGetValue(row.SentToUserProfileId, out var profile);
            string? email = null;
            string englishName = string.Empty;
            string arabicName = string.Empty;
            string? profileTypeName = null;
            if (profile is not null)
            {
                englishName = profile.EnglishName;
                arabicName = profile.ArabicName;
                profileTypeName = profile.ProfileTypeName;
                if (userById.TryGetValue(profile.UserId, out var recipientUser))
                {
                    email = recipientUser.Email;
                }
            }
            userById.TryGetValue(row.SentByUserId, out var senderUser);
            return new AdminInvitationSummary(
                row.Id,
                row.SentByUserId,
                senderUser?.DisplayName ?? string.Empty,
                row.SentToUserProfileId,
                englishName,
                arabicName,
                profileTypeName,
                email,
                row.State,
                row.Notes,
                row.CreatedAt,
                row.RespondedAt,
                row.IsActive);
        }).ToList();

        return GridPage<AdminInvitationSummary>.Of(items, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminInvitationDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var row = await appDbContext.Invitations
            .AsNoTracking()
            .SingleOrDefaultAsync(invitation => invitation.Id == id, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var profile = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(p => p.Id == row.SentToUserProfileId)
            .Select(p => new
            {
                p.UserId,
                p.Name,
                p.NameArabic,
                p.JobTitle,
                ProfileTypeName = p.ProfileType != null ? p.ProfileType.Name : null,
            })
            .SingleOrDefaultAsync(cancellationToken);

        var userIds = new List<Guid> { row.SentByUserId };
        if (profile is not null)
        {
            userIds.Add(profile.UserId);
        }
        var users = await identityDbContext.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.Email, user.DisplayName })
            .ToListAsync(cancellationToken);

        var sender = users.FirstOrDefault(u => u.Id == row.SentByUserId);
        var recipientUser = profile is null
            ? null
            : users.FirstOrDefault(u => u.Id == profile.UserId);

        return new AdminInvitationDetail(
            row.Id,
            row.SentByUserId,
            sender?.DisplayName ?? string.Empty,
            row.SentToUserProfileId,
            profile?.EnglishName ?? string.Empty,
            profile?.ArabicName ?? string.Empty,
            profile?.ProfileTypeName,
            recipientUser?.Email,
            profile?.JobTitle,
            row.State,
            row.Notes,
            row.CreatedAt,
            row.RespondedAt,
            row.UpdatedAt,
            row.IsActive);
    }

    public async Task<AdminInvitationDetail> CreateAsync(
        Guid actorUserId,
        AdminCreateInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateNotes(request.Notes);

        var recipient = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.Id == request.SentToUserProfileId)
            .Select(profile => new { profile.UserId })
            .SingleOrDefaultAsync(cancellationToken);
        if (recipient is null)
        {
            throw new ApiException(
                ErrorCodes.InvitationTargetNotFound, 400,
                $"Recipient profile '{request.SentToUserProfileId}' does not exist.",
                $"الملف المستهدف '{request.SentToUserProfileId}' غير موجود.");
        }

        var now = timeProvider.GetUtcNow();
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            SentByUserId = actorUserId,
            SentToUserProfileId = request.SentToUserProfileId,
            State = InvitationState.Pending,
            Notes = NullIfBlank(request.Notes),
            CreatedAt = now,
            IsActive = true,
        };
        appDbContext.Invitations.Add(invitation);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.InvitationCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            SubjectUserId = recipient.UserId,
            Detail = $"invitationId={invitation.Id}; profileId={invitation.SentToUserProfileId}",
        }, cancellationToken);

        // Dispatch the in-app notification — best-effort, swallowed on
        // failure so the PR rep gets a 200 from the API even if the
        // notification subsystem is down.
        await notificationDispatcher.TryDispatchAsync(
            new NotificationRequest
            {
                UserId = recipient.UserId,
                Kind = NotificationKind.InvitationReceived,
                Title = "You received an invitation",
                TitleArabic = "تلقّيت دعوة",
                Body = invitation.Notes ?? "The Public Relations team has sent you an invitation.",
                BodyArabic = invitation.Notes ?? "أرسل لك فريق العلاقات العامة دعوة.",
                Severity = NotificationSeverity.Info,
                RelatedEntityType = nameof(Invitation),
                RelatedEntityId = invitation.Id,
                SendEmail = false,
            }, logger, cancellationToken);

        logger.LogInformation(
            "PR rep {Actor} created Invitation {InvitationId} for profile {ProfileId}",
            actorUserId, invitation.Id, invitation.SentToUserProfileId);

        return (await GetAsync(invitation.Id, cancellationToken))!;
    }

    public async Task<AdminInvitationDetail> UpdateAsync(
        Guid actorUserId,
        Guid id,
        AdminUpdateInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateNotes(request.Notes);

        var invitation = await appDbContext.Invitations
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.InvitationNotFound, 404,
                "The invitation was not found.",
                "لم يتم العثور على الدعوة.");

        // Reject going backwards from a settled state to Pending — the
        // recipient already responded.
        if (invitation.State != InvitationState.Pending
            && request.State == InvitationState.Pending)
        {
            throw new ApiException(
                ErrorCodes.InvitationStateInvalid, 400,
                "Cannot move an invitation back to Pending once it has been settled.",
                "لا يمكن إعادة الدعوة إلى حالة الانتظار بعد البتّ فيها.");
        }

        var stateChanged = invitation.State != request.State;
        invitation.State = request.State;
        invitation.Notes = NullIfBlank(request.Notes);
        invitation.UpdatedAt = timeProvider.GetUtcNow();
        // Every settled-state transition is a response — including a
        // correction (Confirmed → Declined) — so RespondedAt always
        // reflects the latest response. Pending → Pending stays null.
        if (stateChanged && request.State != InvitationState.Pending)
        {
            invitation.RespondedAt = invitation.UpdatedAt;
        }
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = stateChanged
                ? AuditEvents.InvitationStateChanged
                : AuditEvents.InvitationUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"invitationId={invitation.Id}; state={invitation.State}",
        }, cancellationToken);

        return (await GetAsync(invitation.Id, cancellationToken))!;
    }

    public async Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var invitation = await appDbContext.Invitations
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.InvitationNotFound, 404,
                "The invitation was not found.",
                "لم يتم العثور على الدعوة.");

        if (!invitation.IsActive)
        {
            return; // idempotent
        }
        invitation.IsActive = false;
        invitation.UpdatedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.InvitationDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"invitationId={invitation.Id}",
        }, cancellationToken);
    }

    public async Task<GridPage<AdminVipSummary>> ListVipsAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var vips = appDbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => profile.ProfileType != null
                && VipProfileTypes.All.Contains(profile.ProfileType.Name));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            vips = vips.Where(profile =>
                EF.Functions.Like(profile.Name, $"%{term}%")
                || EF.Functions.Like(profile.NameArabic, $"%{term}%"));
        }

        vips = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("name", true) => vips.OrderByDescending(profile => profile.Name),
            ("name", false) => vips.OrderBy(profile => profile.Name),
            ("profiletype", true) => vips.OrderByDescending(profile => profile.ProfileType!.Name),
            ("profiletype", false) => vips.OrderBy(profile => profile.ProfileType!.Name),
            _ => vips.OrderBy(profile => profile.Name),
        };

        var total = await vips.CountAsync(cancellationToken);
        var pageRows = await vips
            .Skip(skip)
            .Take(top)
            .Select(profile => new
            {
                profile.Id,
                profile.UserId,
                profile.Name,
                profile.NameArabic,
                profile.JobTitle,
                ProfileTypeName = profile.ProfileType!.Name,
                ProfileTypeNameArabic = profile.ProfileType!.NameArabic,
            })
            .ToListAsync(cancellationToken);

        if (pageRows.Count == 0)
        {
            return GridPage<AdminVipSummary>.Of(
                Array.Empty<AdminVipSummary>(), total,
                new GridQuery { Skip = skip, Top = top });
        }

        var userIds = pageRows.Select(row => row.UserId).ToList();
        var emailsByUser = await identityDbContext.Users.AsNoTracking()
            .Where(user => userIds.Contains(user.Id))
            .Select(user => new { user.Id, user.Email })
            .ToDictionaryAsync(user => user.Id, user => user.Email, cancellationToken);

        var items = pageRows.Select(row => new AdminVipSummary(
            row.Id,
            row.UserId,
            row.EnglishName,
            row.ArabicName,
            row.JobTitle,
            row.ProfileTypeName,
            row.ProfileTypeNameArabic,
            emailsByUser.TryGetValue(row.UserId, out var email) ? email : null))
            .ToList();

        return GridPage<AdminVipSummary>.Of(items, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminNotifyVipsResult> NotifyVipsAsync(
        Guid actorUserId,
        AdminNotifyVipsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.UserProfileIds.Count == 0)
        {
            throw new ApiException(
                ErrorCodes.VipNotifyEmpty, 400,
                "Select at least one VIP.",
                "اختر مستلماً واحداً على الأقل.");
        }
        if (request.UserProfileIds.Count > 500)
        {
            throw new ApiException(
                ErrorCodes.VipNotifyTooLarge, 400,
                "Cannot dispatch to more than 500 VIPs in one batch.",
                "لا يمكن الإرسال إلى أكثر من 500 ضيف في دفعة واحدة.");
        }

        var title = (request.Title ?? string.Empty).Trim();
        var titleArabic = (request.TitleArabic ?? string.Empty).Trim();
        var body = (request.Body ?? string.Empty).Trim();
        var bodyArabic = (request.BodyArabic ?? string.Empty).Trim();
        if (title.Length is < 1 or > 200 || titleArabic.Length is < 1 or > 200)
        {
            throw new ApiException(
                ErrorCodes.InvitationInvalid, 400,
                "Message title (EN + AR) must be between 1 and 200 characters each.",
                "يجب أن يكون عنوان الرسالة (إنجليزي + عربي) بين 1 و 200 حرفاً.");
        }
        if (body.Length is < 1 or > 2000 || bodyArabic.Length is < 1 or > 2000)
        {
            throw new ApiException(
                ErrorCodes.InvitationInvalid, 400,
                "Message body (EN + AR) must be between 1 and 2000 characters each.",
                "يجب أن يكون نص الرسالة (إنجليزي + عربي) بين 1 و 2000 حرفاً.");
        }

        var requestedIds = request.UserProfileIds.Distinct().ToList();

        // Validate every recipient is on the VIP list — the endpoint only
        // operates over the canonical {VVIP, VIP, Gold} discriminator;
        // anything else is silently skipped + recorded on the result.
        var vipProfiles = await appDbContext.UserProfiles
            .AsNoTracking()
            .Where(profile => requestedIds.Contains(profile.Id)
                && profile.ProfileType != null
                && VipProfileTypes.All.Contains(profile.ProfileType.Name))
            .Select(profile => new { profile.Id, profile.UserId })
            .ToListAsync(cancellationToken);

        var validUserIds = vipProfiles.Select(p => p.UserId).ToList();
        var skipped = requestedIds.Except(vipProfiles.Select(p => p.Id)).ToList();

        var emailsByUser = await identityDbContext.Users.AsNoTracking()
            .Where(user => validUserIds.Contains(user.Id))
            .Select(user => new { user.Id, user.Email })
            .ToDictionaryAsync(user => user.Id, user => user.Email, cancellationToken);

        var dispatched = 0;
        var emailsEnqueued = 0;
        foreach (var profile in vipProfiles)
        {
            var hasEmail = emailsByUser.TryGetValue(profile.UserId, out var email)
                && !string.IsNullOrWhiteSpace(email);
            // No RelatedEntity — a VIP broadcast is a free-standing
            // message from the PR desk, not tied to an Invitation row.
            await notificationDispatcher.TryDispatchAsync(
                new NotificationRequest
                {
                    UserId = profile.UserId,
                    Kind = NotificationKind.VipBroadcast,
                    Title = title,
                    TitleArabic = titleArabic,
                    Body = body,
                    BodyArabic = bodyArabic,
                    Severity = NotificationSeverity.Info,
                    SendEmail = hasEmail,
                }, logger, cancellationToken);
            dispatched++;
            if (hasEmail)
            {
                emailsEnqueued++;
            }
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.VipNotificationSent,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"dispatched={dispatched}; emails={emailsEnqueued}; skipped={skipped.Count}",
        }, cancellationToken);

        logger.LogInformation(
            "PR rep {Actor} dispatched VIP broadcast to {Count} VIPs ({Emails} emails enqueued)",
            actorUserId, dispatched, emailsEnqueued);

        return new AdminNotifyVipsResult(dispatched, emailsEnqueued, skipped);
    }

    private static void ValidateNotes(string? notes)
    {
        if (notes is not null && notes.Length > 1000)
        {
            throw new ApiException(
                ErrorCodes.InvitationInvalid, 400,
                "Invitation notes cannot exceed 1000 characters.",
                "لا يمكن أن تتجاوز ملاحظات الدعوة 1000 حرف.");
        }
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
