using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Infrastructure.Persistence;
using Xunit;

using SIMF.Common.Enums;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests that the account-creation endpoints write to the operation
/// log with the expected fields (SIMF-FDS-001 section 9).
/// </summary>
public sealed class AuditLogTests : IClassFixture<SimfApiFactory>
{
    private const string ValidPassword = "Zx9#mKp2!";

    private readonly SimfApiFactory _factory;
    private readonly HttpClient _client;

    public AuditLogTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
        _client = factory.CreateClient();
    }

    private static string NewEmail() => $"audit-{Guid.NewGuid():N}@simf.test";

    private Task<HttpResponseMessage> SignUpAsync(string email) =>
        _client.PostAsJsonAsync(
            "/api/v1/app/auth/sign-up",
            new SignUpRequest { Email = email, Password = ValidPassword, ConfirmPassword = ValidPassword });

    [Fact]
    public async Task SignUp_writes_a_success_entry_with_the_subject_user_and_correlation_id()
    {
        var email = NewEmail();

        await SignUpAsync(email);

        var entry = FindAuditEntry(email, AuditEvents.SignUpSucceeded);
        Assert.NotNull(entry);
        Assert.Equal(AuditOutcome.Success, entry!.Outcome);
        Assert.NotNull(entry.SubjectUserId);
        Assert.False(string.IsNullOrWhiteSpace(entry.CorrelationId));
    }

    [Fact]
    public async Task A_restart_of_an_unverified_sign_up_writes_a_restart_audit_entry()
    {
        var email = NewEmail();
        await SignUpAsync(email);

        // Second sign-up of the still-unverified account is a restart, not a
        // duplicate rejection (D-198).
        await SignUpAsync(email);

        var entry = FindAuditEntry(email, AuditEvents.SignUpRestartedUnverified);
        Assert.NotNull(entry);
        Assert.Equal(AuditOutcome.Success, entry!.Outcome);
        Assert.NotNull(entry.SubjectUserId);
    }

    [Fact]
    public async Task A_sign_up_against_a_verified_account_writes_a_deflect_audit_entry()
    {
        var email = NewEmail();
        await SignUpAsync(email);
        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-email",
            new VerifyEmailRequest { Email = email, Code = GetActiveVerificationCode(email) });

        // Sign-up against the now-verified account is deflected (D-198): the
        // owner is notified and the attempt is audited with the duplicate-
        // email code, but no 409 is returned to the caller.
        await SignUpAsync(email);

        var entry = FindAuditEntry(email, AuditEvents.SignUpExistingAccountDeflected);
        Assert.NotNull(entry);
        Assert.Equal(AuditOutcome.Failure, entry!.Outcome);
        Assert.Equal(ErrorCodes.AuthEmailAlreadyRegistered, entry.ErrorCode);
    }

    [Fact]
    public async Task Verify_email_for_an_unknown_account_writes_a_failure_entry()
    {
        var email = NewEmail();

        await _client.PostAsJsonAsync(
            "/api/v1/app/auth/verify-email",
            new VerifyEmailRequest { Email = email, Code = "123456" });

        var entry = FindAuditEntry(email, AuditEvents.EmailVerificationAccountNotFound);
        Assert.NotNull(entry);
        Assert.Equal(AuditOutcome.Failure, entry!.Outcome);
    }

    [Fact]
    public async Task An_audit_entry_records_the_inbound_correlation_id()
    {
        var email = NewEmail();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/app/auth/sign-up")
        {
            Content = JsonContent.Create(new SignUpRequest
            {
                Email = email,
                Password = ValidPassword,
                ConfirmPassword = ValidPassword,
            }),
        };
        request.Headers.Add("X-Correlation-Id", "audit-trace-9");

        await _client.SendAsync(request);

        var entry = FindAuditEntry(email, AuditEvents.SignUpSucceeded);
        Assert.NotNull(entry);
        Assert.Equal("audit-trace-9", entry!.CorrelationId);
    }

    private OperationLogEntry? FindAuditEntry(string email, string eventType)
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return database.OperationLog
            .FirstOrDefault(entry => entry.SubjectEmail == email && entry.EventType == eventType);
    }

    private string GetActiveVerificationCode(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        var user = database.Users.Single(candidate => candidate.Email == email);
        return AuthFlow.RecoverPlaintextCode(database.AccountCodes
            .Where(code => code.UserId == user.Id
                && code.Purpose == AccountCodePurpose.EmailVerification
                && code.ConsumedAt == null)
            .OrderByDescending(code => code.CreatedAt)
            .First()
            .CodeHash);
    }
}
