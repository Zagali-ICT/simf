using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SIMF.Infrastructure.Ai;

/// <summary>D-179 (gap doc G12 hardening) — helpers that produce
/// SIEM-friendly structured audit details + redact common secret
/// patterns before persistence. Threat-detection + security
/// reviews of D-177 flagged free-text audit detail (regex-only
/// parseable) and unredacted invocation inputs (PII / keys land
/// raw in the DB) as the two MED-severity gaps blocking effective
/// SOC coverage of the AI module.</summary>
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

    /// <summary>SHA-256 over the prompt text used in
    /// <see cref="AuditEvents.AiPromptUpdated"/> / Created details so
    /// SOC can detect prompt-content drift across edits without
    /// needing the full text (which may contain prompt-injection
    /// payloads or business secrets).</summary>
    public static string Sha256Hex(string? text)
    {
        var bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Composite hash of (SystemPrompt + 0x1F + UserPromptTemplate)
    /// so a SystemPrompt edit and a UserPromptTemplate edit produce
    /// distinct fingerprints (the unit separator can't appear in
    /// either side because user input flows through
    /// <c>Substitute</c> as <c>{placeholder}</c> markers, not raw
    /// concatenation).</summary>
    public static string PromptContentHash(string? systemPrompt, string? userTemplate) =>
        Sha256Hex((systemPrompt ?? string.Empty) + "\u001f" + (userTemplate ?? string.Empty));

    // -- Redaction patterns ------------------------------------------------

    // Email addresses — RFC 5322 light, defensive against the common case.
    private static readonly Regex EmailRegex = new(
        @"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // JWT-shaped tokens (three base64url segments).
    private static readonly Regex JwtRegex = new(
        @"eyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Common API-key prefixes (OpenAI, Anthropic, generic bearer).
    private static readonly Regex ApiKeyRegex = new(
        @"\b(sk-[A-Za-z0-9_-]{20,}|sk-ant-[A-Za-z0-9_-]{20,}|Bearer\s+[A-Za-z0-9._-]{20,})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // Credit card (PAN, 13–19 digits with optional separators).
    private static readonly Regex PanRegex = new(
        @"\b(?:\d[ -]?){13,19}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>Redact common secret/PII patterns from one input value.
    /// The replacement is deterministic length-1 token so a downstream
    /// reader sees that redaction happened without learning the
    /// original. Order matters — JWT/API-key patterns run first so
    /// their long alphanumeric runs are not mistaken for credit-card
    /// digits.</summary>
    public static string RedactValue(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? string.Empty;
        var s = value;
        s = JwtRegex.Replace(s, "[REDACTED_JWT]");
        s = ApiKeyRegex.Replace(s, "[REDACTED_KEY]");
        s = EmailRegex.Replace(s, "[REDACTED_EMAIL]");
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
}
