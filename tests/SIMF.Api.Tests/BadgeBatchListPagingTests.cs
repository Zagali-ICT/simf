// The badge-orders list used to page itself: a hand-rolled ClampPage plus an
// OrderByDescending(CreatedAt) with NO tiebreak. Orders minted in one transaction
// share a CreatedAt to the tick, so SQL Server was free to return the tied rows in
// a different sequence per page request - the same order could come back on two
// pages while another came back on none. It now runs through the grid seam, which
// makes the tiebreak a required argument and validates every sort and filter key
// against a closed declaration instead of ignoring the ones it does not know.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Badges;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

[Trait(TestAreas.TraitName, TestAreas.Badges)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class BadgeBatchListPagingTests : IClassFixture<SimfApiFactory>
{
    private const string ListRoute = "/api/v1/admin/visitors/badge-batches/list";
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public BadgeBatchListPagingTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Two_orders_minted_on_the_same_tick_page_without_repeating_or_dropping_one()
    {
        var admin = await CreateAdministratorAndSignInAsync();

        // Both rows carry the SAME CreatedAt, which is the condition the tiebreak
        // exists for, and a timestamp ahead of every other order so the newest-first
        // order puts the pair at offsets 0 and 1 whatever else the database holds.
        var tie = SimfClock.Now.AddYears(5);
        var (first, second) = await SeedTiedOrdersAsync(tie);

        var pageOne = await ListAsync(new GridQuery { Skip = 0, Top = 1 }, admin);
        var pageTwo = await ListAsync(new GridQuery { Skip = 1, Top = 1 }, admin);

        var firstId = Assert.Single(pageOne.Items).Id;
        var secondId = Assert.Single(pageTwo.Items).Id;

        // Disjoint, and between them they cover both seeded orders: no row is
        // repeated across the two pages and none is skipped.
        Assert.NotEqual(firstId, secondId);
        Assert.Contains(firstId, new[] { first, second });
        Assert.Contains(secondId, new[] { first, second });

        // And the window is stable: asking for the same page again returns the same
        // row rather than whichever of the tied pair the server happened to reach.
        var pageOneAgain = await ListAsync(new GridQuery { Skip = 0, Top = 1 }, admin);
        Assert.Equal(firstId, Assert.Single(pageOneAgain.Items).Id);
    }

    [Fact]
    public async Task An_order_is_found_by_searching_its_name()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var marker = $"Searchable{Guid.NewGuid():N}";
        await SeedOrderAsync(marker, SimfClock.Now);

        var page = await ListAsync(new GridQuery { Top = 50, Search = marker }, admin);

        // The declaration marks the two names and the recipient searchable; the
        // marker is unique, so the search narrows to exactly the seeded order
        // instead of quietly returning the whole table, which is what an ignored
        // search term would have done.
        var row = Assert.Single(page.Items);
        Assert.Equal(marker, row.Name);
    }

    [Fact]
    public async Task An_undeclared_sort_key_is_a_400_rather_than_a_silently_ignored_one()
    {
        var admin = await CreateAdministratorAndSignInAsync();

        var refused = await PostAuthAsync(
            ListRoute, new GridQuery { Top = 10, Sort = "notAColumn" }, admin);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);

        // The declared key still sorts, so the 400 above is the allow-list working
        // and not the list refusing every sort it is given.
        var accepted = await PostAuthAsync(
            ListRoute, new GridQuery { Top = 10, Sort = "createdAt", SortDescending = true }, admin);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    // -- fixture ---------------------------------------------------------------

    private async Task<GridPage<AdminBadgeBatchSummary>> ListAsync(GridQuery query, string token)
    {
        var response = await PostAuthAsync(ListRoute, query, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminBadgeBatchSummary>>>())!;
        Assert.NotNull(body.Data);
        return body.Data!;
    }

    /// <summary>Two orders written with an identical <c>CreatedAt</c>. The audit
    /// stamping interceptor only fills a value that was left unset, so the tie
    /// survives the save.</summary>
    private async Task<(Guid First, Guid Second)> SeedTiedOrdersAsync(DateTime createdAt)
    {
        var first = await SeedOrderAsync($"Tied A {Guid.NewGuid():N}", createdAt);
        var second = await SeedOrderAsync($"Tied B {Guid.NewGuid():N}", createdAt);
        return (first, second);
    }

    private async Task<Guid> SeedOrderAsync(string name, DateTime createdAt)
    {
        using var scope = _factory.Services.CreateScope();
        var appDb = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var batch = new BadgeBatch
        {
            Id = Guid.NewGuid(),
            Name = name,
            NameArabic = "طلب اختباري",
            IsDelegate = false,
            IsActive = true,
            CreatedAt = createdAt,
        };
        appDb.BadgeBatches.Add(batch);
        await appDb.SaveChangesAsync();
        return batch.Id;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"batch-admin-{Guid.NewGuid():N}@simf.test";
        using (var scope = _factory.Services.CreateScope())
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<SimfRole>>();
            if (!await roleManager.RoleExistsAsync(AdministratorRole))
            {
                await roleManager.CreateAsync(
                    new SimfRole { Name = AdministratorRole, IsBaseline = true });
            }

            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = "Batch Admin",
                AccountState = AccountState.Approved,
                UserType = UserType.Admin,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            await users.AddToRoleAsync(user, AdministratorRole);
        }

        return await AuthFlow.SignInControlPanelAsync(_client, _factory, email);
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
