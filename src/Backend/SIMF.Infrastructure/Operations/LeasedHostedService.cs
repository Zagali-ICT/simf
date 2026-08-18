// Tests: SIMF.Api.Tests/Operations/WorkerLeaseTests.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SIMF.Infrastructure.Operations;

/// <summary>Wraps one background worker so it runs only while this instance holds
/// the <see cref="WorkerLease"/>.
///
/// <para>The wrapper exists so the workers themselves stay unchanged. There are
/// thirteen of them and each already carries its own schedule and its own
/// once-only guard; teaching every one of them about election would be thirteen
/// chances to get it subtly different, and would put an infrastructure concern
/// inside code whose job is the business rule.</para>
///
/// <para><see cref="StartAsync"/> deliberately does not await the lease. A
/// follower would otherwise block the host's startup sequence forever and the
/// API would never begin serving requests, which would turn "this node does not
/// run the workers" into "this node is down".</para>
///
/// <para>Nor is the grant a one-way door. The lease is re-checked every poll and
/// can be lost while the process lives, so this class watches the token that came
/// with the grant and, when it is cancelled, stops the inner worker and goes back
/// to waiting. Starting on the grant but never standing down would leave two
/// instances running every worker for as long as the loser stayed up, which is
/// the whole failure the lease exists to prevent.</para></summary>
internal sealed class LeasedHostedService : IHostedService
{
    private readonly IHostedService _inner;
    private readonly WorkerLease _lease;
    private readonly ILogger<LeasedHostedService> _logger;

    private CancellationTokenSource? _waiting;
    private Task? _following;
    private volatile bool _innerStarted;

    public LeasedHostedService(
        IHostedService inner, WorkerLease lease, ILogger<LeasedHostedService> logger)
    {
        _inner = inner;
        _lease = lease;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _waiting = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _following = FollowTheLeaseAsync(_waiting.Token);
        return Task.CompletedTask;
    }

    /// <summary>Runs the inner worker for as long as, and only as long as, this
    /// instance holds the lease. Each pass waits for a grant, starts the worker,
    /// waits for that same grant to be lost, stops the worker, and waits
    /// again.</summary>
    private async Task FollowTheLeaseAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            CancellationToken lost;
            try
            {
                lost = await _lease.WaitForGrantAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // The host is shutting down, or this instance never won the
                // lease. Neither is an error: a follower is supposed to end here.
                return;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            await _inner.StartAsync(cancellationToken);
            _innerStarted = true;

            _logger.LogInformation(
                "{Worker} started under the worker lease.", _inner.GetType().Name);

            await WaitUntilLostAsync(cancellationToken, lost);

            if (cancellationToken.IsCancellationRequested)
            {
                // Host shutdown, not a lost lease. StopAsync stops the worker,
                // and it must not be stopped twice.
                return;
            }

            _logger.LogWarning(
                "{Worker} stopped: the worker lease was lost, and another instance is "
                + "expected to take it over.",
                _inner.GetType().Name);

            await _inner.StopAsync(cancellationToken);
            _innerStarted = false;
        }
    }

    private static async Task WaitUntilLostAsync(
        CancellationToken cancellationToken, CancellationToken lost)
    {
        using var untilLost =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lost);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, untilLost.Token);
        }
        catch (OperationCanceledException)
        {
            // The only way this delay ever ends.
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_waiting is not null)
        {
            await _waiting.CancelAsync();
        }

        if (_following is not null)
        {
            await _following;
        }

        // Stopping a worker that never started is not merely wasteful: a
        // BackgroundService that was never started has no execute task, and
        // several of these workers open a scope in StopAsync to flush. The same
        // flag keeps a worker already stopped by a lost lease from being stopped
        // a second time here.
        if (_innerStarted)
        {
            await _inner.StopAsync(cancellationToken);
            _innerStarted = false;
        }

        _waiting?.Dispose();
        _waiting = null;
    }
}
