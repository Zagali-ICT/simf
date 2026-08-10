// Tests: SIMF.Api.Tests/Operations/WorkerLeaseTests.cs
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SIMF.Infrastructure.Operations;

/// <summary>Registration for the worker lease and the workers it gates.
///
/// <para>Public because the API host registers two of its own workers
/// (<c>DormantAccountSweepService</c> and <c>RetentionSweepWorker</c>) in its
/// <c>Program.cs</c> and they need the same gate as the eleven registered
/// here.</para></summary>
public static class WorkerLeaseRegistration
{
    /// <summary>Registers the lease itself. Called once, before any leased
    /// worker.</summary>
    internal static IServiceCollection AddWorkerLease(this IServiceCollection services)
    {
        services.AddSingleton<WorkerLease>();
        services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<WorkerLease>());
        return services;
    }

    /// <summary>Registers a background worker that must run on exactly one API
    /// instance, gated behind the worker lease.
    ///
    /// <para>Use this for a worker whose work comes from the database: a
    /// scheduled scan, a sweep, or a queue whose rows are claimed with a status
    /// transition. Those are the ones that duplicate when four instances each
    /// run their own copy.</para>
    ///
    /// <para>Do NOT use it for a worker draining an <b>in-process</b> queue.
    /// <c>EmailBackgroundService</c> is the standing example: its
    /// <c>EmailQueue</c> is a bounded in-memory <c>Channel</c> registered per
    /// process, so a follower that queues a message has only its own drainer to
    /// send it. Gating that one would strand every email raised on a follower
    /// and then silently drop them, the channel being <c>DropWrite</c> when
    /// full.</para></summary>
    public static IServiceCollection AddLeasedHostedService<TWorker>(
        this IServiceCollection services)
        where TWorker : class, IHostedService
    {
        services.AddSingleton<TWorker>();
        services.AddSingleton<IHostedService>(sp => new LeasedHostedService(
            sp.GetRequiredService<TWorker>(),
            sp.GetRequiredService<WorkerLease>(),
            sp.GetRequiredService<ILogger<LeasedHostedService>>()));
        return services;
    }
}
