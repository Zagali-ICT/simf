using System.Globalization;
using System.Net;
using System.Net.Http.Json;
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
/// Regression tests for the two read-modify-write races on <c>AccountCode</c>.
///
/// <para>Both are written as real concurrent bursts rather than as assertions
/// about which repository method gets called, because the defect only exists
/// under concurrency: the old code passed every sequential test it had while the
/// invariant it was supposed to enforce did not actually hold.</para>
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Identity)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class AccountCodeConcurrencyTests : IClassFixture<SimfApiFactory>
{
    // Comfortably past the 5-attempt cap, so the burst has to trip it.
    private const int Burst = 12;
    private const int MaxResetAttempts = 5;
    private const string NewPassword = "NewPassw0rd!";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AccountCodeConcurrencyTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Concurrent_wrong_guesses_cannot_stretch_the_attempt_budget()
    {
        var (email, correct) = await RequestResetAsync();

        // Every one of these reads AttemptCount before any of them writes it. With
        // `AttemptCount++` + UpdateAsync they all wrote 1, so the counter finished
        // at 1 no matter how many guesses were spent and the cap never tripped.
        var responses = await Task.WhenAll(
            WrongCodes(correct).Select(wrong => ResetAsync(email, wrong)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode));

        var stored = await LoadCodeAsync(email);
        Assert.NotNull(stored);

        // Not an equality check: once the budget is spent the code is burned, and
        // requests that arrive after the burn find no live code and never reach the
        // increment. The count is therefore somewhere between the cap and the
        // burst size - the point is that increments were not lost, which the old
        // read-modify-write could not manage even once.
        Assert.InRange(stored!.AttemptCount, MaxResetAttempts, Burst);

        // The load-bearing assertion: past the cap the code must be dead, or a
        // concurrent burst simply buys more guesses against a 10^6 space.
        Assert.NotNull(stored.ConsumedAt);
    }

    [Fact]
    public async Task Past_the_attempt_cap_even_the_correct_code_is_refused()
    {
        var (email, correct) = await RequestResetAsync();

        await Task.WhenAll(WrongCodes(correct).Select(wrong => ResetAsync(email, wrong)));

        var afterBurst = await ResetAsync(email, correct);

        Assert.Equal(HttpStatusCode.BadRequest, afterBurst.StatusCode);
        var body = await afterBurst.Content.ReadFromJsonAsync<ApiResult<object>>();
        Assert.Equal(ErrorCodes.AuthResetCodeInvalid, body!.Error!.Code);
    }

    [Fact]
    public async Task One_valid_code_submitted_concurrently_resets_the_password_once()
    {
        var (email, correct) = await RequestResetAsync();

        // Both submissions passed the `ConsumedAt == null` read before either wrote
        // it, so a single-use code used to complete two resets.
        var responses = await Task.WhenAll(
            Enumerable.Range(0, 2).Select(_ => ResetAsync(email, correct)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.BadRequest);

        var stored = await LoadCodeAsync(email);
        Assert.NotNull(stored!.ConsumedAt);
    }

    // -- helpers ---------------------------------------------------------------

    // Six digits so the request passes validation and reaches the attempt logic,
    // and offset from the issued value so none of them can match it by accident.
    private static IEnumerable<string> WrongCodes(string correct)
    {
        var issued = int.Parse(correct, CultureInfo.InvariantCulture);
        return Enumerable.Range(1, Burst).Select(offset =>
            ((issued + offset) % 1_000_000).ToString("D6", CultureInfo.InvariantCulture));
    }

    private async Task<(string Email, string Code)> RequestResetAsync()
    {
        var email = await AuthFlow.RegisterVerifiedVisitorAsync(_client, _factory);
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/forgot-password",
            new ForgotPasswordRequest { Email = email });
        return (email, AuthFlow.GetActiveCode(_factory, email, AccountCodePurpose.PasswordReset));
    }

    private Task<HttpResponseMessage> ResetAsync(string email, string code) =>
        _client.PostAsJsonAsync(
            "/api/v1/app/auth/reset-password",
            new ResetPasswordRequest
            {
                Email = email,
                Code = code,
                NewPassword = NewPassword,
                ConfirmPassword = NewPassword,
            });

    private async Task<AccountCode?> LoadCodeAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email);
        return await db.AccountCodes
            .Where(c => c.UserId == user.Id && c.Purpose == AccountCodePurpose.PasswordReset)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync();
    }
}
