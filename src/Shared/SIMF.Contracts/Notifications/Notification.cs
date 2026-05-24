namespace SIMF.Contracts.Notifications;

/// <summary>One row in a notification list (P12 — D-053). The Arabic
/// + English title and body travel together so the page picks by
/// culture without a second call.</summary>
public sealed record NotificationDto(
    Guid Id,
    string Kind,
    string Title,
    string TitleArabic,
    string Body,
    string BodyArabic,
    string Severity,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt,
    string? RelatedEntityType,
    Guid? RelatedEntityId);

/// <summary>The body of <c>GET /api/v1/account/notifications/unread-count</c>.</summary>
public sealed record UnreadCountResponse(int UnreadCount);
