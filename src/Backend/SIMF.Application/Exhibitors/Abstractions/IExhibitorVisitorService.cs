using SIMF.Contracts.Contacts;
using SIMF.Contracts.Exhibitors;

namespace SIMF.Application.Exhibitors.Abstractions;

/// <summary>
/// Exhibitor ("Other" profile type) lead capture. An exhibitor scans a
/// visitor's entry-badge QR at their booth, captures the visitor to their
/// "My Visitors" list, and gets the visitor's full card. Visitor-tier callers
/// are rejected (403). Cards resolve live from the visitor's UserProfile — no
/// PII snapshot.
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
    /// visitor's full card resolved on read. 403 if the caller is a visitor.
    /// <para>FR-EXH-003 — the list is scoped to the caller's BOOTH, not to the
    /// caller: every officer of the exhibitor sees the same leads.</para></summary>
    Task<IReadOnlyList<ExhibitorVisitorRow>> ListMyVisitorsAsync(
        Guid exhibitorUserId, CancellationToken cancellationToken = default);

    /// <summary>FR-EXH-002 — drop one captured lead from the booth's list
    /// (soft-delete, the project convention). Idempotent: removing a lead that is
    /// already gone succeeds. 403 unless the caller is a current booth officer,
    /// 404 when the capture does not belong to their booth.</summary>
    Task RemoveCaptureAsync(
        Guid exhibitorUserId, Guid captureId, CancellationToken cancellationToken = default);

    /// <summary>FR-EXH-002 — one captured lead's live card, for the vCard export.
    /// Same booth scope and same subject-eligibility rule as the list, so a lead
    /// the list will not show cannot be exported either.</summary>
    Task<VisitorCard> GetCaptureCardAsync(
        Guid exhibitorUserId, Guid captureId, CancellationToken cancellationToken = default);
}
