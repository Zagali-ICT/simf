using SIMF.Common.Enums;
using SIMF.Domain.Notifications;

namespace SIMF.Application.Notifications;

/// <summary>
/// Writes one in-app notification row (P12 — D-053) and optionally
/// queues an email through <c>IEmailSender</c>. The dispatcher is the
/// seam every lifecycle event uses; P13 wires the call sites.
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>Dispatches one notification request.</summary>
    Task DispatchAsync(
        NotificationRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// One notification dispatch — the in-app row + (when
/// <see cref="SendEmail"/> is true) an email rendered from the
/// matching template (P13).
/// </summary>
public sealed class NotificationRequest
{
    public required Guid UserId { get; init; }
    public required NotificationKind Kind { get; init; }
    public required string Title { get; init; }
    public required string TitleArabic { get; init; }
    public required string Body { get; init; }
    public required string BodyArabic { get; init; }
    public NotificationSeverity Severity { get; init; } = NotificationSeverity.Info;
    public string? RelatedEntityType { get; init; }
    public Guid? RelatedEntityId { get; init; }

    /// <summary>D-713 — when true (and <see cref="RelatedEntityId"/> is set), the
    /// dispatcher skips the write if a notification of the same
    /// <see cref="Kind"/> for the same <see cref="RelatedEntityId"/> already
    /// exists for <see cref="UserId"/>. Gives "one prompt per (user, kind,
    /// entity)" — used so a session-rating prompt fires once whether it comes
    /// from the hall-departure hook (GAP-A) or the clock-end worker. Default
    /// false leaves every existing dispatch untouched (some kinds are
    /// intentionally repeatable).</summary>
    public bool DeduplicateByRelatedEntity { get; init; }

    /// <summary>D-677 — an explicit app-internal deep-link. Null (the default)
    /// lets <see cref="NotificationKindCatalog"/> derive one from the kind +
    /// <see cref="RelatedEntityId"/>; a non-null value here wins.</summary>
    public string? ClickUrl { get; init; }

    /// <summary>D-677 — an explicit group code; null defers to the catalog.</summary>
    public string? Group { get; init; }

    /// <summary>When true, the dispatcher queues an email for the user
    /// (P13 wires the template renderer behind this).</summary>
    public bool SendEmail { get; init; }

    /// <summary>Optional pre-rendered HTML email body — used when the
    /// caller wants to bypass the renderer and supply the email
    /// directly. Null defers rendering to the dispatcher.</summary>
    public string? PreRenderedEmailHtml { get; init; }

    /// <summary>Optional pre-rendered plaintext email body.</summary>
    public string? PreRenderedEmailPlainText { get; init; }
}
