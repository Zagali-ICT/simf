namespace SIMF.Contracts.Programme;

/// <summary>One row in the public speakers list. Only the fields the
/// visitor-facing list card shows: the avatar (resolved from
/// <see cref="PhotoRelativePath"/> by the client), the rank line, and the
/// bilingual name. <see cref="DisplayOrder"/> is carried so the client can keep
/// a stable order if it re-sorts locally. Served by
/// <c>GET /api/v1/app/speakers</c>. Mirrors the <c>PublicBoothSummary</c>
/// public-read shape.</summary>
public sealed record PublicSpeakerSummary(
    Guid Id,
    string Name,
    string NameArabic,
    string? Rank,
    string? RankArabic,
    int? CountryId,
    string? CountryNameEn,
    string? CountryNameAr,
    string? PhotoRelativePath,
    int DisplayOrder,
    // True when the speaker has an active SpeakerPhoto asset in the
    // unified media-asset pipeline; the client/website prefers serving that
    // (via /content/assets or /app/assets) over the legacy PhotoRelativePath.
    // Appended with a default so the shipped mobile wire contract is preserved.
    bool HasPhotoAsset = false);

/// <summary>Envelope for the public speakers list.</summary>
public sealed record PublicSpeakers(IReadOnlyList<PublicSpeakerSummary> Items);

/// <summary>Full public view of one speaker: the bilingual name + rank,
/// nationality, the four bilingual rich-text tabs (Bio / Qualifications /
/// Training experience / Awards — the four tabs on the profile screen), the consent
/// toggle the client uses to show/hide the "Request meeting" affordance,
/// the opted-in social URLs, and the speaker's sessions.
///
/// <para>Privacy: only the consent-gated fields the speaker has opted to
/// publish are surfaced. The social URLs are returned only when
/// <see cref="AllowsDataSharing"/> is true; the
/// <see cref="AllowsMeetingRequests"/> flag drives the client's meeting
/// affordance. The <c>UserProfileId</c> account link is deliberately NOT
/// surfaced on the public projection.</para>
///
/// Served by <c>GET /api/v1/app/speakers/{id}</c>.</summary>
public sealed record PublicSpeakerDetail(
    Guid Id,
    string Name,
    string NameArabic,
    string? Rank,
    string? RankArabic,
    int? CountryId,
    string? CountryNameEn,
    string? CountryNameAr,
    string? Bio,
    string? BioArabic,
    string? Qualifications,
    string? QualificationsArabic,
    string? TrainingExperience,
    string? TrainingExperienceArabic,
    string? Awards,
    string? AwardsArabic,
    bool AllowsMeetingRequests,
    bool AllowsDataSharing,
    string? FacebookUrl,
    string? LinkedInUrl,
    string? XUrl,
    // Opted-in website URL, gated by AllowsDataSharing like the social
    // URLs. Name-keyed JSON, so older app builds simply ignore it (wire-safe).
    string? WebsiteUrl,
    string? PhotoRelativePath,
    int DisplayOrder,
    IReadOnlyList<PublicSpeakerSession> Sessions);

/// <summary>One of the speaker's scheduled sessions, shown on the
/// speaker profile. A deliberately lean line (bilingual title + hall +
/// time window) — the speaker profile does not need the theme chip or the
/// seat summary that the full agenda session detail
/// (<see cref="PublicSessionDetail"/>) carries, so it is not coupled to
/// that contract. Ordered by <see cref="Start"/>. Times are the <b>Saudi wall
/// clock</b>, serialised zone-free; the Flutter client renders them
/// verbatim and must not convert by the device timezone.</summary>
public sealed record PublicSpeakerSession(
    Guid Id,
    string Code,
    string Title,
    string TitleArabic,
    Guid HallId,
    string HallName,
    string HallNameArabic,
    DateTime Start,
    DateTime End);
