using SIMF.Domain.Common;
using SIMF.Domain.Contacts;

namespace SIMF.Domain.Exhibitors;

/// <summary>
/// D-199 #3 (additive schema) — an exhibitor managed from the Control Panel.
/// The owner model is "create the EXHIBITOR NAME first, then create the login
/// ACCOUNTS under it"; the accounts are tracked as
/// <see cref="ExhibitorMembership"/> rows. Exhibitors are CP-created only.
/// Soft-deleted via <see cref="IsActive"/> — admin grids and pickers filter
/// <c>IsActive == true</c>.
/// </summary>
public sealed class Exhibitor : BaseAuditEntity
{

    /// <summary>English display name (1–256 chars).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Arabic display name (1–256 chars) — the primary surface.</summary>
    public string NameArabic { get; set; } = string.Empty;

    /// <summary>Optional primary contact email (≤320 chars). Retained per
    /// SIMF-FDS-014 (D-260): the entity keeps its own inline contact; the linked
    /// <see cref="Contact"/> is the fallback when these are null.</summary>
    public string? ContactEmail { get; set; }

    /// <summary>Optional primary contact phone (≤32 chars).</summary>
    public string? ContactPhone { get; set; }

    /// <summary>Optional website (≤512 chars).</summary>
    public string? Website { get; set; }

    /// <summary>SIMF-FDS-014 (D-260) — optional link to the shared <c>Contact</c>
    /// directory record (logo / name / phones / social / website / location /
    /// country). Null until linked; multiple entities may reference the same
    /// Contact. The public projection prefers the Contact when set.</summary>
    public Guid? ContactId { get; set; }
    public Contact? Contact { get; set; }//Add FK

}
