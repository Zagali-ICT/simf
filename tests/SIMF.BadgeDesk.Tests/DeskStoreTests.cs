// D-823 — the offline badge desk's local record.
//
// This file is the ONLY record that a badge was handed out until the shift is
// uploaded, so the properties under test are not conveniences: losing it, or
// mis-reading it, means a printed badge nobody can account for or a sequence
// number issued twice.
using System.Text;
using FluentAssertions;
using SIMF.BadgeDesk;
using SIMF.Common;
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

    [Fact]
    public void The_upload_batch_carries_the_ProfileId_that_is_printed_on_the_badge()
    {
        // The badge's QR carries ONLY this id, and QrResolver looks a scan up by
        // it with no fallback to the printed sequence. The projection used to
        // omit it: the field arrived as Guid.Empty, the server ignored the empty
        // preset and minted its own, and every badge printed at an offline desk
        // scanned as "not recognised" at the gate.
        //
        // Nothing caught it because the server-side test built its badge from the
        // id the SERVER minted, so the round trip closed on the wrong id and
        // stayed green while real badges were dead.
        var store = new DeskStore(_path);
        var record = Registration(3_000_011);
        store.Append(record);

        var batch = store.BuildPendingBatch(500);

        batch.Should().ContainSingle()
            .Which.ProfileId.Should().Be(
                record.ProfileId,
                "the QR is encrypted around this id, so the server must create "
                + "the profile with it rather than minting its own");
        batch[0].ProfileId.Should().NotBe(Guid.Empty);
    }

    private static StoredRegistration Registration(long sequence) => new()
    {
        // Minted here exactly as MainForm does, because it is what gets printed.
        ProfileId = Guid.NewGuid(),
        Sequence = sequence,
        ProfileTypeCode = 1,
        Name = "Test Visitor",
        NationalId = "1098765432",
        SaudiMobile = "0512345678",
        RegisteredAt = SimfClock.Now,
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
    public void A_correction_supersedes_the_row_and_keeps_its_sequence()
    {
        // D-824 - the property that makes server-side validation safe. The badge
        // is already printed; only the captured data changes, so the paper in the
        // visitor's hand stays valid and the upload can be retried.
        var store = new DeskStore(_path);
        store.Append(Registration(3_000_001));

        var corrected = Registration(3_000_001);
        corrected.NationalId = "1000000009";
        corrected.Name = "Corrected Name";

        store.Correct(corrected).Should().BeTrue();

        var reopened = new DeskStore(_path);
        reopened.Records.Should().ContainSingle();
        reopened.Records[0].Sequence.Should().Be(3_000_001);
        reopened.Records[0].Name.Should().Be("Corrected Name");
        reopened.Records[0].NationalId.Should().Be("1000000009");
        // Still pending, so the corrected row is what gets uploaded.
        reopened.PendingUploadCount.Should().Be(1);
    }

    [Fact]
    public void A_correction_keeps_the_contact_details_it_was_not_given()
    {
        // D-824 review - the desk form is cleared after each registration, so an
        // operator fixing a mistyped ID leaves the mobile box empty. A blank box
        // means "leave it alone", never "clear it": the corrected line supersedes
        // the original, so a dropped mobile is unrecoverable - and it is the only
        // contact channel an offline-registered visitor has.
        //
        // MainForm.CorrectPending carries the originals forward; this pins the
        // store half of that contract - a corrected record round-trips whatever
        // it was handed.
        var store = new DeskStore(_path);
        var original = Registration(3_000_001);
        original.SaudiMobile = "0501234567";
        original.NameArabic = "زائر";
        store.Append(original);

        var corrected = Registration(3_000_001);
        corrected.NationalId = "1000000009";
        corrected.SaudiMobile = original.SaudiMobile;
        corrected.NameArabic = original.NameArabic;
        store.Correct(corrected).Should().BeTrue();

        var reopened = new DeskStore(_path);
        reopened.Records[0].SaudiMobile.Should().Be("0501234567");
        reopened.Records[0].NameArabic.Should().Be("زائر");
        reopened.Records[0].NationalId.Should().Be("1000000009");
    }

    [Fact]
    public void A_correction_does_not_consume_a_new_sequence()
    {
        // Two badge ids for one visitor would break every count in the system.
        var store = new DeskStore(_path);
        store.Append(Registration(3_000_001));
        var corrected = Registration(3_000_001);
        corrected.Name = "Corrected";

        store.Correct(corrected);

        store.NextSequence(Desk(3)).Should().Be(3_000_002);
    }

    [Fact]
    public void An_uploaded_row_cannot_be_corrected_at_the_desk()
    {
        // Once the server has it, the account exists and the Control Panel owns
        // it. A desk-side edit would silently diverge and never be sent.
        var store = new DeskStore(_path);
        store.Append(Registration(3_000_001));
        store.MarkUploaded([3_000_001]);

        var corrected = Registration(3_000_001);
        corrected.Name = "Too late";

        store.Correct(corrected).Should().BeFalse();
        new DeskStore(_path).Records[0].Name.Should().Be("Test Visitor");
    }

    [Fact]
    public void Correcting_an_unknown_sequence_changes_nothing()
    {
        var store = new DeskStore(_path);
        store.Append(Registration(3_000_001));

        store.Correct(Registration(3_000_999)).Should().BeFalse();

        new DeskStore(_path).Records.Should().ContainSingle();
    }

    [Fact]
    public void An_absent_file_is_a_fresh_desk_not_a_failure()
    {
        var store = new DeskStore(_path);

        store.Records.Should().BeEmpty();
        store.PendingUploadCount.Should().Be(0);
    }
}
