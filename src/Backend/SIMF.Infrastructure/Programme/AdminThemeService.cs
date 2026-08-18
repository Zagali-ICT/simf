// Tests: SIMF.Api.Tests/AdminThemeUpdateTests.cs,
//        SIMF.Api.Tests/ThemesExcelTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Grids;
using SIMF.Contracts.Admin;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>
/// Admin CRUD over <see cref="Theme"/>.
/// Built on the <see cref="SimfAppDbContext"/> — `Themes` was the first
/// app-side table. Every mutation audits one row via the existing
/// <see cref="IAuditLog"/>.
/// </summary>
internal sealed class AdminThemeService(
    SimfAppDbContext dbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminThemeService> logger) : IAdminThemeService
{
    /// <summary>
    /// The grid contract for /admin/themes: one entry per key ThemesList.razor can
    /// send, as both its filter and its sort. A key not declared here is a 400, not
    /// a silently ignored request.
    /// </summary>
    private static readonly GridColumns<Theme> Columns = new GridColumns<Theme>()
        .Add("code", theme => theme.Code, searchable: true)
        .Add("name", theme => theme.Name, searchable: true)
        .Add("nameArabic", theme => theme.NameArabic, searchable: true)
        .Add("displayOrder", theme => theme.DisplayOrder)
        .Add("isActive", theme => theme.IsActive)
        .DefaultOrder("displayOrder")
        .DefaultOrder("name")
        .PageSize(fallback: 25, max: 200);

    private static readonly Expression<Func<Theme, AdminThemeSummary>> ToSummary =
        theme => new AdminThemeSummary(
            theme.Id, theme.Code, theme.Name, theme.NameArabic,
            theme.DisplayOrder, theme.PageColor, theme.IsActive,
            theme.CreatedAt,
            // Round-trip the bilingual descriptions through export.
            theme.Description, theme.DescriptionArabic);

    public Task<GridPage<AdminThemeSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        dbContext.Themes.ToGridPageAsync(
            query, Columns, theme => theme.Id, ToSummary, cancellationToken);

    public async Task<AdminThemeDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Themes
            .AsNoTracking()
            .Where(theme => theme.Id == id)
            .Select(theme => ToDetail(theme))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AdminThemeDetail> CreateAsync(
        Guid actorUserId,
        AdminCreateThemeRequest request,
        CancellationToken cancellationToken = default)
    {
        var (code, name, nameArabic, pageColor) = ValidateAndNormalise(request.Code,
            request.Name, request.NameArabic, request.PageColor);
        if (request.DisplayOrder < 0)
        {
            throw new ApiException(
                ErrorCodes.ThemeInvalid, 400,
                "Display order must be zero or a positive integer.",
                "يجب أن يكون ترتيب العرض صفراً أو عدداً صحيحاً موجباً.");
        }

        var clash = await dbContext.Themes
            .AsNoTracking()
            .AnyAsync(theme => theme.Code == code, cancellationToken);
        if (clash)
        {
            throw new ApiException(
                ErrorCodes.ThemeCodeDuplicate, 409,
                $"A theme with code '{code}' already exists.",
                $"يوجد محور بالرمز '{code}' بالفعل.");
        }

        var now = timeProvider.SimfNow();
        var theme = new Theme
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            NameArabic = nameArabic,
            Description = NullIfBlank(request.Description),
            DescriptionArabic = NullIfBlank(request.DescriptionArabic),
            DisplayOrder = request.DisplayOrder,
            PageColor = pageColor,
            IsActive = true,
            CreatedAt = now,
        };
        dbContext.Themes.Add(theme);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.ThemeCreated,
            actorUserId,
            $"id={theme.Id}; code={code}; name={name}",
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created Theme {Code} ({Id})",
            actorUserId, code, theme.Id);

        return ToDetail(theme);
    }

    public async Task<AdminThemeDetail> UpdateAsync(
        Guid actorUserId,
        Guid id,
        AdminUpdateThemeRequest request,
        CancellationToken cancellationToken = default)
    {
        var theme = await dbContext.Themes
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.ThemeNotFound, 404,
                "The theme was not found.",
                "لم يتم العثور على المحور.");

        var (code, name, nameArabic, pageColor) = ValidateAndNormalise(request.Code,
            request.Name, request.NameArabic, request.PageColor);
        if (request.DisplayOrder < 0)
        {
            throw new ApiException(
                ErrorCodes.ThemeInvalid, 400,
                "Display order must be zero or a positive integer.",
                "يجب أن يكون ترتيب العرض صفراً أو عدداً صحيحاً موجباً.");
        }

        if (!string.Equals(theme.Code, code, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await dbContext.Themes
                .AsNoTracking()
                .AnyAsync(row => row.Id != id && row.Code == code, cancellationToken);
            if (clash)
            {
                throw new ApiException(
                    ErrorCodes.ThemeCodeDuplicate, 409,
                    $"A theme with code '{code}' already exists.",
                    $"يوجد محور بالرمز '{code}' بالفعل.");
            }
        }

        theme.Code = code;
        theme.Name = name;
        theme.NameArabic = nameArabic;
        theme.Description = NullIfBlank(request.Description);
        theme.DescriptionArabic = NullIfBlank(request.DescriptionArabic);
        theme.DisplayOrder = request.DisplayOrder;
        theme.PageColor = pageColor;
        theme.IsActive = request.IsActive;
        theme.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.ThemeUpdated,
            actorUserId,
            $"id={theme.Id}; code={code}; active={theme.IsActive}",
            cancellationToken);

        return ToDetail(theme);
    }

    public async Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var theme = await dbContext.Themes
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.ThemeNotFound, 404,
                "The theme was not found.",
                "لم يتم العثور على المحور.");

        if (!theme.IsActive)
        {
            return; // idempotent
        }

        theme.IsActive = false;
        theme.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.ThemeDeactivated,
            actorUserId,
            $"id={theme.Id}; code={theme.Code}",
            cancellationToken);
    }

    private static (string code, string name, string nameArabic, string pageColor)
        ValidateAndNormalise(string codeRaw, string nameRaw, string nameArabicRaw, string pageColorRaw)
    {
        var code = (codeRaw ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length is < 2 or > 16)
        {
            throw new ApiException(
                ErrorCodes.ThemeInvalid, 400,
                "Theme code must be between 2 and 16 characters.",
                "يجب أن يتراوح طول رمز المحور بين 2 و 16 حرفاً.");
        }
        var name = (nameRaw ?? string.Empty).Trim();
        if (name.Length is < 1 or > 128)
        {
            throw new ApiException(
                ErrorCodes.ThemeInvalid, 400,
                "Theme English name must be between 1 and 128 characters.",
                "يجب أن يتراوح طول الاسم الإنجليزي للمحور بين 1 و 128 حرفاً.");
        }
        var nameArabic = (nameArabicRaw ?? string.Empty).Trim();
        if (nameArabic.Length is < 1 or > 128)
        {
            throw new ApiException(
                ErrorCodes.ThemeInvalid, 400,
                "Theme Arabic name must be between 1 and 128 characters.",
                "يجب أن يتراوح طول الاسم العربي للمحور بين 1 و 128 حرفاً.");
        }
        var pageColor = (pageColorRaw ?? string.Empty).Trim();
        if (pageColor.Length is < 1 or > 32)
        {
            throw new ApiException(
                ErrorCodes.ThemeInvalid, 400,
                "Theme page color must be between 1 and 32 characters.",
                "يجب أن يتراوح طول لون الصفحة بين 1 و 32 حرفاً.");
        }
        return (code, name, nameArabic, pageColor);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AdminThemeDetail ToDetail(Theme theme) =>
        new(theme.Id, theme.Code, theme.Name, theme.NameArabic,
            theme.Description, theme.DescriptionArabic,
            theme.DisplayOrder, theme.PageColor, theme.IsActive,
            theme.CreatedAt, theme.UpdatedAt);
}
