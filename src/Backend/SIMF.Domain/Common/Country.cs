namespace SIMF.Domain.Common;

/// <summary>Country lookup, also carrying the delegation fields for countries invited to send one.</summary>
public class Country
{
    /// <summary>The ISO 3166-1 numeric code, seeded rather than IDENTITY, so a country keeps the same id in every environment.</summary>
    public int Id { get; set; }

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>E.164 dial code with its leading "+".</summary>
    public string? PhonePrefix { get; set; }

    /// <summary>Ascending; seeded in gaps of ten so an admin can insert without renumbering.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>A delegate's nationality must be an invited country.</summary>
    public bool IsInvited { get; set; }

    public DateOnly? DelegationArrivalDate { get; set; }
    public DateOnly? DelegationDepartureDate { get; set; }

    public Guid? HeadOfDelegationUserProfileId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
