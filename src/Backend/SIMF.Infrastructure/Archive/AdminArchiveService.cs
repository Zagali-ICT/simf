// Tests: SIMF.Api.Tests/AdminArchiveTests.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SIMF.Application.Archive.Abstractions;
using SIMF.Application.Assets.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Application.Operations.Abstractions;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Archive;
using SIMF.Domain.Archive;
using SIMF.Infrastructure.Persistence;
using SIMF.Application.Files.Abstractions;

namespace SIMF.Infrastructure.Archive;

/// <summary>Admin CRUD over <see cref="ArchiveEdition"/>. One edition
/// per year; year uniqueness is validated and surfaced as a 409. Mirrors
/// <c>AdminDelegationService</c> / <c>AdminCountryService</c> structure
/// (inline Validate, audit on every mutation, soft-delete via IsActive).</summary>
internal sealed class AdminArchiveService(
    SimfAppDbContext appDbContext,
    IFileService fileService,
    IFeedLinkService feedLinks,
    IAuditLog auditLog,
    TimeProvider timeProvider,
    IOperationsToggleService operationsToggleService,
    IAssetService assetService,
    ILogger<AdminArchiveService> logger) : IAdminArchiveService
{
    private const int MinYear = 2000;
    private const int MaxYear = 2100;

    public async Task<GridPage<AdminArchiveEditionSummary>> ListAllAsync(
        GridQuery query, CancellationToken cancellationToken = default)
    {
        var (skip, top) = query.ClampPage(50, 500);

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

        // Per-column grid filters (SimfDataGrid). Each filterable
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
        var pageRows = await rows.Skip(skip).Take(top)
            .Select(edition => new
            {
                edition.Id, edition.Year, edition.TitleEn, edition.TitleAr,
                edition.SummaryEn, edition.SummaryAr,
                edition.Attendees, edition.Sessions, edition.Speakers,
                edition.IsActive,
                edition.CreatedAt,
                edition.LocationEn, edition.LocationAr,
                edition.DateLabelEn, edition.DateLabelAr,
            })
            .ToListAsync(cancellationToken);

        // The grid renders the cover thumbnail only when an active ArchiveCover
        // asset exists (StoredFile store via the /assets proxy, not the legacy
        // CoverImageRelativePath) — one batched query for the page, no N+1.
        var coverOwners = await assetService.WhichOwnersHaveActiveAssetAsync(
            AssetCategory.ArchiveCover, pageRows.Select(edition => edition.Id).ToList(), cancellationToken);

        var page = pageRows
            .Select(edition => new AdminArchiveEditionSummary(
                edition.Id, edition.Year, edition.TitleEn, edition.TitleAr,
                edition.SummaryEn, edition.SummaryAr,
                edition.Attendees, edition.Sessions, edition.Speakers,
                // The cover is a StoredFile; HasCoverAsset below is the flag the
                // grid renders from, and the client builds the URL from the id.
                null,
                edition.IsActive,
                edition.CreatedAt,
                coverOwners.Contains(edition.Id),
                edition.LocationEn, edition.LocationAr,
                edition.DateLabelEn, edition.DateLabelAr))
            .ToList();

        return GridPage<AdminArchiveEditionSummary>.Of(page, total,
            skip, top);
    }

    public async Task<AdminArchiveEditionDetail?> GetAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        // Load the rich child lists so the edit form pre-populates them.
        // A6 — AsSplitQuery: three SIBLING collection Includes on one root would
        // otherwise JOIN into a single Media×SessionTitles×PastSpeakers cartesian
        // rowset; split emits one query per collection (each hitting its
        // (ArchiveEditionId, DisplayOrder) index). Safe here — a single-row root
        // (Id == id, no Skip/Take) and ToDetail re-sorts every child list in
        // memory by DisplayOrder, so the wire order is unchanged.
        var edition = await appDbContext.ArchiveEditions.AsNoTracking()
            .Include(e => e.Media)
            .Include(e => e.SessionTitles)
            .Include(e => e.PastSpeakers)
            .AsSplitQuery()
            .SingleOrDefaultAsync(e => e.Id == id, cancellationToken);
        return edition is null ? null : ToDetail(edition, await ResolveMediaUrlsAsync(edition, cancellationToken));
    }

    public async Task<AdminArchiveEditionDetail> CreateAsync(
        Guid actorUserId, CreateArchiveEditionRequest request,
        CancellationToken cancellationToken = default)
    {
        var v = Validate(request.Year, request.TitleEn, request.TitleAr,
            request.SummaryEn, request.SummaryAr,
            request.Attendees, request.Sessions, request.Speakers,
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

        var now = timeProvider.SimfNow();
        var knownCountryIds = await LoadCountryIdsAsync(cancellationToken);
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
            // Optional place + date label.
            LocationEn = v.LocationEn,
            LocationAr = v.LocationAr,
            DateLabelEn = v.DateLabelEn,
            DateLabelAr = v.DateLabelAr,
            IsActive = true,
            CreatedAt = now,
        };

        // The rich child lists, cascade-inserted with the edition. Create runs
        // the same reconciler the update path does, against an empty collection,
        // so there is one place that decides how a child row is built.
        ReconcileMedia(edition, request.Gallery);
        foreach (var title in BuildSessionTitles(request.SessionTitles))
        {
            edition.SessionTitles.Add(title);
        }
        ReconcilePastSpeakers(edition, request.PastSpeakers, knownCountryIds);

        // The save is two phases - the rows first, then their video links, which
        // need the ids the first phase assigns - so it needs a transaction. The
        // link write VALIDATES the URL, and without this an edition with one bad
        // link was committed anyway: the admin got a 400, corrected the URL,
        // pressed Create again and hit a year-duplicate 409 from the row their
        // failed attempt had already left behind.
        //
        // Through the execution strategy, because the context retries on transient
        // failures and will not allow a hand-rolled transaction otherwise. The
        // edition is built once above with a fixed Id, so a retry re-inserts the
        // same row rather than duplicating it.
        var strategy = appDbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx =
                await appDbContext.Database.BeginTransactionAsync(cancellationToken);

            appDbContext.ArchiveEditions.Add(edition);
            await appDbContext.SaveChangesAsync(cancellationToken);

            await AttachGalleryVideosAsync(edition, request.Gallery, actorUserId, cancellationToken);
            await appDbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        });

        await auditLog.WriteSuccessAsync(
            AuditEvents.ArchiveEditionCreated,
            actorUserId,
            $"id={edition.Id}; year={v.Year}; titleEn={v.TitleEn}",
            cancellationToken);

        logger.LogInformation(
            "Admin {ActorId} created ArchiveEdition {Year} (id {Id})",
            actorUserId, v.Year, edition.Id);

        return ToDetail(edition, await ResolveMediaUrlsAsync(edition, cancellationToken));
    }

    public async Task<AdminArchiveEditionDetail> UpdateAsync(
        Guid actorUserId, Guid id, UpdateArchiveEditionRequest request,
        CancellationToken cancellationToken = default)
    {
        var edition = await appDbContext.ArchiveEditions
            // Load the children so replace-all can clear the orphans.
            .Include(e => e.Media)
            .Include(e => e.SessionTitles)
            .Include(e => e.PastSpeakers)
            .SingleOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new ApiException(ErrorCodes.ArchiveEditionNotFound, 404,
                "The archive edition was not found.",
                "لم يتم العثور على نسخة الأرشيف.");

        var v = Validate(request.Year, request.TitleEn, request.TitleAr,
            request.SummaryEn, request.SummaryAr,
            request.Attendees, request.Sessions, request.Speakers,
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
        // Optional place + date label.
        edition.LocationEn = v.LocationEn;
        edition.LocationAr = v.LocationAr;
        edition.DateLabelEn = v.DateLabelEn;
        edition.DateLabelAr = v.DateLabelAr;
        edition.IsActive = request.IsActive;
        edition.UpdatedAt = timeProvider.SimfNow();

        // Reconcile the rich child lists, but only the ones the caller actually
        // supplied (non-null); null leaves the existing rows untouched.
        //
        // Gallery and past speakers reconcile BY ID so a row keeps its identity
        // across a save and can therefore own an uploaded file. Session titles
        // still replace wholesale: they carry no file, nothing points at them,
        // and their identity is not observable anywhere.
        // One transaction over both phases, for the reason given on Create, and
        // through the execution strategy for the same reason.
        var knownCountryIds = request.PastSpeakers is not null
            ? await LoadCountryIdsAsync(cancellationToken)
            : null;
        var strategy = appDbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx =
                await appDbContext.Database.BeginTransactionAsync(cancellationToken);

            var dropped = new List<Guid>();
            if (request.Gallery is not null)
            {
                dropped.AddRange(ReconcileMedia(edition, request.Gallery));
            }
            if (request.SessionTitles is not null)
            {
                edition.SessionTitles.Clear();
                foreach (var title in BuildSessionTitles(request.SessionTitles))
                {
                    edition.SessionTitles.Add(title);
                }
            }
            if (knownCountryIds is not null)
            {
                dropped.AddRange(
                    ReconcilePastSpeakers(edition, request.PastSpeakers, knownCountryIds));
            }

            // Before the rows go, so the files they own are retired rather than
            // left active and owned by an id no table will hold again.
            await RetireChildFilesAsync(dropped, actorUserId, cancellationToken);

            await appDbContext.SaveChangesAsync(cancellationToken);

            await AttachGalleryVideosAsync(edition, request.Gallery, actorUserId, cancellationToken);
            await appDbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        });

        await auditLog.WriteSuccessAsync(
            AuditEvents.ArchiveEditionUpdated,
            actorUserId,
            $"id={id}; year={v.Year}; active={edition.IsActive}",
            cancellationToken);

        return ToDetail(edition, await ResolveMediaUrlsAsync(edition, cancellationToken));
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
        edition.UpdatedAt = timeProvider.SimfNow();
        await appDbContext.SaveChangesAsync(cancellationToken);

        await auditLog.WriteSuccessAsync(
            AuditEvents.ArchiveEditionDeactivated,
            actorUserId,
            $"id={id}; year={edition.Year}",
            cancellationToken);
    }

    public async Task<AdminArchiveEditionDetail> SnapshotCurrentAsync(
        Guid actorUserId, SnapshotCurrentEditionRequest request,
        CancellationToken cancellationToken = default)
    {
        // Fully automatic: the year + bilingual title are generated
        // and the three counters are computed from live App data (no client input).
        var year = timeProvider.SimfNow().Year;

        var sessions = await appDbContext.Sessions.AsNoTracking()
            .CountAsync(session => session.IsActive, cancellationToken);
        var speakers = await appDbContext.Speakers.AsNoTracking()
            .CountAsync(speaker => speaker.IsActive, cancellationToken);
        // Attendees = distinct people who physically arrived: an allowed CheckIn
        // gate scan with a resolved profile.
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
        string? LocationEn, string? LocationAr,
        string? DateLabelEn, string? DateLabelAr) Validate(
            int yearRaw, string titleEnRaw, string titleArRaw,
            string? summaryEnRaw, string? summaryArRaw,
            int attendeesRaw, int sessionsRaw, int speakersRaw,
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

        // Optional place + date label, length-checked here
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
            attendeesRaw, sessionsRaw, speakersRaw,
            locationEn, locationAr, dateLabelEn, dateLabelAr);
    }

    /// <summary>The media URLs are resolved by the caller and passed in: a
    /// gallery row's media is a file-store row now, and this mapper is static.</summary>
    private static AdminArchiveEditionDetail ToDetail(
        ArchiveEdition edition, IReadOnlyDictionary<Guid, string> mediaUrls) =>
        new(edition.Id, edition.Year, edition.TitleEn, edition.TitleAr,
            edition.SummaryEn, edition.SummaryAr,
            edition.Attendees, edition.Sessions, edition.Speakers,
            null, edition.IsActive,
            edition.CreatedAt, edition.UpdatedAt,
            edition.LocationEn, edition.LocationAr,
            edition.DateLabelEn, edition.DateLabelAr,
            // The rich child lists, ordered (read off the loaded nav
            // collections — Create/Update set them, GetAsync Includes them).
            // Id rides back so the editor can return a row to the same record on
            // the next save, which is what lets that row own an uploaded file.
            edition.Media.OrderBy(m => m.DisplayOrder).Select(m => new ArchiveMediaItemInput
            {
                Id = m.Id, Kind = (int)m.Kind,
                Url = mediaUrls.GetValueOrDefault(m.Id) ?? string.Empty,
                CaptionEn = m.CaptionEn, CaptionAr = m.CaptionAr,
                DisplayOrder = m.DisplayOrder,
            }).ToList(),
            edition.SessionTitles.OrderBy(s => s.DisplayOrder).Select(s => new ArchiveSessionTitleInput
            {
                TitleEn = s.TitleEn, TitleAr = s.TitleAr, DisplayOrder = s.DisplayOrder,
            }).ToList(),
            edition.PastSpeakers.OrderBy(p => p.DisplayOrder).Select(p => new ArchivePastSpeakerInput
            {
                Id = p.Id, NameEn = p.NameEn, NameAr = p.NameAr,
                HasPhoto = p.PhotoFileId is not null, CountryId = p.CountryId,
                DisplayOrder = p.DisplayOrder,
            }).ToList());

    // Build the child entities from the editable inputs, skipping blank
    // rows and re-deriving DisplayOrder from the submitted order. Child string
    // lengths are enforced server-side by RequireChildLength/ChildLengthOrNull
    // below (the CP MaxLength is only a client-side hint; the admin API can be
    // POSTed directly).
    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // The gallery / session-title / past-speaker columns carry
    // EF HasMaxLength limits (ArchiveDetailConfigurations) that neither Validate()
    // nor the FluentValidation validators covered, so an over-length child string
    // reached SQL Server as a truncation 500. Guard them here — the one point both
    // Create and Update materialise the child rows — throwing the same clean
    // bilingual 400 (archive_edition_invalid) the top-level Validate() uses.
    private static string RequireChildLength(
        string value, int maxLength, string fieldEn, string fieldAr)
    {
        if (value.Length > maxLength)
        {
            throw new ApiException(ErrorCodes.ArchiveEditionInvalid, 400,
                $"{fieldEn} must be {maxLength} characters or fewer.",
                $"يجب ألا يتجاوز {fieldAr} {maxLength} حرفاً.");
        }
        return value;
    }

    private static string? ChildLengthOrNull(
        string? value, int maxLength, string fieldEn, string fieldAr) =>
        value is null ? null : RequireChildLength(value, maxLength, fieldEn, fieldAr);

    /// <summary>Reconcile the gallery in place, matching each supplied row to an
    /// existing one by <c>Id</c>.
    ///
    /// <para>It used to clear the collection and re-insert everything, which gave
    /// every row a new identity on every save. That is why the image had to be a
    /// URL an admin typed: a file owned by a child id would have been orphaned
    /// the moment anybody pressed Save. Rows keep their ids now, so a row can own
    /// a real uploaded file, and a caption edit no longer silently detaches the
    /// picture beside it.</para></summary>
    private static List<Guid> ReconcileMedia(
        ArchiveEdition edition, IEnumerable<ArchiveMediaItemInput>? inputs)
    {
        var supplied = (inputs ?? Enumerable.Empty<ArchiveMediaItemInput>()).ToList();
        var existing = edition.Media.ToDictionary(item => item.Id);
        var kept = new HashSet<Guid>();
        var order = 0;

        foreach (var input in supplied)
        {
            // A null id, or one that no longer maps to a live row, inserts fresh;
            // a stale id is never resurrected in place. An id ALREADY claimed in
            // this pass inserts fresh too: two inputs naming one row would
            // otherwise collapse into a single record, leaving fewer rows than
            // were supplied - and the video attach downstream pairs rows to
            // inputs by position, so a collapse there silently gives one row
            // another's link. The Control Panel cannot produce a duplicate, but
            // this API is POSTable directly, and the pairing should hold because
            // the code guarantees it rather than because no UI happens to break it.
            var row = input.Id is { } id
                && !kept.Contains(id)
                && existing.TryGetValue(id, out var found)
                    ? found
                    : null;
            if (row is null)
            {
                row = new ArchiveMediaItem();
                edition.Media.Add(row);
            }

            row.Kind = Enum.IsDefined(typeof(ArchiveMediaKind), input.Kind)
                ? (ArchiveMediaKind)input.Kind
                : ArchiveMediaKind.Image;
            row.CaptionEn = ChildLengthOrNull(NullIfBlank(input.CaptionEn), 256,
                "Gallery caption", "تعليق المعرض");
            row.CaptionAr = ChildLengthOrNull(NullIfBlank(input.CaptionAr), 256,
                "Gallery caption", "تعليق المعرض");
            row.DisplayOrder = order++;
            kept.Add(row.Id);
        }

        // The dropped ids go back to the caller so their files can be retired.
        // A removed row is hard-deleted by the cascade, and its uploaded picture
        // would otherwise stay active for ever, owned by an id no table holds -
        // unreachable through the asset pipeline and invisible to any cleanup.
        var droppedIds = new List<Guid>();
        foreach (var dropped in edition.Media.Where(item => !kept.Contains(item.Id)).ToList())
        {
            droppedIds.Add(dropped.Id);
            edition.Media.Remove(dropped);
        }
        return droppedIds;
    }

    private static List<ArchiveSessionTitle> BuildSessionTitles(
        IEnumerable<ArchiveSessionTitleInput>? inputs)
    {
        var order = 0;
        return (inputs ?? Enumerable.Empty<ArchiveSessionTitleInput>())
            .Where(i => !string.IsNullOrWhiteSpace(i.TitleAr)
                     || !string.IsNullOrWhiteSpace(i.TitleEn))
            .Select(i => new ArchiveSessionTitle
            {
                TitleEn = RequireChildLength((i.TitleEn ?? string.Empty).Trim(), 200,
                    "Session title", "عنوان الجلسة"),
                TitleAr = RequireChildLength((i.TitleAr ?? string.Empty).Trim(), 200,
                    "Session title", "عنوان الجلسة"),
                DisplayOrder = order++,
            })
            .ToList();
    }

    /// <summary>Reconcile the past speakers in place, keyed by <c>Id</c>, for the
    /// reason described on <see cref="ReconcileMedia"/>.</summary>
    private static List<Guid> ReconcilePastSpeakers(
        ArchiveEdition edition,
        IEnumerable<ArchivePastSpeakerInput>? inputs,
        IReadOnlySet<int> knownCountryIds)
    {
        var supplied = (inputs ?? Enumerable.Empty<ArchivePastSpeakerInput>())
            .Where(i => !string.IsNullOrWhiteSpace(i.NameAr)
                     || !string.IsNullOrWhiteSpace(i.NameEn))
            .ToList();
        var existing = edition.PastSpeakers.ToDictionary(speaker => speaker.Id);
        var kept = new HashSet<Guid>();
        var order = 0;

        foreach (var input in supplied)
        {
            // Same duplicate-id guard as the gallery, for the same reason.
            var row = input.Id is { } id
                && !kept.Contains(id)
                && existing.TryGetValue(id, out var found)
                    ? found
                    : null;
            if (row is null)
            {
                row = new ArchivePastSpeaker();
                edition.PastSpeakers.Add(row);
            }

            row.NameEn = RequireChildLength((input.NameEn ?? string.Empty).Trim(), 128,
                "Past speaker name", "اسم المتحدث السابق");
            row.NameAr = RequireChildLength((input.NameAr ?? string.Empty).Trim(), 128,
                "Past speaker name", "اسم المتحدث السابق");
            // Drop an unknown or mistyped country code to null (the editor is
            // free text; an unmatched id would otherwise hit the Country foreign
            // key as a 500). Matches the app's "unknown code means no flag".
            row.CountryId = input.CountryId is { } cid && knownCountryIds.Contains(cid)
                ? cid
                : null;
            row.DisplayOrder = order++;
            kept.Add(row.Id);
        }

        var droppedIds = new List<Guid>();
        foreach (var dropped in edition.PastSpeakers
            .Where(speaker => !kept.Contains(speaker.Id)).ToList())
        {
            droppedIds.Add(dropped.Id);
            edition.PastSpeakers.Remove(dropped);
        }
        return droppedIds;
    }

    /// <summary>Attach each VIDEO gallery row to its link, after the save.
    ///
    /// <para>After, because a file row needs a real owner id and a newly added
    /// gallery row has none until it is written. An image row is skipped: its
    /// picture is uploaded against the row from the editor, not typed into the
    /// edition form.</para></summary>
    private async Task AttachGalleryVideosAsync(
        ArchiveEdition edition,
        IReadOnlyList<ArchiveMediaItemInput>? inputs,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        if (inputs is null)
        {
            return;
        }

        var rows = edition.Media.OrderBy(item => item.DisplayOrder).ToList();
        var supplied = inputs.ToList();
        for (var i = 0; i < rows.Count && i < supplied.Count; i++)
        {
            if (rows[i].Kind == ArchiveMediaKind.Video)
            {
                rows[i].MediaFileId = await feedLinks.SetAsync(
                    FileService.ArchiveGalleryVideo, rows[i].Id,
                    supplied[i].Url, actorUserId, cancellationToken);
                continue;
            }

            // The row is an image NOW, but it may have been a video a moment ago.
            // Rows keep their identity across a save, so a Kind flip leaves the
            // old link behind unless this clears it - and the image branch of the
            // public read tests only whether the pointer is null, so a stale video
            // link would make it advertise an image route that 404s.
            await RetireFeedAsync(FileService.ArchiveGalleryVideo, rows[i].Id, actorUserId, cancellationToken);
        }
    }

    /// <summary>Retire every active file a child row owns, across all three
    /// archive services. Used when a row is dropped, and when a gallery row stops
    /// being a video.</summary>
    private async Task RetireChildFilesAsync(
        IReadOnlyCollection<Guid> ownerIds, Guid actorUserId, CancellationToken cancellationToken)
    {
        if (ownerIds.Count == 0)
        {
            return;
        }

        var services = new[]
        {
            FileService.ArchiveGalleryImage,
            FileService.ArchiveGalleryVideo,
            FileService.ArchivePastSpeakerPhoto,
        };
        var ids = ownerIds.ToList();
        var fileIds = await appDbContext.StoredFiles.AsNoTracking()
            .Where(file => file.IsActive
                && services.Contains(file.Service)
                && file.OwnerEntityId != null
                && ids.Contains(file.OwnerEntityId.Value))
            .Select(file => file.Id)
            .ToListAsync(cancellationToken);

        foreach (var fileId in fileIds)
        {
            await fileService.DeleteAsync(fileId, actorUserId, cancellationToken);
        }
    }

    private async Task RetireFeedAsync(
        FileService service, Guid ownerId, Guid actorUserId, CancellationToken cancellationToken)
    {
        var fileIds = await appDbContext.StoredFiles.AsNoTracking()
            .Where(file => file.IsActive && file.Service == service && file.OwnerEntityId == ownerId)
            .Select(file => file.Id)
            .ToListAsync(cancellationToken);
        foreach (var fileId in fileIds)
        {
            await fileService.DeleteAsync(fileId, actorUserId, cancellationToken);
        }
    }

    /// <summary>The URL behind each gallery row's stored file, in one query.
    /// A video resolves to the external link an admin supplied; an image to the
    /// admin asset route, so the editor can show the picture that is attached.</summary>
    private async Task<IReadOnlyDictionary<Guid, string>> ResolveMediaUrlsAsync(
        ArchiveEdition edition, CancellationToken cancellationToken)
    {
        var ids = edition.Media
            .Where(item => item.MediaFileId is not null)
            .Select(item => item.MediaFileId!.Value)
            .ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var files = await appDbContext.StoredFiles.AsNoTracking()
            .Where(file => ids.Contains(file.Id) && file.IsActive)
            .Select(file => new { file.Id, file.SourceType, file.ExternalUrl })
            .ToListAsync(cancellationToken);
        var byFileId = files.ToDictionary(file => file.Id);

        var result = new Dictionary<Guid, string>();
        foreach (var item in edition.Media)
        {
            if (item.MediaFileId is not { } fileId
                || !byFileId.TryGetValue(fileId, out var file))
            {
                continue;
            }
            var url = file.SourceType == FileSourceType.ExternalLink
                ? file.ExternalUrl
                : $"/account/api/admin/assets/{AssetCategory.ArchiveGalleryImage}/{item.Id}/image";
            if (!string.IsNullOrEmpty(url))
            {
                result[item.Id] = url;
            }
        }
        return result;
    }

    /// <summary>The valid Country lookup ids, used to reject a typo'd
    /// country code in the free-text past-speakers editor (drop to null).</summary>
    private async Task<HashSet<int>> LoadCountryIdsAsync(CancellationToken ct) =>
        (await appDbContext.Countries.AsNoTracking()
            .Select(country => country.Id)
            .ToListAsync(ct))
            .ToHashSet();
}
