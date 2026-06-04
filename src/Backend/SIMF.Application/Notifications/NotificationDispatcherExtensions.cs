using Microsoft.Extensions.Logging;

namespace SIMF.Application.Notifications;

/// <summary>
/// D-099: mirrors the H23 (D-083) <c>TryEnqueueAsync</c> shape for
/// <see cref="INotificationDispatcher"/>. The credential-flow services
/// (forgot-password, email-verification, sign-in-OTP) dispatch an in-app
/// notification ALONGSIDE the existing email so the user has an audit
/// trail visible inside the app after they sign in. The dispatch is a
/// side-effect on a different scope from the credential row write the
/// caller has already committed; a failure here must not propagate or
/// it would break the no-enumeration contract on forgot-password and
/// the no-failure contract on sign-up.
/// </summary>
public static class NotificationDispatcherExtensions
{
    public static async Task TryDispatchAsync(
        this INotificationDispatcher dispatcher,
        NotificationRequest request,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await dispatcher.DispatchAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Notification dispatch failed for {Kind} to {UserId}",
                request.Kind, request.UserId);
        }
    }
}
