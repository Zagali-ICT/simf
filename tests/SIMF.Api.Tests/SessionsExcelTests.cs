using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the D-356 Sessions grid Excel engine: the export
/// round-trip (ZIP magic), the Export permission gate, and an insert-only import
/// round-trip keyed on the session code (resolving the mandatory Hall foreign key
/// from its code). Test data carries Guid-unique codes so it is robust on the
/// non-reset integration DB (the session code is the create de-dup key).
/// </summary>
public sealed class SessionsExcelTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public SessionsExcelTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Export_returns_an_xlsx_workbook()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var hallCode = await CreateHallAsync(adminToken);
        await CreateSessionAsync(adminToken, UniqueCode(), hallCode);
        // S-2 — stagger the second same-hall session so it does not overlap.
        await CreateSessionAsync(adminToken, UniqueCode(), hallCode, startHourOffset: 2);

        var response = await PostAuthAsync(
            "/api/v1/admin/sessions/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 100 } },
            adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        // Every .xlsx is a ZIP — the first four bytes are the local-file header.
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
        Assert.Equal(0x03, bytes[2]);
        Assert.Equal(0x04, bytes[3]);
    }

    [Fact]
    public async Task Export_writes_the_saudi_wall_clock_with_no_UTC_marker()
    {
        // The regression this exists for. Until 2026-08-01 the Start/End columns
        // were formatted "yyyy-MM-ddTHH:mm:ss'Z'", appending a literal Z. Since
        // D-813 the stored value IS the Saudi wall clock, so the Z was a false
        // claim: a 09:00 Riyadh session exported as "09:00Z" and read as 06:00 by
        // anything that honours it. SIMF's own import round-tripped it correctly,
        // which is precisely why nothing caught it — the only test on this path
        // asserted the bytes were a ZIP and never opened a cell.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var hallCode = await CreateHallAsync(adminToken);
        var code = UniqueCode();
        await CreateSessionAsync(adminToken, code, hallCode);

        var response = await PostAuthAsync(
            "/api/v1/admin/sessions/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 200 } },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var start = await ReadStartCellAsync(response, code);

        Assert.False(
            start.EndsWith("Z", StringComparison.Ordinal),
            $"The Start cell read '{start}'. A trailing Z declares the value UTC, "
            + "but it is the Saudi wall clock — the workbook would understate every "
            + "session by three hours for any reader outside SIMF.");
        Assert.Equal("2026-01-30T09:00:00", start);
    }

    [Fact]
    public async Task Import_still_accepts_a_legacy_workbook_that_carries_a_Z()
    {
        // Workbooks exported before the fix are already in circulation. They must
        // import to the SAME wall clock, not three hours earlier, or the fix would
        // quietly corrupt every file an admin already has on disk.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var hallCode = await CreateHallAsync(adminToken);
        var code = UniqueCode();
        var workbook = BuildSessionsWorkbook("Sessions",
            (code, "Legacy Z", "قديم", hallCode,
                "2026-02-14T09:00:00Z", "2026-02-14T10:30:00Z"));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/sessions/import", workbook, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var export = await PostAuthAsync(
            "/api/v1/admin/sessions/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 200 } },
            adminToken);
        var start = await ReadStartCellAsync(export, code);

        Assert.Equal("2026-02-14T09:00:00", start);
    }

    /// <summary>The Start cell of the row whose Code column matches, read out of
    /// the returned workbook. Column positions come from the header row rather
    /// than being hard-coded, so adding a column ahead of Start cannot make this
    /// silently assert on the wrong cell.</summary>
    private static async Task<string> ReadStartCellAsync(
        HttpResponseMessage response, string code)
    {
        using var stream = new MemoryStream(await response.Content.ReadAsByteArrayAsync());
        using var book = new ClosedXML.Excel.XLWorkbook(stream);
        var sheet = book.Worksheets.First();

        var header = sheet.Row(1);
        int Column(string name)
        {
            for (var i = 1; i <= header.LastCellUsed()!.Address.ColumnNumber; i++)
            {
                if (string.Equals(header.Cell(i).GetString(), name, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            throw new InvalidOperationException($"No '{name}' column in the export.");
        }

        var codeColumn = Column("Code");
        var startColumn = Column("Start");
        var row = sheet.RowsUsed()
            .FirstOrDefault(r => string.Equals(
                r.Cell(codeColumn).GetString(), code, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"Session {code} is not in the export.");
        return row.Cell(startColumn).GetString();
    }

    [Fact]
    public async Task Import_creates_each_row_and_reports_the_outcome()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var hallCode = await CreateHallAsync(adminToken);
        var codeOne = UniqueCode();
        var codeTwo = UniqueCode();
        var start = "2026-01-30T09:00:00Z";
        var end = "2026-01-30T10:30:00Z";
        // S-2 — the two rows share one hall, so the second must not overlap.
        var startTwo = "2026-01-30T11:00:00Z";
        var endTwo = "2026-01-30T12:30:00Z";
        var workbook = BuildSessionsWorkbook("Sessions",
            (codeOne, "Session One", "جلسة ١", hallCode, start, end),
            (codeTwo, "Session Two", "جلسة ٢", hallCode, startTwo, endTwo));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/sessions/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.True(result.Created >= 2);
        Assert.Empty(result.Errors);

        // The created rows are now listed.
        var list = await PostAuthAsync(
            "/api/v1/admin/sessions/list", new GridQuery { Top = 200 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSessionSummary>>>())!.Data!;
        Assert.Contains(page.Items, item => item.Code == codeOne);
        Assert.Contains(page.Items, item => item.Code == codeTwo);
    }

    [Fact]
    public async Task Import_reports_a_per_row_error_for_an_unknown_hall()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var code = UniqueCode();
        var workbook = BuildSessionsWorkbook("Sessions",
            (code, "Orphan", "يتيمة", "NO-SUCH-HALL",
                "2026-01-30T09:00:00Z", "2026-01-30T10:00:00Z"));

        var response = await PostFileAuthAsync(
            "/api/v1/admin/sessions/import", workbook, adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        // The unresolved hall is a per-row error, not a batch abort.
        Assert.Equal(0, result.Created);
        Assert.Single(result.Errors);
        Assert.Equal(code, result.Errors[0].Key);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_from_export()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/sessions/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 10 } },
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Export_includes_the_dropped_round_trip_columns()
    {
        // D-506 — the session Excel export must surface the eight fields the IO
        // boundary previously dropped, not silently omit them.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var hallCode = await CreateHallAsync(adminToken);
        var code = UniqueCode();

        // Resolve the hall id so the create request can reference it.
        var hallList = await PostAuthAsync(
            "/api/v1/admin/halls/list",
            new GridQuery { Top = 500, Search = hallCode }, adminToken);
        var halls = (await hallList.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminHallSummary>>>())!.Data!;
        var hallId = halls.Items.First(h =>
            string.Equals(h.Code, hallCode, StringComparison.OrdinalIgnoreCase)).Id;

        var create = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = code,
                Title = $"Export Fields {code}",
                TitleArabic = $"تصدير {code}",
                HallId = hallId,
                Start = DateTime.Parse(
                    "2026-01-30T09:00:00Z", CultureInfo.InvariantCulture),
                End = DateTime.Parse(
                    "2026-01-30T10:00:00Z", CultureInfo.InvariantCulture),
                Description = "An opening keynote.",
                DescriptionArabic = "كلمة افتتاحية.",
                LiveStreamUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
                LiveSignLanguageUrl = "https://www.youtube.com/watch?v=aaaaaaaaaaa",
                LiveCaptions = "Welcome.",
                LiveCaptionsArabic = "مرحبا.",
                Type = SessionType.Event,
                SeatSelectionModeOverride = SeatSelectionMode.OpenSeating,
            },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);

        var response = await PostAuthAsync(
            "/api/v1/admin/sessions/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 500 } },
            adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        using var stream = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet("Sessions");
        var headers = sheet.Row(1).CellsUsed().Select(c => c.GetString()).ToList();
        Assert.Contains("Type", headers);
        Assert.Contains("SeatSelectionModeOverride", headers);
        Assert.Contains("Description", headers);
        Assert.Contains("DescriptionArabic", headers);
        Assert.Contains("LiveStreamUrl", headers);
        Assert.Contains("LiveSignLanguageUrl", headers);
        Assert.Contains("LiveCaptions", headers);
        Assert.Contains("LiveCaptionsArabic", headers);

        var codeCol = headers.IndexOf("Code") + 1;
        var typeCol = headers.IndexOf("Type") + 1;
        var descCol = headers.IndexOf("Description") + 1;
        var dataRow = sheet.RowsUsed().Skip(1)
            .First(r => r.Cell(codeCol).GetString() == code);
        Assert.Equal("Event", dataRow.Cell(typeCol).GetString());
        Assert.Equal("An opening keynote.", dataRow.Cell(descCol).GetString());
    }

    [Fact]
    public async Task Import_round_trips_the_dropped_fields()
    {
        // D-506 — an import workbook carrying the eight previously-dropped columns
        // must persist them: Type + SeatSelectionModeOverride surface on the grid
        // summary, the rest on the GET detail.
        var adminToken = await CreateAdministratorAndSignInAsync();
        var hallCode = await CreateHallAsync(adminToken);
        var code = UniqueCode();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sessions");
        sheet.Cell(1, 1).Value = "Code";
        sheet.Cell(1, 2).Value = "Title";
        sheet.Cell(1, 3).Value = "TitleArabic";
        sheet.Cell(1, 4).Value = "Hall";
        sheet.Cell(1, 5).Value = "Start";
        sheet.Cell(1, 6).Value = "End";
        sheet.Cell(1, 7).Value = "Type";
        sheet.Cell(1, 8).Value = "SeatSelectionModeOverride";
        sheet.Cell(1, 9).Value = "Description";
        sheet.Cell(1, 10).Value = "DescriptionArabic";
        sheet.Cell(1, 11).Value = "LiveStreamUrl";
        sheet.Cell(1, 12).Value = "LiveSignLanguageUrl";
        sheet.Cell(1, 13).Value = "LiveCaptions";
        sheet.Cell(1, 14).Value = "LiveCaptionsArabic";
        sheet.Cell(2, 1).Value = code;
        sheet.Cell(2, 2).Value = $"Imported Fields {code}";
        sheet.Cell(2, 3).Value = $"مستورد {code}";
        sheet.Cell(2, 4).Value = hallCode;
        sheet.Cell(2, 5).Value = "2026-01-30T09:00:00Z";
        sheet.Cell(2, 6).Value = "2026-01-30T10:30:00Z";
        sheet.Cell(2, 7).Value = "Event";
        sheet.Cell(2, 8).Value = "OpenSeating";
        sheet.Cell(2, 9).Value = "A closing session.";
        sheet.Cell(2, 10).Value = "جلسة ختامية.";
        sheet.Cell(2, 11).Value = "https://www.youtube.com/watch?v=dQw4w9WgXcQ";
        sheet.Cell(2, 12).Value = "https://www.youtube.com/watch?v=aaaaaaaaaaa";
        sheet.Cell(2, 13).Value = "Goodbye.";
        sheet.Cell(2, 14).Value = "وداعا.";
        byte[] bytes;
        using (var stream = new MemoryStream())
        {
            workbook.SaveAs(stream);
            bytes = stream.ToArray();
        }

        var response = await PostFileAuthAsync(
            "/api/v1/admin/sessions/import", bytes, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.True(result.Created >= 1);
        Assert.Empty(result.Errors);

        // Type + SeatSelectionModeOverride surface on the grid summary.
        var list = await PostAuthAsync(
            "/api/v1/admin/sessions/list", new GridQuery { Top = 200 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSessionSummary>>>())!.Data!;
        var summary = page.Items.Single(item => item.Code == code);
        Assert.Equal(SessionType.Event, summary.Type);
        Assert.Equal(SeatSelectionMode.OpenSeating, summary.SeatSelectionModeOverride);
        Assert.Equal("A closing session.", summary.Description);
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", summary.LiveStreamUrl);

        // The remaining fields round-trip through the GET detail.
        var detailResponse = await GetAuthAsync(
            $"/api/v1/admin/sessions/{summary.Id}", adminToken);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = (await detailResponse.Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal("A closing session.", detail.Description);
        Assert.Equal("جلسة ختامية.", detail.DescriptionArabic);
        Assert.Equal("https://www.youtube.com/watch?v=dQw4w9WgXcQ", detail.LiveStreamUrl);
        Assert.Equal("https://www.youtube.com/watch?v=aaaaaaaaaaa", detail.LiveSignLanguageUrl);
        Assert.Equal("Goodbye.", detail.LiveCaptions);
        Assert.Equal("وداعا.", detail.LiveCaptionsArabic);
        Assert.Equal(SessionType.Event, detail.Type);
        Assert.Equal(SeatSelectionMode.OpenSeating, detail.SeatSelectionModeOverride);
    }

    // #4 — the import reads an optional Speakers column of active speaker codes and
    // attaches them in listed order (default Speaker role).
    [Fact]
    public async Task Import_attaches_the_speakers_column_in_order()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var hallCode = await CreateHallAsync(adminToken);
        var code = UniqueCode();
        var firstCode = ("SPK" + Guid.NewGuid().ToString("N"))[..9].ToUpperInvariant();
        var secondCode = ("SPK" + Guid.NewGuid().ToString("N"))[..9].ToUpperInvariant();
        var firstId = await SeedSpeakerAsync(firstCode);
        var secondId = await SeedSpeakerAsync(secondCode);

        var workbook = BuildTypedRowWorkbook(
            code, hallCode, "Session", $"{firstCode}, {secondCode}");
        var response = await PostFileAuthAsync(
            "/api/v1/admin/sessions/import", workbook, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.True(result.Created >= 1);
        Assert.Empty(result.Errors);

        var list = await PostAuthAsync(
            "/api/v1/admin/sessions/list", new GridQuery { Top = 200 }, adminToken);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSessionSummary>>>())!.Data!;
        var summary = page.Items.Single(item => item.Code == code);
        var detail = (await (await GetAuthAsync(
            $"/api/v1/admin/sessions/{summary.Id}", adminToken)).Content
            .ReadFromJsonAsync<ApiResult<AdminSessionDetail>>())!.Data!;
        Assert.Equal(2, detail.Speakers.Count);
        Assert.Equal(firstId, detail.Speakers.Single(s => s.DisplayOrder == 0).SpeakerId);
        Assert.Equal(secondId, detail.Speakers.Single(s => s.DisplayOrder == 1).SpeakerId);
    }

    // #4 — a non-Event import row with no Speakers cell is a per-row error.
    [Fact]
    public async Task Import_non_event_row_without_speakers_is_a_per_row_error()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var hallCode = await CreateHallAsync(adminToken);
        var code = UniqueCode();

        var workbook = BuildTypedRowWorkbook(code, hallCode, "Session", speakers: null);
        var response = await PostFileAuthAsync(
            "/api/v1/admin/sessions/import", workbook, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(0, result.Created);
        Assert.Single(result.Errors);
        Assert.Equal(code, result.Errors[0].Key);
    }

    // The Speakers column rejects a code that is not an active speaker.
    [Fact]
    public async Task Import_unknown_speaker_code_is_a_per_row_error()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var hallCode = await CreateHallAsync(adminToken);
        var code = UniqueCode();

        var workbook = BuildTypedRowWorkbook(code, hallCode, "Session", "NO-SUCH-SPK");
        var response = await PostFileAuthAsync(
            "/api/v1/admin/sessions/import", workbook, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(0, result.Created);
        Assert.Single(result.Errors);
        Assert.Equal(code, result.Errors[0].Key);
    }

    // #3 — an import row with a blank Type is a per-row error (a speaker is supplied
    // so the failure isolates the missing type).
    [Fact]
    public async Task Import_row_without_a_type_is_a_per_row_error()
    {
        var adminToken = await CreateAdministratorAndSignInAsync();
        var hallCode = await CreateHallAsync(adminToken);
        var code = UniqueCode();
        var speakerCode = ("SPK" + Guid.NewGuid().ToString("N"))[..9].ToUpperInvariant();
        await SeedSpeakerAsync(speakerCode);

        var workbook = BuildTypedRowWorkbook(code, hallCode, type: null, speakerCode);
        var response = await PostFileAuthAsync(
            "/api/v1/admin/sessions/import", workbook, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminGridImportResult>>())!.Data!;
        Assert.Equal(0, result.Created);
        Assert.Single(result.Errors);
        Assert.Equal(code, result.Errors[0].Key);
    }

    // -- Helpers ---------------------------------------------------------------

    // A unique session/hall code within the 2–16 char / uppercase rule (the
    // create de-dup key) so the test is robust on the non-reset integration DB.
    private static string UniqueCode() =>
        ("S" + Guid.NewGuid().ToString("N"))[..12].ToUpperInvariant();

    private static byte[] BuildSessionsWorkbook(
        string sheetName,
        params (string Code, string Title, string TitleArabic, string Hall,
            string Start, string End)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(sheetName);
        sheet.Cell(1, 1).Value = "Code";
        sheet.Cell(1, 2).Value = "Title";
        sheet.Cell(1, 3).Value = "TitleArabic";
        sheet.Cell(1, 4).Value = "Hall";
        sheet.Cell(1, 5).Value = "Start";
        sheet.Cell(1, 6).Value = "End";
        sheet.Cell(1, 7).Value = "Type";
        for (var i = 0; i < rows.Length; i++)
        {
            sheet.Cell(i + 2, 1).Value = rows[i].Code;
            sheet.Cell(i + 2, 2).Value = rows[i].Title;
            sheet.Cell(i + 2, 3).Value = rows[i].TitleArabic;
            sheet.Cell(i + 2, 4).Value = rows[i].Hall;
            sheet.Cell(i + 2, 5).Value = rows[i].Start;
            sheet.Cell(i + 2, 6).Value = rows[i].End;
            // #3 — every generated row is an Event so the shared builder stays valid
            // under the required-type rule (an Event needs no speaker).
            sheet.Cell(i + 2, 7).Value = "Event";
        }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // Builds a single-row Sessions workbook carrying the Type + Speakers columns so
    // the #3/#4 import paths can be exercised; a null type/speakers leaves the cell
    // blank (the header still exists).
    private static byte[] BuildTypedRowWorkbook(
        string code, string hallCode, string? type, string? speakers)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Sessions");
        sheet.Cell(1, 1).Value = "Code";
        sheet.Cell(1, 2).Value = "Title";
        sheet.Cell(1, 3).Value = "TitleArabic";
        sheet.Cell(1, 4).Value = "Hall";
        sheet.Cell(1, 5).Value = "Start";
        sheet.Cell(1, 6).Value = "End";
        sheet.Cell(1, 7).Value = "Type";
        sheet.Cell(1, 8).Value = "Speakers";
        sheet.Cell(2, 1).Value = code;
        sheet.Cell(2, 2).Value = $"Session {code}";
        sheet.Cell(2, 3).Value = $"جلسة {code}";
        sheet.Cell(2, 4).Value = hallCode;
        sheet.Cell(2, 5).Value = "2026-02-15T09:00:00Z";
        sheet.Cell(2, 6).Value = "2026-02-15T10:00:00Z";
        if (type is not null) { sheet.Cell(2, 7).Value = type; }
        if (speakers is not null) { sheet.Cell(2, 8).Value = speakers; }
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    // Seeds an active speaker with a known code (the import resolves speakers by code).
    private async Task<Guid> SeedSpeakerAsync(string code)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var speaker = new Speaker
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = $"Speaker {code}",
            NameArabic = $"متحدّث {code}",
            DisplayOrder = 0,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        db.Speakers.Add(speaker);
        await db.SaveChangesAsync();
        return speaker.Id;
    }

    // Creates an active hall and returns its (unique) code so the imported /
    // created sessions can reference it by its natural key.
    private async Task<string> CreateHallAsync(string token)
    {
        var code = UniqueCode();
        var response = await PostAuthAsync(
            "/api/v1/admin/halls",
            new AdminCreateHallRequest
            {
                Code = code,
                Name = $"Hall {code}",
                NameArabic = $"قاعة {code}",
                Capacity = 200,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return code;
    }

    private async Task CreateSessionAsync(
        string token, string code, string hallCode, int startHourOffset = 0)
    {
        // Resolve the hall id from its code (the create request takes the id).
        var list = await PostAuthAsync(
            "/api/v1/admin/halls/list",
            new GridQuery { Top = 500, Search = hallCode },
            token);
        var halls = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminHallSummary>>>())!.Data!;
        var hallId = halls.Items.First(h =>
            string.Equals(h.Code, hallCode, StringComparison.OrdinalIgnoreCase)).Id;

        // S-2 — two sessions in the same hall must not overlap; callers seeding a
        // second session in the same hall pass a startHourOffset to stagger it.
        var response = await PostAuthAsync(
            "/api/v1/admin/sessions",
            new AdminCreateSessionRequest
            {
                Code = code,
                Title = $"Session {code}",
                TitleArabic = $"جلسة {code}",
                HallId = hallId,
                // #3 — an Event stays valid under the required-type rule with no speaker.
                Type = SessionType.Event,
                // Zone-free literals. These read "...T09:00:00Z" until 2026-08-01,
                // and DateTime.Parse with default styles converts a Z-suffixed
                // string to the MACHINE's local time — so the fixture seeded 09:00
                // on a differently-zoned runner and 12:00 on a Riyadh workstation. Nothing
                // asserted on the time, so it never failed; it just meant the
                // fixture's data depended on where it ran. Under D-813 a test
                // timestamp is a plain wall clock, like every other one in SIMF.
                Start = DateTime.Parse(
                    "2026-01-30T09:00:00", CultureInfo.InvariantCulture)
                    .AddHours(startHourOffset),
                End = DateTime.Parse(
                    "2026-01-30T10:00:00", CultureInfo.InvariantCulture)
                    .AddHours(startHourOffset),
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"session-xlsx-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AdministratorRole))
            {
                await roles.CreateAsync(new SimfRole { Name = AdministratorRole });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Test Session Excel Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostFileAuthAsync(string url, byte[] xlsx, string token)
    {
        var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(xlsx);
        file.Headers.ContentType = new MediaTypeHeaderValue(XlsxContentType);
        content.Add(file, "file", "import.xlsx");
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
