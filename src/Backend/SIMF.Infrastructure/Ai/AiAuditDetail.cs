using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace SIMF.Infrastructure.Ai;

/// <summary>D-179 (gap doc G12 hardening) — helpers that produce
/// SIEM-friendly structured audit details + redact common secret
/// patterns before persistence. Threat-detection + security
/// reviews of D-177 flagged free-text audit detail (regex-only
/// parseable) and unredacted invocation inputs (PII / keys land
/// raw in the DB) as the two MED-severity gaps blocking effective
/// SOC coverage of the AI module.
///
/// <para>D-181 (review-pass hardening of D-179): switched from raw
/// SHA-256 to HMAC-SHA256 keyed on a server-only secret (preimage
/// resistance against insiders with audit-read); added Saudi NID +
/// mobile + IBAN, AWS AKIA/ASIA, GitHub PATs, Google AIza, Slack,
/// PEM-block redaction patterns; switched all patterns to
/// <see cref="RegexOptions.NonBacktracking"/> as defense-in-depth.</para></summary>
internal static class AiAuditDetail
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    /// <summary>Serialise an arbitrary structure to a compact JSON
    /// string suitable for the <c>AuditEntry.Detail</c> column. SIEM
    /// rules can field-extract instead of regex-parsing.</summary>
    public static string ToJson(object value) =>
        JsonSerializer.Serialize(value, JsonOptions);

    // -- Prompt-content hash (HMAC-SHA256) ---------------------------------

    private static byte[]? _hmacKey;
    private static bool _hmacKeyIsDevFallback;

    /// <summary>Install the HMAC key at startup. Called once
    /// by DI registration. If <paramref name="secret"/> is null/empty,
    /// uses a deterministic per-process derivation so dev + tests
    /// still produce stable hashes (production should always supply
    /// a configured secret via env var). The dev fallback is
    /// announced via <paramref name="logger"/> + a sticky flag so a
    /// hosting layer can refuse to start in production.</summary>
    public static void ConfigureHmacKey(string? secret,
        Microsoft.Extensions.Logging.ILogger? logger = null)
    {
        if (string.IsNullOrEmpty(secret))
        {
            _hmacKey = SHA256.HashData(
                Encoding.UTF8.GetBytes("simf-ai-prompt-hash-dev"));
            _hmacKeyIsDevFallback = true;
            logger?.LogWarning(
                "AI prompt hash secret not configured; using dev "
                + "fallback key. Set Ai:PromptHash:Secret in production.");
        }
        else
        {
            _hmacKey = Encoding.UTF8.GetBytes(secret);
            _hmacKeyIsDevFallback = false;
        }
    }

    /// <summary>D-181 (review-pass) — hosting layer can check this and
    /// refuse to start in production when the HMAC secret was
    /// unconfigured. Required so the dev-fallback hashes never leak
    /// into a production audit trail (the key is derived from a
    /// public source string and is trivially recomputable).</summary>
    public static bool IsHmacKeyDevFallback => _hmacKeyIsDevFallback;

    /// <summary>Raw SHA-256 hex over arbitrary text — used internally
    /// for the dev fallback key derivation; not for prompt-content
    /// hashes (those go through HMAC).</summary>
    private static string Sha256Hex(string? text)
    {
        var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>HMAC-SHA256 over the prompt text used in
    /// <see cref="SIMF.Application.Auditing.AuditEvents.AiPromptUpdated"/> /
    /// Created details so SOC can detect prompt-content drift across
    /// edits without needing the full text (which may contain
    /// prompt-injection payloads or business secrets). Keyed so an
    /// insider with audit-read alone cannot brute-force short or
    /// templated prompts via rainbow tables.
    /// <para>Composite of (SystemPrompt + U+001F + UserPromptTemplate)
    /// so a SystemPrompt edit and a UserPromptTemplate edit produce
    /// distinct fingerprints.</para></summary>
    public static string PromptContentHash(string? systemPrompt, string? userTemplate)
    {
        // D-181 (review-pass) - fail loud if the key was never installed.
        // The previous silent fallback masked a missing-init bug.
        if (_hmacKey is null)
        {
            throw new InvalidOperationException(
                "AiAuditDetail.ConfigureHmacKey was never called - "
                + "wire it from DI registration.");
        }
        var input = (systemPrompt ?? string.Empty) + "\u001f"
            + (userTemplate ?? string.Empty);
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = HMACSHA256.HashData(_hmacKey, bytes);
        // D-181 (review-pass) - `v1:` version prefix so a future HMAC
        // key rotation can bump to `v2:` and SOC rules can explicitly
        // skip cross-version drift comparisons rather than producing a
        // false-positive "content changed" storm on first edit after.
        return "v1:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    // -- Redaction patterns ------------------------------------------------
    //
    // D-181 (review-pass hardening): all patterns use NonBacktracking.
    // NonBacktracking is the .NET 7+ linear-time engine — gives O(n)
    // guaranteed worst case, no catastrophic backtracking even on
    // adversarial input. `Compiled` is intentionally omitted: it is
    // either incompatible with NonBacktracking or a no-op (different
    // codegen path), and code review flagged it as confusing intent.

    private const RegexOptions Opts =
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking;

    // Email addresses — RFC 5322 light, defensive against the common case.
    private static readonly Regex EmailRegex = new(
        @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}", Opts);

    // JWT-shaped tokens (three base64url segments).
    private static readonly Regex JwtRegex = new(
        @"eyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}", Opts);

    // Common API-key prefixes (OpenAI, Anthropic, generic bearer).
    private static readonly Regex ApiKeyRegex = new(
        @"sk-ant-[A-Za-z0-9_-]{20,}|sk-[A-Za-z0-9_-]{20,}"
        + @"|Bearer\s+[A-Za-z0-9._-]{20,}", Opts);

    // Credit card (PAN, 13–19 digits with optional separators).
    private static readonly Regex PanRegex = new(
        @"(?:\d[ -]?){13,19}", Opts);

    // Saudi national ID / Iqama (10 digits starting 1 or 2).
    // D-181 (review-pass): tightened with negative look-around equivalents
    // (NonBacktracking doesn't support look-around, so we use word
    // boundaries + a leading non-digit-or-letter requirement enforced by
    // the match boundary). False-positive class: still matches bare
    // 10-digit Unix epoch seconds in the range 1287..2147 (2010-10 to
    // ~2038). Documented as accepted noise — over-redaction in audit
    // inputs is preferable to leaked PII.
    private static readonly Regex SaudiNationalIdRegex = new(
        @"\b[12]\d{9}\b", Opts);

    // Saudi mobile (05XXXXXXXX or +9665XXXXXXXX or 9665XXXXXXXX).
    // D-181 (review-pass): added \b on the 9665 branch so a 14-digit run
    // doesn't match the inner 9665XXXXXXXX chunk.
    private static readonly Regex SaudiMobileRegex = new(
        @"(\b\+?9665\d{8}\b|\b05\d{8}\b)", Opts);

    // Saudi IBAN (SA + 22 digits, total 24 chars).
    // D-181 (review-pass): allow optional whitespace between groups so
    // the printed form `SA03 8000 0000 ...` is also redacted.
    private static readonly Regex SaudiIbanRegex = new(
        @"\bSA(?:\s?\d){22}\b", Opts);

    // AWS access keys (AKIA = long-term, ASIA = STS session).
    private static readonly Regex AwsAccessKeyRegex = new(
        @"\b(AKIA|ASIA)[0-9A-Z]{16}\b", Opts);

    // AWS secret access keys do NOT have a prefix and look
    // like an arbitrary 40-char base64-ish string, so a value-only
    // match would catch every random 40-char run (huge false-positive
    // rate). The high-fidelity signal is the surrounding context: the
    // key is almost always written as `aws_secret_access_key=<value>`
    // or `aws_secret_access_key: <value>` (env-var / config style),
    // or `AWS_SECRET_ACCESS_KEY=<value>` (uppercase env-var). This
    // context-only pattern redacts the value PLUS the prefix, leaving
    // a marker that names the context. Matches the prefix
    // case-insensitively but redacts the whole `prefix=value` span so
    // the SOC pipeline cannot reconstruct either half.
    private static readonly Regex AwsSecretAccessKeyContextRegex = new(
        @"(?i)\baws[_-]?secret[_-]?access[_-]?key\s*[:=]\s*[A-Za-z0-9/+=]{20,}",
        Opts);

    // GitHub personal-access tokens (ghp_ / gho_ / ghu_ / ghs_ / ghr_).
    private static readonly Regex GithubTokenRegex = new(
        @"\bgh[poshru]_[A-Za-z0-9]{36,}\b", Opts);

    // Google API keys (AIza prefix, 35 base64url-ish chars).
    private static readonly Regex GoogleApiKeyRegex = new(
        @"\bAIza[A-Za-z0-9_-]{35}\b", Opts);

    // Slack tokens (xoxb / xoxa / xoxp / xoxr / xoxs / xoxe / xapp).
    // D-181 (review-pass): added xoxe- (refresh) + xapp- (app-level).
    private static readonly Regex SlackTokenRegex = new(
        @"\b(xox[baprse]-|xapp-)[A-Za-z0-9-]{10,}\b", Opts);

    // PEM private-key block (full block, not just BEGIN line).
    // D-181 (review-pass): security review flagged that the BEGIN-only
    // match left the actual key material raw in the audit row. The
    // bounded `{0,9000}?` body keeps NonBacktracking happy and is sized
    // above the validated AI input cap (8000 chars).
    private static readonly Regex PemPrivateKeyRegex = new(
        @"-----BEGIN [A-Z ]{0,40}PRIVATE KEY-----[\s\S]{0,9000}?"
        + @"-----END [A-Z ]{0,40}PRIVATE KEY-----", Opts);

    // D-181 (review-pass) — fallback: if the END marker is missing or
    // truncated, at least redact the BEGIN line so the indicator + any
    // following key prefix isn't visible. Runs after the spanning
    // pattern so the full-block redaction takes precedence.
    private static readonly Regex PemPrivateKeyBeginRegex = new(
        @"-----BEGIN [A-Z ]{0,40}PRIVATE KEY-----", Opts);

    /// <summary>Redact common secret/PII patterns from one input value.
    /// Replacement tokens are stable so a downstream reader sees that
    /// redaction happened. Order matters — high-entropy / well-anchored
    /// patterns run first so their alphanumeric runs are not mistaken
    /// for digit-greedy patterns like PAN.</summary>
    public static string RedactValue(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        var s = value;
        // High-entropy, well-anchored patterns first.
        s = PemPrivateKeyRegex.Replace(s, "[REDACTED_PEM]");
        s = PemPrivateKeyBeginRegex.Replace(s, "[REDACTED_PEM]");
        s = JwtRegex.Replace(s, "[REDACTED_JWT]");
        s = ApiKeyRegex.Replace(s, "[REDACTED_KEY]");
        s = GithubTokenRegex.Replace(s, "[REDACTED_KEY]");
        s = GoogleApiKeyRegex.Replace(s, "[REDACTED_KEY]");
        s = SlackTokenRegex.Replace(s, "[REDACTED_KEY]");
        s = AwsAccessKeyRegex.Replace(s, "[REDACTED_KEY]");
        // AWS secret-key context redaction runs alongside the
        // AKIA / ASIA value match. The two patterns are independent
        // (one matches the public access-key id; the other matches
        // the secret-key value via its surrounding context).
        s = AwsSecretAccessKeyContextRegex.Replace(s, "[REDACTED_KEY]");
        s = SaudiIbanRegex.Replace(s, "[REDACTED_IBAN]");
        // Saudi NID + mobile before email so a 10-digit ID isn't lost in
        // the @-anchor of the email regex.
        s = SaudiNationalIdRegex.Replace(s, "[REDACTED_NID]");
        s = SaudiMobileRegex.Replace(s, "[REDACTED_PHONE]");
        s = EmailRegex.Replace(s, "[REDACTED_EMAIL]");
        // PAN last — digit-greedy, can shadow other digit-bearing patterns.
        s = PanRegex.Replace(s, "[REDACTED_PAN]");
        return s;
    }

    /// <summary>Run <see cref="RedactValue"/> over every value and
    /// serialise the result as JSON. Keys are NOT redacted — the
    /// caller controls keys via the prompt-template placeholders.</summary>
    public static string SerialiseAndRedact(IReadOnlyDictionary<string, string> inputs)
    {
        if (inputs.Count == 0) return "{}";
        var redacted = new Dictionary<string, string>(inputs.Count);
        foreach (var (key, value) in inputs)
        {
            redacted[key] = RedactValue(value);
        }
        try
        {
            return JsonSerializer.Serialize(redacted, JsonOptions);
        }
        catch
        {
            return "{}";
        }
    }

    // Every canonical redaction marker the redactor emits.
    // Keeps the SIEM rule's canonical set (README "Detail JSON shape")
    // in sync with what the producer scans for.
    private static readonly (string Marker, string Kind)[] RedactionMarkers =
    {
        ("[REDACTED_KEY]", "KEY"),
        ("[REDACTED_JWT]", "JWT"),
        ("[REDACTED_PAN]", "PAN"),
        ("[REDACTED_PEM]", "PEM"),
        ("[REDACTED_NID]", "NID"),
        ("[REDACTED_PHONE]", "PHONE"),
        ("[REDACTED_EMAIL]", "EMAIL"),
        ("[REDACTED_IBAN]", "IBAN"),
    };

    private const int InputPreviewCapChars = 1024;

    /// <summary>Redaction summary derived from already-redacted
    /// text. Returned as a tuple alongside <see cref="SerialiseAndRedact"/>
    /// so SIEM rules AI-005 / AI-007 / AI-008 / AI-009 can field-extract
    /// <c>redactionKinds</c> (string[]) + <c>redactionCount</c> (int) +
    /// <c>inputPreview</c> (sanitised, length-capped). The redacted
    /// values never carry the raw secret, so the preview is safe to ship
    /// to the SOC pipeline.</summary>
    public sealed record RedactedInvocationDetail(
        string InputJson,
        IReadOnlyList<string> RedactionKinds,
        int RedactionCount,
        string InputPreview);

    /// <summary>Redact + serialise + summarise. SIEM-canonical
    /// payload for one invocation. Output text MAY be passed via
    /// <paramref name="extraRedactedText"/> so an LLM completion that
    /// itself contained secrets contributes to the summary.</summary>
    public static RedactedInvocationDetail SerialiseAndRedactWithSummary(
        IReadOnlyDictionary<string, string> inputs,
        string? extraRedactedText = null)
    {
        var json = SerialiseAndRedact(inputs);
        var corpus = json + (extraRedactedText is null
            ? string.Empty
            : "\n" + extraRedactedText);

        var kinds = new List<string>();
        var count = 0;
        foreach (var (marker, kind) in RedactionMarkers)
        {
            var hits = CountOccurrences(corpus, marker);
            if (hits > 0)
            {
                kinds.Add(kind);
                count += hits;
            }
        }

        // Preview is the redacted JSON, length-capped. SOC rule
        // AI-007 regexes against this snippet for intent verbs co-occurring
        // with the IBAN marker, so it must be the redacted text (no raw
        // secrets) and short enough to ship to the audit Detail column.
        var preview = json.Length <= InputPreviewCapChars
            ? json
            : json[..InputPreviewCapChars];

        return new RedactedInvocationDetail(json, kinds, count, preview);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle))
        {
            return 0;
        }
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }
}
