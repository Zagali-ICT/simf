namespace SIMF.Application.Email;

/// <summary>
/// Accepts an email for asynchronous delivery. The caller does not wait for the
/// send — a background worker drains the queue — so a slow mail server never
/// blocks a user request (SIMF-SAD-001 Amendment A.2).
/// </summary>
public interface IEmailQueue
{
    void Enqueue(EmailMessage message);
}
