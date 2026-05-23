namespace SIMF.Contracts.VisitorProfile;

/// <summary>The body returned by <c>GET /api/v1/account/visitor-profile</c>
/// (decision D-046 b, myComment #18). When the visitor has not filled the
/// form yet, every field is empty / null except <see cref="QrId"/> (which
/// is present whenever the account state is Approved).</summary>
public sealed class VisitorProfileResponse
{
    public string VisitorType { get; set; } = "Visitor";
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
    /// <c>GET /api/v1/account/visitor-profile/id-image</c>.</summary>
    public bool HasIdImage { get; set; }

    /// <summary>The 12-character Crockford QR id; null when the account is
    /// not yet Approved.</summary>
    public string? QrId { get; set; }
}

/// <summary>The body posted to <c>POST /api/v1/account/visitor-profile</c>
/// (D-046 b). An upsert — first call creates the row, every later call
/// updates it. The validator enforces the field-shape rules.</summary>
public sealed class UpsertVisitorProfileRequest
{
    public string VisitorType { get; set; } = "Visitor";
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

/// <summary>The body of <c>GET /api/v1/account/visitor-profile/countries</c>.</summary>
public sealed record CountryListResponse(IReadOnlyList<CountryDto> Countries);
