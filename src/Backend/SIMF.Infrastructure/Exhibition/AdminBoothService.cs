// Tests: SIMF.Api.Tests/AdminBoothsTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Exhibition.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Exhibition;
using SIMF.Domain.Exhibition;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Exhibition;

/// <summary>
/// D-199 — admin CRUD over <see cref="Booth"/> (Exhibition module, Mockup
/// page 22 + the 2D venue map). Built on <see cref="SimfAppDbContext"/>.
/// Mirrors <c>AdminSpeakerService</c>: bilingual (NameEn/NameAr), unique
/// Code (409 on duplicate), soft-delete (IsActive), audited via
/// <see cref="IAuditLog"/>. <c>HallId</c> is validated against the live
/// <c>Halls</c> table (same context) when supplied.
/// </summary>
internal sealed class AdminBoothService(
    SimfAppDbContext dbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminBoothService> logger) : IAdminBoothService
{
    public async Task<GridPage<AdminBoothSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 25, 1, 200);

        var rows = dbContext.Booths.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(booth =>
                EF.Functions.Like(booth.Code, $"%{term}%")
                || EF.Functions.Like(booth.NameEn, $"%{term}%")
                || EF.Functions.Like(booth.NameAr, $"%{term}%"));
        }
        if (query.Filters.TryGetValue("isActive", out var activeFilter)
            && bool.TryParse(activeFilter, out var isActive))
        {
            rows = rows.Where(booth => booth.IsActive == isActive);
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("code", true) => rows.OrderByDescending(booth => booth.Code),
            ("code", false) => rows.OrderBy(booth => booth.Code),
            ("name", true) => rows.OrderByDescending(booth => booth.NameEn),
            ("name", false) => rows.OrderBy(booth => booth.NameEn),
            _ => rows.OrderBy(booth => booth.Code),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip)
            .Take(top)
            .Select(booth => new AdminBoothSummary
            {
                Id = booth.Id,
                Code = booth.Code,
                NameEn = booth.NameEn,
                NameAr = booth.NameAr,
                ExhibitorNameEn = booth.ExhibitorNameEn,
                SectorEn = booth.SectorEn,
                HallId = booth.HallId,
                IsActive = booth.IsActive,
            })
            .ToListAsync(cancellationToken);

        return GridPage<AdminBoothSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminBoothDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var booth = await dbContext.Booths
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        return booth is null ? null : ToDetail(booth);
    }

    public async Task<AdminBoothDetail> CreateAsync(
        Guid actorUserId,
        AdminCreateBoothRequest request,
        CancellationToken cancellationToken = default)
    {
        var (code, nameEn, nameAr) = ValidateAndNormalise(
            request.Code, request.NameEn, request.NameAr);
        await EnsureHallIsValidAsync(request.HallId, cancellationToken);

        var clash = await dbContext.Booths
            .AsNoTracking()
            .AnyAsync(row => row.Code == code, cancellationToken);
        if (clash)
        {
            throw new ApiException(
                ErrorCodes.BoothCodeDuplicate, 409,
                $"A booth with code '{code}' already exists.",
                $"يوجد جناح بالرمز '{code}' بالفعل.");
        }

        var now = timeProvider.GetUtcNow();
        var booth = new Booth
        {
            Id = Guid.NewGuid(),
            Code = code,
            NameEn = nameEn,
            NameAr = nameAr,
            ExhibitorNameEn = NullIfBlank(request.ExhibitorNameEn),
            ExhibitorNameAr = NullIfBlank(request.ExhibitorNameAr),
            SectorEn = NullIfBlank(request.SectorEn),
            SectorAr = NullIfBlank(request.SectorAr),
            DescriptionEn = NullIfBlank(request.DescriptionEn),
            DescriptionAr = NullIfBlank(request.DescriptionAr),
            HallId = request.HallId,
            MapX = request.MapX,
            MapY = request.MapY,
            IsActive = true,
            CreatedAt = now,
        };
        dbContext.Booths.Add(booth);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BoothCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={booth.Id}; code={code}; nameEn={nameEn}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created Booth {Code} ({Id})",
            actorUserId, code, booth.Id);

        return ToDetail(booth);
    }

    public async Task<AdminBoothDetail> UpdateAsync(
        Guid actorUserId,
        Guid id,
        AdminUpdateBoothRequest request,
        CancellationToken cancellationToken = default)
    {
        var booth = await dbContext.Booths
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.BoothNotFound, 404,
                "The booth was not found.",
                "لم يتم العثور على الجناح.");

        var (code, nameEn, nameAr) = ValidateAndNormalise(
            request.Code, request.NameEn, request.NameAr);
        await EnsureHallIsValidAsync(request.HallId, cancellationToken);

        if (!string.Equals(booth.Code, code, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await dbContext.Booths
                .AsNoTracking()
                .AnyAsync(row => row.Id != id && row.Code == code, cancellationToken);
            if (clash)
            {
                throw new ApiException(
                    ErrorCodes.BoothCodeDuplicate, 409,
                    $"A booth with code '{code}' already exists.",
                    $"يوجد جناح بالرمز '{code}' بالفعل.");
            }
        }

        booth.Code = code;
        booth.NameEn = nameEn;
        booth.NameAr = nameAr;
        booth.ExhibitorNameEn = NullIfBlank(request.ExhibitorNameEn);
        booth.ExhibitorNameAr = NullIfBlank(request.ExhibitorNameAr);
        booth.SectorEn = NullIfBlank(request.SectorEn);
        booth.SectorAr = NullIfBlank(request.SectorAr);
        booth.DescriptionEn = NullIfBlank(request.DescriptionEn);
        booth.DescriptionAr = NullIfBlank(request.DescriptionAr);
        booth.HallId = request.HallId;
        booth.MapX = request.MapX;
        booth.MapY = request.MapY;
        booth.IsActive = request.IsActive;
        booth.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BoothUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={booth.Id}; code={code}; active={booth.IsActive}",
        }, cancellationToken);

        return ToDetail(booth);
    }

    public async Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var booth = await dbContext.Booths
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.BoothNotFound, 404,
                "The booth was not found.",
                "لم يتم العثور على الجناح.");

        if (!booth.IsActive)
        {
            return; // idempotent
        }

        booth.Deactivate();
        booth.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.BoothDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={booth.Id}; code={booth.Code}",
        }, cancellationToken);
    }

    private static (string code, string nameEn, string nameAr) ValidateAndNormalise(
        string codeRaw, string nameEnRaw, string nameArRaw)
    {
        var code = (codeRaw ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length is < 2 or > 16)
        {
            throw new ApiException(
                ErrorCodes.BoothInvalid, 400,
                "Booth code must be between 2 and 16 characters.",
                "يجب أن يتراوح طول رمز الجناح بين 2 و 16 حرفاً.");
        }
        var nameEn = (nameEnRaw ?? string.Empty).Trim();
        if (nameEn.Length is < 1 or > 128)
        {
            throw new ApiException(
                ErrorCodes.BoothInvalid, 400,
                "Booth English name must be between 1 and 128 characters.",
                "يجب أن يتراوح طول الاسم الإنجليزي للجناح بين 1 و 128 حرفاً.");
        }
        var nameAr = (nameArRaw ?? string.Empty).Trim();
        if (nameAr.Length is < 1 or > 128)
        {
            throw new ApiException(
                ErrorCodes.BoothInvalid, 400,
                "Booth Arabic name must be between 1 and 128 characters.",
                "يجب أن يتراوح طول الاسم العربي للجناح بين 1 و 128 حرفاً.");
        }
        return (code, nameEn, nameAr);
    }

    private async Task EnsureHallIsValidAsync(
        Guid? hallId, CancellationToken cancellationToken)
    {
        if (hallId is null) { return; }
        var exists = await dbContext.Halls
            .AsNoTracking()
            .AnyAsync(hall => hall.Id == hallId.Value && hall.IsActive, cancellationToken);
        if (!exists)
        {
            throw new ApiException(
                ErrorCodes.BoothInvalid, 400,
                $"Hall id '{hallId}' does not exist or is inactive.",
                $"رقم القاعة '{hallId}' غير موجود أو غير مفعّل.");
        }
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AdminBoothDetail ToDetail(Booth b) => new()
    {
        Id = b.Id,
        Code = b.Code,
        NameEn = b.NameEn,
        NameAr = b.NameAr,
        ExhibitorNameEn = b.ExhibitorNameEn,
        ExhibitorNameAr = b.ExhibitorNameAr,
        SectorEn = b.SectorEn,
        SectorAr = b.SectorAr,
        DescriptionEn = b.DescriptionEn,
        DescriptionAr = b.DescriptionAr,
        HallId = b.HallId,
        MapX = b.MapX,
        MapY = b.MapY,
        IsActive = b.IsActive,
    };
}
