namespace SIMF.Contracts.Authentication;

/// <summary>
/// V-1 (D-429) — one VVIP/VIP visitor in the موج (Mawj) welcome roster. The
/// projection the technical teams consume to compose the welcome messages:
/// the identity + the موج extras (Mawj ID, honorific, preferred language) + the
/// tier + the contact details + whether a welcome photo is on file (fetch it via
/// <c>GET /admin/visitors/{UserId}/vip-photo</c>). A cross-DB read resolved on
/// the server (D-157): the profile lives on SIMF_App, the email/state/display
/// name on SIMF_Identity.
/// </summary>
public sealed record VipRosterRow(
    Guid UserId,
    string DisplayName,
    string EnglishName,
    string ArabicName,
    string? Honorific,
    string? JobTitle,
    // 2026-07-20 — Arabic twin of JobTitle for the bilingual موج welcome roster.
    string? JobTitleArabic,
    string? MawjId,
    string? PreferredLanguage,
    string TierName,
    string TierNameArabic,
    string Email,
    string? Mobile,
    string? ReferenceNumber,
    string AccountState,
    bool HasVipPhoto,
    DateTimeOffset CreatedAt);
