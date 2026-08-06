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
using SIMF.Domain.Organization;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Configuration;

/// <summary>Admin CRUD over the platform
/// system-settings store. Built on <see cref="SimfAppDbContext"/>; mirrors
/// <c>AdminSessionCategoryService</c>. Ships empty — the team seeds the keys,
/// so nothing is invented here.</summary>
internal sealed class AdminSystemSettingService(
    SimfAppDbContext db,
    IOrganizationProfileReadService organizationProfileCache,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminSystemSettingService> logger) : IAdminSystemSettingService
{
    public async Task<GridPage<AdminSystemSettingSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(50, 200);

        var rows = db.SystemSettings.AsNoTracking().AsQueryable();

        // CP grid per-column filters. Unknown columns are ignored.
        foreach (var (column, raw) in query.Filters)
        {
            if (string.IsNullOrWhiteSpace(raw)) { continue; }
            var v = raw.Trim();
            switch (column.ToLowerInvariant())
            {
                case "key":
                    rows = rows.Where(s => s.Key.Contains(v));
                    break;
                case "value":
                    rows = rows.Where(s => s.Value.Contains(v));
                    break;
                case "description":
                    rows = rows.Where(s => s.Description != null && s.Description.Contains(v));
                    break;
            }
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(s =>
                EF.Functions.Like(s.Key, $"%{term}%")
                || EF.Functions.Like(s.Value, $"%{term}%"));
        }

        // CP grid sortable columns. Default: Key ascending.
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
            skip, top);
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

        var now = timeProvider.SimfNow();
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

        await auditLog.WriteSuccessAsync(
            AuditEvents.SystemSettingCreated,
            actorUserId,
            $"id={setting.Id}; key={key}",
            cancellationToken);

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
        setting.UpdatedAt = timeProvider.SimfNow();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SystemSettingUpdated,
            actorUserId,
            $"id={setting.Id}; key={setting.Key}; active={setting.IsActive}",
            cancellationToken);

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
        setting.UpdatedAt = timeProvider.SimfNow();
        await db.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.SystemSettingDeactivated,
            actorUserId,
            $"id={setting.Id}; key={setting.Key}",
            cancellationToken);
    }

    public async Task SaveSiteSettingsAsync(
        Guid actorUserId, AdminUpdateSiteSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        // The social links + welcome message live on the singleton
        // OrganizationProfile (one source of truth). null = leave the field
        // unchanged; a provided value (including an empty string) is applied — an
        // empty string clears it. Social links must be absolute http(s) URLs.
        // The Site Settings page now sends only the registration message
        // (social is edited on the Organization Profile page), so its unsent social
        // fields stay null → untouched here; partial updates are supported + tested.
        var profile = await db.OrganizationProfile
            .SingleOrDefaultAsync(p => p.Id == OrganizationProfile.SingletonId, cancellationToken);
        if (profile is null)
        {
            profile = new OrganizationProfile();
            db.OrganizationProfile.Add(profile);
        }

        var changed = false;
        void SetMessage(string? value, Action<string?> set)
        {
            if (value is null) { return; }
            var v = value.Trim();
            set(v.Length == 0 ? null : v.Length > 1024 ? v[..1024] : v);
            changed = true;
        }
        void SetSocial(string? value, Action<string?> set)
        {
            if (value is null) { return; }
            set(CleanSocialUrl(value));
            changed = true;
        }
        // A nullable bool toggle follows the same partial-update rule:
        // null = leave unchanged, a provided value is applied.
        void SetBool(bool? value, Action<bool> set)
        {
            if (value is null) { return; }
            set(value.Value);
            changed = true;
        }

        SetMessage(request.RegistrationMessageAr, v => profile.RegistrationSuccessMessageArabic = v);
        SetMessage(request.RegistrationMessageEn, v => profile.RegistrationSuccessMessage = v);
        SetSocial(request.Facebook, v => profile.FacebookUrl = v);
        SetSocial(request.X, v => profile.XUrl = v);
        SetSocial(request.Instagram, v => profile.InstagramUrl = v);
        SetSocial(request.LinkedIn, v => profile.LinkedInUrl = v);
        SetSocial(request.YouTube, v => profile.YouTubeUrl = v);
        SetSocial(request.TikTok, v => profile.TikTokUrl = v);
        SetSocial(request.Snapchat, v => profile.SnapchatUrl = v);
        SetBool(request.PartnerDirectoryEnabled, v => profile.PartnerDirectoryEnabled = v);

        if (!changed) { return; }

        profile.UpdatedAt = timeProvider.SimfNow();
        profile.UpdatedBy = actorUserId;
        await db.SaveChangesAsync(cancellationToken);
        organizationProfileCache.Invalidate();

        await auditLog.WriteSuccessAsync(
            AuditEvents.OrganizationProfileUpdated,
            actorUserId,
            "site-settings saved (registration message + social links)",
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} saved site settings via the organization profile", actorUserId);
    }

    // A social link must be an absolute http(s) URL (rendered as a link
    // target on the app + website). A blank value clears the link.
    private static string? CleanSocialUrl(string? value)
    {
        var v = (value ?? string.Empty).Trim();
        if (v.Length == 0) { return null; }
        if (!Uri.TryCreate(v, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ApiException(
                ErrorCodes.SystemSettingInvalid, 400,
                "A social link must be an absolute http(s) URL.",
                "يجب أن يكون رابط التواصل رابط http(s) مطلقاً.");
        }
        return v.Length > 1024 ? v[..1024] : v;
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
