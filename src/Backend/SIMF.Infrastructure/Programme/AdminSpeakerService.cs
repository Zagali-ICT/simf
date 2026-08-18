// Tests: SIMF.Api.Tests/AdminSpeakersTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Assets.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Programme.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Common.Grids;
using SIMF.Contracts.Admin;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Common.Grids;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Programme;

/// <summary>
/// Admin CRUD over <see cref="Speaker"/>. Built on
/// <see cref="SimfAppDbContext"/>. <c>CountryId</c> and <c>UserProfileId</c> are
/// both validated up front against the live tables on the same context: each is a
/// real same-database FK with <c>OnDelete.Restrict</c>, so an unknown id that
/// reached SaveChanges would violate the constraint and surface as a 500.
/// </summary>
internal sealed class AdminSpeakerService(
    SimfAppDbContext dbContext,
    IAssetService assetService,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    ILogger<AdminSpeakerService> logger) : IAdminSpeakerService
{
    /// <summary>
    /// The grid contract for /admin/speakers: one entry per key SpeakersList.razor
    /// can send, as both its filter and its sort. A key not declared here is a 400,
    /// not a silently ignored request. <c>nameArabic</c> is declared even though the
    /// page shows no column for it, because it is one of the three columns the free
    /// -text search covers and the search set is drawn from these declarations.
    /// </summary>
    private static readonly GridColumns<Speaker> Columns = new GridColumns<Speaker>()
        .Add("code", speaker => speaker.Code, searchable: true)
        .Add("name", speaker => speaker.Name, searchable: true)
        .Add("nameArabic", speaker => speaker.NameArabic, searchable: true)
        .Add("displayOrder", speaker => speaker.DisplayOrder)
        .Add("isActive", speaker => speaker.IsActive)
        .DefaultOrder("displayOrder")
        .DefaultOrder("name")
        .PageSize(fallback: 25, max: 200);

    public async Task<GridPage<AdminSpeakerSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        // The country columns come off the navigation rather than a second
        // id-keyed round trip. A left join yields the same nulls the dictionary
        // lookup did, both for an unset CountryId and for an id whose row is gone.
        var page = await dbContext.Speakers.ToGridPageAsync(
            query, Columns, speaker => speaker.Id,
            speaker => new
            {
                speaker.Id,
                speaker.Code,
                speaker.Name,
                speaker.NameArabic,
                speaker.Rank,
                speaker.RankArabic,
                speaker.CountryId,
                CountryNameEn = speaker.Country == null ? null : speaker.Country.Name,
                CountryNameAr = speaker.Country == null ? null : speaker.Country.NameArabic,
                CountryCode = speaker.Country == null ? null : speaker.Country.Code,
                speaker.DisplayOrder,
                speaker.IsActive,
                speaker.CreatedAt,
            },
            cancellationToken);

        // The grid renders the real photo thumbnail only when an active
        // speaker-photo asset exists (the /assets/SpeakerPhoto/{id}/image proxy
        // resolves from the StoredFile store, not the legacy PhotoRelativePath),
        // otherwise it falls back to an initials tile — so a missing photo never
        // shows a broken image. One batched query for the whole page, no N+1.
        // It stays behind IAssetService: which FileService backs a category is that
        // service's mapping to own, not something to restate in a projection here.
        var photoOwners = await assetService.WhichOwnersHaveActiveAssetAsync(
            AssetCategory.SpeakerPhoto,
            page.Items.Select(row => row.Id).ToList(),
            cancellationToken);

        var summaries = page.Items
            .Select(row => new AdminSpeakerSummary(
                row.Id, row.Code, row.Name, row.NameArabic, row.Rank, row.RankArabic,
                row.CountryId, row.CountryNameEn, row.CountryNameAr, row.CountryCode,
                row.DisplayOrder, row.IsActive, photoOwners.Contains(row.Id),
                row.CreatedAt))
            .ToList();

        return GridPage<AdminSpeakerSummary>.Of(
            summaries, page.Total, page.Skip, page.Top);
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

        await auditLog.WriteSuccessAsync(
            AuditEvents.SpeakerCreated,
            actorUserId,
            $"id={speaker.Id}; code={code}; name={name}",
            cancellationToken);

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

        await auditLog.WriteSuccessAsync(
            AuditEvents.SpeakerUpdated,
            actorUserId,
            $"id={speaker.Id}; code={code}; active={speaker.IsActive}",
            cancellationToken);

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

        await auditLog.WriteSuccessAsync(
            AuditEvents.SpeakerDeactivated,
            actorUserId,
            $"id={speaker.Id}; code={speaker.Code}",
            cancellationToken);
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

    // Validates the identity-card fields inlined from the removed
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
    /// <c>Speaker.UserProfileId</c> is a real same-database FK with
    /// <c>OnDelete.Restrict</c>, so an unknown id violates the constraint at
    /// SaveChanges and reaches the admin as an unhandled <b>500</b>. Checking it
    /// here turns that into a 400 the caller can act on, exactly as
    /// <see cref="EnsureCountryIsValidAsync"/> does for the country.</summary>
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
            null, // PhotoRelativePath: the column is gone; the photo is a StoredFile
            speaker.DisplayOrder, speaker.IsActive,
            speaker.CreatedAt, speaker.UpdatedAt,
            speaker.Email, speaker.PhonePrimary, speaker.PhoneSecondary,
            speaker.InstagramUrl, speaker.City, speaker.CityArabic,
            speaker.Latitude, speaker.Longitude);
}
