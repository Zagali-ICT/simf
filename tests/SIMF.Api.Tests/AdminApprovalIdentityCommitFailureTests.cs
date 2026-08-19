// Tests: SIMF.Api.Tests/AdminApprovalIdentityCommitFailureTests.cs (this file).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Cross-DB ordering guard for the approve path
/// (<c>AdminAccountService.ApproveAsync</c>, POST
/// <c>/api/v1/admin/visitors/{id}/approve</c>). The two databases cannot share a
/// transaction, and the App-DB half (admission state + the minted QR) is written
/// FIRST so a failure there leaves a retryable pending account rather than an
/// approved visitor with no badge.
///
/// <para>Admission is read off the PROFILE, so the reverse window is the
/// dangerous one: if the App half commits and the Identity flip then fails, the
/// holder is admissible at a gate while every admin list still shows them
/// pending. The approve path therefore UNDOES the profile-side admission when the
/// Identity write fails. These tests pin that.</para>
///
/// <para>The seam is <c>IUserValidator</c>. UserManager runs every registered
/// validator on Create and on Update, so an extra one that fails on demand makes
/// the Identity write fail without touching the database wiring or the
/// connection. Off during migrate/seed and setup; a test arms it for the one call
/// under examination.</para>
/// </summary>
public sealed class ApprovalIdentityFailingApiFactory : SimfApiFactory
{
    /// <summary>Off by default; a test arms it around the approve call.</summary>
    public bool PoisonEnabled { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        var factory = this;
        builder.ConfigureServices(services =>
            services.AddScoped<IUserValidator<SimfUser>>(
                _ => new PoisonUserValidator(() => factory.PoisonEnabled)));
    }

    private sealed class PoisonUserValidator(Func<bool> poisonEnabled)
        : IUserValidator<SimfUser>
    {
        public Task<IdentityResult> ValidateAsync(UserManager<SimfUser> manager, SimfUser user) =>
            Task.FromResult(poisonEnabled()
                ? IdentityResult.Failed(new IdentityError
                {
                    Code = "InjectedIdentityFailure",
                    Description = "Injected Identity write failure (approval ordering test).",
                })
                : IdentityResult.Success);
    }
}

[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class AdminApprovalIdentityCommitFailureTests
    : IClassFixture<ApprovalIdentityFailingApiFactory>
{
    private readonly ApprovalIdentityFailingApiFactory _factory;
    private readonly HttpClient _client;

    public AdminApprovalIdentityCommitFailureTests(ApprovalIdentityFailingApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task A_failed_Identity_flip_undoes_the_profile_side_admission()
    {
        var token = await CreateAdminAndSignInAsync();
        var subjectId = await CreatePendingVisitorAsync(token);

        _factory.PoisonEnabled = true;
        HttpResponseMessage response;
        try
        {
            response = await PostAuthAsync(
                $"/api/v1/admin/visitors/{subjectId}/approve", token, new { });
        }
        finally
        {
            _factory.PoisonEnabled = false;
        }

        // The Identity write throws, so the request fails. The contract under test
        // is the persisted state, not the response shape.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var identityDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();

        // The account never flipped - the whole point of the failure.
        var accountState = (await identityDb.Users.AsNoTracking()
            .SingleAsync(user => user.Id == subjectId)).AccountState;
        Assert.Equal(AccountState.PendingApproval, accountState);

        // And neither did the row a GATE reads. Without the compensation the
        // profile would still say Approved here, and the holder would be admitted
        // on a badge no admin list agrees was ever issued.
        var admission = await appDb.UserProfiles.AsNoTracking()
            .Where(profile => profile.UserId == subjectId)
            .Select(profile => profile.AdmissionState)
            .SingleAsync();
        Assert.Equal(AccountState.PendingApproval, admission);
    }

    [Fact]
    public async Task A_successful_approve_still_admits_the_holder()
    {
        // The positive control. With the poison disarmed the host behaves exactly
        // like the real one, so the approve still lands on both databases. Without
        // this, a factory that broke approval for everyone would make the guard
        // above pass for the wrong reason.
        var token = await CreateAdminAndSignInAsync();
        var subjectId = await CreatePendingVisitorAsync(token);

        var response = await PostAuthAsync(
            $"/api/v1/admin/visitors/{subjectId}/approve", token, new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var identityDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();

        var accountState = (await identityDb.Users.AsNoTracking()
            .SingleAsync(user => user.Id == subjectId)).AccountState;
        Assert.Equal(AccountState.Approved, accountState);

        var profile = await appDb.UserProfiles.AsNoTracking()
            .SingleAsync(row => row.UserId == subjectId);
        Assert.Equal(AccountState.Approved, profile.AdmissionState);
        Assert.False(string.IsNullOrWhiteSpace(profile.QrId));
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<Guid> CreatePendingVisitorAsync(string adminToken)
    {
        var email = $"approve-fail-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync(
            "/api/v1/admin/visitors",
            adminToken,
            new AdminCreateVisitorRequest { Email = email, DisplayName = "Approve Subject" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var identityDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        return (await identityDb.Users.SingleAsync(user => user.Email == email)).Id;
    }

    private async Task<string> CreateAdminAndSignInAsync()
    {
        var email = $"approve-fail-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roles.RoleExistsAsync(AppRoles.Administrator))
            {
                await roles.CreateAsync(new SimfRole { Name = AppRoles.Administrator });
            }
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Approve Failure Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, string token, TBody body) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }
}
