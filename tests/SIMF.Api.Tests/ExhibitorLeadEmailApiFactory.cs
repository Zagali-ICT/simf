using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SIMF.Application.Email;

namespace SIMF.Api.Tests;

/// <summary>
/// BUG-024 — a <see cref="SimfApiFactory"/> whose <see cref="IEmailQueue"/> is a
/// synchronous capturing <see cref="FakeEmailQueue"/>, so the exhibitor
/// lead-capture email tests can assert on the exact enqueued message without
/// racing the async background sender. Same shape as
/// <see cref="BulkBadgeEmailApiFactory"/>, kept separate so the two suites do not
/// share (and pollute) one message list.
/// </summary>
public sealed class ExhibitorLeadEmailApiFactory : SimfApiFactory
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
