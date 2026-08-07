namespace SIMF.Contracts.Admin;

/// <summary>One row in the admin Countries grid.</summary>
public sealed record AdminCountrySummary(
    int Id,
    string Code,
    string Name,
    string NameArabic,
    string? PhonePrefix,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt,
    // Invited to send a delegation (وفد).
    bool IsInvited = false,
    // The invited delegation's arrival/departure dates, carried so the
    // grid Excel export can round-trip them (not rendered as grid columns).
    // Optional; null when unset or the country is not invited.
    DateOnly? DelegationArrivalDate = null,
    DateOnly? DelegationDepartureDate = null);

/// <summary>Full country detail (Details + Edit modals).</summary>
public sealed record AdminCountryDetail(
    int Id,
    string Code,
    string Name,
    string NameArabic,
    string? PhonePrefix,
    int DisplayOrder,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // Invited to send a delegation (وفد).
    bool IsInvited = false,
    // The invited delegation's (الوفود) arrival/departure dates + the
    // UserProfile id of its head of delegation (رئيس الوفد). All nullable.
    DateOnly? DelegationArrivalDate = null,
    DateOnly? DelegationDepartureDate = null,
    Guid? HeadOfDelegationUserProfileId = null);

/// <summary>One delegate of an invited country (الوفود), offered in the
/// CP head-of-delegation picker on the country Edit form.</summary>
public sealed record AdminCountryDelegateOption(
    Guid UserProfileId,
    string Name,
    string NameArabic,
    string? JobTitle);

public sealed class AdminCreateCountryRequest
{
    /// <summary>ISO 3166-1 numeric — manually assigned (e.g. 682 = SA).</summary>
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? PhonePrefix { get; set; }
    public int DisplayOrder { get; set; }

    /// <summary>True for a country invited to send a delegation (وفد).</summary>
    public bool IsInvited { get; set; }

    /// <summary>The arrival date of the invited delegation (الوفود); optional.</summary>
    public DateOnly? DelegationArrivalDate { get; set; }

    /// <summary>The departure date of the invited delegation (الوفود); optional.</summary>
    public DateOnly? DelegationDepartureDate { get; set; }

    /// <summary>الوفود — the UserProfile id of the invited delegation's head of
    /// delegation (رئيس الوفد); must be an active delegate of this country (optional).</summary>
    public Guid? HeadOfDelegationUserProfileId { get; set; }
}

/// <remarks>Not sealed: the admin update endpoint binds {id}+body via a derived
/// route class so it cannot drop a field at bind time.</remarks>
public class AdminUpdateCountryRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;
    public string? PhonePrefix { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>True for a country invited to send a delegation (وفد).</summary>
    public bool IsInvited { get; set; }

    /// <summary>The arrival date of the invited delegation (الوفود); optional.</summary>
    public DateOnly? DelegationArrivalDate { get; set; }

    /// <summary>The departure date of the invited delegation (الوفود); optional.</summary>
    public DateOnly? DelegationDepartureDate { get; set; }

    /// <summary>الوفود — the UserProfile id of the invited delegation's head of
    /// delegation (رئيس الوفد); must be an active delegate of this country (optional).</summary>
    public Guid? HeadOfDelegationUserProfileId { get; set; }
}
