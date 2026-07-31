// FR-803 (register defect `FR-803-80pct-push`) — RecommendationService had no
// threshold constant and no 0.8 comparison anywhere in the file: it sorted by
// score and simply took the top N, so the weakest possible overlap ranked as a
// "recommendation" whenever nothing better existed.
using SIMF.Infrastructure.Recommendations;
using Xunit;

namespace SIMF.Api.Tests;

/// <summary>
/// Unit tests for the FR-803 match threshold. Deliberately narrow: the ranking
/// itself is covered by <see cref="RecommendationServiceTests"/>; what is pinned
/// here is that a threshold exists, that it is the stated 80%, and that it is
/// compared against a number that can actually BE a percentage.
/// </summary>
public sealed class RecommendationThresholdTests
{
    [Fact]
    public void Threshold_is_the_stated_eighty_percent()
    {
        Assert.Equal(0.80, RecommendationService.StrongMatchThreshold);
    }

    [Theory]
    // A perfect Jaccard plus the same-tier tie-break exceeds 1.0 — clamped, so the
    // comparison is against a real percentage.
    [InlineData(1.05, 1.0)]
    [InlineData(1.0, 1.0)]
    [InlineData(0.5, 0.5)]
    [InlineData(0.0, 0.0)]
    // A score can never go negative, but the clamp is total rather than one-sided.
    [InlineData(-0.2, 0.0)]
    public void Score_is_normalised_into_zero_to_one(double raw, double expected)
    {
        Assert.Equal(expected, RecommendationService.NormaliseScore(raw), 6);
    }

    [Theory]
    [InlineData(0.79, false)]
    [InlineData(0.80, true)]
    [InlineData(0.95, true)]
    // Above 1.0 must still qualify after the clamp, not fall out of the comparison.
    [InlineData(1.05, true)]
    public void Only_scores_at_or_above_the_threshold_qualify(double raw, bool qualifies)
    {
        var normalised = RecommendationService.NormaliseScore(raw);
        Assert.Equal(qualifies, normalised >= RecommendationService.StrongMatchThreshold);
    }
}
