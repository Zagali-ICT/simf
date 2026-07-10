using SIMF.Common.Enums;

namespace SIMF.Application.Email;

/// <summary>D-735 — renders one transactional email, preferring the admin's
/// stored override for the type and falling back to the code-owned catalogue
/// default. Implementations MUST NOT throw: a missing, inactive, or broken
/// template — or a database error — degrades to the default, so a sign-in /
/// verification / reset email is never lost.</summary>
public interface IEmailTemplateResolver
{
    /// <summary>Builds the message for <paramref name="type"/> addressed to
    /// <paramref name="to"/>, substituting <paramref name="tokens"/> (e.g.
    /// <c>Code</c>, <c>ExpiryMinutes</c>) into the resolved subject + bilingual
    /// body.</summary>
    Task<EmailMessage> RenderAsync(
        EmailTemplateType type,
        string to,
        IReadOnlyDictionary<string, string> tokens,
        CancellationToken cancellationToken = default);
}
