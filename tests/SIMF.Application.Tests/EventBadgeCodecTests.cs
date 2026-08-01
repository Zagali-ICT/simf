using System.Security.Cryptography;
using FluentAssertions;
using SIMF.Common.Badges;
using SIMF.Common.Options;

namespace SIMF.Application.Tests;

/// <summary>
/// D-809 — the offline event badge codec. These tests are the contract the
/// Windows desk generator and the mobile scanner both have to satisfy: a badge
/// minted at a disconnected desk must validate at a disconnected gate, and
/// anything that is not a genuine badge must be refused rather than throwing.
/// </summary>
public class EventBadgeCodecTests
{
    private static byte[] NewKey()
    {
        var key = new byte[EventBadgeCodec.KeyBytes];
        RandomNumberGenerator.Fill(key);
        return key;
    }

    [Theory]
    [InlineData(1, 1L)]
    [InlineData(3, 3_000_042L)]
    [InlineData(0, 0L)]
    [InlineData(31, 9_999_999_999L)]
    public void RoundTrips(int profileTypeCode, long sequence)
    {
        var key = NewKey();
        var encoded = EventBadgeCodec.Encode(
            new EventBadgePayload(profileTypeCode, sequence), key, keyVersion: 0);

        EventBadgeCodec.TryDecode(encoded, key, out var decoded).Should().BeTrue();
        decoded.ProfileTypeCode.Should().Be(profileTypeCode);
        decoded.Sequence.Should().Be(sequence);
    }

    [Fact]
    public void StaysShortEnoughToPrintAndStore()
    {
        var encoded = EventBadgeCodec.Encode(
            new EventBadgePayload(3, 3_000_042L), NewKey(), keyVersion: 0);

        // The GateScan.QrIdAtScan column is nvarchar(64) after D-809; a badge
        // that does not fit would be truncated on insert.
        encoded.Length.Should().BeLessThan(64);
    }

    [Fact]
    public void SurvivesTheUppercasingEveryScannedQrGoesThrough()
    {
        // QrId.Normalise upper-cases every scanned value before it is resolved.
        // This is precisely why the payload is base32 and not base64.
        var key = NewKey();
        var encoded = EventBadgeCodec.Encode(
            new EventBadgePayload(7, 12_345L), key, keyVersion: 0);

        var normalised = encoded.Trim().ToUpperInvariant();

        EventBadgeCodec.TryDecode(normalised, key, out var decoded).Should().BeTrue();
        decoded.Should().Be(new EventBadgePayload(7, 12_345L));
    }

    [Fact]
    public void RejectsAnotherKeysBadge()
    {
        var encoded = EventBadgeCodec.Encode(
            new EventBadgePayload(3, 42L), NewKey(), keyVersion: 0);

        EventBadgeCodec.TryDecode(encoded, NewKey(), out _).Should().BeFalse();
    }

    [Fact]
    public void RejectsATamperedBadge()
    {
        var key = NewKey();
        var encoded = EventBadgeCodec.Encode(
            new EventBadgePayload(3, 42L), key, keyVersion: 0);

        // Flip one character in the ciphertext body. A hand-made badge claiming
        // a different profile type must not authenticate.
        var index = encoded.Length - 3;
        var swapped = encoded[index] == 'A' ? 'B' : 'A';
        var tampered = encoded[..index] + swapped + encoded[(index + 1)..];

        EventBadgeCodec.TryDecode(tampered, key, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("!!!!")]
    [InlineData("0")]
    [InlineData("0AAAA")]
    public void RefusesMalformedInputWithoutThrowing(string? candidate)
    {
        // A garbled or hostile scan is an ordinary denial, never an exception.
        EventBadgeCodec.TryDecode(candidate, NewKey(), out _).Should().BeFalse();
    }

    [Fact]
    public void RefusesAnOverlongInput()
    {
        var overlong = new string('A', EventBadgeCodec.MaxEncodedLength + 1);

        EventBadgeCodec.TryDecode(overlong, NewKey(), out _).Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(31)]
    public void ExposesTheKeyVersionWithoutDecrypting(int keyVersion)
    {
        var encoded = EventBadgeCodec.Encode(
            new EventBadgePayload(3, 42L), NewKey(), keyVersion);

        EventBadgeCodec.TryReadKeyVersion(encoded, out var read).Should().BeTrue();
        read.Should().Be(keyVersion);
    }

    [Fact]
    public void EncodesDifferentlyEachTimeSoBadgesAreNotCorrelatable()
    {
        var key = NewKey();
        var payload = new EventBadgePayload(3, 42L);

        var first = EventBadgeCodec.Encode(payload, key, keyVersion: 0);
        var second = EventBadgeCodec.Encode(payload, key, keyVersion: 0);

        first.Should().NotBe(second, "each badge carries a fresh random nonce");
        EventBadgeCodec.TryDecode(first, key, out var a).Should().BeTrue();
        EventBadgeCodec.TryDecode(second, key, out var b).Should().BeTrue();
        a.Should().Be(b);
    }

    [Fact]
    public void RejectsAKeyThatIsNotAes256()
    {
        var payload = new EventBadgePayload(3, 42L);
        var shortKey = new byte[16];

        var encode = () => EventBadgeCodec.Encode(payload, shortKey, keyVersion: 0);

        encode.Should().Throw<ArgumentException>();
    }
}

/// <summary>
/// D-809 — the arming semantics. The whole point of the walk-in mode is that it
/// is inert until deliberately switched on, so "off by default" and
/// "fail-closed" are the behaviours worth pinning.
/// </summary>
public class WalkInModeOptionsTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsDisarmedByDefault()
    {
        var options = new WalkInModeOptions();

        options.IsArmed(Now).Should().BeFalse();
        options.QuickRegisterActive(Now).Should().BeFalse();
        options.AutoApproveActive(Now).Should().BeFalse();
        options.SessionWalkInActive(Now).Should().BeFalse();
        options.AcceptOfflineBadgesActive(Now).Should().BeFalse();
        options.OfflineUploadActive(Now).Should().BeFalse();
        options.BadgeActivationAllowedForWalkIns.Should().BeFalse();
    }

    [Fact]
    public void MasterSwitchOffKeepsEveryRuleInertEvenWhenIndividuallySet()
    {
        var options = new WalkInModeOptions
        {
            Enabled = false,
            QuickRegister = true,
            AutoApprove = true,
            SessionWalkIn = true,
        };

        options.QuickRegisterActive(Now).Should().BeFalse();
        options.AutoApproveActive(Now).Should().BeFalse();
        options.SessionWalkInActive(Now).Should().BeFalse();
    }

    [Fact]
    public void ExpiryDisarmsEverythingOnRead()
    {
        var options = new WalkInModeOptions
        {
            Enabled = true,
            AutoApprove = true,
            ExpiresAtUtc = Now.AddMinutes(-1),
        };

        options.IsArmed(Now).Should().BeFalse();
        options.AutoApproveActive(Now).Should().BeFalse();
    }

    [Fact]
    public void ArmsWhenEnabledAndNotExpired()
    {
        var options = new WalkInModeOptions
        {
            Enabled = true,
            AutoApprove = true,
            ExpiresAtUtc = Now.AddHours(1),
        };

        options.IsArmed(Now).Should().BeTrue();
        options.AutoApproveActive(Now).Should().BeTrue();
    }

    [Fact]
    public void ArrivalGraceFallsBackToFifteenMinutesWhenDisarmed()
    {
        var options = new WalkInModeOptions { Enabled = false, ArrivalGraceMinutes = 90 };

        options.ResolveArrivalGrace(Now).Should().Be(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public void ArrivalGraceIsClampedWhenArmed()
    {
        var options = new WalkInModeOptions { Enabled = true, ArrivalGraceMinutes = 9_999 };

        options.ResolveArrivalGrace(Now).Should().Be(TimeSpan.FromMinutes(240));
    }

    [Fact]
    public void OfflineBadgesStayOffWhenTheKeyIsMissingOrMalformed()
    {
        var missing = new WalkInModeOptions { Enabled = true, AcceptOfflineBadges = true };
        missing.AcceptOfflineBadgesActive(Now).Should().BeFalse();

        var malformed = new WalkInModeOptions
        {
            Enabled = true,
            AcceptOfflineBadges = true,
            BadgeKey = "not-base64",
        };
        malformed.AcceptOfflineBadgesActive(Now).Should().BeFalse();

        var tooShort = new WalkInModeOptions
        {
            Enabled = true,
            AcceptOfflineBadges = true,
            BadgeKey = Convert.ToBase64String(new byte[16]),
        };
        tooShort.AcceptOfflineBadgesActive(Now).Should().BeFalse();
    }

    [Fact]
    public void ResolvesBothTheCurrentAndPreviousKeyDuringRotation()
    {
        var current = new byte[EventBadgeCodec.KeyBytes];
        var previous = new byte[EventBadgeCodec.KeyBytes];
        RandomNumberGenerator.Fill(current);
        RandomNumberGenerator.Fill(previous);

        var options = new WalkInModeOptions
        {
            Enabled = true,
            AcceptOfflineBadges = true,
            BadgeKey = Convert.ToBase64String(current),
            BadgeKeyVersion = 2,
            PreviousBadgeKey = Convert.ToBase64String(previous),
            PreviousBadgeKeyVersion = 1,
        };

        options.KeyForVersion(2).Should().Equal(current);
        options.KeyForVersion(1).Should().Equal(previous);
        options.KeyForVersion(3).Should().BeNull();
    }
}
