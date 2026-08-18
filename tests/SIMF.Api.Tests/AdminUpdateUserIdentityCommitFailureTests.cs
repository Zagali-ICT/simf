// Tests: SIMF.Api.Tests/AdminUpdateUserIdentityCommitFailureTests.cs (this file).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Abstractions;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Domain.Profiles;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Cross-DB ordering guard for the per-user admin edit
/// (<c>AdminAccountService.UpdateAccountAsync</c>, PUT
/// <c>/api/v1/admin/visitors/{id}</c>). A profile-type change is a PRIVILEGE
/// change, and the two databases cannot share a transaction, so the Identity
/// unit of work (account update + security-stamp roll + refresh-token revoke)
/// must commit BEFORE the App-DB profile write.
///
/// <para>The seam is <see cref="ITransactionRunner"/>, which wraps the Identity
/// transaction and nothing else. This factory replaces it with a faithful mirror
/// of the real runner that, when armed, throws just before the commit — the
/// exact failure mode the ordering exists for. With the poison off it behaves
/// identically to the real runner, so sign-up, sign-in and every other caller
/// during setup are untouched.</para>
///
/// <para>The injected failure is an <see cref="InvalidOperationException"/> on
/// purpose: the EF execution strategy retries TRANSIENT failures, and a
/// retry loop would hide the very ordering under test.</para>
/// </summary>
public sealed class IdentityCommitFailingApiFactory : SimfApiFactory
{
    /// <summary>Off during migrate/seed and test setup; a test arms it for the
    /// one call under examination.</summary>
    public bool PoisonEnabled { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        var factory = this;
        builder.ConfigureServices(services =>
        {
            // Last registration wins for GetRequiredService<ITransactionRunner>,
            // so the admin edit resolves this runner instead of the real one.
            services.AddScoped<ITransactionRunner>(serviceProvider =>
                new CommitPoisoningTransactionRunner(
                    serviceProvider.GetRequiredService<SimfIdentityDbContext>(),
                    () => factory.PoisonEnabled));
        });
    }

    private sealed class CommitPoisoningTransactionRunner(
        SimfIdentityDbContext dbContext, Func<bool> poisonEnabled) : ITransactionRunner
    {
        public async Task ExecuteAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default)
        {
            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction =
                    await dbContext.Database.BeginTransactionAsync(cancellationToken);
                await action(cancellationToken);
                if (poisonEnabled())
                {
                    // Disposing the transaction without a commit rolls the
                    // Identity work back, which is precisely what a lost
                    // connection or a deadlock victim would do at this point.
                    throw new InvalidOperationException(
                        "Injected Identity-commit failure (ordering regression test).");
                }
                await transaction.CommitAsync(cancellationToken);
            });
        }
    }
}

[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class AdminUpdateUserIdentityCommitFailureTests
    : IClassFixture<IdentityCommitFailingApiFactory>
{
    private readonly IdentityCommitFailingApiFactory _factory;
    private readonly HttpClient _client;

    public AdminUpdateUserIdentityCommitFailureTests(IdentityCommitFailingApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Update_leaves_the_profile_type_unchanged_when_the_Identity_commit_fails()
    {
        // A profile-type change is a privilege change: the type's MobileAppRole
        // sources the app's operational permission claims, so the edit rolls the
        // security stamp and revokes the subject's sessions in the Identity
        // transaction. When that transaction fails, the demotion MUST NOT already
        // be durable on the App database — otherwise the subject keeps a live
        // access token carrying the OLD claims while the profile says otherwise.
        var token = await CreateAdminAndSignInAsync();
        var currentTypeId = await SeedAudienceProfileTypeAsync(MobileAppRole.Moderator);
        var demotedTypeId = await SeedAudienceProfileTypeAsync(MobileAppRole.None);
        var (userId, email) = await CreateVisitorWithProfileAsync(currentTypeId);

        string stampBefore;
        using (var before = _factory.Services.CreateScope())
        {
            var identityDb = before.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
            stampBefore = (await identityDb.Users.AsNoTracking()
                .SingleAsync(user => user.Id == userId)).SecurityStamp!;
        }

        _factory.PoisonEnabled = true;
        HttpResponseMessage response;
        try
        {
            response = await PutAuthAsync(
                $"/api/v1/admin/visitors/{userId}",
                new AdminUpdateVisitorRequest
                {
                    Email = email,
                    DisplayName = "Demotion Attempt",
                    ProfileTypeId = demotedTypeId,
                },
                token);
        }
        finally
        {
            _factory.PoisonEnabled = false;
        }

        // The Identity commit throws, so the request fails. The contract under
        // test is the persisted state, not the response shape.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();

        // The Identity half really did roll back — the stamp roll is inside the
        // poisoned transaction, so the subject's live access tokens survive. That
        // is the whole reason the App-DB write must not have run: a durable
        // demotion here would leave those tokens carrying the OLD claims.
        var stampAfter = (await db.Users.AsNoTracking()
            .SingleAsync(user => user.Id == userId)).SecurityStamp!;
        Assert.Equal(stampBefore, stampAfter);

        // The profile still carries the ORIGINAL type. The App-DB write is
        // ordered after the Identity commit, so it never ran.
        var assignedTypeId = await appDb.UserProfiles.AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => profile.ProfileTypeId)
            .SingleAsync();
        Assert.Equal(currentTypeId, assignedTypeId);

        // And no success audit row claims an edit that did not happen. This also
        // pins the audit write OUT of the transaction lambda, which the EF
        // execution strategy re-runs whole on a transient failure.
        var audited = await appDb.OperationLog.AsNoTracking()
            .AnyAsync(entry => entry.SubjectUserId == userId
                && entry.EventType == AuditEvents.AdminUserUpdated);
        Assert.False(audited);
    }

    [Fact]
    public async Task Update_applies_the_profile_type_when_the_Identity_commit_succeeds()
    {
        // The companion to the test above: with the poison disarmed the runner is
        // a faithful mirror of the real one, so the ordered edit still persists
        // the new type. Without this, a factory that broke the happy path would
        // make the guard above pass for the wrong reason.
        var token = await CreateAdminAndSignInAsync();
        var currentTypeId = await SeedAudienceProfileTypeAsync(MobileAppRole.Moderator);
        var demotedTypeId = await SeedAudienceProfileTypeAsync(MobileAppRole.None);
        var (userId, email) = await CreateVisitorWithProfileAsync(currentTypeId);

        var response = await PutAuthAsync(
            $"/api/v1/admin/visitors/{userId}",
            new AdminUpdateVisitorRequest
            {
                Email = email,
                DisplayName = "Demotion Applied",
                ProfileTypeId = demotedTypeId,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var assignedTypeId = await appDb.UserProfiles.AsNoTracking()
            .Where(profile => profile.UserId == userId)
            .Select(profile => profile.ProfileTypeId)
            .SingleAsync();
        Assert.Equal(demotedTypeId, assignedTypeId);

        var audited = await appDb.OperationLog.AsNoTracking()
            .CountAsync(entry => entry.SubjectUserId == userId
                && entry.EventType == AuditEvents.AdminUserUpdated);
        Assert.Equal(1, audited);
    }

    // -- Helpers --------------------------------------------------------------

    // An active, audience-side (IsForVisitor=true) ProfileType. Both types the
    // tests use are audience-side so the Visitors desk scope guard passes and the
    // edit is a plain tier change rather than a scope flip.
    private async Task<Guid> SeedAudienceProfileTypeAsync(MobileAppRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        var type = new UserProfileType
        {
            Id = Guid.NewGuid(),
            Name = $"Type {Guid.NewGuid():N}",
            NameArabic = "نوع",
            PageColor = "#244A77",
            IsForVisitor = true,
            MobileAppRole = role,
            IsActive = true,
            CreatedAt = SimfClock.Now,
        };
        appDb.ProfileTypes.Add(type);
        await appDb.SaveChangesAsync();
        return type.Id;
    }

    private async Task<(Guid UserId, string Email)> CreateVisitorWithProfileAsync(Guid profileTypeId)
    {
        var email = $"commit-fail-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Commit Failure Subject",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);

        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        appDb.UserProfiles.Add(new UserProfile
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            ProfileTypeId = profileTypeId,
            Name = "Commit Failure Subject",
            NameArabic = "الحساب",
            PlaceOfBirth = "Riyadh",
            NationalityId = 0,
            CreatedAt = SimfClock.Now,
        });
        await appDb.SaveChangesAsync();
        return (user.Id, email);
    }

    private async Task<string> CreateAdminAndSignInAsync()
    {
        var email = $"commit-fail-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Commit Failure Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AppRoles.Administrator);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
    }

    private async Task<HttpResponseMessage> PutAuthAsync<TBody>(
        string url, TBody body, string token) where TBody : class
    {
        var request = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }
}
