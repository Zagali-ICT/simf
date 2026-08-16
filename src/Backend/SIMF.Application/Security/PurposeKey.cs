// Tests: SIMF.Application.Tests/AccountCodeHasherTests.cs (the two hashers derive
//        different keys from one master secret, and neither equals a bare HMAC
//        under that master).
using System.Security.Cryptography;
using System.Text;

namespace SIMF.Application.Security;

/// <summary>
/// Derives a per-purpose HMAC key from one configured master secret, so several
/// keyed hashers can be configured from a single value without sharing key
/// material.
///
/// <para>The property rests on every caller agreeing on the algorithm, output
/// length and salt while disagreeing on the label. Neither half is enforceable
/// when the derivation is copied into each hasher — a later edit to one copy's
/// parameters is a silent asymmetry, and a new purpose can reuse an existing
/// label without anything noticing. Both halves live here instead: one
/// derivation, and the labels declared side by side where a duplicate is
/// visible.</para>
/// </summary>
internal static class PurposeKey
{
    /// <summary>One label per purpose, versioned so a future key rotation is an
    /// edit here rather than a schema change. Adding a purpose means adding a
    /// label to this list, not copying one.</summary>
    internal const string AccountCodeLabel = "simf/account-code/v1";

    internal const string MeetingActionLabel = "simf/meeting-action/v1";

    internal static byte[] Derive(string masterKey, string label) =>
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            Encoding.UTF8.GetBytes(masterKey),
            outputLength: 32,
            salt: null,
            info: Encoding.UTF8.GetBytes(label));
}
