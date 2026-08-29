using SIMF.Contracts.Badges;

namespace SIMF.BadgeDesk;

/// <summary>Sends one batch to the server.
///
/// <para>A delegate rather than the client itself, so the loop below can be
/// exercised against a server that refuses, times out or answers with a captive
/// portal's login page, none of which is reachable through a real
/// <see cref="UploadClient"/> in a test. Production passes
/// <see cref="UploadClient.UploadAsync"/> with the desk's URL and label already
/// bound.</para></summary>
public delegate Task<OfflineBadgeBatchResponse> SendBatch(
    string bearerToken,
    IList<OfflineBadgeRegistration> batch,
    CancellationToken cancellationToken);

/// <summary>How one attempt at the backlog ended.</summary>
public enum UploadAttemptKind
{
    /// <summary>Nothing was sent: no token, an attempt already in flight, the
    /// backoff has not expired, or nothing is waiting. The counters line already
    /// says which; there is nothing to report.</summary>
    NothingAttempted = 0,

    /// <summary>The server answered. It may still have refused rows — that is a
    /// report to read, not a failure to retry.</summary>
    Uploaded = 1,

    /// <summary>The upload did not land and a retry is scheduled.</summary>
    Failed = 2,

    /// <summary>The server refused the CREDENTIAL. The token has been discarded
    /// and the loop wants a human.</summary>
    CredentialExpired = 3,
}

/// <summary>What one attempt produced, for the form to put on screen.</summary>
public sealed record UploadAttempt(
    UploadAttemptKind Kind,
    OfflineBadgeBatchResponse? Response = null,
    string? FailureMessage = null,
    TimeSpan RetryIn = default);

/// <summary>
/// Drains the desk's backlog on its own once the network comes back.
///
/// <para><b>Why this exists.</b> The desk runs disconnected for the three days
/// of the event, and uploading used to be a manual action: press F5, paste a
/// token, watch a report. A desk left running through a shift — which is what a
/// desk on a folding table actually does — uploaded nothing, and the backlog was
/// found only when somebody thought to look. No registration was ever at risk,
/// because the local file is the durable record, but the accounts behind the
/// printed badges did not exist, so the badges opened no gate.</para>
///
/// <para><b>No connectivity probe.</b> The only test of the network that means
/// anything is the upload itself. A venue's wifi hands out a DHCP lease and a
/// captive portal answers every request with a login page, so an interface check
/// or a ping reports "connected" in precisely the situation where nothing can
/// land. Attempting and failing is cheap; being told the wrong answer is not.
/// </para>
///
/// <para><b>The token lives here and nowhere else.</b> It is a field on this
/// object for the life of the process: never written to appsettings.json, never
/// to the registration store, never anywhere on disk. Closing the app forgets
/// it and the operator pastes it again next launch, which is the whole point —
/// the desk is an unattended machine in a public hall and gains no new secret at
/// rest by uploading on its own.</para>
///
/// <para><b>UI thread only.</b> Driven by a <c>System.Windows.Forms.Timer</c>,
/// so every field here is touched from the message loop and needs no locking of
/// its own; the store keeps its own lock for the file.</para>
/// </summary>
public sealed class BacklogUploader(
    DeskStore store,
    SendBatch send,
    TimeProvider? timeProvider = null)
{
    /// <summary>The first wait after a failure.
    ///
    /// <para>Long enough that a desk whose network is down does not spend a
    /// shift retrying, short enough that an operator who walks back into
    /// coverage does not stand and wait. Fifteen seconds is roughly how long it
    /// takes to register the next visitor, so in practice the upload has already
    /// caught up by the time anyone looks.</para></summary>
    public static readonly TimeSpan RetryFloor = TimeSpan.FromSeconds(15);

    /// <summary>The longest the loop will ever wait between attempts.
    ///
    /// <para>Five minutes bounds the cost of a long outage — an hour of dead
    /// wifi is about sixteen attempts, not thousands — while still recovering
    /// the shift within five minutes of the network returning. The desk is a
    /// handful of machines, not a fleet, so the delay is left exact rather than
    /// jittered: the counters line quotes it to the operator as a countdown, and
    /// a number that is honest is worth more here than herd protection nobody at
    /// this scale needs.</para></summary>
    public static readonly TimeSpan RetryCap = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    /// <summary>Sequences the server has REFUSED, kept out of the background
    /// loop until a human has been near them.
    ///
    /// <para>Without this the loop cannot work at all. A batch the server refuses
    /// row by row is still a successful round trip, so the backoff resets and the
    /// next tick sends the same rows again — an upload every second, for the rest
    /// of the shift, each one rejected for the same reason, each one overwriting
    /// the report the operator needs in order to fix it. Held back, a refused row
    /// waits for F3 (which corrects it) or F5 (which is a person saying "try it
    /// anyway"), and everything registered after it still uploads.</para>
    ///
    /// <para>They stay PENDING throughout: they have not been uploaded, and the
    /// "waiting to upload" count must go on saying so.</para></summary>
    private readonly HashSet<long> _heldBack = [];

    private string? _token;
    private bool _inFlight;
    private int _consecutiveFailures;
    private DateTimeOffset _notBefore = DateTimeOffset.MinValue;

    /// <summary>True once the operator has pasted a token this launch.</summary>
    public bool HasToken => _token is not null;

    /// <summary>True while an attempt is on the wire.</summary>
    public bool IsUploading => _inFlight;

    /// <summary>How many refused rows are waiting for a human.</summary>
    public int HeldBackCount => _heldBack.Count;

    /// <summary>How long until the next attempt is due, or zero when it is due
    /// now. Read by the counters line once a second, so the operator sees a real
    /// countdown rather than a spinner that means nothing.</summary>
    public TimeSpan RetryIn
    {
        get
        {
            var remaining = _notBefore - _time.GetUtcNow();
            return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
        }
    }

    /// <summary>Takes the token for this shift and makes the next attempt due
    /// immediately.</summary>
    public void UseToken(string token)
    {
        _token = token;
        _consecutiveFailures = 0;
        _notBefore = DateTimeOffset.MinValue;
    }

    /// <summary>Puts a corrected row back in the loop's way.
    ///
    /// <para>Called after the desk has edited a row the server refused: the
    /// reason it was held back has been dealt with, so it belongs in the next
    /// batch. A row that was never held back is unaffected.</para></summary>
    public void AllowRetry(long sequence) => _heldBack.Remove(sequence);

    /// <summary>
    /// One attempt at the backlog.
    ///
    /// <para>Every reason not to send is checked before anything is built, so a
    /// tick that has nothing to do costs a handful of comparisons. It never
    /// throws for anything the desk can meet in a hall: a refusal, a timeout, a
    /// portal, a mistyped URL all come back as <see cref="UploadAttemptKind"/>
    /// values with a message the form can show.</para>
    ///
    /// <param name="force">The operator pressed F5. Resets the backoff and
    /// releases every held-back row — pressing it is a person saying they have
    /// dealt with whatever the last report complained about.</param>
    /// </summary>
    public async Task<UploadAttempt> TryUploadAsync(
        bool force, CancellationToken cancellationToken = default)
    {
        // NEVER OVERLAP. A slow upload must not have a second one started on top
        // of it every second: the same rows would be sent twice, and while the
        // server is idempotent by sequence, the desk would be marking rows
        // uploaded from two answers at once.
        if (_inFlight) { return new UploadAttempt(UploadAttemptKind.NothingAttempted); }
        if (_token is not { } token)
        {
            return new UploadAttempt(UploadAttemptKind.NothingAttempted);
        }

        if (force)
        {
            _consecutiveFailures = 0;
            _notBefore = DateTimeOffset.MinValue;
            _heldBack.Clear();
        }
        else if (_time.GetUtcNow() < _notBefore)
        {
            return new UploadAttempt(UploadAttemptKind.NothingAttempted);
        }

        var batch = store.BuildPendingBatch(UploadClient.MaxBatchSize, _heldBack);
        if (batch.Count == 0)
        {
            return new UploadAttempt(UploadAttemptKind.NothingAttempted);
        }

        _inFlight = true;
        try
        {
            var response = await send(token, batch, cancellationToken);

            // Only rows the server actually accounted for are marked done. A
            // rejected row stays pending so it is corrected or chased by hand,
            // which is what makes the reconciliation reach zero.
            store.MarkUploaded(response.Results
                .Where(result => result.Status
                    is OfflineBadgeUploadStatus.Created
                    or OfflineBadgeUploadStatus.CreatedPendingApproval
                    or OfflineBadgeUploadStatus.AlreadyUploaded)
                .Select(result => result.Sequence));

            foreach (var refused in response.Results
                .Where(result => result.Status == OfflineBadgeUploadStatus.Rejected))
            {
                _heldBack.Add(refused.Sequence);
            }

            // Due again at once, not after the floor: a desk with more than one
            // batch of backlog drains it batch after batch on consecutive ticks.
            _consecutiveFailures = 0;
            _notBefore = _time.GetUtcNow();
            return new UploadAttempt(UploadAttemptKind.Uploaded, response);
        }
        catch (UploadFailedException ex) when (ex.IsCredentialFailure)
        {
            // DISCARD it. A token the server has refused will be refused again,
            // and a loop replaying a rejected credential every few minutes for
            // three days is how an administrator's account gets locked out. The
            // form stops the timer and asks the operator, which is the only
            // thing that can actually fix either a 401 or a 403.
            _token = null;
            return new UploadAttempt(
                UploadAttemptKind.CredentialExpired, FailureMessage: ex.Message);
        }
        catch (Exception ex) when (ex is InvalidOperationException or HttpRequestException
                                      or TaskCanceledException or FormatException)
        {
            // FormatException covers the UriFormatException a mistyped ApiBaseUrl
            // throws. It used to reach a caller that pressed F5 and could see the
            // crash; on a timer it would fault a discarded task and the desk
            // would tick on, uploading nothing, looking healthy.
            var delay = NextDelay();
            _notBefore = _time.GetUtcNow() + delay;
            return new UploadAttempt(
                UploadAttemptKind.Failed, FailureMessage: ex.Message, RetryIn: delay);
        }
        finally
        {
            _inFlight = false;
        }
    }

    /// <summary>Doubles the wait, floor to cap: 15s, 30s, 1m, 2m, 4m, then 5m
    /// for as long as the outage lasts.</summary>
    private TimeSpan NextDelay()
    {
        _consecutiveFailures++;
        // Shifting past the cap is pointless and, far enough in, overflows.
        if (_consecutiveFailures >= 8) { return RetryCap; }

        var doubled = RetryFloor * Math.Pow(2, _consecutiveFailures - 1);
        return doubled < RetryCap ? doubled : RetryCap;
    }
}
