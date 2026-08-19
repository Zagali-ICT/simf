// Pins the Arabic_CI_AI collation the D-931 schema lift put on every *Arabic
// string column. Nothing else in the suite asserted it, and it is the whole
// point of that change.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Admin;
using SIMF.Domain.IdentityAccess;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Proves the accent-insensitive Arabic collation is real: through the API, over
/// a database built from the migrations, on a column an operator actually
/// searches.
/// </summary>
/// <remarks>
/// <para>
/// WHY THIS CANNOT BE A C# TEST. The needle is the only half of the comparison
/// application code owns. Folding it in process - mapping every alef form onto a
/// bare alef before the query is built - changes what SQL Server is asked to look
/// for and changes nothing about the haystack, which is a column of rows the
/// process never sees. The comparison itself happens inside the engine under the
/// collation of the column, so the only way to learn whether two spellings are
/// one name is to store one spelling and search for the other. That is what
/// these tests do, and it is why a unit test over a normalising helper would
/// prove the helper and not the behaviour.
/// </para>
/// <para>
/// BOTH OPERATORS ROUTE THROUGH THE COLUMN. The grid search reaches the server as
/// CHARINDEX(@term, [NameArabic]) and the duplicate probe as [NameArabic] =
/// @name. In each the column carries an IMPLICIT collation and the parameter only
/// a coercible-default one, so the column's collation wins and one fold governs
/// search, equality, and the filtered UNIQUE index on (Tier, NameArabic).
/// </para>
/// <para>
/// WHAT Arabic_CI_AI FOLDS, measured on the engine this suite runs on (SQL Server
/// 17.0 LocalDB, against a column declared COLLATE Arabic_CI_AI): the alef
/// maksura U+0649 and the yeh U+064A are one letter, for CHARINDEX, for LIKE and
/// for equality. That is the Arabic spelling variant the change buys, it is a
/// variant the same person types both ways, and it is what the first four tests
/// pin. Under the instance default collation (SQL_Latin1_General_CP1_CI_AS) every
/// one of those searches returns nothing, so the tests discriminate.
/// </para>
/// <para>
/// WHAT IT DOES NOT FOLD, which is why the last test exists: the PRECOMPOSED
/// hamza letters. U+0623 (alef with hamza above), U+0625, U+0622, U+0624 and
/// U+0626 each keep their own primary weight, so a search for "احمد" still does
/// not find "أحمد" - the example the collation was added for. Accent-insensitivity
/// reaches only the DECOMPOSED sequence U+0627 U+0654, which is NFD text
/// essentially nobody produces: an Arabic keyboard, and every browser that
/// forwards its output, hands over the precomposed letter. Closing that gap needs
/// a folded shadow of the text to compare against, written on the way in, which
/// is a schema change and therefore a separate lift. The last test pins the limit
/// as measured rather than as hoped, so the day someone closes it the test goes
/// red and is rewritten, instead of the claim staying quietly wrong in a comment.
/// </para>
/// <para>
/// Sponsor is the resource under test for both halves because it is the only one
/// that can carry both: its admin grid declares nameAr searchable, and it owns
/// the only UNIQUE index covering an Arabic column, so the search and the
/// duplicate 409 run over the very same column.
/// </para>
/// </remarks>
[Trait(TestAreas.TraitName, TestAreas.Content)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class ArabicCollationTests : IClassFixture<SimfApiFactory>
{
    private const string AdministratorRole = "Administrator";

    /// <summary>"Hospital", ending in the alef maksura U+0649, the spelling
    /// Arabic orthography prescribes.</summary>
    private const string MaksuraWord = "مستشفى";

    /// <summary>The same word ending in the yeh U+064A, the spelling a keyboard
    /// in a hurry produces just as often.</summary>
    private const string YehWord = "مستشفي";

    /// <summary>A different word that shares the first three letters, so the
    /// negative case proves the collation folds ONE letter rather than matching
    /// anything that looks vaguely similar.</summary>
    private const string DifferentWord = "مستودع";

    /// <summary>"Ahmad", opening with the precomposed alef-hamza U+0623.</summary>
    private const string HamzaName = "أحمد";

    /// <summary>"Ahmad", opening with a bare alef U+0627.</summary>
    private const string BareAlefName = "احمد";

    /// <summary>"School", ending in the teh marbuta U+0629.</summary>
    private const string TehMarbutaWord = "مدرسة";

    /// <summary>The same word ending in a plain heh U+0647, which is how it is
    /// habitually typed.</summary>
    private const string HehWord = "مدرسه";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ArabicCollationTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public void The_literals_differ_by_exactly_the_letter_each_case_is_about()
    {
        // Without this the rest of the class can quietly become a tautology. If an
        // editor, a paste or a re-encode replaced the maksura with a yeh, the two
        // search tests would pass by exact match and pin nothing at all - and the
        // two letters are one pair of dots apart on screen, so the check has to be
        // on the code point rather than on the eye. The expected side is a
        // unicode escape so the guard cannot be corrupted by the very
        // re-encode it exists to catch.
        Assert.Equal('\u0649', MaksuraWord[^1]);
        Assert.Equal('\u064A', YehWord[^1]);
        Assert.Equal(MaksuraWord[..^1], YehWord[..^1]);
        Assert.NotEqual(MaksuraWord, YehWord);

        Assert.Equal('\u0623', HamzaName[0]);
        Assert.Equal('\u0627', BareAlefName[0]);
        Assert.Equal(HamzaName[1..], BareAlefName[1..]);
        Assert.NotEqual(HamzaName, BareAlefName);
    }

    [Fact]
    public async Task Search_finds_a_maksura_spelling_when_the_operator_types_a_yeh()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var suffix = NewSuffix();
        var id = await CreateSponsorAsync(
            admin, $"{MaksuraWord} الملك فهد {suffix}", SponsorTier.Gold);

        var rows = await SearchAsync(admin, YehWord);

        Assert.Contains(rows, row => row.Id == id);
    }

    [Fact]
    public async Task Search_finds_a_yeh_spelling_when_the_operator_types_a_maksura()
    {
        // The opposite direction, asserted separately rather than assumed from the
        // one above: CHARINDEX under an accent-insensitive collation is not
        // obviously symmetric, and a fold that worked one way round would leave
        // half the operators still unable to find their own data.
        var admin = await CreateAdministratorAndSignInAsync();
        var suffix = NewSuffix();
        var id = await CreateSponsorAsync(
            admin, $"{YehWord} النور {suffix}", SponsorTier.Silver);

        var rows = await SearchAsync(admin, MaksuraWord);

        Assert.Contains(rows, row => row.Id == id);
    }

    [Fact]
    public async Task Search_still_misses_a_genuinely_different_word()
    {
        var admin = await CreateAdministratorAndSignInAsync();
        var suffix = NewSuffix();
        var id = await CreateSponsorAsync(
            admin, $"{MaksuraWord} الوطني {suffix}", SponsorTier.Bronze);

        Assert.DoesNotContain(
            await SearchAsync(admin, DifferentWord), row => row.Id == id);

        // The row IS there and IS searchable, so the miss above is the collation
        // discriminating rather than the seed having failed. A negative case
        // without this control passes just as happily when nothing was created.
        Assert.Contains(
            await SearchAsync(admin, MaksuraWord), row => row.Id == id);
    }

    [Fact]
    public async Task Duplicate_probe_treats_the_two_spellings_as_one_name()
    {
        // The equality half. AdminSponsorService answers 409 SPONSOR_DUPLICATE for
        // a second ACTIVE sponsor on the same (Tier, NameArabic); with the column
        // folded, the two spellings are that same pair, so the second create must
        // be refused rather than land a row the public page shows twice.
        var admin = await CreateAdministratorAndSignInAsync();
        var suffix = NewSuffix();
        await CreateSponsorAsync(
            admin, $"{MaksuraWord} الرعاية {suffix}", SponsorTier.Gold);

        var second = await PostAuthAsync(
            "/api/v1/admin/sponsors",
            new AdminCreateSponsorRequest
            {
                NameEn = $"Collation Duplicate {suffix}",
                NameAr = $"{YehWord} الرعاية {suffix}",
                Tier = (int)SponsorTier.Gold,
                DisplayOrder = 1,
            },
            admin);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = (await second.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.SponsorDuplicate, body.Error!.Code);
    }

    [Fact]
    public async Task A_precomposed_alef_hamza_now_matches_a_bare_alef()
    {
        // This is the replacement the previous version of this test predicted. The
        // collation never closed this gap and could not: SQL Server weighs the
        // precomposed U+0623 as its own letter and never decomposes it, so
        // accent-insensitivity has nothing to ignore. The seam folds the letter
        // forms explicitly instead - the same REPLACE chain applied to the column
        // in SQL and to the needle in memory - which is why the row is now found
        // from either spelling.
        var admin = await CreateAdministratorAndSignInAsync();
        var suffix = NewSuffix();
        var id = await CreateSponsorAsync(
            admin, $"{HamzaName} العتيبي {suffix}", SponsorTier.Bronze);

        Assert.Contains(await SearchAsync(admin, HamzaName), row => row.Id == id);
        Assert.Contains(await SearchAsync(admin, BareAlefName), row => row.Id == id);
    }

    [Fact]
    public async Task A_bare_alef_is_found_by_someone_typing_the_hamza()
    {
        // The other direction. The fold is applied to BOTH the stored column and
        // the needle, so it cannot matter which way round the two spellings fall -
        // and a fold applied to only one side would pass the case above while
        // failing this one.
        var admin = await CreateAdministratorAndSignInAsync();
        var suffix = NewSuffix();
        var id = await CreateSponsorAsync(
            admin, $"{BareAlefName} العتيبي {suffix}", SponsorTier.Silver);

        Assert.Contains(await SearchAsync(admin, BareAlefName), row => row.Id == id);
        Assert.Contains(await SearchAsync(admin, HamzaName), row => row.Id == id);
    }

    // -- Helpers --------------------------------------------------------------

    private static string NewSuffix() => Guid.NewGuid().ToString("N")[..8];

    private async Task<Guid> CreateSponsorAsync(
        string token, string nameArabic, SponsorTier tier)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/sponsors",
            new AdminCreateSponsorRequest
            {
                NameEn = $"Collation Fixture {Guid.NewGuid():N}",
                NameAr = nameArabic,
                Tier = (int)tier,
                DisplayOrder = 0,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<AdminSponsorDetail>>())!.Data!.Id;
    }

    /// <summary>One page of the admin Sponsors grid for a free-text term.</summary>
    /// <remarks>
    /// Every assertion in this class is made on the created row's id rather than
    /// on the page count, because the fixture database also carries the seeded
    /// content roster and whatever the other tests in the class created. That
    /// makes each assertion independent of the rest of the database, but leaves
    /// it bounded by Top: if a term ever matched more than 200 rows, the row
    /// under test could be paged out and the assertion would fail for a reason
    /// that has nothing to do with the collation.
    /// </remarks>
    private async Task<IReadOnlyList<AdminSponsorSummary>> SearchAsync(
        string token, string term)
    {
        var response = await PostAuthAsync(
            "/api/v1/admin/sponsors/list",
            new GridQuery { Top = 200, Search = term },
            token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content
            .ReadFromJsonAsync<ApiResult<GridPage<AdminSponsorSummary>>>())!.Data!.Items;
    }

    private async Task<string> CreateAdministratorAndSignInAsync()
    {
        var email = $"collation-admin-{Guid.NewGuid():N}@simf.test";
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
                DisplayName = "Collation Admin",
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
