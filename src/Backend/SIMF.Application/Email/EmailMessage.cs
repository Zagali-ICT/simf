namespace SIMF.Application.Email;

/// <summary>An email to be sent — the unit handed to the email queue.</summary>
public sealed record EmailMessage(string To, string Subject, string HtmlBody);
