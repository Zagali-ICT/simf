// OA-D5 — the meeting hall check-in stamps (CheckedInAt / CheckedInByUserId) were
// written by the two check-in routes but appeared in NO report: they were absent
// from AdminSpeakerMeetingRequestRow, absent from the speaker export's six columns,
// and the delegation desk had no /export route at all.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.Programme;
using SIMF.Domain.BusinessMeetings;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests for the OA-D5 reporting gap: the speaker grid + export now
/// carry the check-in stamps, and the delegation meeting-request desk has its own
/// gated XLSX export.
/// </summary>
public sealed class MeetingCheckInExportTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public MeetingCheckInExportTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Speaker_grid_row_carries_the_check_in_stamps()
    {
        var (token, operatorUserId, operatorName) = await CreateAdministratorAndSignInAsync();
        var requestId = await SeedCheckedInSpeakerRequestAsync(operatorUserId);

        var list = await PostAuthAsync(
            "/api/v1/admin/speaker-meeting-requests/list",
            new GridQuery { Top = 200 }, token);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSpeakerMeetingRequestRow>>>())!.Data!;
        var row = Assert.Single(page.Items, r => r.Id == requestId);

        Assert.NotNull(row.CheckedInAt);
        // The operator name is resolved from the Identity DB (a bare-Guid logical
        // FK, D-157) — never a cross-database JOIN.
        Assert.Equal(operatorName, row.CheckedInByName);
    }

    [Fact]
    public async Task Speaker_row_without_a_check_in_reports_null_stamps()
    {
        var (token, _, _) = await CreateAdministratorAndSignInAsync();
        var requestId = await SeedCheckedInSpeakerRequestAsync(checkedInByUserId: null);

        var list = await PostAuthAsync(
            "/api/v1/admin/speaker-meeting-requests/list",
            new GridQuery { Top = 200 }, token);
        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSpeakerMeetingRequestRow>>>())!.Data!;
        var row = Assert.Single(page.Items, r => r.Id == requestId);

        Assert.Null(row.CheckedInAt);
        Assert.Null(row.CheckedInByName);
    }

    [Fact]
    public async Task Delegation_export_returns_an_xlsx_workbook()
    {
        var (token, _, _) = await CreateAdministratorAndSignInAsync();

        var response = await PostAuthAsync(
            "/api/v1/admin/delegation-meeting-requests/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 100 } },
            token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        // .xlsx is a ZIP — first four bytes are the local-file header.
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);
        Assert.Equal(0x03, bytes[2]);
        Assert.Equal(0x04, bytes[3]);
    }

    [Fact]
    public async Task Delegation_export_is_forbidden_without_the_export_permission()
    {
        var tokens = await AuthFlow.SignInVisitorWithoutTwoFactorAsync(_client, _factory);

        var response = await PostAuthAsync(
            "/api/v1/admin/delegation-meeting-requests/export",
            new AdminGridExportRequest { Query = new GridQuery { Top = 10 } },
            tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delegation_grid_row_carries_the_check_in_stamps()
    {
        var (token, operatorUserId, operatorName) = await CreateAdministratorAndSignInAsync();
        var requestId = await SeedCheckedInDelegationRequestAsync(operatorUserId);

        var list = await PostAuthAsync(
            "/api/v1/admin/delegation-meeting-requests/list",
            new GridQuery { Top = 200 }, token);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var page = (await list.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminDelegationMeetingRequestRow>>>())!.Data!;
        var row = Assert.Single(page.Items, r => r.Id == requestId);

        Assert.NotNull(row.CheckedInAt);
        Assert.Equal(operatorName, row.CheckedInByName);
    }

    // -- Helpers --------------------------------------------------------------

    /// <summary>Seeds a checked-in speaker meeting request straight onto the App DB.
    /// The full submit → accept → check-in journey is covered by
    /// <c>SpeakerMeetingRequestsTests</c>; this suite is about the REPORTING of the
    /// stamps, so it seeds the end state directly.</summary>
    private async Task<Guid> SeedCheckedInSpeakerRequestAsync(Guid? checkedInByUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var speaker = new SIMF.Domain.Programme.Speaker
        {
            Id = Guid.NewGuid(),
            Code = $"SP{Guid.NewGuid():N}"[..8],
            Name = "Dr Amina Al Harbi",
            NameArabic = "د. أمينة الحربي",
            CreatedAt = SimfClock.Now,
        };
        appDb.Speakers.Add(speaker);

        var now = SimfClock.Now;
        var request = new SpeakerMeetingRequest
        {
            Id = Guid.NewGuid(),
            SpeakerId = speaker.Id,
            RequestedByUserId = Guid.NewGuid(),
            RequesterName = "Khalid Al Otaibi",
            Subject = "Naval logistics cooperation",
            Status = checkedInByUserId is null
                ? MeetingRequestStatus.Accepted
                : MeetingRequestStatus.Done,
            CreatedAt = now.AddHours(-4),
            RespondedAt = now.AddHours(-3),
            CheckedInAt = checkedInByUserId is null ? null : now.AddMinutes(-20),
            CheckedInByUserId = checkedInByUserId,
        };
        appDb.SpeakerMeetingRequests.Add(request);
        await appDb.SaveChangesAsync();
        return request.Id;
    }

    private async Task<Guid> SeedCheckedInDelegationRequestAsync(Guid checkedInByUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var countryIds = await appDb.Countries
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => c.Id)
            .Take(2)
            .ToListAsync();
        Assert.True(countryIds.Count == 2,
            "The country lookup must be seeded for the delegation meeting desk.");

        var now = SimfClock.Now;
        var request = new DelegationMeetingRequest
        {
            Id = Guid.NewGuid(),
            RequestingCountryId = countryIds[0],
            TargetCountryId = countryIds[1],
            RequestedByUserId = Guid.NewGuid(),
            AttendeeCount = 4,
            Subject = "Bilateral maritime security talks",
            Status = MeetingRequestStatus.Done,
            CreatedAt = now.AddHours(-5),
            RespondedAt = now.AddHours(-4),
            CheckedInAt = now.AddMinutes(-15),
            CheckedInByUserId = checkedInByUserId,
        };
        appDb.DelegationMeetingRequests.Add(request);
        await appDb.SaveChangesAsync();
        return request.Id;
    }

    private async Task<(string Token, Guid UserId, string DisplayName)> CreateAdministratorAndSignInAsync()
    {
        var email = $"oad5-admin-{Guid.NewGuid():N}@simf.test";
        const string displayName = "Gate Operator Nora";
        Guid userId;
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
                DisplayName = displayName,
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
            userId = user.Id;
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
        return (body.Data!.Tokens!.AccessToken, userId, displayName);
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
}
