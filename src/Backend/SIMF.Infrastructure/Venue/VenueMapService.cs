// Tests: SIMF.Api.Tests/VenueMapTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Venue.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Programme;
using SIMF.Domain.Auditing;
using SIMF.Domain.Venue;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Venue;

/// <summary>P2.5 — D-230 (FR-605, FDS-006 §5.3): the 2D venue map. Admin CRUD
/// over nodes + the public read for the app. Validates optional Hall / Booth
/// references against the same DbContext. Mirrors AdminSessionCategoryService.</summary>
internal sealed class VenueMapService(
    SimfAppDbContext db,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<VenueMapService> logger) : IVenueMapService
{
    public async Task<GridPage<AdminVenueMapNodeSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(50, 500);

        var rows = db.VenueMapNodes.AsNoTracking().AsQueryable();

        // CP grid per-column filters (D-255). Unknown columns are ignored.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "label":
                    rows = rows.Where(n => n.Label.Contains(v) || n.LabelArabic.Contains(v));
                    break;
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(n =>
                EF.Functions.Like(n.Label, $"%{term}%")
                || EF.Functions.Like(n.LabelArabic, $"%{term}%"));
        }

        // CP grid sortable columns (D-255). Default: Label ascending.
        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("label", true) => rows.OrderByDescending(n => n.Label),
            ("kind", false) => rows.OrderBy(n => n.Kind),
            ("kind", true) => rows.OrderByDescending(n => n.Kind),
            _ => rows.OrderBy(n => n.Label),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip).Take(top)
            .Select(n => new AdminVenueMapNodeSummary(
                n.Id, n.Label, n.LabelArabic, n.Kind, n.X, n.Y,
                n.HallId, n.BoothId, n.IsActive))
            .ToListAsync(cancellationToken);

        return GridPage<AdminVenueMapNodeSummary>.Of(page, total,
            skip, top);
    }

    public async Task<AdminVenueMapNodeDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var node = await db.VenueMapNodes.AsNoTracking()
            .SingleOrDefaultAsync(n => n.Id == id, cancellationToken);
        return node is null ? null : ToDetail(node);
    }

    public async Task<AdminVenueMapNodeDetail> CreateAsync(
        Guid actorUserId, AdminCreateVenueMapNodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var (label, labelAr) = ValidateLabels(request.Label, request.LabelArabic);
        await EnsureReferencesAsync(request.HallId, request.BoothId, cancellationToken);
        EnsureKindMatchesReferences(request.Kind, request.HallId, request.BoothId);

        var now = timeProvider.GetUtcNow();
        var node = new VenueMapNode
        {
            Id = Guid.NewGuid(),
            Label = label,
            LabelArabic = labelAr,
            Kind = request.Kind,
            X = request.X,
            Y = request.Y,
            HallId = request.HallId,
            BoothId = request.BoothId,
            IsActive = true,
            CreatedAt = now,
        };
        db.VenueMapNodes.Add(node);
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.VenueMapNodeCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={node.Id}; label={label}; kind={request.Kind}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created venue-map node {Label} ({Id})", actorUserId, label, node.Id);

        return ToDetail(node);
    }

    public async Task<AdminVenueMapNodeDetail> UpdateAsync(
        Guid actorUserId, Guid id, AdminUpdateVenueMapNodeRequest request,
        CancellationToken cancellationToken = default)
    {
        var node = await db.VenueMapNodes
            .SingleOrDefaultAsync(n => n.Id == id, cancellationToken)
            ?? throw NotFound();

        var (label, labelAr) = ValidateLabels(request.Label, request.LabelArabic);
        await EnsureReferencesAsync(request.HallId, request.BoothId, cancellationToken);
        EnsureKindMatchesReferences(request.Kind, request.HallId, request.BoothId);

        node.Label = label;
        node.LabelArabic = labelAr;
        node.Kind = request.Kind;
        node.X = request.X;
        node.Y = request.Y;
        node.HallId = request.HallId;
        node.BoothId = request.BoothId;
        node.IsActive = request.IsActive;
        node.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.VenueMapNodeUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={node.Id}; label={label}; active={node.IsActive}",
        }, cancellationToken);

        return ToDetail(node);
    }

    public async Task DeactivateAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var node = await db.VenueMapNodes
            .SingleOrDefaultAsync(n => n.Id == id, cancellationToken)
            ?? throw NotFound();
        if (!node.IsActive)
        {
            return; // idempotent
        }

        node.Deactivate();
        node.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.VenueMapNodeDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={node.Id}; label={node.Label}",
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<PublicVenueMapNode>> ListPublicAsync(
        CancellationToken cancellationToken = default)
    {
        return await db.VenueMapNodes.AsNoTracking()
            .Where(n => n.IsActive)
            .OrderBy(n => n.Label)
            .Select(n => new PublicVenueMapNode(
                n.Id, n.Label, n.LabelArabic, n.Kind, n.X, n.Y, n.HallId, n.BoothId))
            .ToListAsync(cancellationToken);
    }

    private static (string Label, string LabelArabic) ValidateLabels(
        string labelRaw, string labelArRaw)
    {
        var label = (labelRaw ?? string.Empty).Trim();
        var labelAr = (labelArRaw ?? string.Empty).Trim();
        if (label.Length is < 1 or > 128 || labelAr.Length is < 1 or > 128)
        {
            throw new ApiException(
                ErrorCodes.VenueMapNodeInvalid, 400,
                "Both labels are required and must be 1–128 characters.",
                "كلا الاسمين مطلوبان ويجب أن يتراوح طولهما بين 1 و 128 حرفاً.");
        }
        return (label, labelAr);
    }

    private async Task EnsureReferencesAsync(
        Guid? hallId, Guid? boothId, CancellationToken cancellationToken)
    {
        if (hallId is { } hid)
        {
            var exists = await db.Halls.AsNoTracking()
                .AnyAsync(h => h.Id == hid && h.IsActive, cancellationToken);
            if (!exists)
            {
                throw new ApiException(
                    ErrorCodes.VenueMapNodeInvalid, 400,
                    "The referenced hall was not found.",
                    "لم يتم العثور على القاعة المرتبطة.");
            }
        }
        if (boothId is { } bid)
        {
            var exists = await db.Booths.AsNoTracking()
                .AnyAsync(b => b.Id == bid && b.IsActive, cancellationToken);
            if (!exists)
            {
                throw new ApiException(
                    ErrorCodes.VenueMapNodeInvalid, 400,
                    "The referenced booth was not found.",
                    "لم يتم العثور على الجناح المرتبط.");
            }
        }
    }

    // D-611 (Wave B) — the DB CK_VenueMapNodes_KindArc enforces the weak arc; this
    // guards it at the edge with a clean 400 instead of a SaveChanges 500: a Hall
    // reference requires Kind=Hall, a Booth reference requires Kind=Booth, and a
    // node may not reference both. A Zone / PointOfInterest node carries neither.
    private static void EnsureKindMatchesReferences(
        VenueMapNodeKind kind, Guid? hallId, Guid? boothId)
    {
        var invalid =
            (hallId is not null && kind != VenueMapNodeKind.Hall)
            || (boothId is not null && kind != VenueMapNodeKind.Booth)
            || (hallId is not null && boothId is not null);
        if (invalid)
        {
            throw new ApiException(
                ErrorCodes.VenueMapNodeInvalid, 400,
                "A hall reference requires kind Hall, a booth reference requires kind Booth, and a node cannot reference both.",
                "ربط قاعة يتطلب النوع \"قاعة\"، وربط جناح يتطلب النوع \"جناح\"، ولا يمكن للعقدة ربط الاثنين معاً.");
        }
    }

    private static ApiException NotFound() =>
        new(
            ErrorCodes.VenueMapNodeNotFound, 404,
            "The venue-map node was not found.",
            "لم يتم العثور على عقدة الخريطة.");

    private static AdminVenueMapNodeDetail ToDetail(VenueMapNode n) => new(
        n.Id, n.Label, n.LabelArabic, n.Kind, n.X, n.Y,
        n.HallId, n.BoothId, n.IsActive, n.CreatedAt, n.UpdatedAt);
}
