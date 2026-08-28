namespace SIMF.Contracts.Organisations;

// Organisation lookup — bilingual Saudi-companies directory (gov Excel import).
// Admin CRUD + bulk import + public picker search contracts.

/// <summary>One organisation row in the admin grid.</summary>
public sealed record AdminOrganisationSummary(
    Guid Id,
    string NameAr,
    string? NameEn,
    string? CommercialRegistration,
    string? Sector,
    string? City,
    bool IsActive);

/// <summary>Full organisation detail — every column for the admin view/edit form.</summary>
public sealed record AdminOrganisationDetail(
    Guid Id,
    string NameAr,
    string? NameEn,
    string? CommercialRegistration,
    string? Sector,
    string? City,
    string? Phone,
    string? Email,
    string? Website,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Admin create payload.</summary>
public sealed class CreateOrganisationRequest
{
    /// <summary>Arabic organisation name (required).</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>English organisation name (optional).</summary>
    public string? NameEn { get; set; }

    /// <summary>Commercial registration number (optional, unique when present).</summary>
    public string? CommercialRegistration { get; set; }

    /// <summary>Business sector (optional).</summary>
    public string? Sector { get; set; }

    /// <summary>City (optional).</summary>
    public string? City { get; set; }

    /// <summary>Contact phone (optional).</summary>
    public string? Phone { get; set; }

    /// <summary>Contact email (optional).</summary>
    public string? Email { get; set; }

    /// <summary>Website URL (optional).</summary>
    public string? Website { get; set; }
}

/// <summary>Admin update payload.</summary>
public class UpdateOrganisationRequest
{
    /// <summary>Arabic organisation name (required).</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>English organisation name (optional).</summary>
    public string? NameEn { get; set; }

    /// <summary>Commercial registration number (optional, unique when present).</summary>
    public string? CommercialRegistration { get; set; }

    /// <summary>Business sector (optional).</summary>
    public string? Sector { get; set; }

    /// <summary>City (optional).</summary>
    public string? City { get; set; }

    /// <summary>Contact phone (optional).</summary>
    public string? Phone { get; set; }

    /// <summary>Contact email (optional).</summary>
    public string? Email { get; set; }

    /// <summary>Website URL (optional).</summary>
    public string? Website { get; set; }

    /// <summary>Whether the organisation is active.</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>Lightweight organisation item for the public picker / typeahead search.</summary>
/// <param name="IsOther">True for the single seeded catch-all row. The client
/// reveals a free-text box when it is chosen and sends what the visitor types as
/// <c>organisationOther</c>. Flagged on the item rather than compared against a
/// well-known id, so the app never has to carry a hard-coded GUID. Appended
/// last, so the shipped wire contract stays append-only.</param>
public sealed record OrganisationPickerItem(
    Guid Id,
    string NameAr,
    string? NameEn,
    string? City,
    bool IsOther = false);

/// <summary>Outcome of a bulk Excel import — row tallies plus per-row error messages.</summary>
public sealed record OrganisationImportResult(
    int RowsRead,
    int Inserted,
    int Updated,
    int Skipped,
    IReadOnlyList<string> Errors);
