// Tests: SIMF.Api.Tests/SiteSettingsPublicTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Common;
using SIMF.Contracts.Configuration;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Configuration;

/// <summary>D-461 — reads the whitelisted public <see cref="SiteSettingKeys"/>
/// from <see cref="SimfAppDbContext"/> in one query, falling back to the in-code
/// defaults. The raw key/value store stays admin-only; this projects only the
/// public branding fields.</summary>
internal sealed class SiteSettingsService(SimfAppDbContext db) : ISiteSettingsService
{
    public async Task<SiteSettingsResponse> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var keys = SiteSettingKeys.All.ToArray();
        var values = await db.SystemSettings.AsNoTracking()
            .Where(s => s.IsActive && keys.Contains(s.Key))
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

        string? Value(string key) =>
            values.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v)
                ? v.Trim()
                : null;

        return new SiteSettingsResponse(
            RegistrationSuccessMessageAr:
                Value(SiteSettingKeys.RegistrationSuccessMessageAr)
                    ?? SiteSettingKeys.DefaultRegistrationMessageAr,
            RegistrationSuccessMessageEn:
                Value(SiteSettingKeys.RegistrationSuccessMessageEn)
                    ?? SiteSettingKeys.DefaultRegistrationMessageEn,
            Social: new SiteSocialLinks(
                Facebook: Value(SiteSettingKeys.SocialFacebook),
                X: Value(SiteSettingKeys.SocialX),
                Instagram: Value(SiteSettingKeys.SocialInstagram),
                LinkedIn: Value(SiteSettingKeys.SocialLinkedIn),
                YouTube: Value(SiteSettingKeys.SocialYouTube),
                TikTok: Value(SiteSettingKeys.SocialTikTok),
                Snapchat: Value(SiteSettingKeys.SocialSnapchat)));
    }
}
