using SIMF.Domain.Common;

namespace SIMF.Domain.Exhibitors;

/// <summary>A visitor captured as a lead by scanning their entry-badge QR at a booth. None of the
/// visitor's own details are copied here; the card is resolved on read from their profile. The
/// scan time is the inherited creation stamp.</summary>
public sealed class ExhibitorVisitorScan : BaseAuditEntity
{
    /// <summary>The exhibiting user who scanned. Bare Guid: they live in the Identity database.</summary>
    public Guid ExhibitorUserId { get; set; }

    /// <summary>The owning booth. Null only on legacy rows whose capturer had no
    /// membership to resolve; such a row stays visible to that capturer alone.</summary>
    public Guid? ExhibitorId { get; set; }
    public Exhibitor? Exhibitor { get; set; }

    public Guid VisitorProfileId { get; set; }
    public Profiles.UserProfile? VisitorProfile { get; set; }

    public string? Note { get; set; }
}
