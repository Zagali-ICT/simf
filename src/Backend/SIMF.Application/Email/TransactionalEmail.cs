namespace SIMF.Application.Email;

/// <summary>
/// #8 (owner) — the shared builder for SIMF's transactional identity emails
/// (sign-in / email-verification / password-reset / account-activation /
/// biometric-enrol codes, and the account-exists notice). It consolidates six
/// near-identical inline builders into one place and renders every message
/// **bilingually** — an English block over a right-to-left Arabic block, the same
/// shape the original biometric-code email already used. Styling is intentionally
/// plain: the owner asked only for DRY + bilingual, so when a branded template is
/// supplied this one type is the single place to reskin.
/// </summary>
public static class TransactionalEmail
{
    /// <summary>
    /// A one-time-code email: an English pair (the "your … code is" lead + the
    /// code, then the expiry line) over the matching right-to-left Arabic pair,
    /// separated by a rule. <paramref name="enLead"/> / <paramref name="arLead"/>
    /// are the lead sentences in each language (the code + expiry are appended per
    /// language). The optional <paramref name="enNote"/> / <paramref name="arNote"/>
    /// add a trailing sentence (e.g. "ignore this if you did not request it").
    /// </summary>
    public static EmailMessage Code(
        string to,
        string subject,
        string enLead,
        string arLead,
        string code,
        int expiryMinutes,
        string? enNote = null,
        string? arNote = null)
    {
        var enExpiry = $"The code expires in {expiryMinutes} minutes.";
        var arExpiry = $"ينتهي الرمز خلال {expiryMinutes} دقائق.";
        var body =
            $"<p>{enLead} <strong>{code}</strong>.</p>" +
            $"<p>{enExpiry}{Trailing(enNote)}</p>" +
            "<hr/>" +
            $"<p dir=\"rtl\">{arLead} <strong>{code}</strong>.</p>" +
            $"<p dir=\"rtl\">{arExpiry}{Trailing(arNote)}</p>";
        return new EmailMessage(to, subject, body);
    }

    /// <summary>
    /// A code-less bilingual notice (e.g. the "an account already exists"
    /// heads-up): the caller-supplied English HTML block over the Arabic HTML
    /// block, separated by a rule (the Arabic block is wrapped right-to-left).
    /// </summary>
    public static EmailMessage Notice(
        string to,
        string subject,
        string enHtml,
        string arHtml)
    {
        var body = $"{enHtml}<hr/><div dir=\"rtl\">{arHtml}</div>";
        return new EmailMessage(to, subject, body);
    }

    private static string Trailing(string? note) =>
        string.IsNullOrEmpty(note) ? string.Empty : " " + note;
}
