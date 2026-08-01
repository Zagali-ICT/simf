// Tests: D-809 — the mapping from a desk's offline badge sequence to the
// 12-character QR id every server-side reader resolves by.
using SIMF.Common.Badges;
using Xunit;

namespace SIMF.Application.Tests;

public sealed class OfflineBadgeIdTests
{
    /// <summary>The alphabet <c>QrIdMinter</c> draws from. Copied deliberately:
    /// the disjointness guarantee below is only true while the minter excludes
    /// 0 and 1, so this test fails loudly if that ever changes.</summary>
    private const string MinterAlphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";

    [Theory]
    [InlineData(1L, "W00000000001")]
    [InlineData(3_000_042L, "W00003000042")]
    [InlineData(OfflineBadgeId.MaxSequence, "W09999999999")]
    public void Formats_a_sequence_as_a_twelve_character_id(long sequence, string expected)
    {
        Assert.True(OfflineBadgeId.TryFormat(sequence, out var qrId));
        Assert.Equal(expected, qrId);
        Assert.Equal(OfflineBadgeId.QrIdLength, qrId.Length);
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(OfflineBadgeId.MaxSequence + 1)]
    public void Refuses_a_sequence_outside_the_accepted_range(long sequence)
    {
        // Refuses rather than throws: the caller is an upload of desk-entered
        // data where a bad row is reported, not fatal.
        Assert.False(OfflineBadgeId.TryFormat(sequence, out var qrId));
        Assert.Equal(string.Empty, qrId);
    }

    [Theory]
    [InlineData(1L)]
    [InlineData(3_000_042L)]
    [InlineData(OfflineBadgeId.MaxSequence)]
    public void Round_trips_a_sequence(long sequence)
    {
        Assert.True(OfflineBadgeId.TryFormat(sequence, out var qrId));
        Assert.True(OfflineBadgeId.TryParse(qrId, out var parsed));
        Assert.Equal(sequence, parsed);
    }

    [Fact]
    public void An_offline_id_can_never_collide_with_a_minted_one()
    {
        // This is the property that lets an offline registration share the QrId
        // column with server-minted ids and rely on its UNIQUE constraint alone.
        // MaxSequence is one below 10^10, so the 11-digit padding always leaves a
        // '0' in position 1 — and '0' is not in the minter's alphabet.
        Assert.DoesNotContain('0', MinterAlphabet);

        foreach (var sequence in new[] { 1L, 999L, 3_000_042L, OfflineBadgeId.MaxSequence })
        {
            Assert.True(OfflineBadgeId.TryFormat(sequence, out var qrId));
            Assert.Equal('0', qrId[1]);
        }
    }

    [Theory]
    [InlineData("K7Q2M9ABCDEF")] // a plausible minted id
    [InlineData("W0000000000")]  // one character short
    [InlineData("W000000000012")] // one character long
    [InlineData("WABCDEFGHIJK")] // right shape, not digits
    [InlineData("W-0000000001")] // a sign is not a digit
    [InlineData(null)]
    [InlineData("")]
    public void Does_not_read_a_sequence_out_of_anything_else(string? qrId)
    {
        Assert.False(OfflineBadgeId.TryParse(qrId, out var sequence));
        Assert.Equal(0, sequence);
        Assert.False(OfflineBadgeId.IsOfflineBadge(qrId));
    }

    [Fact]
    public void Reads_a_lower_case_id_the_scanner_normalised()
    {
        Assert.True(OfflineBadgeId.TryFormat(3_000_042L, out var qrId));
        Assert.True(OfflineBadgeId.TryParse(qrId.ToLowerInvariant(), out var parsed));
        Assert.Equal(3_000_042L, parsed);
    }
}
