namespace SIMF.Contracts.Companies;

/// <summary>
/// D-199 #3 — admin grid row for an exhibitor / sponsor company.
/// <c>Type</c> is the <c>CompanyType</c> int value (0 = Exhibitor,
/// 1 = Sponsor) to keep the contract free of an Infrastructure / Domain
/// dependency, matching the AdminSponsorSummary.Tier-as-int convention.
/// </summary>
public sealed record AdminCompanySummary(
    Guid Id,
    string NameEn,
    string NameAr,
    int Type,
    string? ContactEmail,
    string? ContactPhone,
    string? Website,
    int AccountCount,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>D-199 #3 — full admin detail for one company.</summary>
public sealed record AdminCompanyDetail(
    Guid Id,
    string NameEn,
    string NameAr,
    int Type,
    string? ContactEmail,
    string? ContactPhone,
    string? Website,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>
/// D-199 #3 — body of <c>POST /api/v1/admin/companies</c>. Creates the company
/// shell (name + type); accounts are provisioned afterwards via
/// <c>POST /api/v1/admin/companies/{id}/accounts</c>.
/// </summary>
public sealed class CreateCompanyRequest
{
    /// <summary>English display name (1–256 chars).</summary>
    public string NameEn { get; init; } = string.Empty;

    /// <summary>Arabic display name (1–256 chars).</summary>
    public string NameAr { get; init; } = string.Empty;

    /// <summary>The <c>CompanyType</c> int value (0 = Exhibitor, 1 = Sponsor).</summary>
    public int Type { get; init; }

    /// <summary>Optional primary contact email (≤320 chars).</summary>
    public string? ContactEmail { get; init; }

    /// <summary>Optional primary contact phone (≤32 chars).</summary>
    public string? ContactPhone { get; init; }

    /// <summary>Optional company website (≤512 chars).</summary>
    public string? Website { get; init; }
}

/// <summary>D-199 #3 — body of <c>PUT /api/v1/admin/companies/{id}</c>.
/// Not sealed: the endpoint binds {id}+body via a derived route class (D-202).</summary>
public class UpdateCompanyRequest
{
    /// <summary>English display name (1–256 chars).</summary>
    public string NameEn { get; init; } = string.Empty;

    /// <summary>Arabic display name (1–256 chars).</summary>
    public string NameAr { get; init; } = string.Empty;

    /// <summary>The <c>CompanyType</c> int value (0 = Exhibitor, 1 = Sponsor).</summary>
    public int Type { get; init; }

    /// <summary>Optional primary contact email (≤320 chars).</summary>
    public string? ContactEmail { get; init; }

    /// <summary>Optional primary contact phone (≤32 chars).</summary>
    public string? ContactPhone { get; init; }

    /// <summary>Optional company website (≤512 chars).</summary>
    public string? Website { get; init; }

    /// <summary>Soft-delete / restore flag.</summary>
    public bool IsActive { get; init; } = true;
}

/// <summary>
/// D-199 #3 — one provisioned login account under a company. <c>UserId</c> is
/// the SimfUser id on the Identity database (logical FK).
/// </summary>
public sealed record CompanyAccountSummary(
    Guid Id,
    Guid UserId,
    string ContactName,
    string Email,
    string? RoleLabel,
    bool IsActive,
    DateTimeOffset CreatedAt);

/// <summary>
/// D-199 #3 — body of <c>POST /api/v1/admin/companies/{id}/accounts</c>.
/// Provisions a least-privilege login account tagged to the company. The
/// account is created through the existing admin provisioning pipeline
/// (a Visitor account) and a <c>CompanyMembership</c> row links it.
/// Not sealed: the endpoint binds {id}+body via a derived route class (D-202).
/// </summary>
public class ProvisionCompanyAccountRequest
{
    /// <summary>The contact person's name; used as the account display name (1–256 chars).</summary>
    public string ContactName { get; init; } = string.Empty;

    /// <summary>The new account's email address; must not already be registered.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Optional free-text role label inside the company (≤128 chars).</summary>
    public string? RoleLabel { get; init; }
}
