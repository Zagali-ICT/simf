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
[Trait(TestAreas.TraitName, TestAreas.Ops)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
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

    [Fact]
    public async Task A_batched_write_persists_every_entry_in_the_set()
    {
        var email = NewEmail();
        var entries = Enumerable.Range(1, 3)
            .Select(index => new AuditEntry
            {
                EventType = AuditEvents.SeatReservationReleased,
                Outcome = AuditOutcome.Success,
                SubjectEmail = email,
                Detail = $"batched-entry-{index}",
            })
            .ToList();

        using (var scope = _factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IAuditLog>().WriteManyAsync(entries);
        }

        // One save for the whole set, but still one row per entry carrying its own
        // detail: the pre-start no-show sweep audits a seat at a time, and batching
        // the write must not collapse that into a single summary row.
        var details = FindAuditEntries(email)
            .Select(entry => entry.Detail ?? string.Empty)
            .OrderBy(detail => detail, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(
            new List<string> { "batched-entry-1", "batched-entry-2", "batched-entry-3" },
            details);
    }

    [Fact]
    public async Task A_failed_batched_write_is_swallowed_rather_than_failing_the_caller()
    {
        var email = NewEmail();
        using var scope = _factory.Services.CreateScope();
        var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLog>();
        // The audit log writes through the SAME scoped context, so disposing it is
        // the cheapest way to make the write fail for real rather than simulate it.
        scope.ServiceProvider.GetRequiredService<SimfAppDbContext>().Dispose();

        await auditLog.WriteManyAsync(
        [
            new AuditEntry
            {
                EventType = AuditEvents.SeatReservationReleased,
                Outcome = AuditOutcome.Success,
                SubjectEmail = email,
            },
        ]);

        // Nothing landed, and nothing was thrown, which is the point of the test:
        // an audit failure must never break the operation it records, and the
        // batched write inherits that posture from the single-entry one.
        Assert.Empty(FindAuditEntries(email));
    }

    private OperationLogEntry? FindAuditEntry(string email, string eventType)
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return database.OperationLog
            .FirstOrDefault(entry => entry.SubjectEmail == email && entry.EventType == eventType);
    }

    private List<OperationLogEntry> FindAuditEntries(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return database.OperationLog
            .Where(entry => entry.SubjectEmail == email)
            .ToList();
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
            .Code);
    }
}
