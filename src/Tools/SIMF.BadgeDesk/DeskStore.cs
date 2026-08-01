using System.Text;
using System.Text.Json;
using SIMF.Contracts.Badges;

namespace SIMF.BadgeDesk;

/// <summary>
/// D-809 — the desk's local record of everyone it registered.
///
/// <para>Append-only JSON Lines, flushed on every write. A badge desk runs on
/// venue power at a folding table: the realistic failure is the machine losing
/// power mid-shift, and an append-only line-per-record file is the format that
/// survives it — a truncated final line is the only damage possible, and it is
/// detectable and skippable. A database file or a rewritten JSON array would put
/// the whole shift at risk to save nothing.</para>
///
/// <para>This file is the ONLY record that a badge was handed out until it is
/// uploaded, so it is never rewritten and never deleted by the app.</para>
/// </summary>
public sealed class DeskStore
{
    private readonly string _path;
    private readonly List<StoredRegistration> _records = [];
    private readonly Lock _writeLock = new();

    public DeskStore(string path)
    {
        _path = path;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) { Directory.CreateDirectory(directory); }
        Load();
    }

    /// <summary>Everything this desk has registered, oldest first.</summary>
    public IReadOnlyList<StoredRegistration> Records => _records;

    public int PendingUploadCount => _records.Count(record => !record.Uploaded);

    /// <summary>
    /// The next sequence to mint: one past the highest already used, or the
    /// start of the desk's range on a fresh machine. Read from the FILE rather
    /// than a counter kept elsewhere, so restarting the app mid-shift resumes
    /// where it stopped instead of reissuing numbers already on paper.
    /// </summary>
    public long NextSequence(DeskConfig config)
    {
        var highest = _records.Count == 0 ? 0 : _records.Max(record => record.Sequence);
        return highest < config.SequenceRangeStart
            ? config.SequenceRangeStart
            : highest + 1;
    }

    /// <summary>Appends a registration and flushes it to disk before returning,
    /// so the badge is never printed before it is recorded.</summary>
    public void Append(StoredRegistration record)
    {
        lock (_writeLock)
        {
            using var stream = new FileStream(
                _path, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            writer.WriteLine(JsonSerializer.Serialize(record));
            writer.Flush();
            stream.Flush(flushToDisk: true);
            _records.Add(record);
        }
    }

    /// <summary>
    /// Marks sequences as uploaded by appending a receipt line — the file stays
    /// append-only, and the last line for a sequence wins on the next load. A
    /// rewrite would risk the whole shift to update one flag.
    /// </summary>
    public void MarkUploaded(IEnumerable<long> sequences)
    {
        var uploaded = sequences.ToHashSet();
        if (uploaded.Count == 0) { return; }

        lock (_writeLock)
        {
            using var stream = new FileStream(
                _path, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            foreach (var record in _records.Where(r => uploaded.Contains(r.Sequence)))
            {
                record.Uploaded = true;
                writer.WriteLine(JsonSerializer.Serialize(record));
            }
            writer.Flush();
            stream.Flush(flushToDisk: true);
        }
    }

    /// <summary>The not-yet-uploaded registrations, as the upload contract.</summary>
    public List<OfflineBadgeRegistration> BuildPendingBatch(int max) =>
        _records
            .Where(record => !record.Uploaded)
            .Take(max)
            .Select(record => new OfflineBadgeRegistration
            {
                Sequence = record.Sequence,
                ProfileTypeCode = record.ProfileTypeCode,
                Name = record.Name,
                NameArabic = record.NameArabic,
                SaudiMobile = record.SaudiMobile,
                InternationalMobile = record.InternationalMobile,
                NationalId = record.NationalId,
                IqamaNumber = record.IqamaNumber,
                PassportNumber = record.PassportNumber,
                Email = record.Email,
                RegisteredAt = record.RegisteredAt,
            })
            .ToList();

    private void Load()
    {
        if (!File.Exists(_path)) { return; }

        // Last line per sequence wins, which is how an appended upload receipt
        // supersedes the original registration line.
        var latest = new Dictionary<long, StoredRegistration>();
        var order = new List<long>();
        foreach (var line in File.ReadLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line)) { continue; }
            StoredRegistration? record;
            try
            {
                record = JsonSerializer.Deserialize<StoredRegistration>(line);
            }
            catch (JsonException)
            {
                // A line truncated by a power cut. Skipped, not fatal: the rest
                // of the shift is intact and losing it would lose real badges.
                continue;
            }
            if (record is null) { continue; }
            if (!latest.ContainsKey(record.Sequence)) { order.Add(record.Sequence); }
            latest[record.Sequence] = record;
        }

        _records.AddRange(order.Select(sequence => latest[sequence]));
    }
}

/// <summary>D-809 — one registration as the desk stored it.</summary>
public sealed class StoredRegistration
{
    public long Sequence { get; set; }
    public short ProfileTypeCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? NameArabic { get; set; }
    public string? SaudiMobile { get; set; }
    public string? InternationalMobile { get; set; }
    public string? NationalId { get; set; }
    public string? IqamaNumber { get; set; }
    public string? PassportNumber { get; set; }
    public string? Email { get; set; }
    public DateTimeOffset RegisteredAt { get; set; }

    /// <summary>True once the server has confirmed this row.</summary>
    public bool Uploaded { get; set; }
}
