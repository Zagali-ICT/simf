// Tests: SIMF.Api.Tests/EmailSendRetryTests.cs (the delivery attempt counter)
namespace SIMF.Application.Email;

/// <summary>One binary attachment on an outgoing email: the download file
/// name the recipient sees, the MIME content type (e.g. <c>application/zip</c>),
/// and the raw bytes.</summary>
public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>An email to be sent — the unit handed to the email queue.
/// <see cref="Attachments"/> is optional and defaults to null, so every existing
/// caller (which constructs the three-argument message) keeps compiling and stays
/// attachment-free.</summary>
public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    IReadOnlyList<EmailAttachment>? Attachments = null)
{
    /// <summary>
    /// How many delivery attempts this message has already cost. Zero on the
    /// message a caller enqueues; the background email sender increments it each
    /// time it puts the message back on the queue after a send failure that could
    /// plausibly clear on its own.
    ///
    /// <para>The count has to live on the message because the queue is a plain
    /// channel of messages and keeps no per-message state of its own. Without it
    /// the sender cannot tell a relay that hiccuped from an address that is
    /// simply wrong, so a single "try again later" reply discarded the message
    /// for good. Almost everything on this path is a credential (the sign-in
    /// one-time code, the password-reset code, the badge activation), and a
    /// discarded one is a user who is never able to sign in at all.</para>
    ///
    /// <para>Declared in the record body rather than as a positional parameter, so
    /// the three- and four-argument constructors every caller already writes are
    /// untouched and a retry is simply
    /// <c>message with { AttemptCount = ... }</c>. It is deliberately not
    /// persisted: the queue is in-process, so a restart loses the queued messages
    /// and their counts together, which is the same trade the queue itself
    /// already makes.</para>
    /// </summary>
    public int AttemptCount { get; init; }
}
