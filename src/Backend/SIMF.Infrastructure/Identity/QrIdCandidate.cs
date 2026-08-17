namespace SIMF.Infrastructure.Identity;

/// <summary>Generates one candidate badge id. The alphabet and length live here
/// because two callers need them and neither owns the other.
///
/// <para><see cref="QrIdMinter"/> mints ONE id and asks the database whether it is
/// taken. The year-open re-issue mints thousands at once and cannot: a query per
/// row against the whole attendee table, at the moment an operator is waiting, is
/// the cost the set-based clear beside it exists to avoid. It relies on the space
/// instead - 30^12 candidates against a column whose every prior value was just
/// cleared - and enforces uniqueness within its own batch, with the column's UNIQUE
/// index as the backstop either way.</para>
///
/// <para>Crockford base32 minus I, L, O, U and 0/1, so a badge read aloud at a desk
/// or typed from a printed sheet cannot be misheard as a different one.</para></summary>
internal static class QrIdCandidate
{
    private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ";
    private const int Length = 12;

    public static string Generate()
    {
        Span<char> buffer = stackalloc char[Length];
        Span<byte> entropy = stackalloc byte[Length];
        System.Security.Cryptography.RandomNumberGenerator.Fill(entropy);
        for (var i = 0; i < Length; i++)
        {
            buffer[i] = Alphabet[entropy[i] % Alphabet.Length];
        }
        return new string(buffer);
    }
}
