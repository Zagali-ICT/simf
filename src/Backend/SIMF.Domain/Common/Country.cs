namespace SIMF.Domain.Common;

/// <summary>
/// Country lookup, also carrying the delegation fields for countries invited to
/// send one.
/// </summary>
public class Country
{
    /// <summary>The ISO 3166-1 numeric code, 682 for SA and 784 for AE. Assigned
    /// in seed data rather than by IDENTITY, so the same country carries the same
    /// id in every environment and rows can be copied between them without
    /// auto-increment drift.</summary>
    public int Id { get; set; }

    /// <summary>The ISO 3166-1 alpha-2 code, unique. The lookup key when an
    /// external system knows only the two-letter form.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>The E.164 dial code with its leading "+". Optional, because some
    /// non-state territories have none.</summary>
    public string? PhonePrefix { get; set; }

    /// <summary>Ascending sort key in the nationality picker. The seed leaves
    /// gaps of ten so an admin can insert an ordering without renumbering.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>True for a country invited to send a delegation. A delegate's
    /// nationality must be an invited country.</summary>
    public bool IsInvited { get; set; }

    /// <summary>The invited delegation's arrival date, set by an admin alongside
    /// <see cref="IsInvited"/>.</summary>
    public DateOnly? DelegationArrivalDate { get; set; }

    /// <summary>Rendered with the arrival date as the card's date range.</summary>
    public DateOnly? DelegationDepartureDate { get; set; }

    /// <summary>The delegate designated head of this country's delegation. A real
    /// foreign key, since profiles live in the same database, and resolved on read
    /// to the head's name and job title.</summary>
    public Guid? HeadOfDelegationUserProfileId { get; set; }

    /// <summary>Soft-delete flag.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
