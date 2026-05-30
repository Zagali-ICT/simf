using SIMF.Common.Enums;

namespace SIMF.Domain.Companies;

/// <summary>
/// D-199 #3 (additive schema) — an exhibitor / sponsor company managed from the
/// Control Panel. The owner model is "create the COMPANY NAME first, then create
/// the login ACCOUNTS under it"; the accounts are tracked as
/// <see cref="CompanyMembership"/> rows. Exhibitor / sponsor companies are
/// CP-created only. Soft-deleted via <see cref="IsActive"/> — admin grids and
/// pickers filter <c>IsActive == true</c>.
/// </summary>
public sealed class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>English display name (1–256 chars).</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>Arabic display name (1–256 chars) — the primary surface.</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>Whether the company is an exhibitor or a sponsor.</summary>
    public CompanyType Type { get; set; } = CompanyType.Exhibitor;

    /// <summary>Optional primary contact email (≤320 chars).</summary>
    public string? ContactEmail { get; set; }

    /// <summary>Optional primary contact phone (≤32 chars).</summary>
    public string? ContactPhone { get; set; }

    /// <summary>Optional company website (≤512 chars).</summary>
    public string? Website { get; set; }

    /// <summary>Soft-delete flag. List endpoints filter on this.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
