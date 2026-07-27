// Tests: SIMF.Api.Tests/VisitorContactSharingTests.cs
// Tests: SIMF.Api.Tests/ExhibitorLeadManagementTests.cs
using System.Text;
using SIMF.Contracts.Contacts;

namespace SIMF.Api.Endpoints.Contacts;

/// <summary>The one vCard 3.0 rendering of a <see cref="VisitorCard"/>, shared by
/// My Contacts (<c>GET /app/contacts/{id}/vcard</c>) and the exhibitor's captured
/// leads (<c>GET /app/exhibitor/visitors/{id}/vcard</c>).
///
/// <para>FR-EXH-002 gave the lead list the vCard export My Contacts already had.
/// The two exports render the SAME DTO, so this is the one implementation rather
/// than a second copy that would drift the first time either side gains a field —
/// the response headers stay with each endpoint, since the download filename
/// differs.</para></summary>
internal static class VisitorCardVCard
{
    /// <summary>The media type both exports answer with.</summary>
    public const string ContentType = "text/vcard; charset=utf-8";

    /// <summary>vCard 3.0 (mirrors the My-Area contact-card export). The visitor
    /// card adds TEL (Saudi + international mobile) and EMAIL over the My-Area
    /// shape.</summary>
    public static string Build(VisitorCard card)
    {
        var displayName = !string.IsNullOrWhiteSpace(card.Name) ? card.Name : card.NameArabic;
        var sb = new StringBuilder();
        sb.Append("BEGIN:VCARD\r\n");
        sb.Append("VERSION:3.0\r\n");
        sb.Append("FN:").Append(Escape(displayName)).Append("\r\n");
        sb.Append("N:").Append(Escape(displayName)).Append(";;;;\r\n");
        if (!string.IsNullOrWhiteSpace(card.JobTitle))
        {
            sb.Append("TITLE:").Append(Escape(card.JobTitle!)).Append("\r\n");
            // Bilingual title (2026-07-20): the Arabic title as a language-tagged
            // second TITLE (RFC 6350 LANGUAGE param); parsers that keep only the
            // first still get the English one.
            if (!string.IsNullOrWhiteSpace(card.JobTitleArabic))
            {
                sb.Append("TITLE;LANGUAGE=ar:").Append(Escape(card.JobTitleArabic!)).Append("\r\n");
            }
        }
        else if (!string.IsNullOrWhiteSpace(card.JobTitleArabic))
        {
            // Arabic-only title → emit it as the sole (untagged) TITLE so every
            // parser shows it.
            sb.Append("TITLE:").Append(Escape(card.JobTitleArabic!)).Append("\r\n");
        }
        if (!string.IsNullOrWhiteSpace(card.Organisation))
        {
            sb.Append("ORG:").Append(Escape(card.Organisation!)).Append("\r\n");
        }
        if (!string.IsNullOrWhiteSpace(card.Email))
        {
            sb.Append("EMAIL;TYPE=INTERNET:").Append(Escape(card.Email!)).Append("\r\n");
        }
        if (!string.IsNullOrWhiteSpace(card.SaudiMobile))
        {
            sb.Append("TEL;TYPE=CELL:").Append(Escape(card.SaudiMobile!)).Append("\r\n");
        }
        if (!string.IsNullOrWhiteSpace(card.InternationalMobile))
        {
            sb.Append("TEL;TYPE=CELL:").Append(Escape(card.InternationalMobile!)).Append("\r\n");
        }
        sb.Append("END:VCARD\r\n");
        return sb.ToString();
    }

    // vCard text escaping (RFC 6350 §3.4).
    private static string Escape(string value) => value
        .Replace("\\", "\\\\")
        .Replace(";", "\\;")
        .Replace(",", "\\,")
        .Replace("\r\n", "\\n")
        .Replace("\n", "\\n");
}
