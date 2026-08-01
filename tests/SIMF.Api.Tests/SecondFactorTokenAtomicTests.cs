// Tests: SIMF.Infrastructure/Persistence/Repositories/SecondFactorTokenRepository.cs
using Microsoft.Extensions.DependencyInjection;
using SIMF.Application.IdentityAccess.Abstractions;
using SIMF.Common.Enums;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Persistence;
using Xunit;
using SIMF.Common;

namespace SIMF.Api.Tests;

/// <summary>
/// A3 (D-590) — the atomic single-use + attempt-count guarantees the sign-in
/// second-factor flow relies on. These exercise
/// <see cref="ISecondFactorTokenRepository"/> directly: the concurrency window
/// the conditional UPDATE closes (two verifies of the same ticket racing between
/// <c>GetValidTicketAsync</c> and the consume) is not deterministically
/// reproducible through the HTTP flow — <c>GetValidTicketAsync</c> catches the
/// sequential replay first — so the atomic contract is pinned at the repository
/// seam instead: a ticket consumes exactly once, and the attempt counter
/// increments without a read-modify-write.
/// </summary>
public sealed class SecondFactorTokenAtomicTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public SecondFactorTokenAtomicTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task TryConsume_flips_the_ticket_exactly_once()
    {
        var ticketId = await SeedTicketAsync();
        var now = SimfClock.Now;

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<ISecondFactorTokenRepository>();

        // The first caller flips ConsumedAt null→now and wins the mint; a
        // concurrent second verify of the same ticket loses and must reject.
        Assert.True(await repository.TryConsumeAsync(ticketId, now));
        Assert.False(await repository.TryConsumeAsync(ticketId, now.AddSeconds(60)));

        var database = scope.ServiceProvider
            .GetRequiredService<SimfIdentityDbContext>();
        var ticket = database.SecondFactorTokens.Single(t => t.Id == ticketId);
        Assert.NotNull(ticket.ConsumedAt);
        // The first consume's timestamp stuck — the losing second consume did
        // not overwrite it (it would be at now+60s otherwise).
        Assert.True(ticket.ConsumedAt < now.AddSeconds(60));
    }

    [Fact]
    public async Task IncrementAttemptCount_is_atomic_and_persists()
    {
        var ticketId = await SeedTicketAsync();

        using var scope = _factory.Services.CreateScope();
        var repository = scope.ServiceProvider
            .GetRequiredService<ISecondFactorTokenRepository>();

        await repository.IncrementAttemptCountAsync(ticketId);
        await repository.IncrementAttemptCountAsync(ticketId);
        await repository.IncrementAttemptCountAsync(ticketId);

        var database = scope.ServiceProvider
            .GetRequiredService<SimfIdentityDbContext>();
        var ticket = database.SecondFactorTokens.Single(t => t.Id == ticketId);
        Assert.Equal(3, ticket.AttemptCount);
    }

    private async Task<Guid> SeedTicketAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var database = scope.ServiceProvider
            .GetRequiredService<SimfIdentityDbContext>();
        // Any seeded user satisfies the SecondFactorToken → SimfUser FK; the
        // super-admin is always seeded, so a repo test need not mint a user.
        var userId = database.Users.OrderBy(u => u.Email).First().Id;
        var ticket = new SecondFactorToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = $"a3-test-{Guid.NewGuid():N}",
            Kind = SecondFactorKind.EmailOtp,
            CreatedAt = SimfClock.Now,
            ExpiresAt = SimfClock.Now.AddMinutes(5),
        };
        database.SecondFactorTokens.Add(ticket);
        await database.SaveChangesAsync();
        return ticket.Id;
    }
}
