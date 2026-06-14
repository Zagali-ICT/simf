using SIMF.Contracts.Contacts;
using SIMF.Contracts.Exhibitors;

namespace SIMF.Application.Exhibitors.Abstractions;

/// <summary>
/// D-426 — exhibitor ("Other" profile type) lead capture. An exhibitor scans a
/// visitor's entry-badge QR at their booth, captures the visitor to their
/// "My Visitors" list, and gets the visitor's full card. Visitor-tier callers
/// are rejected (403). Cards resolve live from the visitor's UserProfile — no
/// PII snapshot (D-157).
/// </summary>
public interface IExhibitorVisitorService
{
    /// <summary>Resolve the visitor whose entry badge encodes <paramref name="qrId"/>,
    /// record the capture (idempotent per exhibitor+visitor), and return the
    /// visitor's full card. 403 if the caller is a visitor; 404 if no badge
    /// matches.</summary>
    Task<VisitorCard> ScanByBadgeAsync(
        Guid exhibitorUserId, string qrId, string? note,
        CancellationToken cancellationToken = default);

    /// <summary>The exhibitor's captured visitors, newest first, each with the
    /// visitor's full card resolved on read. 403 if the caller is a visitor.</summary>
    Task<IReadOnlyList<ExhibitorVisitorRow>> ListMyVisitorsAsync(
        Guid exhibitorUserId, CancellationToken cancellationToken = default);
}
