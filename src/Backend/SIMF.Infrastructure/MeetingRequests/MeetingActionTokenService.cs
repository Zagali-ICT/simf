// Tests: SIMF.Api.Tests/MeetingActionTokenTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SIMF.Application.Auditing;
using SIMF.Application.MeetingRequests;
using SIMF.Application.MeetingRequests.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Common.Enums;
using SIMF.Common.Options;
using SIMF.Contracts.Programme;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.Notifications;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.MeetingRequests;

/// <summary>D-717 (item 7, FDS-013 §15.7 GAP-3) — the speaker double-opt-in
/// action-link tokens. Mints two single-use, action-bound, 72h tokens per
/// accept-with-hall request (Approve + Reject), stores only their keyed-HMAC hash,
/// and validates / consumes them for the public landing page. GET-safe preview vs
/// POST-consuming apply so a link prefetcher cannot burn the token. Every mint /
/// view / outcome writes OperationLog. All state is in the App DB (tokens FK the
/// request); the requester notification goes through the dispatcher (Identity DB),
/// best-effort, so a notify failure never undoes the committed decision.</summary>
internal sealed class MeetingActionTokenService(
    SimfAppDbContext appDbContext,
    INotificationDispatcher notifications,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    IOptions<MeetingLinksOptions> options,
    ILogger<MeetingActionTokenService> logger) : IMeetingActionTokenService
{
    public MeetingActionLinks StageTokensForRequest(Guid speakerMeetingRequestId)
    {
        var now = timeProvider.GetUtcNow();
        var expires = now.AddHours(Math.Max(1, options.Value.TokenTtlHours));
        var approveSecret = MeetingActionTokenHasher.NewSecret();
        var rejectSecret = MeetingActionTokenHasher.NewSecret();

        // AddRange only — the caller's SaveChanges commits these together with the
        // AwaitingSpeaker transition (one atomic unit of work).
        appDbContext.MeetingActionTokens.AddRange(
            NewToken(speakerMeetingRequestId, MeetingActionType.Approve, approveSecret, now, expires),
            NewToken(speakerMeetingRequestId, MeetingActionType.Reject, rejectSecret, now, expires));

        return new MeetingActionLinks(BuildUrl(approveSecret), BuildUrl(rejectSecret));
    }

    public Task AuditMintedAsync(
        Guid speakerMeetingRequestId, CancellationToken cancellationToken = default) =>
        auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.MeetingActionTokenMinted,
            Outcome = AuditOutcome.Success,
            ActorUserId = Guid.Empty,
            Detail = $"requestId={speakerMeetingRequestId}",
        }, cancellationToken);

    public string StageDelegationConfirmToken(Guid delegationMeetingRequestId)
    {
        var now = timeProvider.GetUtcNow();
        var expires = now.AddHours(Math.Max(1, options.Value.TokenTtlHours));
        var secret = MeetingActionTokenHasher.NewSecret();

        // Add-only — the caller's SaveChanges commits this together with the
        // AwaitingSpeaker transition (one atomic unit of work), so a delegation request
        // can never be AwaitingSpeaker without its confirm token (mirrors the speaker mint).
        appDbContext.DelegationMeetingActionTokens.Add(new DelegationMeetingActionToken
        {
            Id = Guid.NewGuid(),
            DelegationMeetingRequestId = delegationMeetingRequestId,
            TokenHash = MeetingActionTokenHasher.Hash(secret),
            ExpiresUtc = expires,
            CreatedAt = now,
        });

        return BuildUrl(secret);
    }

    public async Task<MeetingActionPreview?> PreviewAsync(
        string tokenSecret, CancellationToken cancellationToken = default)
    {
        // One public endpoint serves both token kinds — a 256-bit secret matches at most
        // one row across the two tables, so try the speaker token first, then delegation.
        if (await ValidateAsync(tokenSecret, cancellationToken) is { } loaded)
        {
            var (token, request) = loaded;

            var speaker = await appDbContext.Speakers.AsNoTracking()
                .Where(s => s.Id == request.SpeakerId)
                .Select(s => new { s.Name, s.NameArabic })
                .SingleOrDefaultAsync(cancellationToken);
            string? hallName = null;
            if (request.HallId is { } hallId)
            {
                hallName = await appDbContext.Halls.AsNoTracking()
                    .Where(h => h.Id == hallId).Select(h => h.Name)
                    .SingleOrDefaultAsync(cancellationToken);
            }

            await auditLog.WriteAsync(new AuditEntry
            {
                EventType = AuditEvents.MeetingActionTokenViewed,
                Outcome = AuditOutcome.Success,
                ActorUserId = Guid.Empty,
                Detail = $"requestId={request.Id}; action={token.Action}",
            }, cancellationToken);

            return new MeetingActionPreview(
                token.Action, speaker?.Name ?? string.Empty, speaker?.NameArabic ?? string.Empty,
                request.RequesterName, request.Subject,
                request.SlotStartUtc, request.SlotEndUtc, hallName);
        }

        if (await ValidateDelegationAsync(tokenSecret, cancellationToken) is { } delegationLoaded)
        {
            return await BuildDelegationPreviewAsync(
                delegationLoaded.Request, cancellationToken);
        }

        return null;
    }

    public async Task<MeetingActionOutcome?> ApplyAsync(
        string tokenSecret, CancellationToken cancellationToken = default)
    {
        // One public endpoint applies both token kinds — try the speaker token first
        // (Approve → Accepted / Reject → Rejected), then a delegation confirm token.
        if (await ValidateAsync(tokenSecret, cancellationToken) is { } loaded)
        {
            var (token, request) = loaded;
            var now = timeProvider.GetUtcNow();

            // Atomic single-use (§15.7) — the DB is the single arbiter, not the read in
            // ValidateAsync. Claim the token (conditional UPDATE ... WHERE UsedAt IS NULL)
            // then the decision (... WHERE Status = AwaitingSpeaker); each affects a row
            // only for the FIRST caller. A double-submit, a retry, or the sibling
            // Approve+Reject racing each other loses (0 rows) and returns the neutral
            // null — never a double-notify or a non-deterministic status. Mirrors the
            // seat/slot uniqueness guard the admin path uses.
            var tokenClaimed = await appDbContext.MeetingActionTokens
                .Where(t => t.Id == token.Id && t.UsedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), cancellationToken);
            if (tokenClaimed == 0)
            {
                return null;
            }

            var newStatus = token.Action == MeetingActionType.Approve
                ? MeetingRequestStatus.Accepted
                : MeetingRequestStatus.Rejected;
            var decisionClaimed = await appDbContext.SpeakerMeetingRequests
                .Where(r => r.Id == request.Id
                    && r.Status == MeetingRequestStatus.AwaitingSpeaker)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, newStatus)
                    .SetProperty(r => r.SpeakerDecisionAt, now), cancellationToken);
            if (decisionClaimed == 0)
            {
                // The sibling token (or a concurrent decision) already moved the request
                // off AwaitingSpeaker. This token is spent; surface the neutral null.
                return null;
            }

            await auditLog.WriteAsync(new AuditEntry
            {
                EventType = AuditEvents.MeetingActionTokenApplied,
                Outcome = AuditOutcome.Success,
                ActorUserId = Guid.Empty,
                Detail = $"requestId={request.Id}; action={token.Action}",
            }, cancellationToken);

            await NotifyRequesterAsync(request, token.Action, cancellationToken);
            return new MeetingActionOutcome(token.Action);
        }

        if (await ValidateDelegationAsync(tokenSecret, cancellationToken) is { } delegationLoaded)
        {
            return await ApplyDelegationConfirmAsync(
                delegationLoaded.Token, delegationLoaded.Request, cancellationToken);
        }

        return null;
    }

    // Look up a token by its hash and confirm it is still usable: exists, unused,
    // unexpired, and its request is still AwaitingSpeaker. Read-only (AsNoTracking) —
    // ApplyAsync does the actual consume atomically via conditional UPDATEs, so this
    // never needs tracked entities. Returns the token + request, or null.
    private async Task<(MeetingActionToken Token, SpeakerMeetingRequest Request)?> ValidateAsync(
        string tokenSecret, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokenSecret))
        {
            return null;
        }
        var hash = MeetingActionTokenHasher.Hash(tokenSecret);
        var now = timeProvider.GetUtcNow();

        var token = await appDbContext.MeetingActionTokens.AsNoTracking()
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null || token.UsedAt != null || token.ExpiresUtc <= now)
        {
            return null;
        }

        var request = await appDbContext.SpeakerMeetingRequests.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == token.SpeakerMeetingRequestId, cancellationToken);
        if (request is null || request.Status != MeetingRequestStatus.AwaitingSpeaker)
        {
            return null;
        }

        return (token, request);
    }

    private async Task NotifyRequesterAsync(
        SpeakerMeetingRequest request, MeetingActionType action, CancellationToken cancellationToken)
    {
        var approved = action == MeetingActionType.Approve;
        var speakerName = await appDbContext.Speakers.AsNoTracking()
            .Where(s => s.Id == request.SpeakerId)
            .Select(s => s.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? "the speaker";

        await notifications.TryDispatchAsync(new NotificationRequest
        {
            UserId = request.RequestedByUserId,
            Kind = approved ? NotificationKind.MeetingRequestConfirmed : NotificationKind.MeetingCancelled,
            Title = approved ? "Meeting confirmed" : "Meeting declined",
            TitleArabic = approved ? "تم تأكيد الاجتماع" : "تم رفض الاجتماع",
            Body = approved
                ? $"{speakerName} confirmed your meeting."
                : $"{speakerName} could not confirm your meeting.",
            BodyArabic = approved
                ? $"أكّد {speakerName} اجتماعك."
                : $"تعذّر على {speakerName} تأكيد اجتماعك.",
            Severity = NotificationSeverity.Info,
            RelatedEntityType = nameof(SpeakerMeetingRequest),
            RelatedEntityId = request.Id,
            SendEmail = false,
        }, logger, cancellationToken);
    }

    // R4 (D-767) — the delegation confirm-token twin of ValidateAsync. Look up by hash,
    // confirm it is unused + unexpired, and that its request is still AwaitingSpeaker (an
    // already-confirmed / cancelled request is no longer confirmable). Read-only.
    private async Task<(DelegationMeetingActionToken Token, DelegationMeetingRequest Request)?>
        ValidateDelegationAsync(string tokenSecret, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tokenSecret))
        {
            return null;
        }
        var hash = MeetingActionTokenHasher.Hash(tokenSecret);
        var now = timeProvider.GetUtcNow();

        var token = await appDbContext.DelegationMeetingActionTokens.AsNoTracking()
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null || token.UsedAt != null || token.ExpiresUtc <= now)
        {
            return null;
        }

        var request = await appDbContext.DelegationMeetingRequests.AsNoTracking()
            .SingleOrDefaultAsync(r => r.Id == token.DelegationMeetingRequestId, cancellationToken);
        if (request is null || request.Status != MeetingRequestStatus.AwaitingSpeaker)
        {
            return null;
        }

        return (token, request);
    }

    // Build the confirm-page preview for a delegation token. Confirm-only, so the shared
    // shape carries Action=Approve; SpeakerName is unused by the public page, and
    // RequesterName carries the requesting delegation so the member sees who wants to meet.
    private async Task<MeetingActionPreview> BuildDelegationPreviewAsync(
        DelegationMeetingRequest request, CancellationToken cancellationToken)
    {
        var requestingCountry = await appDbContext.Countries.AsNoTracking()
            .Where(c => c.Id == request.RequestingCountryId)
            .Select(c => c.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? string.Empty;
        string? hallName = null;
        if (request.HallId is { } hallId)
        {
            hallName = await appDbContext.Halls.AsNoTracking()
                .Where(h => h.Id == hallId).Select(h => h.Name)
                .SingleOrDefaultAsync(cancellationToken);
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.MeetingActionTokenViewed,
            Outcome = AuditOutcome.Success,
            ActorUserId = Guid.Empty,
            Detail = $"delegationRequestId={request.Id}; action=Confirm",
        }, cancellationToken);

        return new MeetingActionPreview(
            MeetingActionType.Approve, string.Empty, string.Empty,
            requestingCountry, request.Subject,
            request.SlotStartUtc, request.SlotEndUtc, hallName);
    }

    // Consume a delegation confirm token: AwaitingSpeaker → Accepted, mirroring the in-app
    // ConfirmByOtherPartyAsync but with the token AS the credential (no member-auth check —
    // holding the emailed link is the proof). Atomic single-use so racing member clicks
    // (or a link + an in-app tap) resolve to one confirm and a neutral null for the losers.
    private async Task<MeetingActionOutcome?> ApplyDelegationConfirmAsync(
        DelegationMeetingActionToken token, DelegationMeetingRequest request,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var tokenClaimed = await appDbContext.DelegationMeetingActionTokens
            .Where(t => t.Id == token.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsedAt, now), cancellationToken);
        if (tokenClaimed == 0)
        {
            return null;
        }

        var confirmed = await appDbContext.DelegationMeetingRequests
            .Where(r => r.Id == request.Id
                && r.Status == MeetingRequestStatus.AwaitingSpeaker)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, MeetingRequestStatus.Accepted)
                .SetProperty(r => r.ConfirmedAt, now), cancellationToken);
        if (confirmed == 0)
        {
            // A concurrent confirm (another member's link or in-app tap) already moved it
            // off AwaitingSpeaker. This token is spent; surface the neutral null.
            return null;
        }

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.MeetingActionTokenApplied,
            Outcome = AuditOutcome.Success,
            ActorUserId = Guid.Empty,
            Detail = $"delegationRequestId={request.Id}; action=Confirm",
        }, cancellationToken);

        await NotifyDelegationRequesterConfirmedAsync(request, cancellationToken);
        return new MeetingActionOutcome(MeetingActionType.Approve);
    }

    private async Task NotifyDelegationRequesterConfirmedAsync(
        DelegationMeetingRequest request, CancellationToken cancellationToken)
    {
        var targetCountry = await appDbContext.Countries.AsNoTracking()
            .Where(c => c.Id == request.TargetCountryId)
            .Select(c => c.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? "the delegation";

        await notifications.TryDispatchAsync(new NotificationRequest
        {
            UserId = request.RequestedByUserId,
            Kind = NotificationKind.MeetingRequestConfirmed,
            Title = "Delegation meeting confirmed",
            TitleArabic = "تم تأكيد اجتماع الوفد",
            Body = $"Your delegation meeting with {targetCountry} is confirmed.",
            BodyArabic = $"تم تأكيد اجتماع وفدك مع {targetCountry}.",
            Severity = NotificationSeverity.Info,
            RelatedEntityType = nameof(DelegationMeetingRequest),
            RelatedEntityId = request.Id,
            SendEmail = true,
        }, logger, cancellationToken);
    }

    private static MeetingActionToken NewToken(
        Guid requestId, MeetingActionType action, string secret,
        DateTimeOffset now, DateTimeOffset expires) =>
        new()
        {
            Id = Guid.NewGuid(),
            SpeakerMeetingRequestId = requestId,
            Action = action,
            TokenHash = MeetingActionTokenHasher.Hash(secret),
            ExpiresUtc = expires,
            CreatedAt = now,
        };

    private string BuildUrl(string secret)
    {
        var baseUrl = options.Value.PublicWebBaseUrl;
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return string.Empty;
        }
        return $"{baseUrl.TrimEnd('/')}/meeting/confirm?token={Uri.EscapeDataString(secret)}";
    }
}
