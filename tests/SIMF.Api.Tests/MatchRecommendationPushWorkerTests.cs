// FR-803 (register defect `FR-803-80pct-push`) — the ranker sorted by score and
// simply took the top N: no threshold constant, no 0.8 comparison anywhere in
// RecommendationService, no NotificationKind for a match, and no worker pushing
// recommendations.
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SIMF.Application.Notifications;
using SIMF.Application.Recommendations.Abstractions;
using SIMF.Common.Enums;
using SIMF.Contracts.Recommendations;
using SIMF.Domain.IdentityAccess;
using SIMF.Infrastructure.Operations;
using SIMF.Infrastructure.Persistence;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Tests for the FR-803 push. The threshold itself is exercised against the real
/// ranker in <see cref="RecommendationServiceTests"/>; this suite drives the
/// internal <see cref="MatchRecommendationPushWorker.RunPushAsync"/> over a stub
/// ranker so the push contract — one notification per (caller, candidate), never a
/// second — is pinned independently of the scoring.
/// </summary>
[Trait(TestAreas.TraitName, TestAreas.Meetings)]
[Trait(TestAreas.SpeedTraitName, TestAreas.Seeded)]
public sealed class MatchRecommendationPushWorkerTests : IClassFixture<SimfApiFactory>
{
    private readonly SimfApiFactory _factory;

    public MatchRecommendationPushWorkerTests(SimfApiFactory factory)
    {
        _factory = factory;
        _factory.EnsureDatabaseCreated();
    }

    [Fact]
    public async Task Each_strong_match_is_pushed_once_per_caller_candidate_pair()
    {
        // The CALLER must be a real account: the push writes a Notification, whose
        // UserId carries a real FK to AspNetUsers. Candidate ids need no account —
        // they land in RelatedEntityId, a bare Guid with no constraint.
        var callerId = await CreateCallerAsync();
        var firstCandidate = Guid.NewGuid();
        var secondCandidate = Guid.NewGuid();
        var ranker = new StubRanker(Match(firstCandidate), Match(secondCandidate));

        var pushed = await RunPushAsync([callerId], ranker);
        Assert.Equal(2, pushed);
        Assert.Equal(1, await PushCountAsync(callerId, firstCandidate));
        Assert.Equal(1, await PushCountAsync(callerId, secondCandidate));

        // Second pass: the D-713 dispatcher guard is the per-pair dedup stamp, so
        // re-running the batch changes nothing.
        await RunPushAsync([callerId], ranker);
        Assert.Equal(1, await PushCountAsync(callerId, firstCandidate));
        Assert.Equal(1, await PushCountAsync(callerId, secondCandidate));
    }

    [Fact]
    public async Task Caller_with_no_strong_match_is_not_interrupted()
    {
        var callerId = Guid.NewGuid();

        var pushed = await RunPushAsync([callerId], new StubRanker());

        Assert.Equal(0, pushed);
        using var scope = _factory.Services.CreateScope();
        var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        Assert.False(await idDb.Notifications.AnyAsync(n =>
            n.UserId == callerId && n.Kind == NotificationKind.MatchRecommended));
    }

    [Fact]
    public async Task One_callers_failure_does_not_abort_the_batch()
    {
        var failing = await CreateCallerAsync();
        var healthy = await CreateCallerAsync();
        var candidate = Guid.NewGuid();
        var ranker = new StubRanker(Match(candidate)) { ThrowForCallerId = failing };

        var pushed = await RunPushAsync([failing, healthy], ranker);

        Assert.Equal(1, pushed);
        Assert.Equal(1, await PushCountAsync(healthy, candidate));
    }

    [Fact]
    public async Task Batching_only_offers_opted_in_profiles_with_interests()
    {
        // The worker pages the roster in batches; only a profile that opted into
        // "Meet People Like You" AND picked at least one interest can be matched at
        // all, so an opted-out profile must never enter a batch.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SimfAppDbContext>();

        var batch = await MatchRecommendationPushWorker.NextBatchAsync(
            db, skip: 0, MatchRecommendationPushWorker.BatchSize, CancellationToken.None);

        var optedOut = await db.UserProfiles
            .AsNoTracking()
            .Where(p => !p.ShowInMeetLikeYou)
            .Select(p => p.UserId)
            .ToListAsync();
        Assert.DoesNotContain(batch, id => optedOut.Contains(id));
        Assert.True(batch.Count <= MatchRecommendationPushWorker.BatchSize);
    }

    // -- Helpers --------------------------------------------------------------

    private async Task<int> RunPushAsync(IReadOnlyList<Guid> callerIds, IRecommendationService ranker)
    {
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
        return await MatchRecommendationPushWorker.RunPushAsync(
            callerIds, ranker, dispatcher, NullLogger.Instance, CancellationToken.None);
    }

    /// <summary>A real caller account. The push writes a Notification, and
    /// <c>FK_Notifications_AspNetUsers_UserId</c> means a fabricated caller id
    /// fails the INSERT — the batch then reports 0 pushed and the assertion reads
    /// as a worker bug rather than a fixture one.</summary>
    private async Task<Guid> CreateCallerAsync()
    {
        var email = $"match-caller-{Guid.NewGuid():N}@simf.test";
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<SimfUser>>();
        var user = new SimfUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = "Match Caller",
            AccountState = AccountState.Approved,
        };
        await users.CreateAsync(user, AuthFlow.Password);
        return user.Id;
    }

    private async Task<int> PushCountAsync(Guid callerId, Guid candidateProfileId)
    {
        using var scope = _factory.Services.CreateScope();
        var idDb = scope.ServiceProvider.GetRequiredService<SimfIdentityDbContext>();
        return await idDb.Notifications.CountAsync(n =>
            n.UserId == callerId
            && n.Kind == NotificationKind.MatchRecommended
            && n.RelatedEntityId == candidateProfileId);
    }

    private static RecommendationEntry Match(Guid profileId) =>
        new(profileId, "Nora Al Qahtani", "نورة القحطاني", "Captain",
            "VIP", "كبار الشخصيات",
            [new MatchedInterest(Guid.NewGuid(), "Maritime security", "الأمن البحري")],
            1, 0.9, 0, "shared interest in Maritime security", "اهتمام مشترك بالأمن البحري");

    /// <summary>Returns a fixed strong-match set, so the push contract is tested
    /// without depending on the ranker's scoring (covered separately).</summary>
    private sealed class StubRanker(params RecommendationEntry[] matches) : IRecommendationService
    {
        public Guid? ThrowForCallerId { get; init; }

        public Task<RecommendationsResponse> MeetPeopleLikeYouAsync(
            Guid callerUserId, int take, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The push uses StrongMatchesAsync.");

        public Task<RecommendationsResponse> StrongMatchesAsync(
            Guid callerUserId, CancellationToken cancellationToken = default)
        {
            if (ThrowForCallerId == callerUserId)
            {
                throw new InvalidOperationException("ranker blew up for this caller");
            }
            return Task.FromResult(new RecommendationsResponse(matches));
        }
    }
}
