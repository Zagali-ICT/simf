// The loop that drains an offline desk once the network comes back.
//
// The owner's requirement is that a desk left running through a shift uploads by
// itself. What is under test here is not "does an upload work" — that is the
// server's own test — but the four ways an unattended loop goes wrong: it hammers
// a dead network, it replays a credential the server has already refused, it
// starts a second upload on top of the first, or it spins forever on rows the
// server will never accept. Each of those is a desk that looks healthy and
// uploads nothing, which is the failure this loop exists to remove.
using System.Net;
using FluentAssertions;
using SIMF.Common;
using SIMF.Contracts.Badges;
using Xunit;

namespace SIMF.BadgeDesk.Tests;

public sealed class BacklogUploaderTests : IDisposable
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(), $"simf-backlog-{Guid.NewGuid():N}", "registrations.dat");

    public void Dispose()
    {
        var directory = Path.GetDirectoryName(_path);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>A clock the test moves by hand, so an hour of dead venue wifi
    /// takes no time to prove.</summary>
    private sealed class TestClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private static StoredRegistration Registration(long sequence) => new()
    {
        ProfileId = Guid.NewGuid(),
        Sequence = sequence,
        ProfileTypeCode = 1,
        Name = $"Visitor {sequence}",
        NationalId = "1098765432",
        RegisteredAt = SimfClock.Now,
    };

    private static OfflineBadgeBatchResponse Accepting(params long[] sequences) =>
        new(sequences.Length, sequences.Length, 0, 0, 0,
            [.. sequences.Select(sequence => new OfflineBadgeUploadResult(
                sequence, $"QR{sequence}", OfflineBadgeUploadStatus.Created, null, null))]);

    private static OfflineBadgeBatchResponse Refusing(
        long accepted, long refused) =>
        new(2, 1, 0, 0, 1,
            [
                new OfflineBadgeUploadResult(
                    accepted, $"QR{accepted}", OfflineBadgeUploadStatus.Created, null, null),
                new OfflineBadgeUploadResult(
                    refused, $"QR{refused}", OfflineBadgeUploadStatus.Rejected,
                    "DUPLICATE_IDENTITY", "That ID already holds a badge."),
            ]);

    [Fact]
    public async Task A_desk_nobody_is_watching_uploads_on_its_own()
    {
        // The whole point. Before this the operator had to press F5 and paste a
        // token for anything to leave the desk, so a shift ran for three days and
        // uploaded nothing while looking perfectly normal.
        var store = new DeskStore(_path);
        store.Append(Registration(3_000_001));
        store.Append(Registration(3_000_002));

        var sent = 0;
        var uploader = new BacklogUploader(
            store,
            (_, batch, _) => { sent++; return Task.FromResult(Accepting([.. batch.Select(r => r.Sequence)])); });
        uploader.UseToken("shift-token");

        var attempt = await uploader.TryUploadAsync(force: false);

        attempt.Kind.Should().Be(UploadAttemptKind.Uploaded);
        sent.Should().Be(1);
        store.PendingUploadCount.Should().Be(
            0, "the rows the server accounted for are marked done");
    }

    [Fact]
    public async Task Nothing_is_sent_until_a_token_has_been_pasted()
    {
        // The credential is per shift and lives in memory only, so a freshly
        // started desk holds none. It must sit quiet rather than send an
        // unauthenticated batch every second.
        var store = new DeskStore(_path);
        store.Append(Registration(3_000_001));

        var sent = 0;
        var uploader = new BacklogUploader(
            store, (_, _, _) => { sent++; return Task.FromResult(Accepting(3_000_001)); });

        var attempt = await uploader.TryUploadAsync(force: false);

        attempt.Kind.Should().Be(UploadAttemptKind.NothingAttempted);
        sent.Should().Be(0);
        uploader.HasToken.Should().BeFalse();
    }

    [Fact]
    public async Task Nothing_is_sent_when_there_is_nothing_waiting()
    {
        var store = new DeskStore(_path);
        var sent = 0;
        var uploader = new BacklogUploader(
            store, (_, _, _) => { sent++; return Task.FromResult(Accepting()); });
        uploader.UseToken("shift-token");

        var attempt = await uploader.TryUploadAsync(force: false);

        attempt.Kind.Should().Be(UploadAttemptKind.NothingAttempted);
        sent.Should().Be(0);
    }

    [Fact]
    public async Task The_wait_doubles_from_fifteen_seconds_and_stops_at_five_minutes()
    {
        // A venue's wifi being down for an hour must cost about sixteen attempts,
        // not thousands. The cap is what bounds it; the floor is what makes the
        // desk catch up quickly when somebody walks back into coverage.
        var store = new DeskStore(_path);
        store.Append(Registration(3_000_001));

        var clock = new TestClock(DateTimeOffset.UnixEpoch);
        var uploader = new BacklogUploader(
            store,
            (_, _, _) => Task.FromException<OfflineBadgeBatchResponse>(
                new HttpRequestException("no route to host")),
            clock);
        uploader.UseToken("shift-token");

        TimeSpan[] expected =
        [
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(30),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(2),
            TimeSpan.FromMinutes(4),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5),
            TimeSpan.FromMinutes(5),
        ];

        foreach (var wait in expected)
        {
            var attempt = await uploader.TryUploadAsync(force: false);

            attempt.Kind.Should().Be(UploadAttemptKind.Failed);
            attempt.RetryIn.Should().Be(wait);
            clock.Advance(wait);
        }
    }

    [Fact]
    public async Task Nothing_is_sent_while_the_backoff_is_still_running()
    {
        // Without this the one-second tick would be the retry rate.
        var store = new DeskStore(_path);
        store.Append(Registration(3_000_001));

        var sent = 0;
        var clock = new TestClock(DateTimeOffset.UnixEpoch);
        var uploader = new BacklogUploader(
            store,
            (_, _, _) =>
            {
                sent++;
                return Task.FromException<OfflineBadgeBatchResponse>(
                    new HttpRequestException("no route to host"));
            },
            clock);
        uploader.UseToken("shift-token");

        await uploader.TryUploadAsync(force: false);
        sent.Should().Be(1);

        clock.Advance(TimeSpan.FromSeconds(14));
        var tooSoon = await uploader.TryUploadAsync(force: false);

        tooSoon.Kind.Should().Be(UploadAttemptKind.NothingAttempted);
        sent.Should().Be(1);
        uploader.RetryIn.Should().Be(TimeSpan.FromSeconds(1));

        clock.Advance(TimeSpan.FromSeconds(1));
        await uploader.TryUploadAsync(force: false);
        sent.Should().Be(2);
    }

    [Fact]
    public async Task Pressing_F5_ignores_the_backoff()
    {
        // An operator who has just fixed the wifi should not be told to wait four
        // minutes for a desk that is already able to upload.
        var store = new DeskStore(_path);
        store.Append(Registration(3_000_001));

        var sent = 0;
        var failing = true;
        var clock = new TestClock(DateTimeOffset.UnixEpoch);
        var uploader = new BacklogUploader(
            store,
            (_, batch, _) =>
            {
                sent++;
                return failing
                    ? Task.FromException<OfflineBadgeBatchResponse>(
                        new HttpRequestException("no route to host"))
                    : Task.FromResult(Accepting([.. batch.Select(r => r.Sequence)]));
            },
            clock);
        uploader.UseToken("shift-token");

        await uploader.TryUploadAsync(force: false);
        uploader.RetryIn.Should().Be(TimeSpan.FromSeconds(15));

        failing = false;
        var forced = await uploader.TryUploadAsync(force: true);

        forced.Kind.Should().Be(UploadAttemptKind.Uploaded);
        sent.Should().Be(2);
        uploader.RetryIn.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task A_success_puts_the_wait_back_on_the_floor_and_the_next_batch_is_due_at_once()
    {
        // Two properties in one run. The backoff must not stay stretched after
        // the network comes back, and a desk holding more than one batch has to
        // drain batch after batch instead of waiting fifteen seconds between them.
        var store = new DeskStore(_path);
        store.Append(Registration(3_000_001));

        var failing = true;
        var clock = new TestClock(DateTimeOffset.UnixEpoch);
        var uploader = new BacklogUploader(
            store,
            (_, batch, _) => failing
                ? Task.FromException<OfflineBadgeBatchResponse>(
                    new HttpRequestException("no route to host"))
                : Task.FromResult(Accepting([.. batch.Select(r => r.Sequence)])),
            clock);
        uploader.UseToken("shift-token");

        foreach (var wait in new[]
                 { TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(1) })
        {
            await uploader.TryUploadAsync(force: false);
            clock.Advance(wait);
        }

        failing = false;
        var recovered = await uploader.TryUploadAsync(force: false);

        recovered.Kind.Should().Be(UploadAttemptKind.Uploaded);
        uploader.RetryIn.Should().Be(
            TimeSpan.Zero, "the next batch of a large backlog goes on the very next tick");

        // And the NEXT failure starts again from the floor, not from where the
        // outage left off.
        store.Append(Registration(3_000_002));
        failing = true;
        var afterRecovery = await uploader.TryUploadAsync(force: false);

        afterRecovery.RetryIn.Should().Be(TimeSpan.FromSeconds(15));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task A_refused_credential_is_discarded_rather_than_replayed(
        HttpStatusCode status)
    {
        // A token the server has refused will be refused again. Replaying one
        // every few minutes for a three-day event is how an administrator's
        // account gets locked out, and it would never upload anything either way.
        // 403 is treated the same as 401 on purpose: both need a person.
        var store = new DeskStore(_path);
        store.Append(Registration(3_000_001));

        var sent = 0;
        var clock = new TestClock(DateTimeOffset.UnixEpoch);
        var uploader = new BacklogUploader(
            store,
            (_, _, _) =>
            {
                sent++;
                return Task.FromException<OfflineBadgeBatchResponse>(
                    new UploadFailedException(status, "the session has expired"));
            },
            clock);
        uploader.UseToken("shift-token");

        var attempt = await uploader.TryUploadAsync(force: false);

        attempt.Kind.Should().Be(UploadAttemptKind.CredentialExpired);
        attempt.FailureMessage.Should().Be(
            "the session has expired",
            "the operator is told which of the two answers it was, in the "
            + "server's own words — an expired token and an account without the "
            + "capability need different actions");
        uploader.HasToken.Should().BeFalse();

        // An hour later, still silent.
        clock.Advance(TimeSpan.FromHours(1));
        var later = await uploader.TryUploadAsync(force: false);

        later.Kind.Should().Be(UploadAttemptKind.NothingAttempted);
        sent.Should().Be(1);
    }

    [Fact]
    public async Task A_batch_already_in_flight_is_never_overlapped()
    {
        // The tick arrives every second and an upload over a bad link can take
        // minutes. Without the guard the same rows would be in flight several
        // times over, and the desk would be marking rows uploaded from two
        // answers at once.
        var store = new DeskStore(_path);
        store.Append(Registration(3_000_001));

        var sent = 0;
        var inFlight = new TaskCompletionSource<OfflineBadgeBatchResponse>();
        var uploader = new BacklogUploader(
            store, (_, _, _) => { sent++; return inFlight.Task; });
        uploader.UseToken("shift-token");

        var first = uploader.TryUploadAsync(force: false);
        uploader.IsUploading.Should().BeTrue();

        var second = await uploader.TryUploadAsync(force: false);
        var forced = await uploader.TryUploadAsync(force: true);

        second.Kind.Should().Be(UploadAttemptKind.NothingAttempted);
        forced.Kind.Should().Be(
            UploadAttemptKind.NothingAttempted, "not even F5 doubles up an upload");
        sent.Should().Be(1);

        inFlight.SetResult(Accepting(3_000_001));
        (await first).Kind.Should().Be(UploadAttemptKind.Uploaded);
        uploader.IsUploading.Should().BeFalse();
    }

    [Fact]
    public async Task A_row_the_server_refused_is_not_sent_again_by_the_timer()
    {
        // A batch refused row by row is still a SUCCESSFUL round trip, so the
        // backoff resets and the next tick would send exactly the same rows —
        // an upload every second for the rest of the shift, each one rejected for
        // the same reason, each one overwriting the report the operator needs in
        // order to fix it. Held back, the refused row waits for a human and
        // everything registered after it still uploads.
        var store = new DeskStore(_path);
        store.Append(Registration(3_000_001));
        store.Append(Registration(3_000_002));

        var sent = 0;
        var uploader = new BacklogUploader(
            store,
            (_, _, _) => { sent++; return Task.FromResult(Refusing(3_000_001, 3_000_002)); });
        uploader.UseToken("shift-token");

        await uploader.TryUploadAsync(force: false);
        sent.Should().Be(1);
        uploader.HeldBackCount.Should().Be(1);
        store.PendingUploadCount.Should().Be(
            1, "a refused row has NOT been uploaded and the counter must go on saying so");

        var again = await uploader.TryUploadAsync(force: false);

        again.Kind.Should().Be(UploadAttemptKind.NothingAttempted);
        sent.Should().Be(1);
    }

    [Fact]
    public async Task A_refused_row_is_sent_again_once_it_has_been_corrected()
    {
        // F3 corrects the row, which is the desk saying the reason it was refused
        // has been dealt with.
        var store = new DeskStore(_path);
        store.Append(Registration(3_000_001));
        store.Append(Registration(3_000_002));

        var batches = new List<List<long>>();
        var refuse = true;
        var uploader = new BacklogUploader(
            store,
            (_, batch, _) =>
            {
                batches.Add([.. batch.Select(row => row.Sequence)]);
                return Task.FromResult(refuse
                    ? Refusing(3_000_001, 3_000_002)
                    : Accepting(3_000_002));
            });
        uploader.UseToken("shift-token");

        await uploader.TryUploadAsync(force: false);
        refuse = false;
        uploader.AllowRetry(3_000_002);

        var retried = await uploader.TryUploadAsync(force: false);

        retried.Kind.Should().Be(UploadAttemptKind.Uploaded);
        batches.Should().HaveCount(2);
        batches[1].Should().ContainSingle().Which.Should().Be(3_000_002L);
        uploader.HeldBackCount.Should().Be(0);
        store.PendingUploadCount.Should().Be(0);
    }

    [Fact]
    public async Task Pressing_F5_sends_the_refused_rows_again()
    {
        // F5 is a person saying "I have dealt with it, try anyway". It releases
        // every held-back row, which is also the way out if the server refused a
        // row for a reason that has since been fixed on the server.
        var store = new DeskStore(_path);
        store.Append(Registration(3_000_001));
        store.Append(Registration(3_000_002));

        var batches = new List<List<long>>();
        var refuse = true;
        var uploader = new BacklogUploader(
            store,
            (_, batch, _) =>
            {
                batches.Add([.. batch.Select(row => row.Sequence)]);
                return Task.FromResult(refuse
                    ? Refusing(3_000_001, 3_000_002)
                    : Accepting(3_000_002));
            });
        uploader.UseToken("shift-token");

        await uploader.TryUploadAsync(force: false);
        refuse = false;

        var forced = await uploader.TryUploadAsync(force: true);

        forced.Kind.Should().Be(UploadAttemptKind.Uploaded);
        batches[1].Should().ContainSingle().Which.Should().Be(3_000_002L);
    }

    [Fact]
    public async Task A_mistyped_ApiBaseUrl_is_a_retry_and_not_a_silent_death()
    {
        // A bad URL throws UriFormatException deep inside the client. Under the
        // old manual flow that was a crash the operator saw; on a timer it would
        // fault a task nobody awaits, and the desk would tick on for three days
        // uploading nothing and looking healthy. It is reported and retried like
        // any other failure — the desk cannot fix it, but the operator can read it.
        var store = new DeskStore(_path);
        store.Append(Registration(3_000_001));

        var uploader = new BacklogUploader(
            store,
            (_, _, _) => Task.FromException<OfflineBadgeBatchResponse>(
                new UriFormatException("the URI is empty")));
        uploader.UseToken("shift-token");

        var attempt = await uploader.TryUploadAsync(force: false);

        attempt.Kind.Should().Be(UploadAttemptKind.Failed);
        attempt.FailureMessage.Should().Be("the URI is empty");
        attempt.RetryIn.Should().Be(TimeSpan.FromSeconds(15));
    }
}
