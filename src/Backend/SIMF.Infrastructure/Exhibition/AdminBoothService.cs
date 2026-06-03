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

        // CP grid per-column filters (D-255). Unknown columns are ignored.
        // The Company + Hall columns are resolved client-side from cached
        // lookups (the summary carries only the ids), so they are NOT
        // server-filterable and are intentionally absent here.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "code":
                    rows = rows.Where(booth => booth.Code.Contains(v));
                    break;
                case "nameen":
                    rows = rows.Where(booth => booth.NameEn.Contains(v));
                    break;
                case "namear":
                    rows = rows.Where(booth => booth.NameAr.Contains(v));
                    break;
                case "sectoren":
                    rows = rows.Where(booth => booth.SectorEn != null && booth.SectorEn.Contains(v));
                    break;
            }
        }

        // CP grid sortable columns (D-255). Default: Code. The Company + Hall
        // columns sort on a client-resolved value, so they are not server-
        // sortable and are absent here.
        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("code", true) => rows.OrderByDescending(booth => booth.Code),
            ("code", false) => rows.OrderBy(booth => booth.Code),
            ("nameen", true) => rows.OrderByDescending(booth => booth.NameEn),
            ("nameen", false) => rows.OrderBy(booth => booth.NameEn),
            ("sectoren", true) => rows.OrderByDescending(booth => booth.SectorEn),
            ("sectoren", false) => rows.OrderBy(booth => booth.SectorEn),
            ("isactive", true) => rows.OrderByDescending(booth => booth.IsActive),
            ("isactive", false) => rows.OrderBy(booth => booth.IsActive),
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
                CompanyId = booth.CompanyId,
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
        var v = ValidateAndNormalise(
            request.Code, request.NameEn, request.NameAr,
            request.OfficerName, request.OfficerPhone, request.OfficerEmail,
            request.SectorEn, request.SectorAr,
            request.DescriptionEn, request.DescriptionAr);
        await EnsureHallIsValidAsync(request.HallId, cancellationToken);
        await EnsureCompanyIsValidAsync(request.CompanyId, cancellationToken);

        var clash = await dbContext.Booths
            .AsNoTracking()
            .AnyAsync(row => row.Code == v.Code, cancellationToken);
        if (clash)
        {
            throw new ApiException(
                ErrorCodes.BoothCodeDuplicate, 409,
                $"A booth with code '{v.Code}' already exists.",
                $"يوجد جناح بالرمز '{v.Code}' بالفعل.");
        }

        var now = timeProvider.GetUtcNow();
        var booth = new Booth
        {
            Id = Guid.NewGuid(),
            Code = v.Code,
            NameEn = v.NameEn,
            NameAr = v.NameAr,
            CompanyId = request.CompanyId,
            OfficerName = v.OfficerName,
            OfficerPhone = v.OfficerPhone,
            OfficerEmail = v.OfficerEmail,
            SectorEn = v.SectorEn,
            SectorAr = v.SectorAr,
            DescriptionEn = v.DescriptionEn,
            DescriptionAr = v.DescriptionAr,
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
            Detail = $"id={booth.Id}; code={v.Code}; nameEn={v.NameEn}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created Booth {Code} ({Id})",
            actorUserId, v.Code, booth.Id);

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

        var v = ValidateAndNormalise(
            request.Code, request.NameEn, request.NameAr,
            request.OfficerName, request.OfficerPhone, request.OfficerEmail,
            request.SectorEn, request.SectorAr,
            request.DescriptionEn, request.DescriptionAr);
        await EnsureHallIsValidAsync(request.HallId, cancellationToken);
        await EnsureCompanyIsValidAsync(request.CompanyId, cancellationToken);

        if (!string.Equals(booth.Code, v.Code, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await dbContext.Booths
                .AsNoTracking()
                .AnyAsync(row => row.Id != id && row.Code == v.Code, cancellationToken);
            if (clash)
            {
                throw new ApiException(
                    ErrorCodes.BoothCodeDuplicate, 409,
                    $"A booth with code '{v.Code}' already exists.",
                    $"يوجد جناح بالرمز '{v.Code}' بالفعل.");
            }
        }

        booth.Code = v.Code;
        booth.NameEn = v.NameEn;
        booth.NameAr = v.NameAr;
        booth.CompanyId = request.CompanyId;
        booth.OfficerName = v.OfficerName;
        booth.OfficerPhone = v.OfficerPhone;
        booth.OfficerEmail = v.OfficerEmail;
        booth.SectorEn = v.SectorEn;
        booth.SectorAr = v.SectorAr;
        booth.DescriptionEn = v.DescriptionEn;
        booth.DescriptionAr = v.DescriptionAr;
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
            Detail = $"id={booth.Id}; code={v.Code}; active={booth.IsActive}",
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

    private sealed record BoothDraft(
        string Code, string NameEn, string NameAr,
        string? OfficerName, string? OfficerPhone, string? OfficerEmail,
        string? SectorEn, string? SectorAr,
        string? DescriptionEn, string? DescriptionAr);

    private static BoothDraft ValidateAndNormalise(
        string codeRaw, string nameEnRaw, string nameArRaw,
        string? officerNameRaw, string? officerPhoneRaw, string? officerEmailRaw,
        string? sectorEnRaw, string? sectorArRaw,
        string? descriptionEnRaw, string? descriptionArRaw)
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

        // Optional fields — lengths mirror BoothConfiguration.HasMaxLength
        // (Officer name = 256 / phone = 32 / email = 320, Sector* = 128,
        // Description* = 2048). B1 — D-222: booth-officer contact.
        var officerName = OptionalText(
            officerNameRaw, 256, "Booth officer name", "اسم مسؤول الجناح");
        var officerPhone = OptionalText(
            officerPhoneRaw, 32, "Booth officer phone", "هاتف مسؤول الجناح");
        var officerEmail = OptionalText(
            officerEmailRaw, 320, "Booth officer email", "بريد مسؤول الجناح");
        if (officerEmail is not null && !officerEmail.Contains('@'))
        {
            throw new ApiException(
                ErrorCodes.BoothInvalid, 400,
                "Booth officer email is not a valid email address.",
                "بريد مسؤول الجناح غير صالح.");
        }
        var sectorEn = OptionalText(
            sectorEnRaw, 128, "Booth English sector", "قطاع الجناح الإنجليزي");
        var sectorAr = OptionalText(
            sectorArRaw, 128, "Booth Arabic sector", "قطاع الجناح العربي");
        var descriptionEn = OptionalText(
            descriptionEnRaw, 2048, "Booth English description", "وصف الجناح الإنجليزي");
        var descriptionAr = OptionalText(
            descriptionArRaw, 2048, "Booth Arabic description", "وصف الجناح العربي");

        return new BoothDraft(
            code, nameEn, nameAr,
            officerName, officerPhone, officerEmail,
            sectorEn, sectorAr,
            descriptionEn, descriptionAr);
    }

    private static string? OptionalText(string? raw, int maxLength, string fieldEn, string fieldAr)
    {
        var value = NullIfBlank(raw);
        if (value is not null && value.Length > maxLength)
        {
            throw new ApiException(
                ErrorCodes.BoothInvalid, 400,
                $"{fieldEn} must be {maxLength} characters or fewer.",
                $"يجب ألا يتجاوز {fieldAr} {maxLength} حرفاً.");
        }
        return value;
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

    // B1 — D-222: the exhibitor must be an active company of kind Exhibitor.
    // Mirrors EnsureHallIsValidAsync. Sponsor / inactive companies are
    // rejected so a booth never points at a non-exhibitor row.
    private async Task EnsureCompanyIsValidAsync(
        Guid? companyId, CancellationToken cancellationToken)
    {
        if (companyId is null) { return; }
        var exists = await dbContext.Companies
            .AsNoTracking()
            .AnyAsync(company => company.Id == companyId.Value
                && company.IsActive
                && company.Type == CompanyType.Exhibitor, cancellationToken);
        if (!exists)
        {
            throw new ApiException(
                ErrorCodes.BoothInvalid, 400,
                $"Company id '{companyId}' is not an active exhibitor company.",
                $"معرّف الشركة '{companyId}' ليس شركة عارضة مفعّلة.");
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
        CompanyId = b.CompanyId,
        OfficerName = b.OfficerName,
        OfficerPhone = b.OfficerPhone,
        OfficerEmail = b.OfficerEmail,
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
