using SIMF.Common.Enums;

namespace SIMF.Contracts.Programme;

/// <summary>D-199 (gap doc G3, Mockup page 16 "Agenda") — one row in the
/// public agenda list. Bilingual title; the hosting hall is projected
/// EN + AR so the app does not need a second fetch; the primary theme
/// (first by the session's theme order) drives the "Hall · Kind" line
/// and the agenda colour chip. <see cref="StartUtc"/>/<see cref="EndUtc"/>
/// are UTC — the Flutter agenda renders local time per the device tz.
/// Served by <c>GET /api/v1/app/programme/sessions</c>.</summary>
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
    string? PrimaryThemeColor,
    // B9b — D-226: appended (additive — wire contract preserved, D-219).
    Guid? CategoryId = null,
    string? CategoryName = null,
    string? CategoryNameArabic = null,
    // P3.2 — D-231: broadcast lifecycle status (appended; lets the agenda
    // chip a "Recorded"/"Published" badge). Default preserves the wire.
    SessionStatus Status = SessionStatus.Scheduled,
    // D-252 (Mockup screen 16 "Agenda" + 17 "Session detail"): the agenda payload
    // is fetched once and CACHED, then the UI filters inline (Upcoming/Forum pills,
    // day strip, search) and previews a session without a second fetch — so the
    // list carries the body + the ordered speaker cards too (the category already
    // carries the "main session"/type tag). Appended (D-219 append-only).
    string? Description = null,
    string? DescriptionArabic = null,
    IReadOnlyList<PublicSessionSpeaker>? Speakers = null);

/// <summary>D-199 — envelope for the public agenda list.</summary>
public sealed record PublicSessions(IReadOnlyList<PublicSessionListItem> Items);

/// <summary>D-199 (Mockup page 17 "Session detail") — full public view
/// of one session: bilingual title + abstract, hall EN/AR, the time
/// window, every tagged theme (ordered), the ordered speaker list, and
/// a cheap seat-availability summary. Served by
/// <c>GET /api/v1/app/programme/sessions/{id}</c>.</summary>
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
    PublicSessionSeatSummary Seats,
    // B9b — D-226: appended (additive — wire contract preserved, D-219).
    Guid? CategoryId = null,
    string? CategoryName = null,
    string? CategoryNameArabic = null,
    // P3.2 — D-231: broadcast lifecycle status + publish stamp. The client
    // badges the state; PublishedAt marks when it went live. Appended; defaults
    // preserve the wire (D-219).
    SessionStatus Status = SessionStatus.Scheduled,
    DateTimeOffset? PublishedAt = null,
    // P3.2b — D-232: true when this published session has a recording the app
    // can stream. The app then POSTs the stream-token endpoint and plays the
    // range-streaming URL. The server only surfaces the recording when
    // Status == Published AND a recording exists (the recorded-Q&A read lands
    // in P3.4). No stored file name is exposed — only this flag.
    bool HasRecording = false,
    // §8 (Mockup screen 25/26): the LIVE broadcast. LiveStreamUrl non-null = the
    // session has a live feed (the app shows the LIVE player + badge); null =
    // recorded/scheduled. LiveSignLanguageUrl = the optional sign-language
    // interpretation feed (drives the live screen's لغة الإشارة toggle).
    // Appended (append-only, D-219). D-349: the provider is YouTube (POC) with a
    // direct HLS/MP4 URL as a fallback — validated by LiveStreamUrlPolicy.
    string? LiveStreamUrl = null,
    string? LiveSignLanguageUrl = null);

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
    SessionSpeakerRole Role = SessionSpeakerRole.Speaker,
    // §7 (Mockup screen 17 "المتحدثون"): the speaker shown WITH a session also
    // carries the country flag + the photo. Appended (append-only, D-219).
    // CountryId is the ISO 3166-1 numeric the client renders the flag from;
    // the names are the label/fallback; PhotoRelativePath is the avatar. All
    // null when the speaker has no recorded country / photo.
    int? CountryId = null,
    string? CountryNameEn = null,
    string? CountryNameAr = null,
    string? PhotoRelativePath = null);

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

/// <summary>P3.4 — D-235 (Completion Programme §5.4): one question in a
/// published session's recorded Q&amp;A archive — the Committee-approved
/// questions, attributed to the asker. <see cref="IsPushed"/> marks the ones
/// that were pushed to the speaker live (answered on stage). Served by
/// <c>GET /api/v1/app/programme/sessions/{id}/recorded-questions</c> for an
/// approved (signed-in) account.</summary>
public sealed record PublicRecordedQuestion(
    Guid Id,
    string QuestionText,
    string AskedByDisplayName,
    SessionQuestionRecipient Recipient,
    bool IsPushed,
    DateTimeOffset CreatedAt);

/// <summary>P4.1 — D-237 (Completion Programme §6.4.1, Mockup screen 34): the
/// published AI session summary / محضر the app reads. Every section is bilingual
/// (English + Arabic) so the app renders the active locale with a fallback;
/// <see cref="KeyPoints"/>/<see cref="KeyPointsArabic"/> are newline-delimited
/// (one bullet per non-empty line). <see cref="GeneratedByAi"/> drives the
/// "auto-generated" banner. Served by
/// <c>GET /api/v1/app/programme/sessions/{id}/summary</c> only once the Committee
/// has published it (else 404).</summary>
public sealed record PublicSessionSummary(
    Guid SessionId,
    string KeyPoints,
    string KeyPointsArabic,
    string Recommendations,
    string RecommendationsArabic,
    string Speakers,
    string SpeakersArabic,
    string FullText,
    string FullTextArabic,
    bool GeneratedByAi,
    DateTimeOffset PublishedAt);

/// <summary>P3.2b — D-232 (D-213): the response from the recording stream-token
/// endpoint. <see cref="Token"/> is a short-lived JWT scoped to one recording;
/// the player appends it to <see cref="StreamUrl"/> on the query string
/// (<c>?access_token=…</c>) since an HTML5 <c>&lt;video&gt;</c> cannot set an
/// Authorization header. <see cref="ExpiresInSeconds"/> tells the client when
/// to re-request a token if a long viewing session outlives it.</summary>
public sealed record RecordingStreamTokenResponse(
    string Token, int ExpiresInSeconds, string StreamUrl);
