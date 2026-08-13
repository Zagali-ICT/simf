namespace SIMF.Contracts.Authentication;

/// <summary>
/// One VVIP/VIP visitor in the موج (Mawj) welcome roster. The
/// projection the technical teams consume to compose the welcome messages:
/// the identity + the موج extras (Mawj ID, honorific, preferred language) + the
/// tier + the contact details + whether a welcome photo is on file.
/// </summary>
/// <param name="ProfileId">The attendee record, and the row's key. Every VVIP
/// on this roster has one — including the ones minted into a bulk order or
/// registered at a walk-in desk, who are exactly the guests the PR desk most
/// needs to see and who used to be missing from it entirely.</param>
/// <param name="UserId">Their app account, or null when they never asked for
/// one. It is not the identity of the guest, only of their sign-in, so it
/// appears here for the two things that genuinely need an account: the welcome
/// photo route (<c>GET /admin/visitors/{UserId}/vip-photo</c>) and the account
/// edit form. Both are unavailable for an accountless guest, which is why
/// <see cref="HasVipPhoto"/> is false whenever this is null.</param>
public sealed record VipRosterRow(
    Guid ProfileId,
    Guid? UserId,
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
    DateTime CreatedAt);
