using System.Text.Json;
using System.Text.Json.Serialization;
using SIMF.Common.Badges;

namespace SIMF.BadgeDesk;

/// <summary>
/// D-819 — a desk's provisioning, read from <c>appsettings.json</c> beside the
/// executable. Provisioned once before the event by whoever hands out the badge
/// key; an operator never edits it.
/// </summary>
public sealed class DeskConfig
{
    /// <summary>Which desk this is. Drives the sequence range, so two desks with
    /// the same number would mint colliding badges — the single most important
    /// value to get right when provisioning.</summary>
    public int DeskNumber { get; set; }

    /// <summary>Shown on screen and sent with the upload for the audit trail.</summary>
    public string DeskLabel { get; set; } = string.Empty;

    /// <summary>Base64 AES-256 badge key, the same one the API carries as
    /// <c>WalkInMode:BadgeKey</c>.</summary>
    public string BadgeKey { get; set; } = string.Empty;

    /// <summary>The version stamped into every badge this desk prints.</summary>
    public int BadgeKeyVersion { get; set; }

    /// <summary>API root for the upload, e.g. <c>https://api.simf.example</c>.
    /// Only ever contacted when the operator asks to upload.</summary>
    public string ApiBaseUrl { get; set; } = string.Empty;

    /// <summary>The profile types this desk can issue, cached while online so the
    /// desk keeps working with no network. Code + name only — a desk has no
    /// reason to know the Guids.</summary>
    public List<DeskProfileType> ProfileTypes { get; set; } = [];

    /// <summary>Width of each desk's sequence range. One million is far more
    /// than a desk will ever issue and keeps the arithmetic readable: desk 3
    /// issues 3,000,001 upward.</summary>
    public const long RangeWidth = 1_000_000L;

    /// <summary>First sequence this desk may mint.</summary>
    [JsonIgnore]
    public long SequenceRangeStart => DeskNumber * RangeWidth + 1;

    /// <summary>Last sequence this desk may mint.</summary>
    [JsonIgnore]
    public long SequenceRangeEnd => (DeskNumber + 1) * RangeWidth;

    /// <summary>
    /// Every reason this configuration cannot be used, in one pass, so a desk
    /// that was provisioned wrongly says so at start-up rather than at the first
    /// visitor.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();
        if (DeskNumber <= 0)
        {
            problems.Add("DeskNumber must be 1 or greater.");
        }
        if (SequenceRangeEnd > OfflineBadgeId.MaxSequence)
        {
            problems.Add($"DeskNumber {DeskNumber} is beyond the badge numbering range.");
        }
        if (BadgeKeyBytes is null)
        {
            problems.Add("BadgeKey must be a base64 AES-256 key (32 bytes).");
        }
        if (BadgeKeyVersion is < 0 or > 31)
        {
            problems.Add("BadgeKeyVersion must be between 0 and 31.");
        }
        if (ProfileTypes.Count == 0)
        {
            problems.Add("No profile types are configured; this desk cannot issue a badge.");
        }
        return problems;
    }

    /// <summary>The decoded key, or null when it is not a usable AES-256 key.</summary>
    [JsonIgnore]
    public byte[]? BadgeKeyBytes
    {
        get
        {
            if (string.IsNullOrWhiteSpace(BadgeKey)) { return null; }
            Span<byte> buffer = stackalloc byte[EventBadgeCodec.KeyBytes];
            return Convert.TryFromBase64String(BadgeKey, buffer, out var written)
                && written == EventBadgeCodec.KeyBytes
                    ? buffer.ToArray()
                    : null;
        }
    }

    public static DeskConfig Load(string path) =>
        JsonSerializer.Deserialize<DeskConfig>(
            File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException($"{path} is empty or not valid JSON.");
}

/// <summary>D-819 — one badge type the desk can print.</summary>
public sealed class DeskProfileType
{
    /// <summary>The number encrypted into the QR (<c>ProfileType.Code</c>).</summary>
    public short Code { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NameArabic { get; set; } = string.Empty;

    public override string ToString() =>
        string.IsNullOrWhiteSpace(NameArabic) ? Name : $"{Name} — {NameArabic}";
}
