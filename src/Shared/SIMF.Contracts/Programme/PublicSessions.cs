using SIMF.Common.Enums;
using SIMF.Common.Options;

namespace SIMF.Contracts.Programme;

/// <summary>One row in the public agenda list. Bilingual title; the hosting
/// hall is projected EN + AR so the app does not need a second fetch; the
/// primary theme (first by the session's theme order) drives the "Hall · Kind"
/// line and the agenda colour chip. <see cref="Start"/>/<see cref="End"/>
/// are the <b>Saudi wall clock</b>, serialised zone-free with no
/// trailing <c>Z</c> and no offset. The Flutter agenda renders them verbatim —
/// it must NOT convert by the device timezone, or a phone set to any other
/// zone would show the wrong start time for a fixed +03:00 event.
/// Served by <c>GET /api/v1/app/programme/sessions</c>.</summary>
public sealed record PublicSessionListItem(
    Guid Id,
    string Code,
    string Title,
    string TitleArabic,
    Guid HallId,
    string HallName,
    string HallNameArabic,
    DateTime Start,
    DateTime End,
    string? PrimaryThemeName,
    string? PrimaryThemeNameArabic,
    string? PrimaryThemeColor,
    // Appended: additive, so the shipped wire contract is preserved.
    Guid? CategoryId = null,
    string? CategoryName = null,
    string? CategoryNameArabic = null,
    // Broadcast lifecycle status (appended; lets the agenda
    // chip a "Recorded"/"Published" badge). Default preserves the wire.
    SessionStatus Status = SessionStatus.Scheduled,
    // The agenda payload is fetched once and CACHED, then the UI filters inline
    // (Upcoming/Forum pills, day strip, search) and previews a session without a
    // second fetch — so the list carries the body + the ordered speaker cards
    // too (the category already carries the "main session"/type tag). Appended.
    string? Description = null,
    string? DescriptionArabic = null,
    IReadOnlyList<PublicSessionSpeaker>? Speakers = null,
    // The session's kind (Workshop / Session / Event), which drives the agenda's
    // type tabs. Null = untyped. Appended.
    SessionType? Type = null,
    // True when this session has an ACTIVE SessionSummary carrying a
    // PublishedAt stamp (the محضر the app renders), so the agenda can badge
    // "summary available" without a per-session GET /summary probe. This is the
    // summary's OWN editorial publish state, orthogonal to Status. Appended.
    bool HasPublishedSummary = false);

/// <summary>Envelope for the public agenda list.</summary>
public sealed record PublicSessions(IReadOnlyList<PublicSessionListItem> Items);

/// <summary>One programme day ("تفاصيل اليوم") on the public agenda: a
/// calendar date with its own bilingual title and logo banner
/// (<see cref="HasImage"/> = a <c>ProgrammeDayImage</c> asset is linked, served
/// by the anonymous route <c>/app/assets/ProgrammeDayImage/{Id}/image</c>), plus
/// the day's sessions. Served by <c>GET /app/programme/days</c>.</summary>
public sealed record PublicProgrammeDay(
    Guid Id,
    DateOnly Date,
    string Title,
    string TitleArabic,
    int DisplayOrder,
    bool HasImage,
    IReadOnlyList<PublicSessionListItem> Sessions);

/// <summary>Envelope for the day-grouped public agenda.</summary>
public sealed record PublicProgrammeDays(IReadOnlyList<PublicProgrammeDay> Days);

/// <summary>Full public view of one session: bilingual title + abstract, hall
/// EN/AR, the time window, every tagged theme (ordered), the ordered speaker
/// list, and a cheap seat-availability summary. Served by
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
    DateTime Start,
    DateTime End,
    IReadOnlyList<PublicSessionTheme> Themes,
    IReadOnlyList<PublicSessionSpeaker> Speakers,
    PublicSessionSeatSummary Seats,
    // Appended: additive, so the shipped wire contract is preserved.
    Guid? CategoryId = null,
    string? CategoryName = null,
    string? CategoryNameArabic = null,
    // Broadcast lifecycle status + publish stamp. The client
    // badges the state; PublishedAt marks when it went live. Appended; defaults
    // preserve the wire.
    SessionStatus Status = SessionStatus.Scheduled,
    DateTime? PublishedAt = null,
    // True when this published session has a recording the app
    // can stream. The app then POSTs the stream-token endpoint and plays the
    // range-streaming URL. The server only surfaces the recording when
    // Status == Published AND a recording exists. No stored file name is
    // exposed — only this flag.
    bool HasRecording = false,
    // The LIVE broadcast. LiveStreamUrl non-null = the session has a live feed
    // (the app shows the LIVE player + badge); null = recorded/scheduled.
    // LiveSignLanguageUrl = the optional sign-language interpretation feed
    // (drives the live screen's لغة الإشارة toggle). Appended (append-only). The
    // provider is YouTube for the proof of concept, with a direct HLS/MP4 URL as
    // a fallback — validated by LiveStreamUrlPolicy.
    string? LiveStreamUrl = null,
    string? LiveSignLanguageUrl = null,
    // The AI live-caption text shown under the player. Non-null = the app
    // renders the caption strip with this text; null = the placeholder hint.
    // Bilingual; provider stubbed (manual CP entry for the POC). Appended.
    string? LiveCaptions = null,
    string? LiveCaptionsArabic = null,
    // The session's 1-based position within its day (sessions ordered by Start),
    // e.g. 2 → the badge shows "02". Computed by the service; 0 = unknown (an
    // older API → the app falls back to the code on the badge). Appended.
    int DisplayOrder = 0,
    // The Website session-detail page's "أبرز المخرجات" key-outcome
    // bullets (ordered) and the "at a glance" card's bilingual language label.
    // Sourced from the SessionOutcome table + Session.Language. Appended
    // (append-only) — null/empty on an older API and the app ignores them.
    IReadOnlyList<PublicSessionOutcome>? Outcomes = null,
    string? Language = null,
    string? LanguageArabic = null,
    // The Website session-detail page's "روابط التحميل" downloads: the
    // session's downloadable presentation files. PUBLIC (owner decision
    // 2026-07-15) — anonymously downloadable from the website, served by the
    // same-origin route the page builds from each item's Id. Sourced from the
    // active SpeakerPresentation rows for the session. Appended (append-only).
    // The app keeps its own signed-in /app/presentations read.
    IReadOnlyList<PublicSessionDownload>? Downloads = null,
    // The session's kind, so the app can reduce a WORKSHOP's detail to title +
    // time. PublicSessionListItem has carried Type since the agenda's type tabs
    // landed, but this DETAIL record did not — the app's SessionDetail.fromJson
    // read json['type'] and always got null, so a type-conditional render on the
    // detail screen could never fire. Appended (append-only); null = an untyped
    // session, which renders the full detail exactly as before.
    SessionType? Type = null,
    // The arrival grace the SERVER will actually apply to this session,
    // in whole minutes, already resolved (its own override, else its hall's, else
    // the global walk-in value). The app decides from it whether to show the
    // "you can check in now" strip.
    //
    // Appended (append-only) with the historical 15 as the default, so an older
    // API answers exactly as the app assumed before. It exists because the grace
    // became configurable per hall and per session: the app had a hard-coded 15
    // under a comment telling the next person to keep it in step with a server
    // constant by hand, and there is no single server constant left to mirror.
    int ArrivalGraceMinutes = WalkInModeOptions.DefaultArrivalGraceMinutes,
    // The informational notice the client shows WITH the live stream, in the
    // active locale (falling back to the other pair member when one is blank).
    // NOTHING is gated by it — the notice was originally specified as a
    // Riyadh-region restriction on the feed and the owner reversed that
    // (2026-07-31), so the stream above still plays for every caller and
    // LiveStreamUrl is served unchanged. Both null/blank = no banner. Carried on
    // the DETAIL only (not on PublicSessionListItem): the live surface reads the
    // detail — the list has no live-feed field at all — so an agenda row cannot
    // render the banner, and putting it there would be paid for on every row of
    // every fetch. Appended.
    string? LiveNotice = null,
    string? LiveNoticeArabic = null);

/// <summary>One bilingual key-outcome bullet on the public session-detail page
/// ("أبرز المخرجات"), in the session's display order. Sourced from the
/// <c>SessionOutcome</c> table.</summary>
public sealed record PublicSessionOutcome(
    string Text,
    string TextArabic);

/// <summary>One downloadable presentation file on the public session-detail page
/// ("روابط التحميل"). Metadata only — the bytes are fetched anonymously
/// from the same-origin download route the website builds from
/// <see cref="Id"/>. Sourced from the active <c>SpeakerPresentation</c> rows for
/// the session.</summary>
public sealed record PublicSessionDownload(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes);

/// <summary>One theme/pillar tag on a public session. Order
/// follows the session's theme order; the first is the primary pillar
/// the agenda groups under.</summary>
public sealed record PublicSessionTheme(
    Guid Id,
    string Name,
    string NameArabic,
    string Color,
    // The Website session-detail "المحاور الرئيسية" cards render the theme
    // description under the name. Sourced from the existing Theme.Description
    // columns. Appended (append-only) — the app ignores them.
    string? Description = null,
    string? DescriptionArabic = null);

/// <summary>One speaker on the public session detail card.
/// <see cref="Title"/> is the speaker's rank/role (for example "Chief
/// Scientist"); order follows the session's <c>DisplayOrder</c>
/// (0 = primary). The speaker's organisation is not part of the frozen
/// Speaker schema, so it is not surfaced here.</summary>
public sealed record PublicSessionSpeaker(
    Guid Id,
    string Name,
    string NameArabic,
    string? Title,
    int DisplayOrder,
    // Appended, so the shipped wire contract is preserved: an older client
    // simply does not see it.
    SessionSpeakerRole Role = SessionSpeakerRole.Speaker,
    // The speaker ("المتحدثون") shown WITH a session also carries the country
    // flag + the photo. Appended.
    // CountryId is the ISO 3166-1 numeric the client renders the flag from;
    // the names are the label/fallback; PhotoRelativePath is the avatar. All
    // null when the speaker has no recorded country / photo.
    int? CountryId = null,
    string? CountryNameEn = null,
    string? CountryNameAr = null,
    string? PhotoRelativePath = null,
    // True when the speaker has an active SpeakerPhoto asset in the
    // unified StoredFile store; the Website session page then serves it via the
    // same-origin /content/assets/SpeakerPhoto/{id}/image proxy (since the media
    // pipeline landed the photo usually lives there, not in PhotoRelativePath).
    // Appended (append-only). The app keeps using PhotoRelativePath / its own
    // avatar route.
    bool HasPhotoAsset = false,
    // 2026-07-19 (owner) — the Arabic rank/title, mapped from Speaker.RankArabic
    // (the twin of Title = Speaker.Rank). Appended (append-only) so the app
    // shows the rank in the active locale; older builds ignore it and keep Title.
    string? TitleArabic = null);

/// <summary>Cheap seat-availability summary for the session
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

/// <summary>One question in a published session's recorded Q&amp;A archive — the
/// questions that were actually asked on stage (pushed to the speaker by the
/// moderator), attributed to the asker. Owner 2026-07-19 (two-path Q&amp;A): the
/// archive filters on <see cref="IsPushed"/>, so this flag is always
/// <c>true</c> for an archive row.
/// Served by <c>GET /api/v1/app/programme/sessions/{id}/recorded-questions</c> for
/// an approved (signed-in) account.</summary>
public sealed record PublicRecordedQuestion(
    Guid Id,
    string QuestionText,
    string AskedByDisplayName,
    SessionQuestionRecipient Recipient,
    bool IsPushed,
    DateTime CreatedAt);

/// <summary>The published AI session summary / محضر the app reads. Every
/// section is bilingual (English + Arabic) so the app renders the active locale
/// with a fallback; <see cref="KeyPoints"/>/<see cref="KeyPointsArabic"/> are
/// newline-delimited (one bullet per non-empty line).
/// <see cref="GeneratedByAi"/> drives the
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
    DateTime PublishedAt,
    // The two videos on the summary surface. RecordingUrl = the session's FULL
    // live recording, sourced from Session.LiveStreamUrl (the YouTube/HLS live
    // feed that doubles as the recording in the proof of concept — NOT a schema
    // addition). SummaryVideoUrl = the team's OPTIONAL short summary cut (the new
    // SessionSummary.SummaryVideoUrl column). Both nullable: the app hides each
    // player when its URL is null. Appended (defaulted) so the wire stays
    // append-only.
    string? RecordingUrl = null,
    string? SummaryVideoUrl = null);

/// <summary>The approved محضر served to the session host / moderator
/// ("ready for المحاور"). Same content as <see cref="PublicSessionSummary"/> but
/// gated on the team <c>ApprovedAt</c> stamp rather than the public publish, so a
/// host / moderator can read it before (or instead of) a public release. Served by
/// <c>GET /api/v1/app/programme/sessions/{id}/summary/approved</c> (403 if the
/// caller is neither the session host nor a session moderator; 404 if not yet
/// approved).</summary>
public sealed record HostSessionSummary(
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
    DateTime ApprovedAt);

/// <summary>One downloadable session presentation on the public list
/// ("عروض الجلسات"): the session it belongs to (title bilingual + start,
/// for the app's day tabs), the presenting speaker (bilingual), and the file
/// metadata. The bytes are fetched from
/// <c>GET /app/presentations/{id}/file</c>, not embedded. Served by
/// <c>GET /api/v1/app/presentations</c> for an approved (signed-in) account.</summary>
public sealed record PublicPresentationItem(
    Guid Id,
    Guid SessionId,
    string SessionTitle,
    string SessionTitleArabic,
    DateTime SessionStart,
    string SpeakerName,
    string SpeakerNameArabic,
    string FileName,
    string ContentType,
    long SizeBytes);

/// <summary>Envelope for the public session-presentations list.
/// Time-ordered by session start so the app groups by day.</summary>
public sealed record PublicPresentations(IReadOnlyList<PublicPresentationItem> Items);

/// <summary>The response from the recording stream-token
/// endpoint. <see cref="Token"/> is a short-lived JWT scoped to one recording;
/// the player appends it to <see cref="StreamUrl"/> on the query string
/// (<c>?access_token=…</c>) since an HTML5 <c>&lt;video&gt;</c> cannot set an
/// Authorization header. <see cref="ExpiresInSeconds"/> tells the client when
/// to re-request a token if a long viewing session outlives it.</summary>
public sealed record RecordingStreamTokenResponse(
    string Token, int ExpiresInSeconds, string StreamUrl);
