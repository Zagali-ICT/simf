// Tests: SIMF.Api.Tests/Operations/WorkerLeaseTests.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SIMF.Infrastructure.Operations;

/// <summary>Wraps one background worker so it runs only on the instance holding
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
/// run the workers" into "this node is down".</para></summary>
internal sealed class LeasedHostedService : IHostedService
{
    private readonly IHostedService _inner;
    private readonly WorkerLease _lease;
    private readonly ILogger<LeasedHostedService> _logger;

    private CancellationTokenSource? _waiting;
    private Task? _startWhenGranted;
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
        _startWhenGranted = StartWhenGrantedAsync(_waiting.Token);
        return Task.CompletedTask;
    }

    private async Task StartWhenGrantedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _lease.Granted.WaitAsync(cancellationToken);
            await _inner.StartAsync(cancellationToken);
            _innerStarted = true;

            _logger.LogInformation(
                "{Worker} started under the worker lease.", _inner.GetType().Name);
        }
        catch (OperationCanceledException)
        {
            // The host is shutting down, or this instance never won the lease.
            // Neither is an error: a follower is supposed to end up here.
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_waiting is not null)
        {
            await _waiting.CancelAsync();
        }

        if (_startWhenGranted is not null)
        {
            await _startWhenGranted;
        }

        // Stopping a worker that never started is not merely wasteful: a
        // BackgroundService that was never started has no execute task, and
        // several of these workers open a scope in StopAsync to flush.
        if (_innerStarted)
        {
            await _inner.StopAsync(cancellationToken);
        }

        _waiting?.Dispose();
        _waiting = null;
    }
}
