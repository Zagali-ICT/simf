// Tests: SIMF.Api.Tests/OrganizationProfileTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Configuration.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Organization;
using SIMF.Domain.Auditing;
using SIMF.Domain.Organization;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Configuration;

/// <summary>D-495 — the admin write-path for the singleton Organization Profile.
/// A single full-document upsert: updates the scalar branding fields and reconciles
/// the about-items + details lists by id. Every save touches <c>UpdatedAt</c> (so the
/// public read's <c>Last-Modified</c> token advances), invalidates the read cache, and
/// audits the actor. URLs / status / coordinates are validated here; lengths are
/// clamped to the EF column sizes.</summary>
internal sealed class OrganizationProfileAdminService(
    SimfAppDbContext db,
    IOrganizationProfileReadService readCache,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<OrganizationProfileAdminService> logger) : IOrganizationProfileAdminService
{
    public async Task<OrganizationProfileResponse> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var profile = await db.OrganizationProfile.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == OrganizationProfile.SingletonId, cancellationToken)
            ?? new OrganizationProfile();

        var about = await db.OrganizationAboutItems.AsNoTracking()
            .Where(a => a.OrganizationProfileId == OrganizationProfile.SingletonId && a.IsActive)
            .OrderBy(a => a.DisplayOrder)
            .ToListAsync(cancellationToken);

        var details = await db.OrganizationDetails.AsNoTracking()
            .Where(d => d.OrganizationProfileId == OrganizationProfile.SingletonId && d.IsActive)
            .OrderBy(d => d.DisplayOrder)
            .ToListAsync(cancellationToken);

        var hasLogo = await db.Assets.AsNoTracking().AnyAsync(
            a => a.IsActive
                && a.Category == AssetCategory.OrganizationLogo
                && a.OwnerId == OrganizationProfile.SingletonId, cancellationToken);

        var logoUrl = hasLogo
            ? $"app/assets/{AssetCategory.OrganizationLogo}/{OrganizationProfile.SingletonId}/image"
            : null;

        return OrganizationProfileMapper.ToResponse(profile, about, details, logoUrl);
    }

    public async Task UpdateAsync(
        AdminUpdateOrganizationProfileRequest request, Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var profile = await db.OrganizationProfile
            .SingleOrDefaultAsync(p => p.Id == OrganizationProfile.SingletonId, cancellationToken);
        if (profile is null)
        {
            profile = new OrganizationProfile();
            db.OrganizationProfile.Add(profile);
        }

        var now = timeProvider.GetUtcNow();

        profile.Name = RequireText(request.Name, "name", 256);
        profile.NameArabic = RequireText(request.NameArabic, "Arabic name", 256);
        profile.Title = RequireText(request.Title, "title", 256);
        profile.TitleArabic = RequireText(request.TitleArabic, "Arabic title", 256);
        profile.Slogan = Trim(request.Slogan, 512);
        profile.SloganArabic = Trim(request.SloganArabic, 512);
        profile.Bio = Trim(request.Bio, 4000);
        profile.BioArabic = Trim(request.BioArabic, 4000);
        profile.Version = Trim(request.Version, 64);
        profile.VersionDate = request.VersionDate;
        profile.SysVersion = Trim(request.SysVersion, 64);
        profile.ReleaseDate = request.ReleaseDate;
        profile.EventStartDate = request.EventStartDate;
        profile.EventEndDate = request.EventEndDate;
        profile.CurrentYear = request.CurrentYear;
        profile.Status = ParseStatus(request.Status);
        profile.LocationText = Trim(request.LocationText, 512);
        profile.LocationTextArabic = Trim(request.LocationTextArabic, 512);
        profile.Latitude = ValidateCoordinate(request.Latitude, 90m, "latitude");
        profile.Longitude = ValidateCoordinate(request.Longitude, 180m, "longitude");
        profile.ContactPhone = Trim(request.ContactPhone, 64);
        profile.ContactEmail = Trim(request.ContactEmail, 256);
        profile.ContactWebsite = ValidateUrl(request.ContactWebsite, "website");
        profile.LiveStreamUrl = ValidateUrl(request.LiveStreamUrl, "live-stream link");
        profile.FacebookUrl = ValidateUrl(request.Facebook, "Facebook link");
        profile.XUrl = ValidateUrl(request.X, "X link");
        profile.InstagramUrl = ValidateUrl(request.Instagram, "Instagram link");
        profile.LinkedInUrl = ValidateUrl(request.LinkedIn, "LinkedIn link");
        profile.YouTubeUrl = ValidateUrl(request.YouTube, "YouTube link");
        profile.TikTokUrl = ValidateUrl(request.TikTok, "TikTok link");
        profile.SnapchatUrl = ValidateUrl(request.Snapchat, "Snapchat link");
        profile.UpdatedAt = now;
        profile.UpdatedBy = actorUserId;

        await ReconcileAboutAsync(request.AboutItems, now, cancellationToken);
        await ReconcileDetailsAsync(request.Details, now, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        readCache.Invalidate();

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.OrganizationProfileUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"organization profile saved; about={request.AboutItems.Count}; details={request.Details.Count}",
        }, cancellationToken);

        logger.LogInformation("Admin {ActorId} saved the organization profile", actorUserId);
    }

    private async Task ReconcileAboutAsync(
        List<AdminOrganizationAboutItem> items, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await db.OrganizationAboutItems
            .Where(a => a.OrganizationProfileId == OrganizationProfile.SingletonId && a.IsActive)
            .ToListAsync(ct);
        var kept = new HashSet<Guid>();

        foreach (var item in items)
        {
            // A null match is either a new item (no Id) or an Id that no longer maps
            // to an active row (e.g. one concurrently deactivated) — both insert a
            // fresh row; a stale Id is never resurrected in place.
            var row = item.Id is { } id ? existing.FirstOrDefault(a => a.Id == id) : null;
            if (row is null)
            {
                row = new OrganizationAboutItem
                {
                    Id = Guid.NewGuid(),
                    OrganizationProfileId = OrganizationProfile.SingletonId,
                    CreatedAt = now,
                };
                db.OrganizationAboutItems.Add(row);
            }
            else
            {
                row.UpdatedAt = now;
                kept.Add(row.Id);
            }

            row.Title = RequireText(item.Title, "about title", 256);
            row.TitleArabic = RequireText(item.TitleArabic, "about Arabic title", 256);
            row.Text = RequireText(item.Text, "about text", 4000);
            row.TextArabic = RequireText(item.TextArabic, "about Arabic text", 4000);
            row.DisplayOrder = item.DisplayOrder;
        }

        foreach (var row in existing.Where(a => !kept.Contains(a.Id)))
        {
            row.Deactivate();
            row.UpdatedAt = now;
        }
    }

    private async Task ReconcileDetailsAsync(
        List<AdminOrganizationDetail> items, DateTimeOffset now, CancellationToken ct)
    {
        var existing = await db.OrganizationDetails
            .Where(d => d.OrganizationProfileId == OrganizationProfile.SingletonId && d.IsActive)
            .ToListAsync(ct);
        var kept = new HashSet<Guid>();

        foreach (var item in items)
        {
            var row = item.Id is { } id ? existing.FirstOrDefault(d => d.Id == id) : null;
            if (row is null)
            {
                row = new OrganizationDetail
                {
                    Id = Guid.NewGuid(),
                    OrganizationProfileId = OrganizationProfile.SingletonId,
                    CreatedAt = now,
                };
                db.OrganizationDetails.Add(row);
            }
            else
            {
                row.UpdatedAt = now;
                kept.Add(row.Id);
            }

            row.Name = RequireText(item.Name, "detail name", 256);
            row.NameArabic = RequireText(item.NameArabic, "detail Arabic name", 256);
            row.Value = RequireText(item.Value, "detail value", 1024);
            row.DisplayOrder = item.DisplayOrder;
        }

        foreach (var row in existing.Where(d => !kept.Contains(d.Id)))
        {
            row.Deactivate();
            row.UpdatedAt = now;
        }
    }

    private static string RequireText(string? raw, string field, int max)
    {
        var v = (raw ?? string.Empty).Trim();
        if (v.Length == 0)
        {
            throw Invalid($"The {field} is required.", $"حقل {field} مطلوب.");
        }
        return v.Length > max ? v[..max] : v;
    }

    private static string? Trim(string? raw, int max)
    {
        var v = (raw ?? string.Empty).Trim();
        if (v.Length == 0) { return null; }
        return v.Length > max ? v[..max] : v;
    }

    private static string? ValidateUrl(string? raw, string field)
    {
        var v = (raw ?? string.Empty).Trim();
        if (v.Length == 0) { return null; }
        if (!Uri.TryCreate(v, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw Invalid(
                $"The {field} must be an absolute http(s) URL.",
                $"يجب أن يكون {field} رابط http(s) مطلقاً.");
        }
        return v.Length > 1024 ? v[..1024] : v;
    }

    private static decimal? ValidateCoordinate(decimal? value, decimal bound, string field)
    {
        if (value is null) { return null; }
        if (value < -bound || value > bound)
        {
            throw Invalid($"The {field} is out of range.", $"قيمة {field} خارج النطاق.");
        }
        return value;
    }

    private static ForumStatus ParseStatus(string? raw)
    {
        if (Enum.TryParse<ForumStatus>(raw, ignoreCase: true, out var status)
            && Enum.IsDefined(status))
        {
            return status;
        }
        throw Invalid("The status is invalid.", "الحالة غير صحيحة.");
    }

    private static ApiException Invalid(string en, string ar) =>
        new(ErrorCodes.OrganizationProfileInvalid, 400, en, ar);
}
