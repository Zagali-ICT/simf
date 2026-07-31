// Tests: SIMF.Api.Tests/AdminApprovalAppSaveFailureTests.cs (this file).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// FIX #4 (R-1 data-integrity) — cross-DB ordering guard for the approve
/// flow. <c>AdminAccountService.ApproveAsync</c> mints + persists the QR on
/// the App DB BEFORE flipping the Identity account to Approved (there is no
/// cross-DB transaction — D-157). This test injects a failure on the App-DB
/// save and asserts the account is left in PendingApproval (retryable) rather
/// than orphaned as Approved-without-a-QR.
///
/// <para>The seam is the QR minter: the approve path is the ONLY caller that
/// mints (create / reject never do), so replacing it here poisons exactly the
/// approve's App-DB unit of work and nothing else. After minting the QR onto
/// the subject's profile the stub adds a SECOND UserProfile carrying the SAME
/// QrId, which trips the filtered-UNIQUE index (<c>WHERE [QrId] IS NOT NULL</c>)
/// the moment <c>appDbContext.SaveChangesAsync</c> runs — the same class of
/// failure a transient deadlock / timeout / connection drop would raise.</para>
/// </summary>
public sealed class AppSaveFailingOnApproveApiFactory : SimfApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        var factory = this;
        builder.ConfigureServices(services =>
        {
            // Last registration wins for GetRequiredService<IQrIdMinter> — the
            // approve path resolves this poisoning minter instead of the real one.
            // The poison is gated on PoisonEnabled so that DB seeding (which also
            // mints QRs via EnsureDemoAccountsAsync) is left intact; the test flips
            // it on only for the approve call under test.
            services.AddScoped<IQrIdMinter>(serviceProvider =>
                new AppSavePoisoningQrIdMinter(
                    serviceProvider.GetRequiredService<SimfAppDbContext>(),
                    () => factory.PoisonEnabled));
        });
    }

    /// <summary>Off during seeding/setup; the test flips it on for the approve call.</summary>
    public bool PoisonEnabled { get; set; }

    private sealed class AppSavePoisoningQrIdMinter(
        SimfAppDbContext appDb, Func<bool> poisonEnabled) : IQrIdMinter
    {
        public Task<string> MintIfMissingAsync(
            UserProfile profile, CancellationToken cancellationToken = default)
        {
            var qr = string.IsNullOrEmpty(profile.QrId)
                ? "TESTQR" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()
                : profile.QrId;
            profile.QrId = qr;

            // Poison ONLY the approve's App-DB unit of work: a duplicate QrId trips
            // the filtered-UNIQUE index on the next appDbContext.SaveChangesAsync,
            // so the App save fails while the Identity flip is still pending.
            if (poisonEnabled())
            {
                appDb.UserProfiles.Add(new UserProfile
                {
                    Id = Guid.NewGuid(),
                    UserId = Guid.NewGuid(),
                    QrId = qr,
                    CreatedAt = SimfClock.Now,
                });
            }
            return Task.FromResult(qr);
        }
    }
}

public sealed class AdminApprovalAppSaveFailureTests
    : IClassFixture<AppSaveFailingOnApproveApiFactory>
{
    private const string Password = "Zx9#mKp2!";

    private readonly AppSaveFailingOnApproveApiFactory _factory;
    private readonly HttpClient _client;

    public AdminApprovalAppSaveFailureTests(AppSaveFailingOnApproveApiFactory factory)
    {
        _factory = factory;
        factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Approve_leaves_the_account_PendingApproval_when_the_App_DB_save_fails()
    {
        // Regression guard for FIX #4: the QR is persisted on the App DB
        // BEFORE the Identity account flips to Approved. When that App save
        // fails, the account MUST stay PendingApproval (retryable) — it must
        // NOT be left Approved-without-a-QR, which LoadPendingSubjectAsync
        // would then 409 on every retry (a permanently un-scannable orphan).
        var adminToken = await CreateAdministratorAndSignInAsync();
        var subjectId = await CreateVisitorSubjectAsync(adminToken);

        // Arm the App-DB fault only now — seeding + setup minted QRs cleanly above.
        _factory.PoisonEnabled = true;
        var response = await PostAuthAsync(
            $"/api/v1/admin/visitors/{subjectId}/approve", new { }, adminToken);

        // The App-DB save throws → the global error handler surfaces a 500.
        // The contract under test is the persisted state, not the response shape.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        // Account stayed PendingApproval — the Identity flip never committed
        // (the App save is ordered first and aborted the request before it).
        var subject = await db.Users.SingleAsync(u => u.Id == subjectId);
        Assert.Equal(AccountState.PendingApproval, subject.AccountState);

        // No QR was persisted — the App unit of work rolled back as one, so the
        // account is safe to re-approve rather than being an Approved orphan.
        var qr = await appDb.UserProfiles
            .Where(p => p.UserId == subjectId)
            .Select(p => p.QrId)
            .SingleOrDefaultAsync();
        Assert.True(string.IsNullOrEmpty(qr));
    }

    // -- helpers (mirrors AdminApprovalTests; self-contained per test class) --

    private async Task<Guid> CreateVisitorSubjectAsync(string adminToken)
    {
        var email = $"visitor-{Guid.NewGuid():N}@simf.test";
        var response = await PostAuthAsync("/api/v1/admin/visitors",
            new AdminCreateVisitorRequest
            {
                Email = email, DisplayName = "Pending Visitor",
            }, adminToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        return (await db.Users.SingleAsync(u => u.Email == email)).Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"admin-approve-fail-{Guid.NewGuid():N}@simf.test";
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
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "Approval Failure Tests Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }
        return await SignInAndGetTokenAsync(email, SignInAudience.Cp);
    }

    private async Task<string> SignInAndGetTokenAsync(string email, SignInAudience audience)
    {
        var sign = await _client.PostAsJsonAsync("/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = Password, Audience = audience });
        var body = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return body.Data!.Tokens!.AccessToken;
    }

    private async Task<HttpResponseMessage> PostAuthAsync<TBody>(
        string url, TBody? body, string token) where TBody : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) { request.Content = JsonContent.Create(body); }
        return await _client.SendAsync(request);
    }
}
