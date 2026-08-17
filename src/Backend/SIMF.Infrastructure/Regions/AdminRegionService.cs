// Tests: SIMF.Api.Tests/RegionTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Regions.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Regions;
using SIMF.Domain.Regions;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Regions;

/// <summary>
/// Region lookup — admin CRUD over <see cref="Region"/> (bilingual
/// administrative-regions directory). Built on <see cref="SimfAppDbContext"/>.
/// Mirrors <c>AdminOrganisationService</c>: bilingual (NameArabic / Name),
/// unique <c>Code</c> (409 on duplicate), soft-delete (IsActive), audited via
/// <see cref="IAuditLog"/>.
/// </summary>
internal sealed class AdminRegionService(
    SimfAppDbContext db,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminRegionService> logger) : IAdminRegionService
{
    /// <summary>
    /// The grid contract for /admin/regions: one entry per key a caller can send.
    /// <c>name</c> is the ARABIC name and <c>nameEn</c> the English one, which is
    /// how RegionsList labels its two name columns. <c>sortOrder</c> is not a grid
    /// column; it is declared because it is the first level of the natural order.
    /// <c>isActive</c> is the active-rows filter the walk-in registration form
    /// sends when it loads its birth-region picker.
    /// </summary>
    private static readonly GridColumns<Region> Columns = new GridColumns<Region>()
        .Add("code", region => region.Code, searchable: true)
        .Add("name", region => region.NameArabic, searchable: true)
        .Add("nameEn", region => region.Name, searchable: true)
        .Add("sortOrder", region => region.SortOrder)
        .Add("isActive", region => region.IsActive)
        .DefaultOrder("sortOrder")
        .DefaultOrder("name")
        .PageSize(fallback: 25, max: 200);

    private static readonly Expression<Func<Region, AdminRegionSummary>> ToSummary =
        region => new AdminRegionSummary(
            region.Id,
            region.Code,
            region.Name,
            region.NameArabic,
            region.IsActive);

    public Task<GridPage<AdminRegionSummary>> ListAsync(
        GridQuery query, CancellationToken ct = default) =>
        db.Regions.ToGridPageAsync(query, Columns, region => region.Id, ToSummary, ct);

    public async Task<AdminRegionDetail?> GetAsync(
        Guid id, CancellationToken ct = default)
    {
        var region = await db.Regions
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id, ct);
        return region is null ? null : ToDetail(region);
    }

    public async Task<AdminRegionDetail> CreateAsync(
        Guid actorUserId,
        CreateRegionRequest request,
        CancellationToken ct = default)
    {
        var v = ValidateAndNormalise(
            request.Code, request.NameArabic, request.Name, request.SortOrder);

        var clash = await db.Regions
            .AsNoTracking()
            .AnyAsync(row => row.Code == v.Code, ct);
        if (clash)
        {
            throw DuplicateCode(v.Code);
        }

        var now = timeProvider.SimfNow();
        var region = new Region
        {
            Id = Guid.NewGuid(),
            Code = v.Code,
            NameArabic = v.NameAr,
            Name = v.NameEn,
            SortOrder = v.SortOrder,
            IsActive = true,
            CreatedAt = now,
        };
        db.Regions.Add(region);
        await db.SaveChangesAsync(ct);

        await auditLog.WriteSuccessAsync(
            AuditEvents.RegionCreated,
            actorUserId,
            $"id={region.Id}; code={v.Code}; nameAr={v.NameAr}",
            ct);

        logger.LogInformation(
            "Admin {ActorId} created Region {Code} ({Id})",
            actorUserId, v.Code, region.Id);

        return ToDetail(region);
    }

    public async Task<AdminRegionDetail> UpdateAsync(
        Guid actorUserId,
        Guid id,
        UpdateRegionRequest request,
        CancellationToken ct = default)
    {
        var region = await db.Regions
            .SingleOrDefaultAsync(row => row.Id == id, ct)
            ?? throw NotFound();

        var v = ValidateAndNormalise(
            request.Code, request.NameArabic, request.Name, request.SortOrder);

        if (!string.Equals(region.Code, v.Code, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await db.Regions
                .AsNoTracking()
                .AnyAsync(row => row.Id != id && row.Code == v.Code, ct);
            if (clash)
            {
                throw DuplicateCode(v.Code);
            }
        }

        region.Code = v.Code;
        region.NameArabic = v.NameAr;
        region.Name = v.NameEn;
        region.SortOrder = v.SortOrder;
        region.IsActive = request.IsActive;
        region.UpdatedAt = timeProvider.SimfNow();
        await db.SaveChangesAsync(ct);

        await auditLog.WriteSuccessAsync(
            AuditEvents.RegionUpdated,
            actorUserId,
            $"id={region.Id}; code={v.Code}; active={region.IsActive}",
            ct);

        return ToDetail(region);
    }

    public async Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken ct = default)
    {
        var region = await db.Regions
            .SingleOrDefaultAsync(row => row.Id == id, ct)
            ?? throw NotFound();

        if (!region.IsActive)
        {
            return; // idempotent
        }

        region.Deactivate();
        region.UpdatedAt = timeProvider.SimfNow();
        await db.SaveChangesAsync(ct);

        await auditLog.WriteSuccessAsync(
            AuditEvents.RegionDeactivated, actorUserId, $"id={region.Id}; code={region.Code}", ct);
    }

    private sealed record RegionDraft(string Code, string NameAr, string? NameEn, int SortOrder);

    private static RegionDraft ValidateAndNormalise(
        string codeRaw, string nameArRaw, string? nameEnRaw, int sortOrder)
    {
        var code = (codeRaw ?? string.Empty).Trim();
        if (code.Length is < 1 or > 16)
        {
            throw new ApiException(
                ErrorCodes.RegionInvalid, 400,
                "Region code must be between 1 and 16 characters.",
                "يجب أن يتراوح طول رمز المنطقة بين 1 و 16 حرفاً.");
        }

        var nameAr = (nameArRaw ?? string.Empty).Trim();
        if (nameAr.Length is < 1 or > 256)
        {
            throw new ApiException(
                ErrorCodes.RegionInvalid, 400,
                "Region Arabic name must be between 1 and 256 characters.",
                "يجب أن يتراوح طول الاسم العربي للمنطقة بين 1 و 256 حرفاً.");
        }

        // Optional field — length mirrors RegionConfiguration.HasMaxLength.
        var nameEn = NullIfBlank(nameEnRaw);
        if (nameEn is not null && nameEn.Length > 256)
        {
            throw new ApiException(
                ErrorCodes.RegionInvalid, 400,
                "Region English name must be 256 characters or fewer.",
                "يجب ألا يتجاوز الاسم الإنجليزي للمنطقة 256 حرفاً.");
        }

        return new RegionDraft(code, nameAr, nameEn, sortOrder);
    }

    private static ApiException DuplicateCode(string code) =>
        new(
            ErrorCodes.RegionInvalid, 409,
            $"A region with code '{code}' already exists.",
            $"توجد منطقة بالرمز '{code}' بالفعل.");

    private static ApiException NotFound() =>
        new(
            ErrorCodes.RegionNotFound, 404,
            "The region was not found.",
            "لم يتم العثور على المنطقة.");

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AdminRegionDetail ToDetail(Region region) => new(
        region.Id,
        region.Code,
        region.Name,
        region.NameArabic,
        region.SortOrder,
        region.IsActive,
        region.CreatedAt,
        region.UpdatedAt);
}
