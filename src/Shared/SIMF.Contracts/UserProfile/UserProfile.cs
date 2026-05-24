namespace SIMF.Contracts.UserProfile;

/// <summary>The body returned by <c>GET /api/v1/account/profile</c>
/// (decisions D-046 b, P8 — D-049). When the user has not filled the
/// form yet, every field is empty / null except <see cref="QrId"/> (which
/// is present whenever the account state is Approved) and
/// <see cref="ProfileTypeId"/> (which is present when the admin has
/// already assigned a subtype).</summary>
public sealed class UserProfileResponse
{
    /// <summary>The <see cref="ProfileType"/> id, when one is assigned.
    /// P8 moved this off <c>SimfUser</c>.</summary>
    public Guid? ProfileTypeId { get; set; }

    /// <summary>The picked interest ids (P9 — D-050; الاهتمامات). Empty
    /// when the user has not filled the form yet; the validator requires
    /// 1-10 ids on every save.</summary>
    public IList<Guid> InterestIds { get; set; } = new List<Guid>();

    public string ArabicName { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;
    public string NationalityCode { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string PlaceOfBirth { get; set; } = string.Empty;
    public bool IsSaudi { get; set; }
    public string? NationalId { get; set; }
    public string? IqamaNumber { get; set; }
    public string? PassportNumber { get; set; }
    public string? SaudiMobile { get; set; }
    public string? InternationalMobile { get; set; }

    /// <summary>True when an ID-image has been uploaded. The image bytes
    /// themselves are not in the response — fetch them at
    /// <c>GET /api/v1/account/profile/id-image</c>.</summary>
    public bool HasIdImage { get; set; }

    /// <summary>The 12-character Crockford QR id; null when the account is
    /// not yet Approved.</summary>
    public string? QrId { get; set; }
}

/// <summary>The body posted to <c>POST /api/v1/account/profile</c>
/// (D-046 b, P8). An upsert — first call creates the row, every later
/// call updates it. The validator enforces the field-shape rules.
/// <see cref="ProfileTypeId"/> is not on the request shape — the
/// profile-type is admin-assigned only; the user cannot self-pick it
/// today.</summary>
public sealed class UpsertUserProfileRequest
{
    /// <summary>The picked interest ids (P9 — D-050). Required: 1-10
    /// active <see cref="Interest"/> ids; the validator rejects empties /
    /// duplicates / unknown ids / deactivated ids.</summary>
    public IList<Guid> InterestIds { get; set; } = new List<Guid>();

    public string ArabicName { get; set; } = string.Empty;
    public string EnglishName { get; set; } = string.Empty;
    public string NationalityCode { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string PlaceOfBirth { get; set; } = string.Empty;
    public bool IsSaudi { get; set; }
    public string? NationalId { get; set; }
    public string? IqamaNumber { get; set; }
    public string? PassportNumber { get; set; }
    public string? SaudiMobile { get; set; }
    public string? InternationalMobile { get; set; }
}

/// <summary>One country entry surfaced to the client picker.</summary>
public sealed record CountryDto(string Code, string NameEn, string NameAr);

/// <summary>The body of <c>GET /api/v1/account/profile/countries</c>.</summary>
public sealed record CountryListResponse(IReadOnlyList<CountryDto> Countries);
