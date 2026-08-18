namespace SIMF.Application.Email;

/// <summary>
/// Accepts an email for asynchronous delivery. The caller does not wait for the
/// send — a background worker drains the queue — so a slow mail server never
/// blocks a user request (SIMF-SAD-001 Amendment A.2).
/// </summary>
public interface IEmailQueue
{
    /// <summary>
    /// Offers a message to the queue. Returns true when the queue accepted it for
    /// delivery, and false when it refused because the queue is full — in which
    /// case the message is NOT sent and never will be.
    ///
    /// <para>The return value is not advisory. Most of what SIMF sends through
    /// this queue is credential-bearing: the sign-in one-time code, the
    /// password-reset code, the email-verification code, the badge activation and
    /// the admin invite. Discarding one silently produces a user who is simply
    /// never able to sign in, with nothing in the log or the audit trail to say
    /// why. Callers should prefer <c>TryEnqueueAsync</c>, which turns both a
    /// refusal and a throwing implementation into an <c>Email.EnqueueFailed</c>
    /// audit row; a caller that calls this directly must handle false itself.</para>
    /// </summary>
    bool Enqueue(EmailMessage message);

    /// <summary>The number of messages currently buffered and not yet sent. A mass
    /// broadcast reads this to pace its fan-out so it never overruns the bounded
    /// queue (which refuses overflow). Best-effort — the value can change the instant
    /// after it is read.</summary>
    int PendingCount { get; }
}
