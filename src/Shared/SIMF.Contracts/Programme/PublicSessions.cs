using SIMF.Common.Enums;

namespace SIMF.Contracts.Programme;

/// <summary>D-199 (gap doc G3, Mockup page 16 "Agenda") — one row in the
/// public agenda list. Bilingual title; the hosting hall is projected
/// EN + AR so the app does not need a second fetch; the primary theme
/// (first by the session's theme order) drives the "Hall · Kind" line
/// and the agenda colour chip. <see cref="StartUtc"/>/<see cref="EndUtc"/>
/// are UTC — the Flutter agenda renders local time per the device tz.
/// Served by <c>GET /api/v1/programme/sessions</c>.</summary>
public sealed record PublicSessionListItem(
    Guid Id,
    string Code,
    string Title,
    string TitleArabic,
    Guid HallId,
    string HallName,
    string HallNameArabic,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string? PrimaryThemeName,
    string? PrimaryThemeNameArabic,
    string? PrimaryThemeColor);

/// <summary>D-199 — envelope for the public agenda list.</summary>
public sealed record PublicSessions(IReadOnlyList<PublicSessionListItem> Items);

/// <summary>D-199 (Mockup page 17 "Session detail") — full public view
/// of one session: bilingual title + abstract, hall EN/AR, the time
/// window, every tagged theme (ordered), the ordered speaker list, and
/// a cheap seat-availability summary. Served by
/// <c>GET /api/v1/programme/sessions/{id}</c>.</summary>
public sealed record PublicSessionDetail(
    Guid Id,
    string Code,
    string Title,
    string TitleArabic,
    string? Description,
    string? DescriptionArabic,
    Guid HallId,
    string HallName,
    string HallNameArabic,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    IReadOnlyList<PublicSessionTheme> Themes,
    IReadOnlyList<PublicSessionSpeaker> Speakers,
    PublicSessionSeatSummary Seats);

/// <summary>D-199 — one theme/pillar tag on a public session. Order
/// follows the session's theme order; the first is the primary pillar
/// the agenda groups under.</summary>
public sealed record PublicSessionTheme(
    Guid Id,
    string Name,
    string NameArabic,
    string Color);

/// <summary>D-199 — one speaker on the public session detail card.
/// <see cref="Title"/> is the speaker's rank/role (Mockup "Chief
/// Scientist"); order follows the session's <c>DisplayOrder</c>
/// (0 = primary). The speaker's organisation is not part of the frozen
/// Speaker schema, so it is not surfaced here.</summary>
public sealed record PublicSessionSpeaker(
    Guid Id,
    string Name,
    string NameArabic,
    string? Title,
    int DisplayOrder,
    // B9 — D-225: appended (additive — wire contract preserved, D-219).
    SessionSpeakerRole Role = SessionSpeakerRole.Speaker);

/// <summary>D-199 — cheap seat-availability summary for the session
/// detail. <see cref="Capacity"/> is the effective capacity
/// (<c>CapacityOverride ?? Hall.Capacity</c>); <see cref="Reserved"/>
/// is the count of active (non-released) reservations;
/// <see cref="Available"/> is <c>Capacity - Reserved</c> floored at 0.
/// A single COUNT query — no per-seat grid (that is the seat-map
/// endpoint's job).</summary>
public sealed record PublicSessionSeatSummary(
    int Capacity,
    int Reserved,
    int Available);
