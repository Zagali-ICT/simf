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

    public async Task<MeetingActionPreview?> PreviewAsync(
        string tokenSecret, CancellationToken cancellationToken = default)
    {
        if (await ValidateAsync(tokenSecret, cancellationToken) is not { } loaded)
        {
            return null;
        }
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

    public async Task<MeetingActionOutcome?> ApplyAsync(
        string tokenSecret, CancellationToken cancellationToken = default)
    {
        if (await ValidateAsync(tokenSecret, cancellationToken) is not { } loaded)
        {
            return null;
        }
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
