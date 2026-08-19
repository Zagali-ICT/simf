using System.Security.Cryptography;
using System.Text.RegularExpressions;
using FluentAssertions;
using SIMF.Common.Badges;
using SIMF.Common.Options;

namespace SIMF.Application.Tests;

/// <summary>
/// D-819 — the offline event badge codec. These tests are the contract the
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
    [InlineData(1, 2026)]
    [InlineData(3, 2027)]
    [InlineData(0, 2000)]
    [InlineData(65535, 2999)]
    public void RoundTrips(int profileTypeCode, int editionYear)
    {
        var key = NewKey();
        var profileId = Guid.NewGuid();
        var encoded = EventBadgeCodec.Encode(
            new EventBadgePayload(profileId, editionYear, profileTypeCode), key, keyVersion: 0);

        EventBadgeCodec.TryDecode(encoded, key, out var decoded).Should().BeTrue();
        // The profile id is the one that matters: it is what the server seeks by
        // primary key, so a byte-order slip here would resolve to nobody.
        decoded.ProfileId.Should().Be(profileId);
        decoded.EditionYear.Should().Be(editionYear);
        decoded.ProfileTypeCode.Should().Be(profileTypeCode);
    }

    [Fact]
    public void APayloadThisSystemDidNotAuthorIsRefusedEvenUnderTheRightKey()
    {
        // The plaintext is a fixed 20 bytes now, so a decrypt that succeeds but
        // yields any other width is something else encrypted under the same key.
        // There is no ASCII check left to lean on: every byte is legitimately
        // arbitrary once the payload is raw binary.
        var key = NewKey();
        var foreign = new byte[] { 1, 2, 3 };
        var blob = new byte[EventBadgeCodec.NonceBytes + foreign.Length + EventBadgeCodec.TagBytes];
        System.Security.Cryptography.RandomNumberGenerator.Fill(
            blob.AsSpan(0, EventBadgeCodec.NonceBytes));
        using (var aes = new System.Security.Cryptography.AesGcm(key, EventBadgeCodec.TagBytes))
        {
            aes.Encrypt(
                blob.AsSpan(0, EventBadgeCodec.NonceBytes),
                foreign,
                blob.AsSpan(EventBadgeCodec.NonceBytes, foreign.Length),
                blob.AsSpan(EventBadgeCodec.NonceBytes + foreign.Length, EventBadgeCodec.TagBytes));
        }
        var encoded = CrockfordBase32.EncodeSymbol(0) + CrockfordBase32.Encode(blob);

        EventBadgeCodec.TryDecode(encoded, key, out _).Should().BeFalse();
    }

    [Fact]
    public void StaysShortEnoughToPrintAndStore()
    {
        var encoded = EventBadgeCodec.Encode(
            new EventBadgePayload(Guid.NewGuid(), 2026, 3), NewKey(), keyVersion: 0);

        // GateScans.QrIdAtScan is nvarchar(96), and the gate refuses anything
        // over that ceiling as "not recognised" BEFORE decrypting — so a payload
        // that outgrew it would be undiagnosable at a desk with a queue. The
        // whole reason the fields are packed as raw bytes.
        encoded.Length.Should().Be(78);
        encoded.Length.Should().BeLessThan(96);
    }

    [Fact]
    public void SurvivesTheUppercasingEveryScannedQrGoesThrough()
    {
        // QrId.Normalise upper-cases every scanned value before it is resolved.
        // This is precisely why the payload is base32 and not base64.
        var key = NewKey();
        var payload = new EventBadgePayload(Guid.NewGuid(), 2026, 7);
        var encoded = EventBadgeCodec.Encode(payload, key, keyVersion: 0);

        var normalised = encoded.Trim().ToUpperInvariant();

        EventBadgeCodec.TryDecode(normalised, key, out var decoded).Should().BeTrue();
        decoded.Should().Be(payload);
    }

    [Fact]
    public void RejectsAnotherKeysBadge()
    {
        var encoded = EventBadgeCodec.Encode(
            new EventBadgePayload(Guid.NewGuid(), 2026, 3), NewKey(), keyVersion: 0);

        EventBadgeCodec.TryDecode(encoded, NewKey(), out _).Should().BeFalse();
    }

    [Fact]
    public void RejectsATamperedBadge()
    {
        var key = NewKey();
        var encoded = EventBadgeCodec.Encode(
            new EventBadgePayload(Guid.NewGuid(), 2026, 3), key, keyVersion: 0);

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
            new EventBadgePayload(Guid.NewGuid(), 2026, 3), NewKey(), keyVersion);

        EventBadgeCodec.TryReadKeyVersion(encoded, out var read).Should().BeTrue();
        read.Should().Be(keyVersion);
    }

    [Fact]
    public void EncodesDifferentlyEachTimeSoBadgesAreNotCorrelatable()
    {
        var key = NewKey();
        var payload = new EventBadgePayload(Guid.NewGuid(), 2026, 3);

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
        var payload = new EventBadgePayload(Guid.NewGuid(), 2026, 3);
        var shortKey = new byte[16];

        var encode = () => EventBadgeCodec.Encode(payload, shortKey, keyVersion: 0);

        encode.Should().Throw<ArgumentException>();
    }
}

/// <summary>
/// D-819 — the arming semantics. The whole point of the walk-in mode is that it
/// is inert until deliberately switched on, so "off by default" and
/// "fail-closed" are the behaviours worth pinning.
/// </summary>
public class WalkInModeOptionsTests
{
    // Saudi wall-clock, per the owner's 2026-07-31 no-zoned-time decision:
    // the arming window and the clock it is compared against are now the
    // same kind, so the comparison cannot drift by the +03:00 offset.
    private static readonly DateTime Now = new(2026, 8, 1, 9, 0, 0);

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
            ExpiresAt = Now.AddMinutes(-1),
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
            ExpiresAt = Now.AddHours(1),
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

/// <summary>
/// D-820 — the cross-language contract with the Flutter scanner.
///
/// <para>These five strings were produced by <see cref="EventBadgeCodec.Encode"/>
/// and are pinned IDENTICALLY in
/// <c>simf_app/test/features/gates/offline_badge_test.dart</c>. The app decodes
/// badges the desks print, so a codec change that only one language follows
/// would leave a shipped scanner unable to read live badges. Pinning the same
/// fixtures on both sides turns that into two red suites instead.</para>
///
/// <para>The nonce is random, so these cannot be reproduced by re-encoding —
/// they are DECODE fixtures. Do not hand-edit them; regenerate both files
/// together from the encoder.</para>
/// </summary>
public sealed class EventBadgeCrossLanguageFixtureTests
{
    /// <summary>The fixture key: bytes 0..31, matching the Dart test.</summary>
    private static byte[] FixtureKey =>
        Enumerable.Range(0, EventBadgeCodec.KeyBytes).Select(i => (byte)i).ToArray();

    [Theory]
    [InlineData("15F9NADHE9H94MTGBNK7WMMWB6Q3GTDED9NMBF161ZHXRFWS8KFRCYG8SWRCQ0NP1EJQ0SQXAS0NRA",
        "11111111-2222-3333-4444-555555555555", 2026, 1)]
    [InlineData("11Z0KE14GCKKEHCT1MYEYJ6QK1Q4S6WCG5694NS23BMPNBWXYB85TDTN3C3XKQA7JD92EFHFNMQN20",
        "00000000-0000-0000-0000-000000000001", 2026, 2)]
    [InlineData("1CVTC4V9WG32MWFARN461D6MK7EZ7A0B7SW1S7QZW4EJCXYSYSFBCZ3M69YD16B3TP591QT4GPXPRG",
        "aabbccdd-eeff-0011-2233-445566778899", 2027, 7)]
    [InlineData("Y62XPW76MG7BZRV8SKQRWBDFNJFV6QS1JXAYRGEA12GHP54CVBKWKS32QXD0VP5YM9MH7WHE9R71S8",
        "ffffffff-ffff-ffff-ffff-ffffffffffff", 2999, 65535)]
    [InlineData("00Y8GQ011PGAFNGMDY97JQSEVMD552KA0DZNFJ66RVPZ261AZ3NPYQFJPC3EC1KWNV0ETN7K5AQ9Y6",
        "12345678-9abc-def0-1234-56789abcdef0", 2000, 0)]
    public void Decodes_the_fixtures_the_flutter_scanner_pins(
        string encoded, string profileId, int editionYear, int profileTypeCode)
    {
        EventBadgeCodec.TryDecode(encoded, FixtureKey, out var payload)
            .Should().BeTrue();
        payload.ProfileId.Should().Be(Guid.Parse(profileId));
        payload.EditionYear.Should().Be(editionYear);
        payload.ProfileTypeCode.Should().Be(profileTypeCode);
    }

    [Theory]
    [InlineData("15F9NADHE9H94MTGBNK7WMMWB6Q3GTDED9NMBF161ZHXRFWS8KFRCYG8SWRCQ0NP1EJQ0SQXAS0NRA", 1)]
    [InlineData("Y62XPW76MG7BZRV8SKQRWBDFNJFV6QS1JXAYRGEA12GHP54CVBKWKS32QXD0VP5YM9MH7WHE9R71S8", 30)]
    [InlineData("00Y8GQ011PGAFNGMDY97JQSEVMD552KA0DZNFJ66RVPZ261AZ3NPYQFJPC3EC1KWNV0ETN7K5AQ9Y6", 0)]
    public void Reads_the_stamped_key_version_without_the_key(
        string encoded, int expectedVersion)
    {
        EventBadgeCodec.TryReadKeyVersion(encoded, out var version).Should().BeTrue();
        version.Should().Be(expectedVersion);
    }

    [Theory]
    [InlineData("15F9NADHE9H94MTGBNK7WMMWB6Q3GTDED9NMBF161ZHXRFWS8KFRCYG8SWRCQ0NP1EJQ0SQXAS0NRA")]
    [InlineData("Y62XPW76MG7BZRV8SKQRWBDFNJFV6QS1JXAYRGEA12GHP54CVBKWKS32QXD0VP5YM9MH7WHE9R71S8")]
    public void A_real_badge_fits_the_widened_audit_column(string encoded)
    {
        // GateScans.QrIdAtScan is nvarchar(96), and the gate stores the whole
        // blob so the server can decrypt it independently. Every badge is now
        // exactly 78 characters - the payload is fixed-width, so there is no
        // longer a "typical" and an "extreme" case to reason about.
        encoded.Length.Should().Be(78);
        encoded.Length.Should().BeLessThanOrEqualTo(96);
    }

    /// <summary>The badge length is stated in prose across seven files, and the
    /// prose went stale: the 12-byte-tag era produced a variable-width badge of
    /// "about 61" characters, and every one of those comments still said so weeks
    /// after the tag went to 16 and the payload became a fixed 20 raw bytes. The
    /// assertions above pin the behaviour and could not see any of it.
    ///
    /// <para>So the comments are pinned too. A reader who trusts a stale size
    /// comment mis-sizes a column or a buffer, which is exactly the class of bug
    /// the widened audit column exists to prevent - and this is cheaper than
    /// hoping the next person greps.</para></summary>
    [Fact]
    public void No_source_comment_still_claims_the_old_badge_length()
    {
        // Matches "61 characters", "~61-character", "about 61 characters" and the
        // "9-byte payload" that went with them. Deliberately NOT a bare "61":
        // the number is legitimate elsewhere (a line number, a byte count).
        var stale = new Regex(
            @"(~\s*61[\s-]*character|about\s+61\s+character|61\s+characters|9-byte\s+payload)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        var offenders = SourceFiles()
            .Where(file => stale.IsMatch(File.ReadAllText(file)))
            .Select(file => Path.GetRelativePath(RepoRoot(), file).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();

        offenders.Should().BeEmpty(
            "a badge is EXACTLY 78 characters - 12-byte nonce + 20-byte plaintext + "
            + "16-byte tag = 48 bytes, which is 77 base32 symbols plus the leading "
            + "key-version character. The \"~61\" wording predates the full tag and "
            + "the fixed-width payload, and describes a badge this codec can no "
            + "longer produce.");
    }

    /// <summary>Every C# and Dart source under src/, which is where a size comment
    /// can mislead someone writing code. docs/ is excluded deliberately: the
    /// decisions log records what was true on the day it was written, and
    /// correcting history there would make it lie about what was decided when.</summary>
    private static IEnumerable<string> SourceFiles() =>
        Directory
            .EnumerateFiles(Path.Combine(RepoRoot(), "src"), "*.*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                           || file.EndsWith(".dart", StringComparison.OrdinalIgnoreCase))
            .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                           && !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "SIMF.slnx")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull();
        return directory!.FullName;
    }
}
