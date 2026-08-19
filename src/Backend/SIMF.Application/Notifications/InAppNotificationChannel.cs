// Tests: SIMF.Api.Tests/NotificationChannelTests.cs
//        SIMF.Api.Tests/NotificationTests.cs (round-trips, unchanged behaviour)
using Microsoft.Extensions.Logging;
using SIMF.Domain.Notifications;
using SIMF.Common;

namespace SIMF.Application.Notifications;

/// <summary>
/// `sms-whatsapp-channels` — the in-app channel: writes the one
/// <see cref="Notification"/> row the app's bell reads. Always handles, because an
/// in-app record is the baseline every dispatch produces; the other channels are
/// additions on top of it, never replacements. Runs first (<see cref="Order"/> 0)
/// so no later transport's failure can cost the user that record.
///
/// <para>The row-building logic is lifted verbatim out of
/// <c>NotificationDispatcher</c> — this is a move, not a rewrite.</para>
/// </summary>
internal sealed class InAppNotificationChannel(
    INotificationRepository notifications,
    TimeProvider timeProvider,
    ILogger<InAppNotificationChannel> logger) : INotificationChannel
{
    public string Name => "in-app";

    public int Order => 0;

    public bool ShouldHandle(NotificationRequest request) => true;

    public async Task SendAsync(
        NotificationRequest request, CancellationToken cancellationToken = default)
    {
        var notification = BuildRow(request);

        await notifications.AddAsync(notification, cancellationToken);

        logger.LogInformation(
            "Notification {Kind} dispatched for {UserId} (id {Id})",
            request.Kind, request.UserId, notification.Id);
    }

    /// <summary>The batched form: the same rows, written in one round-trip instead
    /// of one per recipient. This channel's cost is entirely per round-trip, so an
    /// audience fan-out was spending 20,000 sequential INSERT + SaveChanges calls
    /// where one AddRange per batch does the same work. Same rows, same order, same
    /// mapping — <see cref="BuildRow"/> is shared with <see cref="SendAsync"/> so
    /// the two can never drift.</summary>
    public async Task SendManyAsync(
        IReadOnlyList<NotificationRequest> requests,
        CancellationToken cancellationToken = default)
    {
        if (requests.Count == 0)
        {
            return;
        }

        var rows = new List<Notification>(requests.Count);
        foreach (var request in requests)
        {
            rows.Add(BuildRow(request));
        }

        await notifications.AddRangeAsync(rows, cancellationToken);

        // One line per batch, not per row: an "Everyone" send would otherwise put
        // 20,000 information-level entries into the log for a single announcement.
        logger.LogInformation(
            "Notification {Kind} dispatched to {Count} recipient(s).",
            requests[0].Kind, rows.Count);
    }

    private Notification BuildRow(NotificationRequest request) => new()
    {
        Id = Guid.NewGuid(),
        UserId = request.UserId,
        Kind = request.Kind,
        Title = request.Title,
        TitleArabic = request.TitleArabic,
        Body = request.Body,
        BodyArabic = request.BodyArabic,
        Severity = request.Severity,
        RelatedEntityType = request.RelatedEntityType,
        RelatedEntityId = request.RelatedEntityId,
        // Stamp the group + deep-link from the catalog when the call
        // site doesn't set them, so every existing dispatch gets correct values
        // and a new kind adds one catalog arm.
        ClickUrl = request.ClickUrl
            ?? NotificationKindCatalog.ClickUrlFor(request.Kind, request.RelatedEntityId),
        GroupCode = request.Group
            ?? NotificationKindCatalog.GroupFor(request.Kind),
        CreatedAt = timeProvider.SimfNow(),
    };
}
