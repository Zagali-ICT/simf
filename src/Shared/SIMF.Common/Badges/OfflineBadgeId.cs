using System.Globalization;

namespace SIMF.Common.Badges;

/// <summary>
/// D-819 — maps a badge desk's offline <b>sequence</b> onto the 12-character
/// <c>UserProfile.QrId</c> the rest of the system already resolves by.
///
/// <para>The encrypted badge (<see cref="EventBadgeCodec"/>) carries two plain
/// numbers — profile-type code and sequence — because that is what keeps the
/// printed QR small. Every server-side reader, though, resolves a holder by
/// <c>QrId</c>. Rather than add a second identity column and a second lookup
/// path, an offline registration is stored with its <c>QrId</c> DERIVED from the
/// sequence, so <c>QrResolver</c>, the seating desk, the exhibitor scan and the
/// gate engine all keep working unchanged.</para>
///
/// <para><b>Why it can never collide with a minted id.</b> The server's
/// <c>QrIdMinter</c> draws from the Crockford alphabet
/// <c>23456789ABCDEFGHJKMNPQRSTVWXYZ</c>, which deliberately excludes <c>0</c>
/// and <c>1</c>. An offline id is <c>W</c> followed by the sequence padded to 11
/// digits, and <see cref="MaxSequence"/> caps the sequence below 10^10 so the
/// padding ALWAYS leaves a leading <c>0</c> in position 1. A minted id can
/// therefore never equal an offline id, and the existing UNIQUE constraint on
/// the column is the only guard needed.</para>
/// </summary>
public static class OfflineBadgeId
{
    /// <summary>Length of every QR id in the system, minted or offline.</summary>
    public const int QrIdLength = 12;

    /// <summary>Marks the id as desk-minted. Readable at a glance in an audit
    /// export, and it is what makes an offline registration greppable.</summary>
    public const char Prefix = 'W';

    /// <summary>Digits available to the sequence once the prefix is taken.</summary>
    private const int SequenceDigits = QrIdLength - 1;

    /// <summary>
    /// Highest accepted sequence. One below 10^10, so the 11-digit padding always
    /// produces a leading zero — the property that guarantees disjointness from
    /// minted ids. Ten billion badges is far beyond any plausible event, and the
    /// desk numbering scheme (one million-wide range per desk) uses a small
    /// fraction of it.
    /// </summary>
    public const long MaxSequence = 9_999_999_999L;

    /// <summary>Lowest accepted sequence. Zero is reserved as "unset".</summary>
    public const long MinSequence = 1L;

    /// <summary>
    /// Renders a desk sequence as a QR id. Returns false — rather than throwing —
    /// for a sequence outside the accepted range, because the caller is an upload
    /// of operator-entered data where a bad row must be reported, not fatal.
    /// </summary>
    public static bool TryFormat(long sequence, out string qrId)
    {
        if (sequence is < MinSequence or > MaxSequence)
        {
            qrId = string.Empty;
            return false;
        }
        qrId = string.Create(
            QrIdLength,
            sequence,
            static (buffer, value) =>
            {
                buffer[0] = Prefix;
                value.TryFormat(
                    buffer[1..], out _, "D" + SequenceDigits, CultureInfo.InvariantCulture);
            });
        return true;
    }

    /// <summary>
    /// Reads the desk sequence back out of a QR id. False for anything that is
    /// not an offline id, including a normally minted one.
    /// </summary>
    public static bool TryParse(string? qrId, out long sequence)
    {
        sequence = 0;
        if (qrId is null || qrId.Length != QrIdLength) { return false; }
        if (char.ToUpperInvariant(qrId[0]) != Prefix) { return false; }
        if (!long.TryParse(
                qrId.AsSpan(1),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parsed))
        {
            return false;
        }
        if (parsed is < MinSequence or > MaxSequence) { return false; }
        sequence = parsed;
        return true;
    }

    /// <summary>True when this id was minted at an offline desk.</summary>
    public static bool IsOfflineBadge(string? qrId) => TryParse(qrId, out _);
}
