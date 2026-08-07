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
    IReadOnlyList<EmailAttachment>? Attachments = null);
