// Tests: SIMF.Api.Tests/SiteSettingsPublicTests.cs
using Microsoft.EntityFrameworkCore;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Common;
using SIMF.Contracts;
using SIMF.Contracts.Configuration;
using SIMF.Domain.Organization;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Configuration;

/// <summary>The public site-settings read. The social links +
/// registration welcome message now live on the singleton
/// <see cref="OrganizationProfile"/> (migrated out of the old SystemSetting keys —
/// one source of truth), but this keeps returning the exact same
/// <see cref="SiteSettingsResponse"/> shape so the app, website footer and tests are
/// unchanged. URLs are sanitised to http(s)-only on read (D-467); the registration
/// message falls back to the in-code default.</summary>
internal sealed class SiteSettingsService(SimfAppDbContext db) : ISiteSettingsService
{
    public async Task<SiteSettingsResponse> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var p = await db.OrganizationProfile.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == OrganizationProfile.SingletonId, cancellationToken);

        // The session-rating prompt is gated by the CP RatingConfig "Session" type
        // toggle; expose it so the app can suppress the after-watch prompt when it
        // is off. Fail-open: enabled unless an explicit inactive "Session" row says
        // otherwise (a missing type reads as enabled, matching the payload default).
        var sessionRatingEnabled = !await db.RatingTypes
            .AnyAsync(t => t.Code == "Session" && !t.IsActive, cancellationToken);

        static string Message(string? value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        // A social URL is rendered as a link target, so only an absolute
        // http(s) URL is surfaced; anything else drops to an inert null.
        static string? SocialUrl(string? value) =>
            !string.IsNullOrWhiteSpace(value)
            && Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? value.Trim()
                : null;

        return new SiteSettingsResponse(
            RegistrationSuccessMessageAr: Message(
                p?.RegistrationSuccessMessageArabic,
                SiteSettingKeys.DefaultRegistrationMessageAr),
            RegistrationSuccessMessageEn: Message(
                p?.RegistrationSuccessMessage,
                SiteSettingKeys.DefaultRegistrationMessageEn),
            Social: new SocialLinks(
                Facebook: SocialUrl(p?.FacebookUrl),
                X: SocialUrl(p?.XUrl),
                Instagram: SocialUrl(p?.InstagramUrl),
                LinkedIn: SocialUrl(p?.LinkedInUrl),
                YouTube: SocialUrl(p?.YouTubeUrl),
                TikTok: SocialUrl(p?.TikTokUrl),
                Snapchat: SocialUrl(p?.SnapchatUrl)),
            // Build #13 — the "Meet People Like You" partner-directory switch;
            // fail-open (true) when the singleton row is somehow absent.
            PartnerDirectoryEnabled: p?.PartnerDirectoryEnabled ?? true,
            // 2026-07-22 — the CP RatingConfig "Session" rating-type toggle.
            SessionRatingEnabled: sessionRatingEnabled);
    }
}
