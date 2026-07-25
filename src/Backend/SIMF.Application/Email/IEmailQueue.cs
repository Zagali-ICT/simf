namespace SIMF.Application.Email;

/// <summary>
/// Accepts an email for asynchronous delivery. The caller does not wait for the
/// send — a background worker drains the queue — so a slow mail server never
/// blocks a user request (SIMF-SAD-001 Amendment A.2).
/// </summary>
public interface IEmailQueue
{
    void Enqueue(EmailMessage message);

    /// <summary>The number of messages currently buffered and not yet sent. A mass
    /// broadcast reads this to pace its fan-out so it never overruns the bounded
    /// queue (which drops overflow). Best-effort — the value can change the instant
    /// after it is read.</summary>
    int PendingCount { get; }
}
