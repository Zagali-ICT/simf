// Tests: SIMF.Api.Tests/SystemSettingsTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.Auditing;
using SIMF.Domain.Configuration;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Configuration;

/// <summary>P2.4 — D-229 (FDS-012 §5.5): admin CRUD over the platform
/// system-settings store. Built on <see cref="SimfAppDbContext"/>; mirrors
/// <c>AdminSessionCategoryService</c>. Ships empty — the team seeds the keys
/// (FDS-012 OI-2), so nothing is invented here.</summary>
internal sealed class AdminSystemSettingService(
    SimfAppDbContext db,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminSystemSettingService> logger) : IAdminSystemSettingService
{
    public async Task<GridPage<AdminSystemSettingSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 50, 1, 200);

        var rows = db.SystemSettings.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(s =>
                EF.Functions.Like(s.Key, $"%{term}%")
                || EF.Functions.Like(s.Value, $"%{term}%"));
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("key", true) => rows.OrderByDescending(s => s.Key),
            _ => rows.OrderBy(s => s.Key),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows
            .Skip(skip).Take(top)
            .Select(s => new AdminSystemSettingSummary(
                s.Id, s.Key, s.Value, s.Description, s.IsActive))
            .ToListAsync(cancellationToken);

        return GridPage<AdminSystemSettingSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminSystemSettingDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var setting = await db.SystemSettings.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        return setting is null ? null : ToDetail(setting);
    }

    public async Task<AdminSystemSettingDetail> CreateAsync(
        Guid actorUserId, AdminCreateSystemSettingRequest request,
        CancellationToken cancellationToken = default)
    {
        var key = ValidateKey(request.Key);
        var value = ValidateValue(request.Value);
        var description = NormaliseDescription(request.Description);

        var duplicate = await db.SystemSettings.AsNoTracking()
            .AnyAsync(s => s.IsActive && s.Key == key, cancellationToken);
        if (duplicate)
        {
            throw new ApiException(
                ErrorCodes.SystemSettingKeyDuplicate, 409,
                $"A setting with the key '{key}' already exists.",
                $"يوجد إعداد بالمفتاح '{key}' بالفعل.");
        }

        var now = timeProvider.GetUtcNow();
        var setting = new SystemSetting
        {
            Id = Guid.NewGuid(),
            Key = key,
            Value = value,
            Description = description,
            IsActive = true,
            CreatedAt = now,
        };
        db.SystemSettings.Add(setting);
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SystemSettingCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={setting.Id}; key={key}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created system setting {Key} ({Id})", actorUserId, key, setting.Id);

        return ToDetail(setting);
    }

    public async Task<AdminSystemSettingDetail> UpdateAsync(
        Guid actorUserId, Guid id, AdminUpdateSystemSettingRequest request,
        CancellationToken cancellationToken = default)
    {
        var setting = await db.SystemSettings
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw NotFound();

        setting.Value = ValidateValue(request.Value);
        setting.Description = NormaliseDescription(request.Description);
        setting.IsActive = request.IsActive;
        setting.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SystemSettingUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={setting.Id}; key={setting.Key}; active={setting.IsActive}",
        }, cancellationToken);

        return ToDetail(setting);
    }

    public async Task DeactivateAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var setting = await db.SystemSettings
            .SingleOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw NotFound();
        if (!setting.IsActive)
        {
            return; // idempotent
        }

        setting.Deactivate();
        setting.UpdatedAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SystemSettingDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={setting.Id}; key={setting.Key}",
        }, cancellationToken);
    }

    private static string ValidateKey(string raw)
    {
        var key = (raw ?? string.Empty).Trim();
        if (key.Length is < 1 or > 128)
        {
            throw new ApiException(
                ErrorCodes.SystemSettingInvalid, 400,
                "The setting key must be between 1 and 128 characters.",
                "يجب أن يتراوح طول مفتاح الإعداد بين 1 و 128 حرفاً.");
        }
        return key;
    }

    private static string ValidateValue(string raw)
    {
        var value = (raw ?? string.Empty).Trim();
        if (value.Length > 2048)
        {
            throw new ApiException(
                ErrorCodes.SystemSettingInvalid, 400,
                "The setting value must be 2048 characters or fewer.",
                "يجب ألا يتجاوز طول قيمة الإعداد 2048 حرفاً.");
        }
        return value;
    }

    private static string? NormaliseDescription(string? raw)
    {
        var description = raw?.Trim();
        if (string.IsNullOrEmpty(description))
        {
            return null;
        }
        return description.Length > 512 ? description[..512] : description;
    }

    private static ApiException NotFound() =>
        new(
            ErrorCodes.SystemSettingNotFound, 404,
            "The system setting was not found.",
            "لم يتم العثور على الإعداد.");

    private static AdminSystemSettingDetail ToDetail(SystemSetting s) => new(
        s.Id, s.Key, s.Value, s.Description, s.IsActive, s.CreatedAt, s.UpdatedAt);
}
