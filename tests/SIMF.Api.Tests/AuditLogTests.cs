using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.Auditing;
using SIMF.Contracts.Authentication;
using SIMF.Domain.Auditing;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Integration tests that the account-creation endpoints write to the operation
/// log (SIMF-FDS-001 section 9).
/// </summary>
public sealed class AuditLogTests : IClassFixture<SimfApiFactory>
{
    private const string ValidPassword = "Passw0rd!";

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
            "/api/v1/auth/sign-up",
            new SignUpRequest { Email = email, Password = ValidPassword, ConfirmPassword = ValidPassword });

    [Fact]
    public async Task SignUp_writes_a_success_audit_entry()
    {
        var email = NewEmail();

        await SignUpAsync(email);

        var entry = FindAuditEntry(email, AuditEvents.SignUpSucceeded);
        Assert.NotNull(entry);
        Assert.Equal(AuditOutcome.Success, entry!.Outcome);
    }

    [Fact]
    public async Task A_duplicate_sign_up_writes_a_failure_audit_entry()
    {
        var email = NewEmail();
        await SignUpAsync(email);

        await SignUpAsync(email);

        var entry = FindAuditEntry(email, AuditEvents.SignUpDuplicateEmail);
        Assert.NotNull(entry);
        Assert.Equal(AuditOutcome.Failure, entry!.Outcome);
    }

    private OperationLogEntry? FindAuditEntry(string email, string eventType)
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();
        return database.OperationLog
            .FirstOrDefault(entry => entry.SubjectEmail == email && entry.EventType == eventType);
    }
}
