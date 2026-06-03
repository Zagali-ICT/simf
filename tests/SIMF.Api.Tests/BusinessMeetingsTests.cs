// SIMF-FDS-013 (D-248) — admin-arranged B2B/B2C business meetings + flexible
// hall configuration (purpose, meeting tables, hall allocations). Mirrors
// AdminBoothsTests.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.BusinessMeetings;
using SIMF.Domain.Companies;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class BusinessMeetingsTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public BusinessMeetingsTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Set_purpose_then_create_table_then_schedule_b2b_meeting()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.General);

        // Set the hall purpose to Meeting.
        var setPurpose = await PutAuthAsync(
            $"/api/v1/admin/halls/{hallId}/purpose",
            new SetHallPurposeRequest { Purpose = HallPurpose.Meeting }, token);
        Assert.Equal(HttpStatusCode.OK, setPurpose.StatusCode);

        var tableId = await CreateTableAsync(hallId, token, capacity: 4);
        var (a, b) = (await SeedCompanyAsync(), await SeedCompanyAsync());

        var start = DateTimeOffset.UtcNow.AddDays(1);
        var schedule = await PostAuthAsync(
            "/api/v1/admin/business-meetings",
            new ScheduleMeetingRequest
            {
                MeetingTableId = tableId,
                MeetingType = BusinessMeetingType.B2B,
                StartUtc = start,
                EndUtc = start.AddHours(1),
                Participants =
                [
                    new() { Kind = MeetingPartyKind.Company, CompanyId = a },
                    new() { Kind = MeetingPartyKind.Company, CompanyId = b },
                ],
            }, token);
        Assert.Equal(HttpStatusCode.OK, schedule.StatusCode);
        var scheduled = (await schedule.Content
            .ReadFromJsonAsync<ApiResult<BusinessMeetingScheduled>>())!.Data!;
        Assert.Equal(BusinessMeetingStatus.Confirmed, scheduled.Status);

        var get = await GetAuthAsync($"/api/v1/admin/business-meetings/{scheduled.Id}", token);
        var detail = (await get.Content
            .ReadFromJsonAsync<ApiResult<BusinessMeetingDetail>>())!.Data!;
        Assert.Equal(2, detail.Participants.Count);
        Assert.Equal(BusinessMeetingType.B2B, detail.MeetingType);
    }

    [Fact]
    public async Task Schedule_with_fewer_than_two_participants_is_400()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var tableId = await CreateTableAsync(hallId, token);
        var company = await SeedCompanyAsync();
        var start = DateTimeOffset.UtcNow.AddDays(1);

        var response = await PostAuthAsync(
            "/api/v1/admin/business-meetings",
            new ScheduleMeetingRequest
            {
                MeetingTableId = tableId,
                MeetingType = BusinessMeetingType.B2B,
                StartUtc = start,
                EndUtc = start.AddHours(1),
                Participants = [new() { Kind = MeetingPartyKind.Company, CompanyId = company }],
            }, token);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.MeetingParticipantInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Overlapping_meeting_on_the_same_table_is_409_table_conflict()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var tableId = await CreateTableAsync(hallId, token);
        var start = DateTimeOffset.UtcNow.AddDays(2);

        var first = await ScheduleAsync(tableId, token, start, start.AddHours(1),
            await SeedCompanyAsync(), await SeedCompanyAsync());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await ScheduleAsync(tableId, token, start.AddMinutes(30), start.AddHours(2),
            await SeedCompanyAsync(), await SeedCompanyAsync());
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BusinessMeetingTableConflict, body.Error!.Code);
    }

    [Fact]
    public async Task Same_party_in_two_overlapping_meetings_is_409_participant_conflict()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var table1 = await CreateTableAsync(hallId, token);
        var table2 = await CreateTableAsync(hallId, token);
        var shared = await SeedCompanyAsync();
        var start = DateTimeOffset.UtcNow.AddDays(3);

        var first = await ScheduleAsync(table1, token, start, start.AddHours(1),
            shared, await SeedCompanyAsync());
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await ScheduleAsync(table2, token, start.AddMinutes(30), start.AddHours(2),
            shared, await SeedCompanyAsync());
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BusinessMeetingParticipantConflict, body.Error!.Code);
    }

    [Fact]
    public async Task Participants_over_table_capacity_is_409()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var tableId = await CreateTableAsync(hallId, token, capacity: 2);
        var start = DateTimeOffset.UtcNow.AddDays(4);

        var response = await PostAuthAsync(
            "/api/v1/admin/business-meetings",
            new ScheduleMeetingRequest
            {
                MeetingTableId = tableId,
                MeetingType = BusinessMeetingType.B2B,
                StartUtc = start,
                EndUtc = start.AddHours(1),
                Participants =
                [
                    new() { Kind = MeetingPartyKind.Company, CompanyId = await SeedCompanyAsync() },
                    new() { Kind = MeetingPartyKind.Company, CompanyId = await SeedCompanyAsync() },
                    new() { Kind = MeetingPartyKind.Company, CompanyId = await SeedCompanyAsync() },
                ],
            }, token);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.MeetingCapacityExceeded, body.Error!.Code);
    }

    [Fact]
    public async Task Schedule_b2c_with_a_visitor_then_cancel()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var tableId = await CreateTableAsync(hallId, token);
        var company = await SeedCompanyAsync();
        var visitor = await SeedVisitorAsync();
        var start = DateTimeOffset.UtcNow.AddDays(5);

        var schedule = await PostAuthAsync(
            "/api/v1/admin/business-meetings",
            new ScheduleMeetingRequest
            {
                MeetingTableId = tableId,
                MeetingType = BusinessMeetingType.B2C,
                StartUtc = start,
                EndUtc = start.AddHours(1),
                Participants =
                [
                    new() { Kind = MeetingPartyKind.Company, CompanyId = company },
                    new() { Kind = MeetingPartyKind.Visitor, VisitorUserId = visitor },
                ],
            }, token);
        Assert.Equal(HttpStatusCode.OK, schedule.StatusCode);
        var scheduled = (await schedule.Content
            .ReadFromJsonAsync<ApiResult<BusinessMeetingScheduled>>())!.Data!;

        var cancel = await PostAuthAsync(
            $"/api/v1/admin/business-meetings/{scheduled.Id}/cancel",
            new CancelMeetingRequest { Reason = "Rescheduled by organiser." }, token);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var get = await GetAuthAsync($"/api/v1/admin/business-meetings/{scheduled.Id}", token);
        var detail = (await get.Content
            .ReadFromJsonAsync<ApiResult<BusinessMeetingDetail>>())!.Data!;
        Assert.Equal(BusinessMeetingStatus.Cancelled, detail.Status);
        Assert.Equal("Rescheduled by organiser.", detail.CancellationReason);

        // The slot is free again — a fresh meeting on the same table/slot succeeds.
        var reuse = await ScheduleAsync(tableId, token, start, start.AddHours(1),
            await SeedCompanyAsync(), await SeedCompanyAsync());
        Assert.Equal(HttpStatusCode.OK, reuse.StatusCode);
    }

    [Fact]
    public async Task Generate_random_by_count_creates_tables()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);

        var generate = await PostAuthAsync(
            $"/api/v1/admin/halls/{hallId}/meeting-tables/generate",
            new GenerateMeetingTablesRequest
            {
                Mode = HallAllocationMode.RandomByCount,
                Count = 6,
                Capacity = 2,
            }, token);
        Assert.Equal(HttpStatusCode.OK, generate.StatusCode);
        var result = (await generate.Content
            .ReadFromJsonAsync<ApiResult<MeetingTablesGenerated>>())!.Data!;
        Assert.Equal(6, result.Created);

        var list = await PostAuthAsync(
            $"/api/v1/admin/halls/{hallId}/meeting-tables/list",
            new { Skip = 0, Top = 100 }, token);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<MeetingTableRow>>>())!.Data!;
        Assert.Equal(6, page.Total);
    }

    [Fact]
    public async Task Creating_a_table_in_a_session_hall_is_409()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Session);

        var response = await PostAuthAsync(
            $"/api/v1/admin/halls/{hallId}/meeting-tables",
            new CreateMeetingTableRequest { Code = "T-1", Capacity = 2 }, token);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.HallNotMeetingPurpose, body.Error!.Code);
    }

    [Fact]
    public async Task Overlapping_hall_allocation_is_409()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var start = DateTimeOffset.UtcNow.AddDays(6);

        var first = await PostAuthAsync(
            $"/api/v1/admin/halls/{hallId}/hall-allocations",
            new CreateHallAllocationRequest
            {
                Purpose = HallPurpose.Meeting,
                Mode = HallAllocationMode.Whole,
                StartUtc = start,
                EndUtc = start.AddHours(2),
            }, token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync(
            $"/api/v1/admin/halls/{hallId}/hall-allocations",
            new CreateHallAllocationRequest
            {
                Purpose = HallPurpose.Session,
                Mode = HallAllocationMode.Whole,
                StartUtc = start.AddHours(1),
                EndUtc = start.AddHours(3),
            }, token);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.HallAllocationOverlap, body.Error!.Code);
    }

    [Fact]
    public async Task Non_admin_caller_is_forbidden_on_schedule()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);
        var start = DateTimeOffset.UtcNow.AddDays(7);
        var response = await PostAuthAsync(
            "/api/v1/admin/business-meetings",
            new ScheduleMeetingRequest
            {
                MeetingTableId = Guid.NewGuid(),
                MeetingType = BusinessMeetingType.B2B,
                StartUtc = start,
                EndUtc = start.AddHours(1),
                Participants =
                [
                    new() { Kind = MeetingPartyKind.Company, CompanyId = Guid.NewGuid() },
                    new() { Kind = MeetingPartyKind.Company, CompanyId = Guid.NewGuid() },
                ],
            }, tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // -- D-248 review-hardening regression tests ------------------------------

    [Fact]
    public async Task Delete_table_with_a_confirmed_upcoming_meeting_is_409()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var tableId = await CreateTableAsync(hallId, token, capacity: 4);
        var start = DateTimeOffset.UtcNow.AddDays(10);
        await ScheduleConfirmedAsync(tableId, token, start, start.AddHours(1));

        var del = await DeleteAuthAsync($"/api/v1/admin/meeting-tables/{tableId}", token);
        Assert.Equal(HttpStatusCode.Conflict, del.StatusCode);
        var body = (await del.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.MeetingTableInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Delete_table_with_no_meetings_succeeds()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var tableId = await CreateTableAsync(hallId, token);

        var del = await DeleteAuthAsync($"/api/v1/admin/meeting-tables/{tableId}", token);
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);

        var list = await PostAuthAsync(
            $"/api/v1/admin/halls/{hallId}/meeting-tables/list", new { Skip = 0, Top = 100 }, token);
        var page = (await list.Content.ReadFromJsonAsync<ApiResult<GridPage<MeetingTableRow>>>())!.Data!;
        Assert.DoesNotContain(page.Items, t => t.Id == tableId);
    }

    [Fact]
    public async Task Cancel_an_already_cancelled_meeting_is_409()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var tableId = await CreateTableAsync(hallId, token);
        var start = DateTimeOffset.UtcNow.AddDays(11);
        var id = await ScheduleConfirmedAsync(tableId, token, start, start.AddHours(1));

        var first = await PostAuthAsync($"/api/v1/admin/business-meetings/{id}/cancel",
            new CancelMeetingRequest(), token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostAuthAsync($"/api/v1/admin/business-meetings/{id}/cancel",
            new CancelMeetingRequest(), token);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BusinessMeetingNotConfirmed, body.Error!.Code);
    }

    [Fact]
    public async Task Cancel_unknown_meeting_is_404()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var resp = await PostAuthAsync(
            $"/api/v1/admin/business-meetings/{Guid.NewGuid()}/cancel",
            new CancelMeetingRequest(), token);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var body = (await resp.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BusinessMeetingNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Schedule_with_unknown_company_is_400()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var tableId = await CreateTableAsync(hallId, token);
        var start = DateTimeOffset.UtcNow.AddDays(12);

        var resp = await PostAuthAsync("/api/v1/admin/business-meetings",
            new ScheduleMeetingRequest
            {
                MeetingTableId = tableId,
                MeetingType = BusinessMeetingType.B2B,
                StartUtc = start,
                EndUtc = start.AddHours(1),
                Participants =
                [
                    new() { Kind = MeetingPartyKind.Company, CompanyId = await SeedCompanyAsync() },
                    new() { Kind = MeetingPartyKind.Company, CompanyId = Guid.NewGuid() },
                ],
            }, token);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = (await resp.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.MeetingParticipantInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Schedule_with_unknown_visitor_is_400()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var tableId = await CreateTableAsync(hallId, token);
        var start = DateTimeOffset.UtcNow.AddDays(13);

        var resp = await PostAuthAsync("/api/v1/admin/business-meetings",
            new ScheduleMeetingRequest
            {
                MeetingTableId = tableId,
                MeetingType = BusinessMeetingType.B2C,
                StartUtc = start,
                EndUtc = start.AddHours(1),
                Participants =
                [
                    new() { Kind = MeetingPartyKind.Company, CompanyId = await SeedCompanyAsync() },
                    new() { Kind = MeetingPartyKind.Visitor, VisitorUserId = Guid.NewGuid() },
                ],
            }, token);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = (await resp.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.MeetingParticipantInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Schedule_with_a_non_visitor_user_as_visitor_is_400()
    {
        // An Admin user id must not be acceptable as a "visitor" party.
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var tableId = await CreateTableAsync(hallId, token);
        var adminUserId = await SeedUserAsync(UserType.Admin, AccountState.Approved);
        var start = DateTimeOffset.UtcNow.AddDays(14);

        var resp = await PostAuthAsync("/api/v1/admin/business-meetings",
            new ScheduleMeetingRequest
            {
                MeetingTableId = tableId,
                MeetingType = BusinessMeetingType.B2C,
                StartUtc = start,
                EndUtc = start.AddHours(1),
                Participants =
                [
                    new() { Kind = MeetingPartyKind.Company, CompanyId = await SeedCompanyAsync() },
                    new() { Kind = MeetingPartyKind.Visitor, VisitorUserId = adminUserId },
                ],
            }, token);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = (await resp.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.MeetingParticipantInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Schedule_with_the_same_company_twice_is_400()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var tableId = await CreateTableAsync(hallId, token, capacity: 4);
        var company = await SeedCompanyAsync();
        var start = DateTimeOffset.UtcNow.AddDays(15);

        var resp = await PostAuthAsync("/api/v1/admin/business-meetings",
            new ScheduleMeetingRequest
            {
                MeetingTableId = tableId,
                MeetingType = BusinessMeetingType.B2B,
                StartUtc = start,
                EndUtc = start.AddHours(1),
                Participants =
                [
                    new() { Kind = MeetingPartyKind.Company, CompanyId = company },
                    new() { Kind = MeetingPartyKind.Company, CompanyId = company },
                ],
            }, token);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = (await resp.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.MeetingParticipantInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Schedule_company_participant_without_company_id_is_400()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var tableId = await CreateTableAsync(hallId, token);
        var start = DateTimeOffset.UtcNow.AddDays(16);

        var resp = await PostAuthAsync("/api/v1/admin/business-meetings",
            new ScheduleMeetingRequest
            {
                MeetingTableId = tableId,
                MeetingType = BusinessMeetingType.B2B,
                StartUtc = start,
                EndUtc = start.AddHours(1),
                Participants =
                [
                    new() { Kind = MeetingPartyKind.Company, CompanyId = await SeedCompanyAsync() },
                    new() { Kind = MeetingPartyKind.Company, CompanyId = null },
                ],
            }, token);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Schedule_on_unknown_table_is_404()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var start = DateTimeOffset.UtcNow.AddDays(17);
        var resp = await PostAuthAsync("/api/v1/admin/business-meetings",
            new ScheduleMeetingRequest
            {
                MeetingTableId = Guid.NewGuid(),
                MeetingType = BusinessMeetingType.B2B,
                StartUtc = start,
                EndUtc = start.AddHours(1),
                Participants =
                [
                    new() { Kind = MeetingPartyKind.Company, CompanyId = await SeedCompanyAsync() },
                    new() { Kind = MeetingPartyKind.Company, CompanyId = await SeedCompanyAsync() },
                ],
            }, token);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var body = (await resp.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.MeetingTableNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Get_unknown_meeting_is_404()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var resp = await GetAuthAsync($"/api/v1/admin/business-meetings/{Guid.NewGuid()}", token);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        var body = (await resp.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BusinessMeetingNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task A_whole_hall_session_allocation_blocks_a_meeting_in_that_hall()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var tableId = await CreateTableAsync(hallId, token);
        var start = DateTimeOffset.UtcNow.AddDays(18);

        var alloc = await PostAuthAsync($"/api/v1/admin/halls/{hallId}/hall-allocations",
            new CreateHallAllocationRequest
            {
                Purpose = HallPurpose.Session,
                Mode = HallAllocationMode.Whole,
                StartUtc = start,
                EndUtc = start.AddHours(3),
            }, token);
        Assert.Equal(HttpStatusCode.OK, alloc.StatusCode);

        var resp = await ScheduleAsync(tableId, token, start.AddMinutes(30), start.AddHours(1),
            await SeedCompanyAsync(), await SeedCompanyAsync());
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var body = (await resp.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.BusinessMeetingTableConflict, body.Error!.Code);
    }

    [Fact]
    public async Task Create_duplicate_table_code_in_same_hall_is_409()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var first = await PostAuthAsync($"/api/v1/admin/halls/{hallId}/meeting-tables",
            new CreateMeetingTableRequest { Code = "T-1", Capacity = 2 }, token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Case-insensitive: 't-1' must also clash.
        var dup = await PostAuthAsync($"/api/v1/admin/halls/{hallId}/meeting-tables",
            new CreateMeetingTableRequest { Code = "t-1", Capacity = 2 }, token);
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
        var body = (await dup.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.MeetingTableCodeDuplicate, body.Error!.Code);
    }

    [Fact]
    public async Task Update_table_fields_then_keeping_same_code_succeeds_and_unknown_is_404()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var tableId = await CreateTableAsync(hallId, token, capacity: 2);

        // Happy path: change capacity, keep the (same) code -> 200.
        var get = await PostAuthAsync($"/api/v1/admin/halls/{hallId}/meeting-tables/list",
            new { Skip = 0, Top = 100 }, token);
        var row = (await get.Content.ReadFromJsonAsync<ApiResult<GridPage<MeetingTableRow>>>())!
            .Data!.Items.Single(t => t.Id == tableId);
        var upd = await PutAuthAsync($"/api/v1/admin/meeting-tables/{tableId}",
            new UpdateMeetingTableRequest { Code = row.Code, Capacity = 8 }, token);
        Assert.Equal(HttpStatusCode.OK, upd.StatusCode);
        var updated = (await upd.Content.ReadFromJsonAsync<ApiResult<MeetingTableRow>>())!.Data!;
        Assert.Equal(8, updated.Capacity);

        var unknown = await PutAuthAsync($"/api/v1/admin/meeting-tables/{Guid.NewGuid()}",
            new UpdateMeetingTableRequest { Code = "X-1", Capacity = 2 }, token);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        var body = (await unknown.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.MeetingTableNotFound, body.Error!.Code);
    }

    [Fact]
    public async Task Create_table_with_capacity_out_of_range_is_400()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);

        var low = await PostAuthAsync($"/api/v1/admin/halls/{hallId}/meeting-tables",
            new CreateMeetingTableRequest { Code = "L-1", Capacity = 1 }, token);
        Assert.Equal(HttpStatusCode.BadRequest, low.StatusCode);

        var high = await PostAuthAsync($"/api/v1/admin/halls/{hallId}/meeting-tables",
            new CreateMeetingTableRequest { Code = "H-1", Capacity = 101 }, token);
        Assert.Equal(HttpStatusCode.BadRequest, high.StatusCode);
    }

    [Fact]
    public async Task Generate_by_row_column_parses_row_and_column_and_skips_duplicates()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);

        var gen = await PostAuthAsync($"/api/v1/admin/halls/{hallId}/meeting-tables/generate",
            new GenerateMeetingTablesRequest
            {
                Mode = HallAllocationMode.RowColumn,
                RowColumnSpec = "A1,A1,B3",
                Capacity = 2,
            }, token);
        Assert.Equal(HttpStatusCode.OK, gen.StatusCode);
        var result = (await gen.Content.ReadFromJsonAsync<ApiResult<MeetingTablesGenerated>>())!.Data!;
        Assert.Equal(2, result.Created); // A1 deduped

        var list = await PostAuthAsync($"/api/v1/admin/halls/{hallId}/meeting-tables/list",
            new { Skip = 0, Top = 100 }, token);
        var page = (await list.Content.ReadFromJsonAsync<ApiResult<GridPage<MeetingTableRow>>>())!.Data!;
        var a1 = page.Items.Single(t => t.Code == "A1");
        Assert.Equal("A", a1.RowLabel);
        Assert.Equal(1, a1.ColumnNumber);
    }

    [Fact]
    public async Task Generate_with_reset_removes_existing_tables()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        await PostAuthAsync($"/api/v1/admin/halls/{hallId}/meeting-tables/generate",
            new GenerateMeetingTablesRequest { Mode = HallAllocationMode.RandomByCount, Count = 4, Capacity = 2 }, token);

        var gen = await PostAuthAsync($"/api/v1/admin/halls/{hallId}/meeting-tables/generate",
            new GenerateMeetingTablesRequest
            { Mode = HallAllocationMode.RandomByCount, Count = 3, Capacity = 2, Reset = true }, token);
        var result = (await gen.Content.ReadFromJsonAsync<ApiResult<MeetingTablesGenerated>>())!.Data!;
        Assert.Equal(4, result.Removed);
        Assert.Equal(3, result.Created);

        var list = await PostAuthAsync($"/api/v1/admin/halls/{hallId}/meeting-tables/list",
            new { Skip = 0, Top = 100 }, token);
        var page = (await list.Content.ReadFromJsonAsync<ApiResult<GridPage<MeetingTableRow>>>())!.Data!;
        Assert.Equal(3, page.Total);
    }

    [Fact]
    public async Task Generate_random_count_is_capped_at_hall_capacity()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting, capacity: 3);

        var gen = await PostAuthAsync($"/api/v1/admin/halls/{hallId}/meeting-tables/generate",
            new GenerateMeetingTablesRequest { Mode = HallAllocationMode.RandomByCount, Count = 10, Capacity = 2 }, token);
        var result = (await gen.Content.ReadFromJsonAsync<ApiResult<MeetingTablesGenerated>>())!.Data!;
        Assert.Equal(3, result.Created);
    }

    [Fact]
    public async Task Generate_invalid_modes_and_specs_are_rejected()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);

        var zero = await PostAuthAsync($"/api/v1/admin/halls/{hallId}/meeting-tables/generate",
            new GenerateMeetingTablesRequest { Mode = HallAllocationMode.RandomByCount, Count = 0, Capacity = 2 }, token);
        Assert.Equal(HttpStatusCode.BadRequest, zero.StatusCode);

        var emptySpec = await PostAuthAsync($"/api/v1/admin/halls/{hallId}/meeting-tables/generate",
            new GenerateMeetingTablesRequest { Mode = HallAllocationMode.RowColumn, RowColumnSpec = "  ", Capacity = 2 }, token);
        Assert.Equal(HttpStatusCode.BadRequest, emptySpec.StatusCode);

        var whole = await PostAuthAsync($"/api/v1/admin/halls/{hallId}/meeting-tables/generate",
            new GenerateMeetingTablesRequest { Mode = HallAllocationMode.Whole, Capacity = 2 }, token);
        Assert.Equal(HttpStatusCode.BadRequest, whole.StatusCode);
    }

    [Fact]
    public async Task Generate_in_a_session_hall_is_409()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Session);
        var gen = await PostAuthAsync($"/api/v1/admin/halls/{hallId}/meeting-tables/generate",
            new GenerateMeetingTablesRequest { Mode = HallAllocationMode.RandomByCount, Count = 2, Capacity = 2 }, token);
        Assert.Equal(HttpStatusCode.Conflict, gen.StatusCode);
        var body = (await gen.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.HallNotMeetingPurpose, body.Error!.Code);
    }

    [Fact]
    public async Task Create_allocation_invalid_inputs_are_rejected()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var start = DateTimeOffset.UtcNow.AddDays(19);

        var badSlot = await PostAuthAsync($"/api/v1/admin/halls/{hallId}/hall-allocations",
            new CreateHallAllocationRequest
            { Purpose = HallPurpose.Meeting, Mode = HallAllocationMode.Whole, StartUtc = start, EndUtc = start }, token);
        Assert.Equal(HttpStatusCode.BadRequest, badSlot.StatusCode);

        var noCount = await PostAuthAsync($"/api/v1/admin/halls/{hallId}/hall-allocations",
            new CreateHallAllocationRequest
            { Purpose = HallPurpose.Meeting, Mode = HallAllocationMode.RandomByCount, StartUtc = start, EndUtc = start.AddHours(1) }, token);
        Assert.Equal(HttpStatusCode.BadRequest, noCount.StatusCode);

        var unknownHall = await PostAuthAsync($"/api/v1/admin/halls/{Guid.NewGuid()}/hall-allocations",
            new CreateHallAllocationRequest
            { Purpose = HallPurpose.Meeting, Mode = HallAllocationMode.Whole, StartUtc = start, EndUtc = start.AddHours(1) }, token);
        Assert.Equal(HttpStatusCode.NotFound, unknownHall.StatusCode);
    }

    [Fact]
    public async Task Release_allocation_removes_it_and_second_release_is_404()
    {
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var start = DateTimeOffset.UtcNow.AddDays(20);
        var create = await PostAuthAsync($"/api/v1/admin/halls/{hallId}/hall-allocations",
            new CreateHallAllocationRequest
            { Purpose = HallPurpose.Meeting, Mode = HallAllocationMode.Whole, StartUtc = start, EndUtc = start.AddHours(2) }, token);
        var allocId = (await create.Content.ReadFromJsonAsync<ApiResult<HallAllocationRow>>())!.Data!.Id;

        var rel = await DeleteAuthAsync($"/api/v1/admin/hall-allocations/{allocId}", token);
        Assert.Equal(HttpStatusCode.OK, rel.StatusCode);

        var list = await PostAuthAsync($"/api/v1/admin/halls/{hallId}/hall-allocations/list",
            new { Skip = 0, Top = 100 }, token);
        var page = (await list.Content.ReadFromJsonAsync<ApiResult<GridPage<HallAllocationRow>>>())!.Data!;
        Assert.DoesNotContain(page.Items, a => a.Id == allocId);

        var again = await DeleteAuthAsync($"/api/v1/admin/hall-allocations/{allocId}", token);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        var body = (await again.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.HallAllocationNotFound, body.Error!.Code);
    }

    // -- Helpers --------------------------------------------------------------

    private Task<HttpResponseMessage> ScheduleAsync(
        Guid tableId, string token, DateTimeOffset start, DateTimeOffset end,
        Guid companyA, Guid companyB) =>
        PostAuthAsync(
            "/api/v1/admin/business-meetings",
            new ScheduleMeetingRequest
            {
                MeetingTableId = tableId,
                MeetingType = BusinessMeetingType.B2B,
                StartUtc = start,
                EndUtc = end,
                Participants =
                [
                    new() { Kind = MeetingPartyKind.Company, CompanyId = companyA },
                    new() { Kind = MeetingPartyKind.Company, CompanyId = companyB },
                ],
            }, token);

    private async Task<Guid> CreateTableAsync(Guid hallId, string token, int capacity = 2)
    {
        var code = "T-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var create = await PostAuthAsync(
            $"/api/v1/admin/halls/{hallId}/meeting-tables",
            new CreateMeetingTableRequest { Code = code, Capacity = capacity }, token);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        return (await create.Content.ReadFromJsonAsync<ApiResult<MeetingTableRow>>())!.Data!.Id;
    }

    private async Task<Guid> SeedHallAsync(HallPurpose purpose, int capacity = 50)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var hall = new Hall
        {
            Id = Guid.NewGuid(),
            Code = "H-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            Name = "Meeting Hall",
            NameArabic = "قاعة اجتماعات",
            Capacity = capacity,
            IsActive = true,
            Purpose = purpose,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.Halls.Add(hall);
        await appDb.SaveChangesAsync();
        return hall.Id;
    }

    private async Task<Guid> SeedCompanyAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = $"Co {Guid.NewGuid():N}",
            NameArabic = "شركة",
            Type = CompanyType.Exhibitor,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.Companies.Add(company);
        await appDb.SaveChangesAsync();
        return company.Id;
    }

    private async Task<Guid> SeedVisitorAsync()
    {
        var email = $"visitor-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Visitor Party",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"bm-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "BM Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest
            {
                Email = email,
                Password = AuthFlow.Password,
                Audience = SignInAudience.Cp,
            });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private Task<HttpResponseMessage> GetAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> PutAuthAsync<TBody>(string url, TBody body, string token)
        where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> DeleteAuthAsync(string url, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private async Task<Guid> ScheduleConfirmedAsync(
        Guid tableId, string token, DateTimeOffset start, DateTimeOffset end)
    {
        var resp = await ScheduleAsync(tableId, token, start, end,
            await SeedCompanyAsync(), await SeedCompanyAsync());
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        return (await resp.Content.ReadFromJsonAsync<ApiResult<BusinessMeetingScheduled>>())!.Data!.Id;
    }

    private async Task<Guid> SeedUserAsync(UserType userType, AccountState state)
    {
        var email = $"party-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Party",
            AccountState = state,
            UserType = userType,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }
}
