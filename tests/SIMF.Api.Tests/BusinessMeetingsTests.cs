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
            NameEn = $"Co {Guid.NewGuid():N}",
            NameAr = "شركة",
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
}
