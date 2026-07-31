using SIMF.Contracts.Contacts;

namespace SIMF.Contracts.Exhibitors;

// D-426 — exhibitor ("Other" profile type) lead capture. An exhibitor scans a
// visitor's entry-badge QR at their booth and captures the visitor to their
// "My Visitors" list with the full visitor card. Visitor-only accounts cannot
// use these endpoints (403). The card resolves live from the visitor's
// UserProfile (+ Organisation / Country / email) — no PII snapshot (D-157).

/// <summary>Body of <c>POST /app/exhibitor/visitors/scan</c> — the scanned
/// entry-badge QR id (the value the badge QR encodes), plus an optional note.</summary>
public sealed class ScanVisitorBadgeRequest
{
    public string QrId { get; set; } = string.Empty;
    public string? Note { get; set; }
}

/// <summary>One captured visitor in the exhibitor's <em>My Visitors</em> list —
/// the scan metadata plus the visitor's full <see cref="VisitorCard"/> resolved
/// on read.</summary>
public sealed record ExhibitorVisitorRow(
    Guid Id,
    DateTime ScannedAt,
    string? Note,
    VisitorCard Card);
