// D-813 — the offline badge desk's local record.
//
// This file is the ONLY record that a badge was handed out until the shift is
// uploaded, so the properties under test are not conveniences: losing it, or
// mis-reading it, means a printed badge nobody can account for or a sequence
// number issued twice.
using System.Text;
using FluentAssertions;
using SIMF.BadgeDesk;
using Xunit;

namespace SIMF.BadgeDesk.Tests;

public sealed class DeskStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(), $"simf-desk-{Guid.NewGuid():N}", "registrations.dat");

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_path);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static DeskConfig Desk(int number) => new()
    {
        DeskNumber = number,
        DeskLabel = $"Desk {number}",
        BadgeKey = Convert.ToBase64String(new byte[32]),
        BadgeKeyVersion = 1,
        ProfileTypes = [new DeskProfileType { Code = 1, Name = "Visitor" }],
    };

    private static StoredRegistration Registration(long sequence) => new()
    {
        Sequence = sequence,
        ProfileTypeCode = 1,
        Name = "Test Visitor",
        NationalId = "1098765432",
        SaudiMobile = "0512345678",
        RegisteredAt = DateTimeOffset.Now,
    };

    [Fact]
    public void The_stored_file_never_contains_the_identity_document_in_clear()
    {
        // The reason the store is encrypted at all. The server holds these same
        // columns AES-GCM encrypted behind a blind index; a plaintext copy on an
        // unattended machine in a public hall was the softest target for them.
        new DeskStore(_path).Append(Registration(3_000_001));

        var raw = File.ReadAllText(_path, Encoding.UTF8);

        raw.Should().NotContain("1098765432");
        raw.Should().NotContain("0512345678");
    }

    [Fact]
    public void A_shift_survives_a_restart()
    {
        var store = new DeskStore(_path);
        store.Append(Registration(3_000_001));
        store.Append(Registration(3_000_002));

        var reopened = new DeskStore(_path);

        reopened.Records.Should().HaveCount(2);
        reopened.PendingUploadCount.Should().Be(2);
    }

    [Fact]
    public void Reopening_resumes_the_sequence_instead_of_reissuing_numbers()
    {
        // Numbers already on paper must never come round again: two visitors
        // sharing a badge id is indistinguishable from one visitor to every
        // count in the system.
        new DeskStore(_path).Append(Registration(3_000_007));

        new DeskStore(_path).NextSequence(Desk(3)).Should().Be(3_000_008);
    }

    [Fact]
    public void A_fresh_desk_starts_at_the_beginning_of_its_own_range()
    {
        new DeskStore(_path).NextSequence(Desk(4)).Should().Be(4_000_001);
    }

    [Fact]
    public void An_upload_receipt_supersedes_the_registration_line()
    {
        var store = new DeskStore(_path);
        store.Append(Registration(3_000_001));
        store.Append(Registration(3_000_002));

        store.MarkUploaded([3_000_001]);

        var reopened = new DeskStore(_path);
        reopened.Records.Should().HaveCount(2);
        reopened.PendingUploadCount.Should().Be(1);
        reopened.BuildPendingBatch(500).Should().ContainSingle()
            .Which.Sequence.Should().Be(3_000_002);
    }

    [Fact]
    public void A_damaged_line_in_the_middle_does_not_stop_the_desk_opening()
    {
        // A power cut leaves a fragment; the desk restarts and keeps working, so
        // the fragment ends up with readable lines on both sides. Refusing to
        // open here would strand a shift of real badges at a venue — a worse
        // failure than losing the one damaged line.
        var store = new DeskStore(_path);
        store.Append(Registration(3_000_007));
        File.AppendAllText(_path, "dGhpcyBsaW5lIHdhcyBjdXQgb2Zm\n");
        new DeskStore(_path).Append(Registration(3_000_008));

        var reopened = new DeskStore(_path);

        reopened.Records.Should().HaveCount(2);
        reopened.NextSequence(Desk(3)).Should().Be(3_000_009);
    }

    [Fact]
    public void A_file_that_decrypts_to_nothing_refuses_to_open()
    {
        // The signature of a record written on ANOTHER desk, or of a machine
        // rebuilt since. Opening would silently reset the sequence counter and
        // reissue numbers already printed, so the desk stops and says so.
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllLines(_path, ["dGhpcyBpcyBub3Qgb3Vycw==", "bm9yIGlzIHRoaXM="]);

        var open = () => new DeskStore(_path);

        open.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void An_absent_file_is_a_fresh_desk_not_a_failure()
    {
        var store = new DeskStore(_path);

        store.Records.Should().BeEmpty();
        store.PendingUploadCount.Should().Be(0);
    }
}
