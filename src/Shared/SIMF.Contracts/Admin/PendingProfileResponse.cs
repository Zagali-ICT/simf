namespace SIMF.Contracts.Admin;

/// <summary>
/// Body returned by the admin scoped pending-profile reads
/// (<c>GET /admin/visitors/{id}/profile-for-approval</c> and
/// <c>GET /admin/others/{id}/profile-for-approval</c>). Carries the
/// rich profile fields the approving admin needs to verify the
/// application; never reveals anything for accounts that aren't in
/// PendingApproval (the endpoint returns 404 in that case so an admin
/// can't enumerate approved users via this surface).
///
/// <para>Every field maps to a column that already exists on
/// <c>SimfUser</c> or <c>UserProfile</c> — no schema change.</para>
/// </summary>
public sealed record PendingProfileResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string UserType,
    Guid? ProfileTypeId,
    string? ProfileTypeName,
    string? ProfileTypeNameArabic,
    string? ArabicName,
    string? EnglishName,
    string? JobTitle,
    string? NationalityCode,
    DateOnly? DateOfBirth,
    string? PlaceOfBirth,
    bool IsSaudi,
    string? NationalId,
    string? IqamaNumber,
    string? PassportNumber,
    string? SaudiMobile,
    string? InternationalMobile,
    bool HasIdImage,
    IReadOnlyList<Guid> InterestIds,
    DateTime CreatedAt,
    // The remaining captured profile columns, surfaced on the
    // approval screen so the admin sees ALL data. No schema change.
    string Gender = "Unspecified",
    Guid? OrganisationId = null,
    string? OrganisationName = null,
    string? OrganisationNameArabic = null,
    string? PlateNumber = null,
    string? ReferenceNumber = null,
    IReadOnlyList<PendingProfileInterest>? Interests = null,
    // Does the account have a profile photo (avatar)? Shown alongside the
    // ID image in the approve modal. The avatar lives on SimfUser (Identity)
    // and is resolved on read — no cross-database relation, no schema change.
    bool HasAvatar = false);

/// <summary>A picked interest's bilingual name for the approval
/// screen's interest list (the modal previously showed only the count).</summary>
public sealed record PendingProfileInterest(string Name, string NameArabic);
