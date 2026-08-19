using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Two admin grids declare a sort/filter key by CONCATENATING a plain column with
/// an Arabic one, and every <c>*Arabic</c> column now carries the
/// <c>Arabic_CI_AI</c> collation while the rest carry the SQL Server instance
/// default. SQL Server refuses to evaluate an expression whose operands disagree
/// on collation, so those keys raised "Cannot resolve the collation conflict" -
/// Msg 451 on the add operator, Msg 4191 on CHARINDEX - the moment an admin
/// clicked the column's sort arrow or typed into its filter box.
///
/// <para>Both grids answered 500 and the whole suite stayed green, because
/// nothing anywhere sent a sort or a filter for those keys: the export tests post
/// a bare <c>Top</c>, the default order is a date column, and the projection
/// selects the parts as separate fields. That is the gap these tests close. They
/// are deliberately end-to-end rather than a translation check, because the
/// failure is the database refusing a statement EF translated perfectly
/// happily.</para>
///
/// <para>Sending the key is the whole point of each case. An assertion on the
/// rows would pass just as well against the broken build, since the broken build
/// never got as far as returning any.</para>
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Reporting)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class GridCollationBoundaryTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public GridCollationBoundaryTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    public static TheoryData<string> GridsWhoseSessionKeySpansTheCollationBoundary() =>
        new()
        {
            "/api/v1/admin/session-moderators/list",
            "/api/v1/admin/bookings/list",
        };

    [Theory]
    [MemberData(nameof(GridsWhoseSessionKeySpansTheCollationBoundary))]
    public async Task Sorting_on_a_key_that_concatenates_an_arabic_column_succeeds(string route)
    {
        var token = await CreateAdministratorAndSignInAsync();

        foreach (var descending in new[] { false, true })
        {
            var response = await PostAuthAsync(
                route,
                new GridQuery { Top = 10, Sort = "session", SortDescending = descending },
                token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Theory]
    [MemberData(nameof(GridsWhoseSessionKeySpansTheCollationBoundary))]
    public async Task Filtering_on_a_key_that_concatenates_an_arabic_column_succeeds(string route)
    {
        var token = await CreateAdministratorAndSignInAsync();

        // A Latin needle and an Arabic one: the first goes through the forced
        // COLLATE on the plain half, the second through the Arabic half, and both
        // reach the same concatenated expression.
        foreach (var needle in new[] { "keynote", "الجلسة" })
        {
            var response = await PostAuthAsync(
                route,
                new GridQuery
                {
                    Top = 10,
                    Filters = new Dictionary<string, string> { ["session"] = needle },
                },
                token);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"grid-collation-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Test Grid Collation Admin",
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
}
