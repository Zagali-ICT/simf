// Tests: D-753 — forum-day scheduling bound is SKIPPED when no active programme days
// exist (GetForumDaysAsync returns null). A dedicated class with its own throwaway DB
// so deactivating the fixture-seeded programme days cannot leak into other test classes.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Contracts.BusinessMeetings;
using SIMF.Domain.Exhibitors;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Programme;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class ForumWindowNoProgrammeDaysTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ForumWindowNoProgrammeDaysTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();

        // Deactivate the fixture-seeded programme days (2026-11-20..22) so the forum
        // window resolves to null and the bound is skipped. Idempotent; this class owns
        // its own DB (IClassFixture instance) so it never affects the other suites.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        db.ProgrammeDays.ExecuteUpdate(s => s.SetProperty(d => d.IsActive, false));
    }

    [Fact]
    public async Task Schedule_ignores_the_forum_bound_when_no_programme_days_exist()
    {
        // With no active programme days the forum window is null, so a future slot that
        // is nowhere near the (now-inactive) event days is still accepted — the pre-D-753
        // behaviour, preserved. Only the not-in-past lower bound remains.
        var token = await CreateAdministratorAndSignInAsync();
        var hallId = await SeedHallAsync(HallPurpose.Meeting);
        var tableId = await CreateTableAsync(hallId, token);
        var start = DateTimeOffset.UtcNow.AddDays(30);

        var schedule = await PostAuthAsync(
            "/api/v1/admin/business-meetings",
            new ScheduleMeetingRequest
            {
                MeetingTableId = tableId,
                MeetingType = BusinessMeetingType.B2B,
                Start = start,
                End = start.AddHours(1),
                Participants =
                [
                    new() { Kind = MeetingPartyKind.Company, CompanyId = await SeedCompanyAsync() },
                    new() { Kind = MeetingPartyKind.Company, CompanyId = await SeedCompanyAsync() },
                ],
            }, token);
        Assert.Equal(HttpStatusCode.OK, schedule.StatusCode);
    }

    // -- Helpers --------------------------------------------------------------

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
        var exhibitor = new Exhibitor
        {
            Id = Guid.NewGuid(),
            Name = $"Co {Guid.NewGuid():N}",
            NameArabic = "شركة",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        appDb.Exhibitors.Add(exhibitor);
        await appDb.SaveChangesAsync();
        return exhibitor.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"nodays-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "No-Days Admin",
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
}
