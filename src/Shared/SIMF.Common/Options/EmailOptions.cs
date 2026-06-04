namespace SIMF.Common.Options;

/// <summary>
/// SMTP configuration, bound from the <c>Email</c> configuration section. The
/// SMTP password is supplied through the environment / <c>set-env</c> scripts
/// and is never committed.
/// </summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    public string User { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "SIMF";

    /// <summary>
    /// D-097: comma-, semicolon- or whitespace-separated list of operations
    /// addresses that receive a failure-alert email when the email queue
    /// fails to enqueue a transactional message (the
    /// <c>Email.EnqueueFailed</c> audit row, H10 — D-065). Empty when the
    /// alert channel is not configured — the audit row is still written,
    /// the alert email is skipped. Typical values: a Support / IT
    /// distribution list address, or a small set of pager addresses.
    /// </summary>
    public string FailureAlertRecipients { get; set; } = string.Empty;
}
