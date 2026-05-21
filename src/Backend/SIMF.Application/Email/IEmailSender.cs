namespace SIMF.Application.Email;

/// <summary>Sends an email through the configured provider.</summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
