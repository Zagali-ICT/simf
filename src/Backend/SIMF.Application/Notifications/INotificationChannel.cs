// Tests: SIMF.Api.Tests/NotificationChannelTests.cs
namespace SIMF.Application.Notifications;

/// <summary>
/// `sms-whatsapp-channels` — the delivery seam behind
/// <see cref="INotificationDispatcher"/>. Before this existed the dispatcher hard-
/// coded exactly two deliveries (write the in-app row, then enqueue an email), so
/// "the notification abstraction" did not generalise past email and a third
/// transport could only be added by editing the dispatcher.
///
/// <para>Each channel decides for itself whether a given request concerns it
/// (<see cref="ShouldHandle"/>) and then delivers it (<see cref="SendAsync"/>).
/// The dispatcher owns only the policy that is common to every channel — the
/// D-713 one-per-(user, kind, entity) guard — and runs the registered channels in
/// <see cref="Order"/>.</para>
///
/// <para><b>Scope.</b> The two shipped implementations are
/// <see cref="InAppNotificationChannel"/> and <see cref="EmailNotificationChannel"/>.
/// SMS and WhatsApp are deliberately NOT here: both need a procured gateway
/// (owner-action), and shipping a stub that silently drops messages would be worse
/// than shipping none. When a provider is chosen, the whole change is one new
/// class implementing this interface plus one DI line — no dispatcher edit.</para>
/// </summary>
public interface INotificationChannel
{
    /// <summary>Stable name for logs and tests (e.g. <c>in-app</c>, <c>email</c>).</summary>
    string Name { get; }

    /// <summary>Run order, ascending. The in-app row is written first (0) so a
    /// later transport's failure can never cost the user the in-app record.</summary>
    int Order { get; }

    /// <summary>True when this channel is responsible for delivering
    /// <paramref name="request"/>. A channel that returns false is skipped
    /// entirely — no logging noise, no work.</summary>
    bool ShouldHandle(NotificationRequest request);

    /// <summary>Delivers the request over this channel.</summary>
    Task SendAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}
