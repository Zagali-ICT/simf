using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SIMF.Application.Email;

namespace SIMF.Api.Tests;

/// <summary>
/// D-751 — a <see cref="SimfApiFactory"/> whose <see cref="IEmailQueue"/> is a
/// synchronous capturing <see cref="FakeEmailQueue"/>, so the bulk-badge email
/// tests can assert on the exact enqueued message + ZIP attachment without racing
/// the async background sender. The concrete <c>EmailQueue</c> singleton stays
/// registered (the background service still resolves it), it just never receives
/// anything.
/// <para>Also used by <see cref="SessionLifecycleNoticeTests"/> (A2 / B2) — any test
/// that must assert an email was raised deterministically wants this factory rather
/// than the default one, whose queue is drained on a background thread.</para>
/// </summary>
public sealed class BulkBadgeEmailApiFactory : SimfApiFactory
{
    public FakeEmailQueue Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmailQueue>();
            services.AddSingleton<IEmailQueue>(Emails);
        });
    }
}
