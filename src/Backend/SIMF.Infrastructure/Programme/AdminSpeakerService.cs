// Tests: SIMF.Api.Tests/AdminSpeakersTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Application.Assets.Abstractions;
using SIMF.Contracts.Admin;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>
/// D-153 — admin CRUD over <see cref="Speaker"/>. Built on
/// <see cref="SimfAppDbContext"/>. <c>CountryId</c> is validated against
/// the live <c>Country</c> table (same context), and so is
/// <c>UserProfileId</c>. NOTE: this comment previously said UserProfileId was
/// cross-context and deliberately unchecked because "a stale FK degrades
/// gracefully to no linked account". That has not been true since
/// <c>UserProfile</c> moved onto <see cref="SimfAppDbContext"/> — it is a real
/// same-database FK with <c>OnDelete.Restrict</c>, so an unknown id threw at
/// SaveChanges and surfaced as a 500. Both ids are now validated up front.
/// </summary>
internal sealed class AdminSpeakerService(
    SimfAppDbContext dbContext,
    IAssetService assetService,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminSpeakerService> logger) : IAdminSpeakerService
{
    public async Task<GridPage<AdminSpeakerSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(25, 200);

        var rows = dbContext.Speakers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(speaker =>
                EF.Functions.Like(speaker.Code, $"%{term}%")
                || EF.Functions.Like(speaker.Name, $"%{term}%")
                || EF.Functions.Like(speaker.NameArabic, $"%{term}%"));
        }
        if (query.Filters.TryGetValue("isActive", out var activeFilter)
            && bool.TryParse(activeFilter, out var isActive))
        {
            rows = rows.Where(speaker => speaker.IsActive == isActive);
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("code", true) => rows.OrderByDescending(speaker => speaker.Code),
            ("code", false) => rows.OrderBy(speaker => speaker.Code),
            ("name", true) => rows.OrderByDescending(speaker => speaker.Name),
            ("name", false) => rows.OrderBy(speaker => speaker.Name),
            ("displayorder", true) => rows.OrderByDescending(speaker => speaker.DisplayOrder),
            _ => rows.OrderBy(speaker => speaker.DisplayOrder)
                     .ThenBy(speaker => speaker.Name),
        };

        var total = await rows.CountAsync(cancellationToken);
        var pageRaw = await rows
            .Skip(skip)
            .Take(top)
            .Select(speaker => new
            {
                speaker.Id,
                speaker.Code,
                speaker.Name,
                speaker.NameArabic,
                speaker.Rank, speaker.RankArabic,
                speaker.CountryId,
                speaker.DisplayOrder,
                speaker.IsActive,
                speaker.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var countryIds = pageRaw
            .Where(row => row.CountryId.HasValue)
            .Select(row => row.CountryId!.Value)
            .Distinct()
            .ToList();
        var countriesById = await dbContext.Countries
            .AsNoTracking()
            .Where(country => countryIds.Contains(country.Id))
            .Select(country => new { country.Id, country.Name, country.NameArabic, country.Code })
            .ToDictionaryAsync(country => country.Id, cancellationToken);

        // The grid renders the real photo thumbnail only when an active
        // speaker-photo asset exists (the /assets/SpeakerPhoto/{id}/image proxy
        // resolves from the StoredFile store, not the legacy PhotoRelativePath),
        // otherwise it falls back to an initials tile — so a missing photo never
        // shows a broken image. One batched query for the whole page, no N+1.
        var speakerIds = pageRaw.Select(row => row.Id).ToList();
        var photoOwners = await assetService.WhichOwnersHaveActiveAssetAsync(
            AssetCategory.SpeakerPhoto, speakerIds, cancellationToken);

        var page = pageRaw
            .Select(row =>
            {
                string? en = null, ar = null, code = null;
                if (row.CountryId.HasValue
                    && countriesById.TryGetValue(row.CountryId.Value, out var country))
                {
                    en = country.Name;
                    ar = country.NameArabic;
                    code = country.Code;
                }
                return new AdminSpeakerSummary(
                    row.Id, row.Code, row.Name, row.NameArabic, row.Rank, row.RankArabic,
                    row.CountryId, en, ar, code,
                    row.DisplayOrder, row.IsActive, photoOwners.Contains(row.Id),
                    row.CreatedAt);
            })
            .ToList();

        return GridPage<AdminSpeakerSummary>.Of(page, total,
            skip, top);
    }

    public async Task<AdminSpeakerDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var speaker = await dbContext.Speakers
            .AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        if (speaker is null) { return null; }
        var (en, ar) = await ResolveCountryAsync(speaker.CountryId, cancellationToken);
        return ToDetail(speaker, en, ar);
    }

    public async Task<AdminSpeakerDetail> CreateAsync(
        Guid actorUserId,
        AdminCreateSpeakerRequest request,
        CancellationToken cancellationToken = default)
    {
        var (code, name, nameArabic) = ValidateAndNormalise(
            request.Code, request.Name, request.NameArabic);
        if (request.DisplayOrder < 0)
        {
            throw new ApiException(
                ErrorCodes.SpeakerInvalid, 400,
                "Display order must be zero or a positive integer.",
                "يجب أن يكون ترتيب العرض صفراً أو عدداً صحيحاً موجباً.");
        }
        ValidateSocialUrls(
            request.FacebookUrl, request.LinkedInUrl, request.XUrl, request.WebsiteUrl);
        ValidateContactFields(
            request.Email, request.PhonePrimary, request.PhoneSecondary,
            request.InstagramUrl, request.City, request.CityArabic,
            request.Latitude, request.Longitude);
        await EnsureCountryIsValidAsync(request.CountryId, cancellationToken);
        await EnsureUserProfileIsValidAsync(request.UserProfileId, cancellationToken);

        var clash = await dbContext.Speakers
            .AsNoTracking()
            .AnyAsync(row => row.Code == code, cancellationToken);
        if (clash)
        {
            throw new ApiException(
                ErrorCodes.SpeakerCodeDuplicate, 409,
                $"A speaker with code '{code}' already exists.",
                $"يوجد متحدّث بالرمز '{code}' بالفعل.");
        }

        var now = timeProvider.SimfNow();
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            NameArabic = nameArabic,
            Rank = NullIfBlank(request.Rank),
            RankArabic = NullIfBlank(request.RankArabic),
            CountryId = request.CountryId,
            UserProfileId = request.UserProfileId,
            Bio = NullIfBlank(request.Bio),
            BioArabic = NullIfBlank(request.BioArabic),
            Qualifications = NullIfBlank(request.Qualifications),
            QualificationsArabic = NullIfBlank(request.QualificationsArabic),
            TrainingExperience = NullIfBlank(request.TrainingExperience),
            TrainingExperienceArabic = NullIfBlank(request.TrainingExperienceArabic),
            Awards = NullIfBlank(request.Awards),
            AwardsArabic = NullIfBlank(request.AwardsArabic),
            AllowsMeetingRequests = request.AllowsMeetingRequests,
            AllowsDataSharing = request.AllowsDataSharing,
            FacebookUrl = NullIfBlank(request.FacebookUrl),
            LinkedInUrl = NullIfBlank(request.LinkedInUrl),
            XUrl = NullIfBlank(request.XUrl),
            WebsiteUrl = NullIfBlank(request.WebsiteUrl),
            Email = NullIfBlank(request.Email),
            PhonePrimary = NullIfBlank(request.PhonePrimary),
            PhoneSecondary = NullIfBlank(request.PhoneSecondary),
            InstagramUrl = NullIfBlank(request.InstagramUrl),
            City = NullIfBlank(request.City),
            CityArabic = NullIfBlank(request.CityArabic),
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            DisplayOrder = request.DisplayOrder,
            IsActive = true,
            CreatedAt = now,
        };
        dbContext.Speakers.Add(speaker);
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SpeakerCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={speaker.Id}; code={code}; name={name}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created Speaker {Code} ({Id})",
            actorUserId, code, speaker.Id);

        var (en, ar) = await ResolveCountryAsync(speaker.CountryId, cancellationToken);
        return ToDetail(speaker, en, ar);
    }

    public async Task<AdminSpeakerDetail> UpdateAsync(
        Guid actorUserId,
        Guid id,
        AdminUpdateSpeakerRequest request,
        CancellationToken cancellationToken = default)
    {
        var speaker = await dbContext.Speakers
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SpeakerNotFound, 404,
                "The speaker was not found.",
                "لم يتم العثور على المتحدّث.");

        var (code, name, nameArabic) = ValidateAndNormalise(
            request.Code, request.Name, request.NameArabic);
        if (request.DisplayOrder < 0)
        {
            throw new ApiException(
                ErrorCodes.SpeakerInvalid, 400,
                "Display order must be zero or a positive integer.",
                "يجب أن يكون ترتيب العرض صفراً أو عدداً صحيحاً موجباً.");
        }
        ValidateSocialUrls(
            request.FacebookUrl, request.LinkedInUrl, request.XUrl, request.WebsiteUrl);
        ValidateContactFields(
            request.Email, request.PhonePrimary, request.PhoneSecondary,
            request.InstagramUrl, request.City, request.CityArabic,
            request.Latitude, request.Longitude);
        await EnsureCountryIsValidAsync(request.CountryId, cancellationToken);
        await EnsureUserProfileIsValidAsync(request.UserProfileId, cancellationToken);

        if (!string.Equals(speaker.Code, code, StringComparison.OrdinalIgnoreCase))
        {
            var clash = await dbContext.Speakers
                .AsNoTracking()
                .AnyAsync(row => row.Id != id && row.Code == code, cancellationToken);
            if (clash)
            {
                throw new ApiException(
                    ErrorCodes.SpeakerCodeDuplicate, 409,
                    $"A speaker with code '{code}' already exists.",
                    $"يوجد متحدّث بالرمز '{code}' بالفعل.");
            }
        }

        speaker.Code = code;
        speaker.Name = name;
        speaker.NameArabic = nameArabic;
        speaker.Rank = NullIfBlank(request.Rank);
        speaker.RankArabic = NullIfBlank(request.RankArabic);
        speaker.CountryId = request.CountryId;
        speaker.UserProfileId = request.UserProfileId;
        speaker.Bio = NullIfBlank(request.Bio);
        speaker.BioArabic = NullIfBlank(request.BioArabic);
        speaker.Qualifications = NullIfBlank(request.Qualifications);
        speaker.QualificationsArabic = NullIfBlank(request.QualificationsArabic);
        speaker.TrainingExperience = NullIfBlank(request.TrainingExperience);
        speaker.TrainingExperienceArabic = NullIfBlank(request.TrainingExperienceArabic);
        speaker.Awards = NullIfBlank(request.Awards);
        speaker.AwardsArabic = NullIfBlank(request.AwardsArabic);
        speaker.AllowsMeetingRequests = request.AllowsMeetingRequests;
        speaker.AllowsDataSharing = request.AllowsDataSharing;
        speaker.FacebookUrl = NullIfBlank(request.FacebookUrl);
        speaker.LinkedInUrl = NullIfBlank(request.LinkedInUrl);
        speaker.XUrl = NullIfBlank(request.XUrl);
        speaker.WebsiteUrl = NullIfBlank(request.WebsiteUrl);
        speaker.Email = NullIfBlank(request.Email);
        speaker.PhonePrimary = NullIfBlank(request.PhonePrimary);
        speaker.PhoneSecondary = NullIfBlank(request.PhoneSecondary);
        speaker.InstagramUrl = NullIfBlank(request.InstagramUrl);
        speaker.City = NullIfBlank(request.City);
        speaker.CityArabic = NullIfBlank(request.CityArabic);
        speaker.Latitude = request.Latitude;
        speaker.Longitude = request.Longitude;
        speaker.DisplayOrder = request.DisplayOrder;
        speaker.IsActive = request.IsActive;
        speaker.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SpeakerUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={speaker.Id}; code={code}; active={speaker.IsActive}",
        }, cancellationToken);

        var (en, ar) = await ResolveCountryAsync(speaker.CountryId, cancellationToken);
        return ToDetail(speaker, en, ar);
    }

    public async Task DeactivateAsync(
        Guid actorUserId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var speaker = await dbContext.Speakers
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken)
            ?? throw new ApiException(
                ErrorCodes.SpeakerNotFound, 404,
                "The speaker was not found.",
                "لم يتم العثور على المتحدّث.");

        if (!speaker.IsActive)
        {
            return; // idempotent
        }

        speaker.IsActive = false;
        speaker.UpdatedAt = timeProvider.SimfNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.SpeakerDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={speaker.Id}; code={speaker.Code}",
        }, cancellationToken);
    }

    private static (string code, string name, string nameArabic) ValidateAndNormalise(
        string codeRaw, string nameRaw, string nameArabicRaw)
    {
        var code = (codeRaw ?? string.Empty).Trim().ToUpperInvariant();
        if (code.Length is < 2 or > 16)
        {
            throw new ApiException(
                ErrorCodes.SpeakerInvalid, 400,
                "Speaker code must be between 2 and 16 characters.",
                "يجب أن يتراوح طول رمز المتحدّث بين 2 و 16 حرفاً.");
        }
        var name = (nameRaw ?? string.Empty).Trim();
        if (name.Length is < 1 or > 128)
        {
            throw new ApiException(
                ErrorCodes.SpeakerInvalid, 400,
                "Speaker English name must be between 1 and 128 characters.",
                "يجب أن يتراوح طول الاسم الإنجليزي للمتحدّث بين 1 و 128 حرفاً.");
        }
        var nameArabic = (nameArabicRaw ?? string.Empty).Trim();
        if (nameArabic.Length is < 1 or > 128)
        {
            throw new ApiException(
                ErrorCodes.SpeakerInvalid, 400,
                "Speaker Arabic name must be between 1 and 128 characters.",
                "يجب أن يتراوح طول الاسم العربي للمتحدّث بين 1 و 128 حرفاً.");
        }
        return (code, name, nameArabic);
    }

    private static void ValidateSocialUrls(
        string? facebook, string? linkedIn, string? x, string? website)
    {
        foreach (var url in new[] { facebook, linkedIn, x, website })
        {
            if (!string.IsNullOrWhiteSpace(url) && url.Length > 256)
            {
                throw new ApiException(
                    ErrorCodes.SpeakerInvalid, 400,
                    "Social URLs must be 256 characters or less.",
                    "يجب ألا يتجاوز رابط الشبكات الاجتماعية 256 حرفاً.");
            }
        }
    }

    // D-766 — validates the identity-card fields inlined from the removed
    // shared Contact directory. Lengths mirror the EF configuration; latitude
    // and longitude are an all-or-nothing pair with real-world ranges.
    private static void ValidateContactFields(
        string? email, string? phonePrimary, string? phoneSecondary,
        string? instagram, string? city, string? cityArabic,
        double? latitude, double? longitude)
    {
        if (!string.IsNullOrWhiteSpace(email) && email.Length > 320)
        {
            throw Invalid("Email must be 320 characters or less.",
                "يجب ألا يتجاوز البريد الإلكتروني 320 حرفاً.");
        }
        foreach (var phone in new[] { phonePrimary, phoneSecondary })
        {
            if (!string.IsNullOrWhiteSpace(phone) && phone.Length > 32)
            {
                throw Invalid("Phone numbers must be 32 characters or less.",
                    "يجب ألا يتجاوز رقم الهاتف 32 حرفاً.");
            }
        }
        if (!string.IsNullOrWhiteSpace(instagram) && instagram.Length > 256)
        {
            throw Invalid("Social URLs must be 256 characters or less.",
                "يجب ألا يتجاوز رابط الشبكات الاجتماعية 256 حرفاً.");
        }
        foreach (var cityValue in new[] { city, cityArabic })
        {
            if (!string.IsNullOrWhiteSpace(cityValue) && cityValue.Length > 128)
            {
                throw Invalid("City must be 128 characters or less.",
                    "يجب ألا تتجاوز المدينة 128 حرفاً.");
            }
        }
        if (latitude is null != (longitude is null))
        {
            throw Invalid("Latitude and longitude must be provided together.",
                "يجب إدخال خط العرض وخط الطول معاً.");
        }
        if (latitude is < -90 or > 90)
        {
            throw Invalid("Latitude must be between -90 and 90.",
                "يجب أن يكون خط العرض بين -90 و 90.");
        }
        if (longitude is < -180 or > 180)
        {
            throw Invalid("Longitude must be between -180 and 180.",
                "يجب أن يكون خط الطول بين -180 و 180.");
        }
    }

    private static ApiException Invalid(string english, string arabic) =>
        new(ErrorCodes.SpeakerInvalid, 400, english, arabic);

    private async Task EnsureCountryIsValidAsync(
        int? countryId, CancellationToken cancellationToken)
    {
        if (countryId is null) { return; }
        var exists = await dbContext.Countries
            .AsNoTracking()
            .AnyAsync(country => country.Id == countryId.Value && country.IsActive, cancellationToken);
        if (!exists)
        {
            throw new ApiException(
                ErrorCodes.SpeakerInvalid, 400,
                $"Country id '{countryId}' does not exist or is inactive.",
                $"رقم البلد '{countryId}' غير موجود أو غير مفعّل.");
        }
    }

    /// <summary>Validates the linked-account id BEFORE SaveChanges.
    ///
    /// <para>This class's summary used to say existence was deliberately not
    /// pre-checked because the link is cross-context and "a stale FK degrades
    /// gracefully to no linked account". That stopped being true when
    /// <c>UserProfile</c> moved onto <see cref="SimfAppDbContext"/>:
    /// <c>Speaker.UserProfileId</c> is now a real same-database FK with
    /// <c>OnDelete.Restrict</c>, so an unknown id no longer degrades — it violates
    /// the constraint at SaveChanges and reaches the admin as an unhandled
    /// <b>500</b>. Checking it here turns that into a 400 the caller can act on,
    /// exactly as <see cref="EnsureCountryIsValidAsync"/> does for the country.
    /// Found while executing BF-13's over-posting scenario.</para></summary>
    private async Task EnsureUserProfileIsValidAsync(
        Guid? userProfileId, CancellationToken cancellationToken)
    {
        if (userProfileId is null) { return; }
        var exists = await dbContext.UserProfiles
            .AsNoTracking()
            .AnyAsync(profile => profile.Id == userProfileId.Value, cancellationToken);
        if (!exists)
        {
            throw new ApiException(
                ErrorCodes.SpeakerInvalid, 400,
                $"User profile '{userProfileId}' does not exist.",
                $"الملف الشخصي '{userProfileId}' غير موجود.");
        }
    }

    private async Task<(string? en, string? ar)> ResolveCountryAsync(
        int? countryId, CancellationToken cancellationToken)
    {
        if (countryId is null) { return (null, null); }
        var row = await dbContext.Countries
            .AsNoTracking()
            .Where(country => country.Id == countryId.Value)
            .Select(country => new { country.Name, country.NameArabic })
            .SingleOrDefaultAsync(cancellationToken);
        return (row?.Name, row?.NameArabic);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AdminSpeakerDetail ToDetail(
        Speaker speaker, string? countryNameEn, string? countryNameAr) =>
        new(speaker.Id, speaker.Code, speaker.Name, speaker.NameArabic,
            speaker.Rank, speaker.RankArabic,
            speaker.CountryId, countryNameEn, countryNameAr,
            speaker.UserProfileId,
            speaker.Bio, speaker.BioArabic,
            speaker.Qualifications, speaker.QualificationsArabic,
            speaker.TrainingExperience, speaker.TrainingExperienceArabic,
            speaker.Awards, speaker.AwardsArabic,
            speaker.AllowsMeetingRequests, speaker.AllowsDataSharing,
            speaker.FacebookUrl, speaker.LinkedInUrl, speaker.XUrl,
            speaker.WebsiteUrl,
            speaker.PhotoRelativePath,
            speaker.DisplayOrder, speaker.IsActive,
            speaker.CreatedAt, speaker.UpdatedAt,
            speaker.Email, speaker.PhonePrimary, speaker.PhoneSecondary,
            speaker.InstagramUrl, speaker.City, speaker.CityArabic,
            speaker.Latitude, speaker.Longitude);
}
