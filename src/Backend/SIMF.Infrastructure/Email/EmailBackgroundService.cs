using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SIMF.Application.Email;

namespace SIMF.Infrastructure.Email;

/// <summary>
/// Drains the <see cref="EmailQueue"/> and sends each message. A failed send is
/// logged and does not stop the worker, so one bad address never blocks the
/// queue (SIMF-SAD-001 Amendment A.2).
/// </summary>
public sealed class EmailBackgroundService(
    EmailQueue queue,
    IEmailSender sender,
    ILogger<EmailBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await sender.SendAsync(message, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send email to {Recipient}", message.To);
            }
        }
    }
}
