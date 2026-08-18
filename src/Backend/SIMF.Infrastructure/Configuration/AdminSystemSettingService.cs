// Tests: SIMF.Api.Tests/SystemSettingsTests.cs
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Common;
using SIMF.Common.Grids;
using SIMF.Contracts.Admin;
using SIMF.Domain.Configuration;
using SIMF.Domain.Organization;
using SIMF.Infrastructure.Common.Grids;
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
    /// <summary>
    /// The grid contract for /admin/system-settings: one entry per key
    /// ConfigurationList can send. The page's Active column is display-only, so no
    /// <c>isActive</c> key exists here either.
    /// </summary>
    private static readonly GridColumns<SystemSetting> Columns = new GridColumns<SystemSetting>()
        .Add("key", setting => setting.Key, searchable: true)
        .Add("value", setting => setting.Value, searchable: true)
        .Add("description", setting => setting.Description)
        .DefaultOrder("key")
        .PageSize(fallback: 50, max: 200);

    private static readonly Expression<Func<SystemSetting, AdminSystemSettingSummary>> ToSummary =
        setting => new AdminSystemSettingSummary(
            setting.Id, setting.Key, setting.Value, setting.Description, setting.IsActive);

    public Task<GridPage<AdminSystemSettingSummary>> ListAsync(
        GridQuery query, CancellationToken cancellationToken = default) =>
        db.SystemSettings.ToGridPageAsync(
            query, Columns, setting => setting.Id, ToSummary, cancellationToken);

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
            var trimmed = value.Trim();
            set(trimmed.Length == 0 ? null : trimmed.Length > 1024 ? trimmed[..1024] : trimmed);
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

        SetMessage(request.RegistrationMessageAr, value => profile.RegistrationSuccessMessageArabic = value);
        SetMessage(request.RegistrationMessageEn, value => profile.RegistrationSuccessMessage = value);
        SetSocial(request.Facebook, value => profile.FacebookUrl = value);
        SetSocial(request.X, value => profile.XUrl = value);
        SetSocial(request.Instagram, value => profile.InstagramUrl = value);
        SetSocial(request.LinkedIn, value => profile.LinkedInUrl = value);
        SetSocial(request.YouTube, value => profile.YouTubeUrl = value);
        SetSocial(request.TikTok, value => profile.TikTokUrl = value);
        SetSocial(request.Snapchat, value => profile.SnapchatUrl = value);
        SetBool(request.PartnerDirectoryEnabled, value => profile.PartnerDirectoryEnabled = value);

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
        var url = (value ?? string.Empty).Trim();
        if (url.Length == 0) { return null; }
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ApiException(
                ErrorCodes.SystemSettingInvalid, 400,
                "A social link must be an absolute http(s) URL.",
                "يجب أن يكون رابط التواصل رابط http(s) مطلقاً.");
        }
        return url.Length > 1024 ? url[..1024] : url;
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

    private static AdminSystemSettingDetail ToDetail(SystemSetting setting) => new(
        setting.Id, setting.Key, setting.Value, setting.Description, setting.IsActive,
        setting.CreatedAt, setting.UpdatedAt);
}
