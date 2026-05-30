// Tests: SIMF.Api.Tests/AdminCountriesTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Common.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.Common;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Common;

/// <summary>D-151 — admin CRUD over <see cref="Country"/>. Id is the ISO
/// 3166-1 numeric code, manually assigned at create time (caller is
/// responsible for picking a free id; service validates uniqueness).
/// Mirrors AdminThemeService / AdminHallService structure.</summary>
internal sealed class AdminCountryService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminCountryService> logger) : IAdminCountryService
{
    public async Task<GridPage<AdminCountrySummary>> ListAllAsync(GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 50, 1, 500);

        var rows = appDbContext.Countries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(country =>
                EF.Functions.Like(country.Code, $"%{term}%")
                || EF.Functions.Like(country.NameEn, $"%{term}%")
                || EF.Functions.Like(country.NameAr, $"%{term}%"));
        }

        if (query.Filters.TryGetValue("isActive", out var activeFilter) && bool.TryParse(activeFilter, out var isActive))
        {
            rows = rows.Where(country => country.IsActive == isActive);
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("id", true) => rows.OrderByDescending(country => country.Id),
            ("id", false) => rows.OrderBy(country => country.Id),
            ("code", true) => rows.OrderByDescending(country => country.Code),
            ("code", false) => rows.OrderBy(country => country.Code),
            ("nameen", true) => rows.OrderByDescending(country => country.NameEn),
            ("nameen", false) => rows.OrderBy(country => country.NameEn),
            ("displayorder", true) => rows.OrderByDescending(country => country.DisplayOrder),
            _ => rows.OrderBy(country => country.DisplayOrder)
                     .ThenBy(country => country.NameEn),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows.Skip(skip).Take(top)
            .Select(country => new AdminCountrySummary(
                country.Id, country.Code, country.NameEn, country.NameAr,
                country.PhonePrefix, country.DisplayOrder,
                country.IsActive, country.CreatedAt))
            .ToListAsync(cancellationToken);

        return GridPage<AdminCountrySummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminCountryDetail?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        await appDbContext.Countries.AsNoTracking()
            .Where(country => country.Id == id)
            .Select(country => ToDetail(country))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AdminCountryDetail> CreateAsync(Guid actorUserId, AdminCreateCountryRequest request, CancellationToken cancellationToken = default)
    {
        var (id, code, nameEn, nameAr, phonePrefix, displayOrder) = Validate(
            request.Id, request.Code, request.NameEn, request.NameAr,
            request.PhonePrefix, request.DisplayOrder);

        var idClash = await appDbContext.Countries.AsNoTracking().AnyAsync(c => c.Id == id, cancellationToken);
        if (idClash)
        {
            throw new ApiException(ErrorCodes.CountryIdDuplicate, 409,
                $"A country with id {id} already exists.",
                $"يوجد بلد بالمعرّف {id} بالفعل.");
        }

        var codeClash = await appDbContext.Countries.AsNoTracking().AnyAsync(c => c.Code == code, cancellationToken);
        if (codeClash)
        {
            throw new ApiException(ErrorCodes.CountryCodeDuplicate, 409,
                $"A country with code '{code}' already exists.",
                $"يوجد بلد بالرمز '{code}' بالفعل.");
        }

        var now = timeProvider.GetUtcNow();
        var country = new Country
        {
            Id = id,
            Code = code,
            NameEn = nameEn,
            NameAr = nameAr,
            PhonePrefix = phonePrefix,
            DisplayOrder = displayOrder,
            IsActive = true,
            CreatedAt = now,
        };

        appDbContext.Countries.Add(country);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.CountryCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={id}; code={code}; nameEn={nameEn}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created Country {Code} (id {Id})",
            actorUserId, code, id);

        return ToDetail(country);
    }

    public async Task<AdminCountryDetail> UpdateAsync(Guid actorUserId, int id, AdminUpdateCountryRequest request, CancellationToken cancellationToken = default)
    {
        var country = await appDbContext.Countries
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.CountryNotFound, 404,
                "The country was not found.",
                "لم يتم العثور على البلد.");

        var (_, code, nameEn, nameAr, phonePrefix, displayOrder) = Validate(id, request.Code, request.NameEn, request.NameAr, request.PhonePrefix, request.DisplayOrder);

        if (!string.Equals(country.Code, code, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await appDbContext.Countries.AsNoTracking().AnyAsync(c => c.Id != id && c.Code == code, cancellationToken);
            if (clash)
            {
                throw new ApiException(ErrorCodes.CountryCodeDuplicate, 409,
                    $"A country with code '{code}' already exists.",
                    $"يوجد بلد بالرمز '{code}' بالفعل.");
            }
        }

        country.Code = code;
        country.NameEn = nameEn;
        country.NameAr = nameAr;
        country.PhonePrefix = phonePrefix;
        country.DisplayOrder = displayOrder;
        country.IsActive = request.IsActive;
        country.UpdatedAt = timeProvider.GetUtcNow();

        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.CountryUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={id}; code={code}; active={country.IsActive}",
        }, cancellationToken);

        return ToDetail(country);
    }

    public async Task DeactivateAsync(Guid actorUserId, int id, CancellationToken cancellationToken = default)
    {
        var country = await appDbContext.Countries
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.CountryNotFound, 404,
                "The country was not found.",
                "لم يتم العثور على البلد.");

        if (!country.IsActive) { return; }

        country.IsActive = false;
        country.UpdatedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.CountryDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={id}; code={country.Code}",
        }, cancellationToken);
    }

    private static (int id, string code, string nameEn, string nameAr, string? phonePrefix, int displayOrder) Validate(int idRaw, string codeRaw, string nameEnRaw, string nameArRaw, string? phonePrefixRaw, int displayOrderRaw)
    {
        if (idRaw <= 0)
        {
            throw new ApiException(ErrorCodes.CountryInvalid, 400,
                "Country id must be a positive integer (ISO 3166-1 numeric).",
                "يجب أن يكون معرّف البلد عدداً صحيحاً موجباً (ISO 3166-1).");
        }
        var code = (codeRaw ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length != 2)
        {
            throw new ApiException(ErrorCodes.CountryInvalid, 400,
                "Country code must be exactly 2 characters (ISO 3166-1 alpha-2).",
                "يجب أن يتكون رمز البلد من حرفين بالضبط (ISO 3166-1 alpha-2).");
        }
        var nameEn = (nameEnRaw ?? string.Empty).Trim();
        if (nameEn.Length is < 1 or > 128)
        {
            throw new ApiException(ErrorCodes.CountryInvalid, 400,
                "Country English name must be between 1 and 128 characters.",
                "يجب أن يتراوح الاسم الإنجليزي للبلد بين 1 و 128 حرفاً.");
        }
        var nameAr = (nameArRaw ?? string.Empty).Trim();
        if (nameAr.Length is < 1 or > 128)
        {
            throw new ApiException(ErrorCodes.CountryInvalid, 400,
                "Country Arabic name must be between 1 and 128 characters.",
                "يجب أن يتراوح الاسم العربي للبلد بين 1 و 128 حرفاً.");
        }
        var phonePrefix = string.IsNullOrWhiteSpace(phonePrefixRaw)
            ? null : phonePrefixRaw.Trim();
        if (phonePrefix is { Length: > 8 })
        {
            throw new ApiException(ErrorCodes.CountryInvalid, 400,
                "Phone prefix must be 8 characters or fewer.",
                "يجب أن يكون مقدمة الهاتف 8 أحرف أو أقل.");
        }
        if (displayOrderRaw < 0)
        {
            throw new ApiException(ErrorCodes.CountryInvalid, 400,
                "Display order must be zero or a positive integer.",
                "يجب أن يكون ترتيب العرض صفراً أو عدداً صحيحاً موجباً.");
        }
        return (idRaw, code, nameEn, nameAr, phonePrefix, displayOrderRaw);
    }

    private static AdminCountryDetail ToDetail(Country country) =>
        new(country.Id, country.Code, country.NameEn, country.NameAr,
            country.PhonePrefix, country.DisplayOrder,
            country.IsActive, country.CreatedAt, country.UpdatedAt);
}
