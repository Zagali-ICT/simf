// Tests: SIMF.Api.Tests/NotificationBroadcastTests.cs
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Email;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.Notifications;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Notifications;

/// <summary>
/// The Control Panel "Announcements" desk service. Persists a Pending broadcast
/// job and fans it out (in-app row + email per recipient) when the worker calls
/// <see cref="ProcessNextPendingAsync"/>. Recipients are resolved at send time —
/// a session's active seat-holders (App DB) or a broad audience (Identity DB) —
/// and their emails via <see cref="IIdentityUserDirectory"/>, so no recipient
/// data is ever copied across the D-157 boundary and the two DBs never share a
/// transaction. Modelled on <c>AdminInvitationService.NotifyVipsAsync</c>, made
/// durable + paced.
/// </summary>
internal sealed class NotificationBroadcastService(
    SimfAppDbContext appDbContext,
    SimfIdentityDbContext identityDbContext,
    IIdentityUserDirectory userDirectory,
    INotificationDispatcher notificationDispatcher,
    IEmailQueue emailQueue,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<NotificationBroadcastService> logger) : INotificationBroadcastService
{
    // The bounded email queue drops writes above its capacity; pace the fan-out so
    // a batch never pushes it over. High-watermark leaves headroom for one batch.
    private const int DispatchBatchSize = 100;
    private const int EmailQueueHighWatermark = 700;
    private static readonly TimeSpan EmailPacingDelay = TimeSpan.FromMilliseconds(500);
    // Never pace forever: if the email queue stays full this long (a wedged SMTP
    // sender that never drains), stop pacing and proceed — the in-app rows still
    // land and the queue drops+logs the emails it cannot hold, rather than hanging
    // the worker (and every later broadcast) indefinitely.
    private static readonly TimeSpan MaxPacingWait = TimeSpan.FromMinutes(2);
    // A Processing row older than this was interrupted (a crash/restart mid-send);
    // the worker's startup reconciliation marks it Failed so it is not stuck forever.
    private static readonly TimeSpan StalledProcessingAge = TimeSpan.FromMinutes(15);

    public async Task<AdminBroadcastResult> CreateAsync(
        Guid actorUserId, AdminCreateBroadcastRequest request,
        CancellationToken cancellationToken = default)
    {
        var mode = ParseTargetMode(request.TargetMode);
        var severity = ParseSeverity(request.Severity);
        Guid? sessionId = null;
        BroadcastAudienceScope? scope = null;

        if (mode == BroadcastTargetMode.Session)
        {
            if (request.SessionId is not { } sid)
            {
                throw Invalid(
                    "Select a session for a session broadcast.",
                    "اختر جلسة لبثّ خاص بجلسة.");
            }
            var exists = await appDbContext.Sessions.AsNoTracking()
                .AnyAsync(session => session.Id == sid, cancellationToken);
            if (!exists)
            {
                throw new ApiException(
                    ErrorCodes.SessionNotFound, 404,
                    "The session was not found.",
                    "لم يتم العثور على الجلسة.");
            }
            sessionId = sid;
        }
        else
        {
            scope = ParseAudienceScope(request.AudienceScope);
        }

        var title = (request.Title ?? string.Empty).Trim();
        var titleArabic = (request.TitleArabic ?? string.Empty).Trim();
        var body = (request.Body ?? string.Empty).Trim();
        var bodyArabic = (request.BodyArabic ?? string.Empty).Trim();
        if (title.Length is < 1 or > 200 || titleArabic.Length is < 1 or > 200)
        {
            throw Invalid(
                "Message title (EN + AR) must be between 1 and 200 characters each.",
                "يجب أن يكون عنوان الرسالة (إنجليزي + عربي) بين 1 و200 حرفاً.");
        }
        if (body.Length is < 1 or > 2000 || bodyArabic.Length is < 1 or > 2000)
        {
            throw Invalid(
                "Message body (EN + AR) must be between 1 and 2000 characters each.",
                "يجب أن يكون نص الرسالة (إنجليزي + عربي) بين 1 و2000 حرفاً.");
        }

        var now = timeProvider.GetUtcNow();
        var broadcast = new NotificationBroadcast
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = actorUserId,
            TargetMode = mode,
            SessionId = sessionId,
            AudienceScope = scope,
            Title = title,
            TitleArabic = titleArabic,
            Body = body,
            BodyArabic = bodyArabic,
            Severity = severity,
            Status = BroadcastStatus.Pending,
            CreatedAt = now,
        };
        appDbContext.NotificationBroadcasts.Add(broadcast);
        await appDbContext.SaveChangesAsync(cancellationToken);

        var estimate = await CountRecipientsAsync(mode, sessionId, scope, cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BroadcastQueued,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"broadcastId={broadcast.Id}; mode={mode}; sessionId={sessionId}; scope={scope}; estimate={estimate}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {Actor} queued broadcast {BroadcastId} (mode={Mode}, estimate={Estimate}).",
            actorUserId, broadcast.Id, mode, estimate);

        return new AdminBroadcastResult(broadcast.Id, estimate);
    }

    public async Task<AdminBroadcastEstimateResult> EstimateAsync(
        AdminBroadcastEstimateRequest request,
        CancellationToken cancellationToken = default)
    {
        var mode = ParseTargetMode(request.TargetMode);
        Guid? sessionId = null;
        BroadcastAudienceScope? scope = null;

        if (mode == BroadcastTargetMode.Session)
        {
            // The composer calls this while the admin is still choosing — an
            // unpicked session estimates as 0 rather than erroring.
            if (request.SessionId is not { } sid)
            {
                return new AdminBroadcastEstimateResult(0);
            }
            sessionId = sid;
        }
        else
        {
            if (!Enum.TryParse<BroadcastAudienceScope>(
                request.AudienceScope, ignoreCase: true, out var parsed))
            {
                return new AdminBroadcastEstimateResult(0);
            }
            scope = parsed;
        }

        var count = await CountRecipientsAsync(mode, sessionId, scope, cancellationToken);
        return new AdminBroadcastEstimateResult(count);
    }

    public async Task<bool> ProcessNextPendingAsync(CancellationToken cancellationToken = default)
    {
        // Pick the oldest Pending id, then claim it ATOMICALLY (SET Processing
        // WHERE still Pending) in one SQL statement. If a second worker instance
        // races us (the SAD's scale-out / AlwaysOn posture), exactly one claim
        // affects the row — the loser sees 0 rows and moves on — so a broadcast is
        // sent at-most-once across instances, not just within one. Claiming before
        // dispatch also means a restart mid-send leaves the row Processing (never
        // re-picked, only Pending is), so a crash cannot resend either.
        var nextId = await appDbContext.NotificationBroadcasts
            .Where(row => row.Status == BroadcastStatus.Pending)
            .OrderBy(row => row.CreatedAt)
            .Select(row => (Guid?)row.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (nextId is not { } broadcastId)
        {
            return false;
        }

        var startedAt = timeProvider.GetUtcNow();
        var claimed = await appDbContext.NotificationBroadcasts
            .Where(row => row.Id == broadcastId && row.Status == BroadcastStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.Status, BroadcastStatus.Processing)
                .SetProperty(row => row.StartedAt, startedAt), cancellationToken);
        if (claimed == 0)
        {
            // Lost the race to another worker; report "did work" so the caller
            // loops to the next Pending row instead of sleeping a poll interval.
            return true;
        }

        var broadcast = await appDbContext.NotificationBroadcasts
            .FirstAsync(row => row.Id == broadcastId, cancellationToken);

        try
        {
            await FanOutAsync(broadcast, cancellationToken);
            broadcast.Status = BroadcastStatus.Completed;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            broadcast.Status = BroadcastStatus.Failed;
            broadcast.Error = ex.Message.Length > 1024 ? ex.Message[..1024] : ex.Message;
            logger.LogError(ex, "Broadcast {BroadcastId} failed during fan-out.", broadcast.Id);
        }
        broadcast.CompletedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BroadcastSent,
            Outcome = broadcast.Status == BroadcastStatus.Completed
                ? AuditOutcome.Success
                : AuditOutcome.Failure,
            ActorUserId = broadcast.CreatedByUserId,
            Detail = $"broadcastId={broadcast.Id}; status={broadcast.Status}; recipients={broadcast.TotalRecipients}; dispatched={broadcast.Dispatched}; emails={broadcast.EmailsEnqueued}; skipped={broadcast.Skipped}",
        }, cancellationToken);

        logger.LogInformation(
            "Broadcast {BroadcastId} {Status}: {Dispatched}/{Total} dispatched, {Emails} emails, {Skipped} skipped.",
            broadcast.Id, broadcast.Status, broadcast.Dispatched, broadcast.TotalRecipients,
            broadcast.EmailsEnqueued, broadcast.Skipped);

        return true;
    }

    public async Task<int> RecoverStalledAsync(CancellationToken cancellationToken = default)
    {
        // A Processing row whose StartedAt is older than the cutoff was interrupted
        // (a crash/restart mid-send) — it is never re-picked (only Pending is), so
        // mark it Failed so it surfaces in history instead of sitting Processing
        // forever. ExecuteUpdate — no row is loaded or tracked.
        var now = timeProvider.GetUtcNow();
        var cutoff = now - StalledProcessingAge;
        return await appDbContext.NotificationBroadcasts
            .Where(row => row.Status == BroadcastStatus.Processing
                && row.StartedAt != null && row.StartedAt < cutoff)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(row => row.Status, BroadcastStatus.Failed)
                .SetProperty(row => row.Error,
                    "Interrupted before completion (the worker restarted mid-send).")
                .SetProperty(row => row.CompletedAt, now), cancellationToken);
    }

    private async Task FanOutAsync(NotificationBroadcast broadcast, CancellationToken cancellationToken)
    {
        var recipientIds = await ResolveRecipientIdsAsync(
            broadcast.TargetMode, broadcast.SessionId, broadcast.AudienceScope, cancellationToken);
        broadcast.TotalRecipients = recipientIds.Count;
        if (recipientIds.Count == 0)
        {
            return;
        }

        var emailsByUser = await userDirectory.GetEmailsAsync(recipientIds, cancellationToken);

        // Bilingual HTML email body (EN block, rule, RTL AR block) — the free-text
        // message is HTML-encoded to neutralise any markup the admin typed.
        var emailHtml = EmailTemplateRenderer.ComposeBody(
            $"<p>{WebUtility.HtmlEncode(broadcast.Body)}</p>",
            $"<p>{WebUtility.HtmlEncode(broadcast.BodyArabic)}</p>");

        // Session-scoped rows carry the session reference + group under the app's
        // Sessions chip; audience rows defer to the catalog default group.
        var relatedType = broadcast.TargetMode == BroadcastTargetMode.Session ? "Session" : null;
        var relatedId = broadcast.TargetMode == BroadcastTargetMode.Session ? broadcast.SessionId : null;
        var group = broadcast.TargetMode == BroadcastTargetMode.Session
            ? NotificationKindCatalog.Groups.Sessions
            : null;

        var dispatched = 0;
        var emailsEnqueued = 0;
        var skipped = 0;

        var pacingAbandoned = false;
        for (var offset = 0; offset < recipientIds.Count; offset += DispatchBatchSize)
        {
            // Pace: wait until the bounded email queue has headroom for a batch so
            // no email is dropped (the queue drops writes above capacity). Bounded
            // by MaxPacingWait so a wedged SMTP sender never hangs the worker — past
            // the cap we stop pacing for the rest of this broadcast and let the
            // queue drop+log what it cannot hold (the in-app rows still land).
            if (!pacingAbandoned)
            {
                var pacingDeadline = timeProvider.GetUtcNow() + MaxPacingWait;
                while (emailQueue.PendingCount > EmailQueueHighWatermark
                    && !cancellationToken.IsCancellationRequested)
                {
                    if (timeProvider.GetUtcNow() >= pacingDeadline)
                    {
                        pacingAbandoned = true;
                        logger.LogWarning(
                            "Broadcast {BroadcastId}: email queue stayed full for {Cap}; sending the remainder without pacing (some emails may be dropped).",
                            broadcast.Id, MaxPacingWait);
                        break;
                    }
                    await Task.Delay(EmailPacingDelay, timeProvider, cancellationToken);
                }
            }

            var count = Math.Min(DispatchBatchSize, recipientIds.Count - offset);
            foreach (var userId in recipientIds.GetRange(offset, count))
            {
                var hasEmail = emailsByUser.TryGetValue(userId, out var email)
                    && !string.IsNullOrWhiteSpace(email);
                try
                {
                    await notificationDispatcher.DispatchAsync(new NotificationRequest
                    {
                        UserId = userId,
                        Kind = NotificationKind.AdminAnnouncement,
                        Title = broadcast.Title,
                        TitleArabic = broadcast.TitleArabic,
                        Body = broadcast.Body,
                        BodyArabic = broadcast.BodyArabic,
                        Severity = broadcast.Severity,
                        RelatedEntityType = relatedType,
                        RelatedEntityId = relatedId,
                        Group = group,
                        SendEmail = hasEmail,
                        PreRenderedEmailHtml = hasEmail ? emailHtml : null,
                    }, cancellationToken);
                    dispatched++;
                    if (hasEmail)
                    {
                        emailsEnqueued++;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    skipped++;
                    logger.LogError(ex,
                        "Broadcast {BroadcastId} dispatch failed for user {UserId}.",
                        broadcast.Id, userId);
                }
            }

            // Keep the shared Identity change-tracker from growing across the whole
            // fan-out: each notification is already persisted (the dispatcher saves
            // per row) and the SimfUser reads are never mutated, so detaching them
            // each batch avoids O(N^2) DetectChanges over a large "Everyone" send.
            identityDbContext.ChangeTracker.Clear();
        }

        broadcast.Dispatched = dispatched;
        broadcast.EmailsEnqueued = emailsEnqueued;
        broadcast.Skipped = skipped;
    }

    // Builds the distinct recipient-user-id query for a target — a single-context
    // query per branch (no cross-DB JOIN, D-157). Session/EventAttendees read the
    // App-DB seat reservations; the audience scopes read the Identity-DB users. All
    // reads are AsNoTracking so the Identity context (shared with the dispatcher's
    // writes) is never dirtied by a read. Returned as an IQueryable so the caller
    // chooses ToList (fan-out) or Count (estimate) — a Count runs in SQL and never
    // materialises the id list.
    private IQueryable<Guid> BuildRecipientQuery(
        BroadcastTargetMode mode, Guid? sessionId, BroadcastAudienceScope? scope)
    {
        if (mode == BroadcastTargetMode.Session)
        {
            if (sessionId is not { } sid)
            {
                return appDbContext.SeatReservations
                    .Where(_ => false)
                    .Select(reservation => reservation.ReservedForUserId!.Value);
            }
            return appDbContext.SeatReservations.AsNoTracking()
                .Where(reservation => reservation.SessionId == sid
                    && reservation.ReleasedAt == null
                    && reservation.ReservedForUserId != null)
                .Select(reservation => reservation.ReservedForUserId!.Value)
                .Distinct();
        }

        return scope switch
        {
            BroadcastAudienceScope.ApprovedAppUsers => identityDbContext.Users.AsNoTracking()
                .Where(user => user.UserType != UserType.Admin
                    && user.AccountState == AccountState.Approved)
                .Select(user => user.Id),

            BroadcastAudienceScope.EveryoneIncludingPending => identityDbContext.Users.AsNoTracking()
                .Where(user => user.UserType != UserType.Admin)
                .Select(user => user.Id),

            BroadcastAudienceScope.EventAttendees => appDbContext.SeatReservations.AsNoTracking()
                .Where(reservation => reservation.ReleasedAt == null
                    && reservation.ReservedForUserId != null)
                .Select(reservation => reservation.ReservedForUserId!.Value)
                .Distinct(),

            // A new BroadcastAudienceScope with no arm must fail loudly, not send to
            // zero recipients and report success.
            _ => throw new ArgumentOutOfRangeException(
                nameof(scope), scope, "Unhandled broadcast audience scope."),
        };
    }

    private Task<List<Guid>> ResolveRecipientIdsAsync(
        BroadcastTargetMode mode, Guid? sessionId, BroadcastAudienceScope? scope,
        CancellationToken cancellationToken) =>
        BuildRecipientQuery(mode, sessionId, scope).ToListAsync(cancellationToken);

    // Counts recipients in SQL (COUNT) without materialising the id list — used for
    // the composer's live estimate + the create response.
    private Task<int> CountRecipientsAsync(
        BroadcastTargetMode mode, Guid? sessionId, BroadcastAudienceScope? scope,
        CancellationToken cancellationToken) =>
        BuildRecipientQuery(mode, sessionId, scope).CountAsync(cancellationToken);

    public async Task<GridPage<AdminBroadcastSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(25, 200);

        var broadcasts = appDbContext.NotificationBroadcasts.AsNoTracking()
            .OrderByDescending(row => row.CreatedAt);

        var total = await broadcasts.CountAsync(cancellationToken);
        var rows = await broadcasts.Skip(skip).Take(top).ToListAsync(cancellationToken);
        var items = await MapAsync(rows, cancellationToken);

        return GridPage<AdminBroadcastSummary>.Of(items, total, skip, top);
    }

    public async Task<AdminBroadcastSummary?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var row = await appDbContext.NotificationBroadcasts.AsNoTracking()
            .SingleOrDefaultAsync(broadcast => broadcast.Id == id, cancellationToken);
        if (row is null)
        {
            return null;
        }
        return (await MapAsync([row], cancellationToken))[0];
    }

    // Projects broadcast rows to summaries, resolving the composer's display name
    // (Identity DB) and the target session's title (App DB) in one round-trip each.
    private async Task<List<AdminBroadcastSummary>> MapAsync(
        List<NotificationBroadcast> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var creatorIds = rows.Select(row => row.CreatedByUserId).Distinct().ToList();
        var namesByUser = await userDirectory.GetDisplayNamesAsync(creatorIds, cancellationToken);

        var sessionIds = rows
            .Where(row => row.SessionId.HasValue)
            .Select(row => row.SessionId!.Value)
            .Distinct()
            .ToList();
        var titlesBySession = sessionIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await appDbContext.Sessions.AsNoTracking()
                .Where(session => sessionIds.Contains(session.Id))
                .Select(session => new { session.Id, session.Title })
                .ToDictionaryAsync(session => session.Id, session => session.Title, cancellationToken);

        return rows.Select(row => new AdminBroadcastSummary(
            row.Id,
            row.CreatedByUserId,
            namesByUser.TryGetValue(row.CreatedByUserId, out var name) ? name : string.Empty,
            row.TargetMode.ToString(),
            row.SessionId,
            row.SessionId is { } sid && titlesBySession.TryGetValue(sid, out var title) ? title : null,
            row.AudienceScope?.ToString(),
            row.Title,
            row.TitleArabic,
            row.Body,
            row.BodyArabic,
            row.Severity.ToString(),
            row.Status.ToString(),
            row.TotalRecipients,
            row.Dispatched,
            row.EmailsEnqueued,
            row.Skipped,
            row.CreatedAt,
            row.StartedAt,
            row.CompletedAt,
            row.Error)).ToList();
    }

    private static BroadcastTargetMode ParseTargetMode(string? raw) =>
        Enum.TryParse<BroadcastTargetMode>(raw, ignoreCase: true, out var mode)
            ? mode
            : throw Invalid(
                "Choose a valid target (Session or Audience).",
                "اختر هدفاً صحيحاً (جلسة أو جمهور).");

    private static BroadcastAudienceScope ParseAudienceScope(string? raw) =>
        Enum.TryParse<BroadcastAudienceScope>(raw, ignoreCase: true, out var scope)
            ? scope
            : throw Invalid(
                "Choose a valid audience.",
                "اختر جمهوراً صحيحاً.");

    private static NotificationSeverity ParseSeverity(string? raw) =>
        Enum.TryParse<NotificationSeverity>(raw, ignoreCase: true, out var severity)
            ? severity
            : NotificationSeverity.Info;

    private static ApiException Invalid(string english, string arabic) =>
        new(ErrorCodes.BroadcastInvalid, 400, english, arabic);
}
