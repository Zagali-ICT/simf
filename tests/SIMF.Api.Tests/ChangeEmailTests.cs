// #24 — self-service change-email: POST /app/auth/change-email/send-otp emails a
// code to the NEW address (uniqueness + same-email + rate-limit guards); POST
// /app/auth/change-email/confirm verifies the code (bound to the target address),
// moves the login email, marks it confirmed and rolls the security stamp.
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.IdentityAccess;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Contracts.Authentication;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

public sealed class ChangeEmailTests : IClassFixture<SimfApiFactory>
{
    private const string SendOtpUrl = "/api/v1/app/auth/change-email/send-otp";
    private const string ConfirmUrl = "/api/v1/app/auth/change-email/confirm";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public ChangeEmailTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Send_otp_returns_masked_new_email_and_issues_a_code()
    {
        var (token, userId, _) = await CreateApprovedVisitorAsync();
        var newEmail = $"new-{Guid.NewGuid():N}@simf.test";

        var send = await PostAuthAsync(
            SendOtpUrl, new SendEmailChangeOtpRequest { NewEmail = newEmail }, token);

        Assert.Equal(HttpStatusCode.OK, send.StatusCode);
        var result = (await send.Content
            .ReadFromJsonAsync<ApiResult<SendEmailChangeOtpResponse>>())!.Data!;
        Assert.StartsWith("n***@", result.MaskedNewEmail);
        Assert.EndsWith("@simf.test", result.MaskedNewEmail);
        Assert.Equal(600, result.ExpiresInSeconds);
        Assert.Equal(120, result.ResendCooldownSeconds);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var count = await db.AccountCodes.CountAsync(c =>
            c.UserId == userId
            && c.Purpose == AccountCodePurpose.EmailChangeVerification);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Send_otp_same_as_current_email_is_400_AUTH_EMAIL_UNCHANGED()
    {
        var (token, _, currentEmail) = await CreateApprovedVisitorAsync();

        var send = await PostAuthAsync(
            SendOtpUrl, new SendEmailChangeOtpRequest { NewEmail = currentEmail }, token);

        Assert.Equal(HttpStatusCode.BadRequest, send.StatusCode);
        var body = (await send.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AuthEmailUnchanged, body.Error!.Code);
    }

    [Fact]
    public async Task Send_otp_taken_email_is_deflected_generic_success_with_no_code()
    {
        var (token, userId, _) = await CreateApprovedVisitorAsync();
        var taken = $"taken-{Guid.NewGuid():N}@simf.test";
        await CreateVisitorAsync(taken);

        var send = await PostAuthAsync(
            SendOtpUrl, new SendEmailChangeOtpRequest { NewEmail = taken }, token);

        // Enumeration-resistant: a taken address returns the SAME 200 success shape
        // as a free one but issues NO code (the real owner is warned instead), so
        // an authenticated caller cannot tell registered from unregistered.
        Assert.Equal(HttpStatusCode.OK, send.StatusCode);
        var result = (await send.Content
            .ReadFromJsonAsync<ApiResult<SendEmailChangeOtpResponse>>())!.Data!;
        Assert.StartsWith("t***@", result.MaskedNewEmail);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var count = await db.AccountCodes.CountAsync(c =>
            c.UserId == userId
            && c.Purpose == AccountCodePurpose.EmailChangeVerification);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Confirm_wrong_password_is_401_and_does_not_burn_the_code()
    {
        var (token, userId, _) = await CreateApprovedVisitorAsync();
        var newEmail = $"pw-{Guid.NewGuid():N}@simf.test";
        await SeedEmailChangeCodeAsync(userId, "123456", newEmail);

        var confirm = await PostAuthAsync(
            ConfirmUrl,
            new ConfirmEmailChangeRequest
            {
                NewEmail = newEmail, Code = "123456", CurrentPassword = "WrongPassword1!",
            },
            token);

        Assert.Equal(HttpStatusCode.Unauthorized, confirm.StatusCode);
        var body = (await confirm.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, body.Error!.Code);

        // The password check runs before the code, so a wrong password neither
        // changes the email nor consumes the still-valid code.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var code = await db.AccountCodes.AsNoTracking().SingleAsync(c =>
            c.UserId == userId
            && c.Purpose == AccountCodePurpose.EmailChangeVerification);
        Assert.Null(code.ConsumedAt);
    }

    [Fact]
    public async Task Send_otp_invalid_email_is_400_validation()
    {
        var (token, _, _) = await CreateApprovedVisitorAsync();

        var send = await PostAuthAsync(
            SendOtpUrl, new SendEmailChangeOtpRequest { NewEmail = "not-an-email" }, token);

        Assert.Equal(HttpStatusCode.BadRequest, send.StatusCode);
        var body = (await send.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.ValidationFailed, body.Error!.Code);
    }

    [Fact]
    public async Task Send_otp_without_auth_is_401()
    {
        var send = await _client.PostAsJsonAsync(
            SendOtpUrl, new SendEmailChangeOtpRequest { NewEmail = "x@simf.test" });

        Assert.Equal(HttpStatusCode.Unauthorized, send.StatusCode);
    }

    [Fact]
    public async Task Send_otp_is_capped_per_account_per_window()
    {
        var (token, _, _) = await CreateApprovedVisitorAsync();

        // Distinct free addresses each time, so this exercises the per-ACCOUNT cap
        // (5 codes/hour) in isolation from the per-email limiter now on the route.
        for (var i = 0; i < 5; i++)
        {
            var ok = await PostAuthAsync(
                SendOtpUrl,
                new SendEmailChangeOtpRequest { NewEmail = $"cap-{Guid.NewGuid():N}@simf.test" },
                token);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        var sixth = await PostAuthAsync(
            SendOtpUrl,
            new SendEmailChangeOtpRequest { NewEmail = $"cap-{Guid.NewGuid():N}@simf.test" },
            token);
        Assert.Equal(HttpStatusCode.TooManyRequests, sixth.StatusCode);
        var body = (await sixth.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.RateLimitExceeded, body.Error!.Code);
    }

    [Fact]
    public async Task Confirm_with_valid_code_changes_email_confirms_and_rolls_stamp()
    {
        var (token, userId, _) = await CreateApprovedVisitorAsync();
        var newEmail = $"moved-{Guid.NewGuid():N}@simf.test";
        await SeedEmailChangeCodeAsync(userId, "123456", newEmail);
        var stampBefore = await StampOfAsync(userId);

        var confirm = await PostAuthAsync(
            ConfirmUrl,
            new ConfirmEmailChangeRequest
            {
                NewEmail = newEmail, Code = "123456", CurrentPassword = AuthFlow.Password,
            },
            token);

        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var result = (await confirm.Content
            .ReadFromJsonAsync<ApiResult<ConfirmEmailChangeResponse>>())!.Data!;
        Assert.True(result.EmailChanged);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId);
        Assert.Equal(newEmail, user.Email);
        Assert.Equal(newEmail, user.UserName);
        Assert.Equal(newEmail.ToUpperInvariant(), user.NormalizedEmail);
        Assert.True(user.EmailConfirmed);
        Assert.NotEqual(stampBefore, user.SecurityStamp);
    }

    [Fact]
    public async Task Confirm_is_single_use()
    {
        var (token, userId, _) = await CreateApprovedVisitorAsync();
        var newEmail = $"once-{Guid.NewGuid():N}@simf.test";
        await SeedEmailChangeCodeAsync(userId, "123456", newEmail);

        var first = await PostAuthAsync(
            ConfirmUrl,
            new ConfirmEmailChangeRequest
            {
                NewEmail = newEmail, Code = "123456", CurrentPassword = AuthFlow.Password,
            },
            token);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // The stamp rolled, so the old access token is now invalid — a stale
        // token can no longer replay the (already consumed) code.
        var reuse = await PostAuthAsync(
            ConfirmUrl,
            new ConfirmEmailChangeRequest
            {
                NewEmail = newEmail, Code = "123456", CurrentPassword = AuthFlow.Password,
            },
            token);
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
    }

    [Fact]
    public async Task Confirm_wrong_code_is_400_AUTH_CODE_INVALID()
    {
        var (token, userId, _) = await CreateApprovedVisitorAsync();
        var newEmail = $"wrong-{Guid.NewGuid():N}@simf.test";
        await SeedEmailChangeCodeAsync(userId, "123456", newEmail);

        var confirm = await PostAuthAsync(
            ConfirmUrl,
            new ConfirmEmailChangeRequest
            {
                NewEmail = newEmail, Code = "999999", CurrentPassword = AuthFlow.Password,
            },
            token);

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
        var body = (await confirm.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AuthCodeInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Confirm_code_bound_to_target_rejects_a_different_address()
    {
        var (token, userId, _) = await CreateApprovedVisitorAsync();
        var issuedFor = $"issued-{Guid.NewGuid():N}@simf.test";
        var other = $"other-{Guid.NewGuid():N}@simf.test";
        // The code was issued for `issuedFor`; the same plaintext must NOT confirm
        // a change to a different address (the hash is bound to the target email).
        await SeedEmailChangeCodeAsync(userId, "123456", issuedFor);

        var confirm = await PostAuthAsync(
            ConfirmUrl,
            new ConfirmEmailChangeRequest
            {
                NewEmail = other, Code = "123456", CurrentPassword = AuthFlow.Password,
            },
            token);

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
        var body = (await confirm.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AuthCodeInvalid, body.Error!.Code);
    }

    [Fact]
    public async Task Confirm_expired_code_is_400_AUTH_CODE_EXPIRED()
    {
        var (token, userId, _) = await CreateApprovedVisitorAsync();
        var newEmail = $"expired-{Guid.NewGuid():N}@simf.test";
        await SeedEmailChangeCodeAsync(
            userId, "123456", newEmail,
            expiresAt: _factory.Time.GetUtcNow().AddMinutes(-1));

        var confirm = await PostAuthAsync(
            ConfirmUrl,
            new ConfirmEmailChangeRequest
            {
                NewEmail = newEmail, Code = "123456", CurrentPassword = AuthFlow.Password,
            },
            token);

        Assert.Equal(HttpStatusCode.BadRequest, confirm.StatusCode);
        var body = (await confirm.Content.ReadFromJsonAsync<ApiResult<object>>())!;
        Assert.Equal(ErrorCodes.AuthCodeExpired, body.Error!.Code);
    }

    // -- helpers --------------------------------------------------------------

    /// <summary>Inserts a real <see cref="AccountCode"/> whose hash matches
    /// <paramref name="plaintext"/> bound to <paramref name="newEmail"/> — the
    /// same binding the service compares against (Hash("{code}:{email-lower}")).</summary>
    private async Task SeedEmailChangeCodeAsync(
        Guid userId, string plaintext, string newEmail, DateTimeOffset? expiresAt = null)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        db.AccountCodes.Add(new AccountCode
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Purpose = AccountCodePurpose.EmailChangeVerification,
            Code = AccountCodeHasher.Hash($"{plaintext}:{newEmail.Trim().ToLowerInvariant()}"),
            CreatedAt = _factory.Time.GetUtcNow(),
            ExpiresAt = expiresAt ?? _factory.Time.GetUtcNow().AddMinutes(10),
        });
        await db.SaveChangesAsync();
    }

    private async Task<string?> StampOfAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        return (await db.Users.AsNoTracking().SingleAsync(u => u.Id == userId)).SecurityStamp;
    }

    private async Task<Guid> CreateVisitorAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email, Email = email, EmailConfirmed = true,
            DisplayName = "Other Visitor",
            AccountState = AccountState.Approved,
            UserType = UserType.Visitor,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }

    private async Task<(string accessToken, Guid userId, string email)> CreateApprovedVisitorAsync()
    {
        var email = $"ce-{Guid.NewGuid():N}@simf.test";
        Guid userId;
        using (var scope = _factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
            var user = new SimfUser
            {
                UserName = email, Email = email, EmailConfirmed = true,
                DisplayName = "CE Visitor",
                AccountState = AccountState.Approved,
                UserType = UserType.Visitor,
            };
            await users.CreateAsync(user, AuthFlow.Password);
            userId = user.Id;
        }

        var sign = await _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-in",
            new SignInRequest { Email = email, Password = AuthFlow.Password });
        var envelope = (await sign.Content.ReadFromJsonAsync<ApiResult<SignInResponse>>())!;
        return (envelope.Data!.Tokens!.AccessToken, userId, email);
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
