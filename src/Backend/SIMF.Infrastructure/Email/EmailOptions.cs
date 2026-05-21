namespace SIMF.Infrastructure.Email;

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
}
