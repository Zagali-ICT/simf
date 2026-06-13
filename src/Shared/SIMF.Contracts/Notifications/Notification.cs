using SIMF.Common.Enums;

namespace SIMF.Contracts.Notifications;

/// <summary>One row in a notification list (P12 — D-053). The Arabic
/// + English title and body travel together so the page picks by
/// culture without a second call.
///
/// <para>D-108: <c>Kind</c> carries the <c>NotificationKind</c> enum
/// name (e.g. <c>"AccountApproved"</c>) — matches the on-disk form
/// and the dispatch enum — and <c>IsRead</c> is the materialised
/// derived flag (true once the row has a <c>ReadAt</c>).</para>
/// </summary>
public sealed record NotificationDto(
    Guid Id,
    string Kind,
    string Title,
    string TitleArabic,
    string Body,
    string BodyArabic,
    string Severity,
    DateTimeOffset? ReadAt,
    bool IsRead,
    DateTimeOffset CreatedAt,
    string? RelatedEntityType,
    Guid? RelatedEntityId);

/// <summary>The body of <c>GET /api/v1/app/account/notifications/unread-count</c>.</summary>
public sealed record UnreadCountResponse(int UnreadCount);
