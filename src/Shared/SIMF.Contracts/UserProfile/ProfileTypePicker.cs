namespace SIMF.Contracts.UserProfile;

/// <summary>D-190 (D-186 follow-up) — one row in the public profile-type
/// picker the mobile sign-up Screen 2 calls. Deliberately omits
/// <c>MobileAppRole</c> — that value is admin-curated authority that
/// rides on the JWT mobile_app_role claim only; the picker has no
/// need for it.</summary>
public sealed record ProfileTypePickerDto(
    Guid Id,
    string Name,
    string NameArabic,
    string PageColor,
    bool IsVisitor);

/// <summary>Response body for
/// <c>GET /api/v1/app/account/profile-types</c>.</summary>
public sealed record ProfileTypePickerListResponse(
    IReadOnlyList<ProfileTypePickerDto> Items);
