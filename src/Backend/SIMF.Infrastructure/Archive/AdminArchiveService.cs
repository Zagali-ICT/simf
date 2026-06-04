// Tests: SIMF.Api.Tests/AdminArchiveTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Archive.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Operations.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Archive;
using SIMF.Domain.Archive;
using SIMF.Infrastructure.Persistence;

namespace SIMF.Infrastructure.Archive;

/// <summary>D-199 — admin CRUD over <see cref="ArchiveEdition"/>. One edition
/// per year; year uniqueness is validated and surfaced as a 409. Mirrors
/// <c>AdminDelegationService</c> / <c>AdminCountryService</c> structure
/// (inline Validate, audit on every mutation, soft-delete via IsActive).</summary>
internal sealed class AdminArchiveService(
    SimfAppDbContext appDbContext,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    IOperationsToggleService operationsToggleService,
    ILogger<AdminArchiveService> logger) : IAdminArchiveService
{
    private const int MinYear = 2000;
    private const int MaxYear = 2100;

    public async Task<GridPage<AdminArchiveEditionSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var skip = Math.Max(0, query.Skip);
        var top = Math.Clamp(query.Top is > 0 ? query.Top : 50, 1, 500);

        var rows = appDbContext.ArchiveEditions.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            rows = rows.Where(edition =>
                EF.Functions.Like(edition.TitleEn, $"%{term}%")
                || EF.Functions.Like(edition.TitleAr, $"%{term}%"));
        }

        if (query.Filters.TryGetValue("isActive", out var activeFilter)
            && bool.TryParse(activeFilter, out var isActive))
        {
            rows = rows.Where(edition => edition.IsActive == isActive);
        }

        // D-258 — per-column grid filters (SimfDataGrid). Each filterable
        // column contributes a Contains() narrowing on its mapped property.
        foreach (var filter in query.Filters)
        {
            var value = filter.Value;
            if (string.IsNullOrWhiteSpace(value)) { continue; }

            rows = filter.Key.ToLowerInvariant() switch
            {
                "titleen" => rows.Where(edition => edition.TitleEn.Contains(value)),
                "titlear" => rows.Where(edition => edition.TitleAr.Contains(value)),
                _ => rows,
            };
        }

        rows = (query.Sort?.ToLowerInvariant(), query.SortDescending) switch
        {
            ("year", false) => rows.OrderBy(edition => edition.Year),
            ("titleen", true) => rows.OrderByDescending(edition => edition.TitleEn),
            ("titleen", false) => rows.OrderBy(edition => edition.TitleEn),
            _ => rows.OrderByDescending(edition => edition.Year),
        };

        var total = await rows.CountAsync(cancellationToken);
        var page = await rows.Skip(skip).Take(top)
            .Select(edition => new AdminArchiveEditionSummary(
                edition.Id, edition.Year, edition.TitleEn, edition.TitleAr,
                edition.SummaryEn, edition.SummaryAr,
                edition.Attendees, edition.Sessions, edition.Speakers,
                edition.CoverImageRelativePath, edition.IsActive,
                edition.CreatedAt,
                edition.LocationEn, edition.LocationAr,
                edition.DateLabelEn, edition.DateLabelAr))
            .ToListAsync(cancellationToken);

        return GridPage<AdminArchiveEditionSummary>.Of(page, total,
            new GridQuery { Skip = skip, Top = top });
    }

    public async Task<AdminArchiveEditionDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default) =>
        await appDbContext.ArchiveEditions.AsNoTracking()
            .Where(edition => edition.Id == id)
            .Select(edition => ToDetail(edition))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<AdminArchiveEditionDetail> CreateAsync(
        Guid actorUserId, CreateArchiveEditionRequest request,
        CancellationToken cancellationToken = default)
    {
        var v = Validate(request.Year, request.TitleEn, request.TitleAr,
            request.SummaryEn, request.SummaryAr,
            request.Attendees, request.Sessions, request.Speakers,
            request.CoverImageRelativePath,
            request.LocationEn, request.LocationAr,
            request.DateLabelEn, request.DateLabelAr);

        var yearClash = await appDbContext.ArchiveEditions.AsNoTracking()
            .AnyAsync(e => e.Year == v.Year, cancellationToken);
        if (yearClash)
        {
            throw new ApiException(ErrorCodes.ArchiveEditionYearDuplicate, 409,
                $"An archive edition for year {v.Year} already exists.",
                $"توجد نسخة أرشيف للعام {v.Year} بالفعل.");
        }

        var now = timeProvider.GetUtcNow();
        var edition = new ArchiveEdition
        {
            Id = Guid.NewGuid(),
            Year = v.Year,
            TitleEn = v.TitleEn,
            TitleAr = v.TitleAr,
            SummaryEn = v.SummaryEn,
            SummaryAr = v.SummaryAr,
            Attendees = v.Attendees,
            Sessions = v.Sessions,
            Speakers = v.Speakers,
            CoverImageRelativePath = v.CoverImageRelativePath,
            // §9 (screen 24-01) — optional place + date label.
            LocationEn = v.LocationEn,
            LocationAr = v.LocationAr,
            DateLabelEn = v.DateLabelEn,
            DateLabelAr = v.DateLabelAr,
            IsActive = true,
            CreatedAt = now,
        };

        appDbContext.ArchiveEditions.Add(edition);
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.ArchiveEditionCreated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={edition.Id}; year={v.Year}; titleEn={v.TitleEn}",
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created ArchiveEdition {Year} (id {Id})",
            actorUserId, v.Year, edition.Id);

        return ToDetail(edition);
    }

    public async Task<AdminArchiveEditionDetail> UpdateAsync(
        Guid actorUserId, Guid id, UpdateArchiveEditionRequest request,
        CancellationToken cancellationToken = default)
    {
        var edition = await appDbContext.ArchiveEditions
            .SingleOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.ArchiveEditionNotFound, 404,
                "The archive edition was not found.",
                "لم يتم العثور على نسخة الأرشيف.");

        var v = Validate(request.Year, request.TitleEn, request.TitleAr,
            request.SummaryEn, request.SummaryAr,
            request.Attendees, request.Sessions, request.Speakers,
            request.CoverImageRelativePath,
            request.LocationEn, request.LocationAr,
            request.DateLabelEn, request.DateLabelAr);

        if (edition.Year != v.Year)
        {
            var clash = await appDbContext.ArchiveEditions.AsNoTracking()
                .AnyAsync(e => e.Id != id && e.Year == v.Year, cancellationToken);
            if (clash)
            {
                throw new ApiException(ErrorCodes.ArchiveEditionYearDuplicate, 409,
                    $"An archive edition for year {v.Year} already exists.",
                    $"توجد نسخة أرشيف للعام {v.Year} بالفعل.");
            }
        }

        edition.Year = v.Year;
        edition.TitleEn = v.TitleEn;
        edition.TitleAr = v.TitleAr;
        edition.SummaryEn = v.SummaryEn;
        edition.SummaryAr = v.SummaryAr;
        edition.Attendees = v.Attendees;
        edition.Sessions = v.Sessions;
        edition.Speakers = v.Speakers;
        edition.CoverImageRelativePath = v.CoverImageRelativePath;
        // §9 (screen 24-01) — optional place + date label.
        edition.LocationEn = v.LocationEn;
        edition.LocationAr = v.LocationAr;
        edition.DateLabelEn = v.DateLabelEn;
        edition.DateLabelAr = v.DateLabelAr;
        edition.IsActive = request.IsActive;
        edition.UpdatedAt = timeProvider.GetUtcNow();

        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.ArchiveEditionUpdated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={id}; year={v.Year}; active={edition.IsActive}",
        }, cancellationToken);

        return ToDetail(edition);
    }

    public async Task DeactivateAsync(
        Guid actorUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var edition = await appDbContext.ArchiveEditions
            .SingleOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.ArchiveEditionNotFound, 404,
                "The archive edition was not found.",
                "لم يتم العثور على نسخة الأرشيف.");

        if (!edition.IsActive) { return; }

        edition.IsActive = false;
        edition.UpdatedAt = timeProvider.GetUtcNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteAsync(new AuditEntry
        {
            EventType = AuditEvents.ArchiveEditionDeactivated,
            Outcome = AuditOutcome.Success,
            ActorUserId = actorUserId,
            Detail = $"id={id}; year={edition.Year}",
        }, cancellationToken);
    }

    public async Task<AdminArchiveEditionDetail> SnapshotCurrentAsync(
        Guid actorUserId, SnapshotCurrentEditionRequest request,
        CancellationToken cancellationToken = default)
    {
        // §9 (D-275) — fully automatic: the year + bilingual title are generated
        // and the three counters are computed from live App data (no client input).
        var year = timeProvider.GetUtcNow().Year;

        var sessions = await appDbContext.Sessions.AsNoTracking()
            .CountAsync(session => session.IsActive, cancellationToken);
        var speakers = await appDbContext.Speakers.AsNoTracking()
            .CountAsync(speaker => speaker.IsActive, cancellationToken);
        // Attendees = distinct people who physically arrived: an allowed CheckIn
        // gate scan with a resolved profile (owner's "gate-scan arrivals", D-275).
        var attendees = await appDbContext.GateScans.AsNoTracking()
            .Where(scan => scan.Outcome == ScanOutcome.Allowed
                        && scan.Direction == ScanDirection.CheckIn
                        && scan.UserProfileId != null)
            .Select(scan => scan.UserProfileId!.Value)
            .Distinct()
            .CountAsync(cancellationToken);

        // Reuse CreateAsync: it enforces the one-edition-per-year 409 and writes
        // the ArchiveEditionCreated audit. The snapshot is a create with computed
        // counters + a generated title.
        var detail = await CreateAsync(actorUserId, new CreateArchiveEditionRequest
        {
            Year = year,
            TitleEn = $"SIMF {year}",
            TitleAr = $"سيمف {year}",
            Attendees = attendees,
            Sessions = sessions,
            Speakers = speakers,
        }, cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} snapshotted the current event into ArchiveEdition {Year} "
            + "(attendees {Attendees}, sessions {Sessions}, speakers {Speakers}; makeVisible {MakeVisible})",
            actorUserId, year, attendees, sessions, speakers, request.MakeVisible);

        if (request.MakeVisible)
        {
            await operationsToggleService.UpdateArchiveVisibilityAsync(
                actorUserId,
                new UpdateArchiveVisibilityRequest { IsVisible = true },
                cancellationToken);
        }

        return detail;
    }

    private static (int Year, string TitleEn, string TitleAr,
        string? SummaryEn, string? SummaryAr,
        int Attendees, int Sessions, int Speakers,
        string? CoverImageRelativePath,
        string? LocationEn, string? LocationAr,
        string? DateLabelEn, string? DateLabelAr) Validate(
            int yearRaw, string titleEnRaw, string titleArRaw,
            string? summaryEnRaw, string? summaryArRaw,
            int attendeesRaw, int sessionsRaw, int speakersRaw,
            string? coverRaw,
            string? locationEnRaw, string? locationArRaw,
            string? dateLabelEnRaw, string? dateLabelArRaw)
    {
        if (yearRaw is < MinYear or > MaxYear)
        {
            throw new ApiException(ErrorCodes.ArchiveEditionInvalid, 400,
                $"Year must be between {MinYear} and {MaxYear}.",
                $"يجب أن يكون العام بين {MinYear} و {MaxYear}.");
        }

        var titleEn = (titleEnRaw ?? string.Empty).Trim();
        if (titleEn.Length is < 1 or > 200)
        {
            throw new ApiException(ErrorCodes.ArchiveEditionInvalid, 400,
                "English title must be between 1 and 200 characters.",
                "يجب أن يتراوح العنوان الإنجليزي بين 1 و 200 حرفاً.");
        }

        var titleAr = (titleArRaw ?? string.Empty).Trim();
        if (titleAr.Length is < 1 or > 200)
        {
            throw new ApiException(ErrorCodes.ArchiveEditionInvalid, 400,
                "Arabic title must be between 1 and 200 characters.",
                "يجب أن يتراوح العنوان العربي بين 1 و 200 حرفاً.");
        }

        var summaryEn = string.IsNullOrWhiteSpace(summaryEnRaw)
            ? null : summaryEnRaw.Trim();
        if (summaryEn is { Length: > 1024 })
        {
            throw new ApiException(ErrorCodes.ArchiveEditionInvalid, 400,
                "English summary must be 1024 characters or fewer.",
                "يجب ألا يتجاوز الملخص الإنجليزي 1024 حرفاً.");
        }

        var summaryAr = string.IsNullOrWhiteSpace(summaryArRaw)
            ? null : summaryArRaw.Trim();
        if (summaryAr is { Length: > 1024 })
        {
            throw new ApiException(ErrorCodes.ArchiveEditionInvalid, 400,
                "Arabic summary must be 1024 characters or fewer.",
                "يجب ألا يتجاوز الملخص العربي 1024 حرفاً.");
        }

        if (attendeesRaw < 0 || sessionsRaw < 0 || speakersRaw < 0)
        {
            throw new ApiException(ErrorCodes.ArchiveEditionInvalid, 400,
                "Attendees, sessions and speakers must be zero or positive.",
                "يجب أن تكون أعداد الحضور والجلسات والمتحدثين صفراً أو موجبة.");
        }

        var cover = string.IsNullOrWhiteSpace(coverRaw) ? null : coverRaw.Trim();
        if (cover is { Length: > 512 })
        {
            throw new ApiException(ErrorCodes.ArchiveEditionInvalid, 400,
                "Cover image path must be 512 characters or fewer.",
                "يجب ألا يتجاوز مسار صورة الغلاف 512 حرفاً.");
        }

        // §9 (screen 24-01) — optional place + date label, length-checked here
        // too so the service mirrors the FluentValidation + EF limits (256 / 128)
        // for every persisted string field.
        var locationEn = string.IsNullOrWhiteSpace(locationEnRaw) ? null : locationEnRaw.Trim();
        var locationAr = string.IsNullOrWhiteSpace(locationArRaw) ? null : locationArRaw.Trim();
        if (locationEn is { Length: > 256 } || locationAr is { Length: > 256 })
        {
            throw new ApiException(ErrorCodes.ArchiveEditionInvalid, 400,
                "Location must be 256 characters or fewer.",
                "يجب ألا يتجاوز المكان 256 حرفاً.");
        }

        var dateLabelEn = string.IsNullOrWhiteSpace(dateLabelEnRaw) ? null : dateLabelEnRaw.Trim();
        var dateLabelAr = string.IsNullOrWhiteSpace(dateLabelArRaw) ? null : dateLabelArRaw.Trim();
        if (dateLabelEn is { Length: > 128 } || dateLabelAr is { Length: > 128 })
        {
            throw new ApiException(ErrorCodes.ArchiveEditionInvalid, 400,
                "Date label must be 128 characters or fewer.",
                "يجب ألا يتجاوز وصف التاريخ 128 حرفاً.");
        }

        return (yearRaw, titleEn, titleAr, summaryEn, summaryAr,
            attendeesRaw, sessionsRaw, speakersRaw, cover,
            locationEn, locationAr, dateLabelEn, dateLabelAr);
    }

    private static AdminArchiveEditionDetail ToDetail(ArchiveEdition edition) =>
        new(edition.Id, edition.Year, edition.TitleEn, edition.TitleAr,
            edition.SummaryEn, edition.SummaryAr,
            edition.Attendees, edition.Sessions, edition.Speakers,
            edition.CoverImageRelativePath, edition.IsActive,
            edition.CreatedAt, edition.UpdatedAt,
            edition.LocationEn, edition.LocationAr,
            edition.DateLabelEn, edition.DateLabelAr);
}
