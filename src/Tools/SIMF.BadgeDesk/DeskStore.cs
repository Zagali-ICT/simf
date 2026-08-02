using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SIMF.Contracts.Badges;

namespace SIMF.BadgeDesk;

/// <summary>
/// D-809 — the desk's local record of everyone it registered.
///
/// <para>Append-only, one encrypted record per line, flushed on every write. A
/// badge desk runs on venue power at a folding table: the realistic failure is
/// the machine losing power mid-shift, and an append-only line-per-record file
/// is the format that survives it — a truncated final line is the only damage
/// possible, and it is detectable and skippable. A database file or a rewritten
/// JSON array would put the whole shift at risk to save nothing.</para>
///
/// <para><b>D-813 — why the lines are encrypted.</b> A record carries the
/// holder's name, mobile and national ID / Iqama / passport number. The server
/// holds those same columns AES-GCM encrypted at rest behind a blind index, so a
/// plaintext copy of them here was the softest target for that data in the whole
/// system — on a machine this codebase itself describes as unattended on a
/// folding table in a public hall. Each line is protected with Windows DPAPI, so
/// the file reads ONLY on the desk that wrote it: a copy carried off on a USB
/// stick, or the disk read outside Windows, yields nothing.</para>
///
/// <para><b>Why LocalMachine and not CurrentUser.</b> CurrentUser would tie the
/// file to one Windows profile, and this file is the ONLY record that a badge
/// was handed out. A shift change onto a different login, or a rebuilt profile,
/// would make every pending registration permanently unreadable — losing real
/// visitors to defend against an attacker who, by then, is already running code
/// on the desk. LocalMachine is NOT a substitute for disk encryption on the desk
/// machine; it removes the file-copy exposure, which is the one the desk's
/// physical situation actually creates.</para>
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
            writer.WriteLine(Protect(record));
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
                writer.WriteLine(Protect(record));
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

    /// <summary>
    /// One record as a line: JSON, encrypted to this machine, base64 so it stays
    /// on one line. No extra entropy is passed — the only place to keep it would
    /// be this same disk, so it would buy a failure mode and no protection.
    /// </summary>
    private static string Protect(StoredRegistration record)
    {
        var plaintext = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(record));
        return Convert.ToBase64String(
            ProtectedData.Protect(plaintext, null, DataProtectionScope.LocalMachine));
    }

    /// <summary>
    /// Reads one line back, or null if it cannot be read. A line truncated by a
    /// power cut fails base64 or the authentication tag; a file written on
    /// another machine fails to decrypt at all. The caller decides which of those
    /// is survivable.
    /// </summary>
    private static StoredRegistration? Unprotect(string line)
    {
        try
        {
            var plaintext = ProtectedData.Unprotect(
                Convert.FromBase64String(line), null, DataProtectionScope.LocalMachine);
            return JsonSerializer.Deserialize<StoredRegistration>(plaintext);
        }
        catch (Exception ex) when (ex is FormatException
                                      or CryptographicException or JsonException)
        {
            return null;
        }
    }

    // Streamed, and a line that cannot be read is skipped rather than fatal:
    // the realistic damage at a venue is one line truncated by a power cut, and
    // the rest of the shift is real badges in real hands.
    private void Load()
    {
        if (!File.Exists(_path)) { return; }

        // Last line per sequence wins, which is how an appended upload receipt
        // supersedes the original registration line.
        var latest = new Dictionary<long, StoredRegistration>();
        var order = new List<long>();
        var unreadable = 0;
        foreach (var line in File.ReadLines(_path))
        {
            if (string.IsNullOrWhiteSpace(line)) { continue; }

            var record = Unprotect(line);
            if (record is null) { unreadable++; continue; }

            if (!latest.ContainsKey(record.Sequence)) { order.Add(record.Sequence); }
            latest[record.Sequence] = record;
        }

        // NOTHING readable is the signature of a file written on another desk, or
        // of a machine that has been rebuilt since. Carrying on would open the
        // desk empty, reset the sequence counter and reissue numbers that are
        // already on paper. One damaged line among readable ones is the opposite
        // case and must NOT be fatal: a desk that will not start, holding the
        // only record of every badge handed out, is a worse failure at a venue
        // than a lost line.
        if (latest.Count == 0 && unreadable > 0)
        {
            throw new InvalidDataException(
                "The local record cannot be read at all. It was written on a "
                + "different desk, or that machine has been rebuilt. Restore the "
                + "right file or reconcile it by hand — opening the desk without "
                + "it would reissue badge numbers already on paper.");
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
