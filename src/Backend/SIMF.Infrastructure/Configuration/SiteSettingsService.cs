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

        // D-467 (security review) — a social URL is rendered as a link target
        // (the website footer sets `a.href`, the app launches it externally), so
        // only an absolute http(s) URL is surfaced. Anything else (a
        // `javascript:` / `data:` / arbitrary-scheme value that could have been
        // stored via the generic /admin/configuration page, bypassing the
        // dedicated page's validation) is dropped to null → an inert link.
        // Sanitising on read protects every consumer regardless of write path.
        string? SocialUrl(string key)
        {
            var v = Value(key);
            return v is not null
                && Uri.TryCreate(v, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? v
                : null;
        }

        return new SiteSettingsResponse(
            RegistrationSuccessMessageAr:
                Value(SiteSettingKeys.RegistrationSuccessMessageAr)
                    ?? SiteSettingKeys.DefaultRegistrationMessageAr,
            RegistrationSuccessMessageEn:
                Value(SiteSettingKeys.RegistrationSuccessMessageEn)
                    ?? SiteSettingKeys.DefaultRegistrationMessageEn,
            Social: new SiteSocialLinks(
                Facebook: SocialUrl(SiteSettingKeys.SocialFacebook),
                X: SocialUrl(SiteSettingKeys.SocialX),
                Instagram: SocialUrl(SiteSettingKeys.SocialInstagram),
                LinkedIn: SocialUrl(SiteSettingKeys.SocialLinkedIn),
                YouTube: SocialUrl(SiteSettingKeys.SocialYouTube),
                TikTok: SocialUrl(SiteSettingKeys.SocialTikTok),
                Snapchat: SocialUrl(SiteSettingKeys.SocialSnapchat)));
    }
}
