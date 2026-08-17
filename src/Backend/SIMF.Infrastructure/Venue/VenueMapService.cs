// Tests: SIMF.Api.Tests/VenueMapTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Venue.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Programme;
using SIMF.Domain.Auditing;
using SIMF.Domain.Venue;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Venue;

/// <summary>The 2D venue map. Admin CRUD
/// over nodes + the public read for the app. Validates optional Hall / Booth
/// references against the same DbContext. Mirrors AdminSessionCategoryService.</summary>
internal sealed class VenueMapService(
    SimfAppDbContext db,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<VenueMapService> logger) : IVenueMapService
{
    /// <summary>The grid contract for /admin/venue-map: one entry per key
    /// VenueMapList.razor can send, as both its filter and its sort.
    ///
    /// <para><c>label</c> is the key the grid's filter box sends AND the key its
    /// sort button sends, so it has to be one declared column over one property.
    /// It is the English label — the column the grid actually renders. The Arabic
    /// label keeps its own key, and both are searched by the free-text term.</para>
    /// </summary>
    private static readonly GridColumns<VenueMapNode> Columns = new GridColumns<VenueMapNode>()
        .Add("label", node => node.Label, searchable: true)
        .Add("labelArabic", node => node.LabelArabic, searchable: true)
        .Add("kind", node => node.Kind)
        .Add("isActive", node => node.IsActive)
        .DefaultOrder("label")
        .PageSize(fallback: 50, max: 500);

    private static readonly Expression<Func<VenueMapNode, AdminVenueMapNodeSummary>> ToSummary =
        node => new AdminVenueMapNodeSummary(
            node.Id, node.Label, node.LabelArabic, node.Kind, node.X, node.Y,
            node.HallId, node.BoothId, node.IsActive);

    public Task<GridPage<AdminVenueMapNodeSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        db.VenueMapNodes.ToGridPageAsync(
            query, Columns, node => node.Id, ToSummary, cancellationToken);

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

        var now = timeProvider.SimfNow();
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

        await auditLog.WriteSuccessAsync(
            AuditEvents.VenueMapNodeCreated,
            actorUserId,
            $"id={node.Id}; label={label}; kind={request.Kind}",
            cancellationToken);

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
        node.UpdatedAt = timeProvider.SimfNow();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.VenueMapNodeUpdated,
            actorUserId,
            $"id={node.Id}; label={label}; active={node.IsActive}",
            cancellationToken);

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
        node.UpdatedAt = timeProvider.SimfNow();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.VenueMapNodeDeactivated,
            actorUserId,
            $"id={node.Id}; label={node.Label}",
            cancellationToken);
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

    // The DB CK_VenueMapNodes_KindArc enforces the weak arc; this
    // guards it at the edge with a clean 400 instead of a SaveChanges 500: a Hall
    // reference requires Kind=Hall, a Booth reference requires Kind=Booth, and a
    // node may not reference both. A Zone / PointOfInterest node carries neither.
    private static void EnsureKindMatchesReferences(
        VenueMapNodeKind kind, Guid? hallId, Guid? boothId)
    {
        // Reject an out-of-range Kind (e.g. a direct API call sending an
        // undefined enum value) before it persists: the DB has no enum-range check,
        // only the weak arc below, so an unreferenced node would otherwise store it.
        if (!Enum.IsDefined(kind))
        {
            throw new ApiException(
                ErrorCodes.VenueMapNodeInvalid, 400,
                "The venue-map node kind is not a recognised value.",
                "نوع عقدة الخريطة غير معروف.");
        }

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
