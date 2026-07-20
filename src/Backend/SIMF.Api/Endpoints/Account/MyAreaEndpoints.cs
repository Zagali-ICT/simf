// Tests: SIMF.Api.Tests/MyAreaDashboardTests.cs
using System.Globalization;
using System.Security.Claims;
using System.Text;
using FastEndpoints;
using SIMF.Api.Endpoints.Admin;
using SIMF.Application.MyArea;
using SIMF.Common;
using SIMF.Contracts.Account;

namespace SIMF.Api.Endpoints.Account;

/// <summary>
/// <c>GET /api/v1/app/account/dashboard</c> — the My-Area (منطقتي) personal
/// dashboard, App Screen 14 (Page_014): identity card + the two counters +
/// today's merged schedule. Approved account, own <c>sub</c>; an additive
/// read-only aggregate over existing App-DB tables. D-249.
/// </summary>
public sealed class MyAreaDashboardEndpoint(IMyAreaService service)
    : EndpointWithoutRequest<ApiResult<MyAreaDashboard>>
{
    public override void Configure()
    {
        Get("/app/account/dashboard");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Account");
        Summary(summary => summary.Summary =
            "My-Area dashboard: identity card, counters, today's schedule.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var dashboard = await service.GetDashboardAsync(userId, ct);
        await Send.OkAsync(ApiResult<MyAreaDashboard>.Ok(dashboard), ct);
    }
}

/// <summary>
/// <c>GET /api/v1/app/account/sessions</c> — the "my sessions" list (App
/// "تفاصيل الجلسات", Figma 1388:9067): the user's booked / joined sessions across
/// all days, each with the per-user heart + attended flag, time-ordered. The app
/// partitions them into the القادمة / حضرتها / فاتتني / الأرشيف tabs client-side.
/// Approved account, own <c>sub</c>; an additive read-only aggregate (no schema).
/// </summary>
public sealed class MyAreaSessionsEndpoint(IMyAreaService service)
    : EndpointWithoutRequest<ApiResult<MyAreaSessions>>
{
    public override void Configure()
    {
        Get("/app/account/sessions");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Account");
        Summary(summary => summary.Summary =
            "My sessions: the caller's booked / joined sessions with the heart + attended flags.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var sessions = await service.GetMySessionsAsync(userId, ct);
        await Send.OkAsync(ApiResult<MyAreaSessions>.Ok(sessions), ct);
    }
}

/// <summary>
/// <c>GET /api/v1/app/account/calendar.ics</c> — the user's full schedule (every
/// held session + accepted speaker meeting + confirmed business meeting, all
/// days) as an RFC 5545 calendar the app hands to the native add-to-calendar /
/// share intent (Page_014 E2). Approved account, own <c>sub</c>.
/// </summary>
public sealed class MyAreaCalendarEndpoint(IMyAreaService service, TimeProvider timeProvider)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/app/account/calendar.ics");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Account");
        Summary(summary => summary.Summary = "My calendar (RFC 5545 .ics) for the native share intent.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var events = await service.GetCalendarEventsAsync(userId, ct);
        var ics = BuildCalendar(events, timeProvider.GetUtcNow());

        HttpContext.Response.ContentType = "text/calendar; charset=utf-8";
        HttpContext.Response.Headers.ContentDisposition = "attachment; filename=\"simf.ics\"";
        await HttpContext.Response.WriteAsync(ics, ct);
    }

    private static string BuildCalendar(IReadOnlyList<MyAreaCalendarEvent> events, DateTimeOffset stamp)
    {
        var dtstamp = ToIcsUtc(stamp);
        var sb = new StringBuilder();
        sb.Append("BEGIN:VCALENDAR\r\n");
        sb.Append("VERSION:2.0\r\n");
        sb.Append("PRODID:-//SIMF//My Area//EN\r\n");
        sb.Append("CALSCALE:GREGORIAN\r\n");
        foreach (var e in events)
        {
            sb.Append("BEGIN:VEVENT\r\n");
            sb.Append("UID:").Append(e.Uid.ToString("N")).Append("@simf\r\n");
            sb.Append("DTSTAMP:").Append(dtstamp).Append("\r\n");
            sb.Append("DTSTART:").Append(ToIcsUtc(e.StartUtc)).Append("\r\n");
            if (e.EndUtc is { } end)
            {
                sb.Append("DTEND:").Append(ToIcsUtc(end)).Append("\r\n");
            }
            sb.Append("SUMMARY:").Append(EscapeText(e.Summary)).Append("\r\n");
            if (!string.IsNullOrWhiteSpace(e.Location))
            {
                sb.Append("LOCATION:").Append(EscapeText(e.Location!)).Append("\r\n");
            }
            sb.Append("END:VEVENT\r\n");
        }
        sb.Append("END:VCALENDAR\r\n");
        return sb.ToString();
    }

    private static string ToIcsUtc(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);

    // RFC 5545 §3.3.11 text escaping: backslash, semicolon, comma, newlines.
    private static string EscapeText(string value) => value
        .Replace("\\", "\\\\")
        .Replace(";", "\\;")
        .Replace(",", "\\,")
        .Replace("\r\n", "\\n")
        .Replace("\n", "\\n");
}

/// <summary>
/// <c>GET /api/v1/app/account/contact-card.vcf</c> — the user's contact card
/// (vCard 3.0) for the native share intent (Page_014 E3). The QR id is the
/// badge's unique key. Approved account, own <c>sub</c>.
/// </summary>
public sealed class MyAreaContactCardEndpoint(IMyAreaService service)
    : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/app/account/contact-card.vcf");
        Policies(nameof(AuthorizationPolicies.RequireApprovedAccount));
        Tags("Account");
        Summary(summary => summary.Summary = "My contact card (vCard .vcf) for the native share intent.");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("sub"), out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var card = await service.GetContactCardAsync(userId, ct);
        var vcard = BuildVCard(card);

        HttpContext.Response.ContentType = "text/vcard; charset=utf-8";
        HttpContext.Response.Headers.ContentDisposition = "attachment; filename=\"simf.vcf\"";
        await HttpContext.Response.WriteAsync(vcard, ct);
    }

    private static string BuildVCard(MyAreaContactCard card)
    {
        // D-470 — requirement #8 ("Name ar, phones"): the Arabic name leads (the
        // English name is the fallback), and the mobile numbers become TEL lines.
        // The gate QrId is intentionally NOT emitted — this vCard is encoded in a
        // QR any phone camera can read, so leaking the badge/lead key here would
        // let anyone harvest the holder's gate identity.
        var name = !string.IsNullOrWhiteSpace(card.FullNameAr) ? card.FullNameAr : card.FullNameEn;
        var sb = new StringBuilder();
        sb.Append("BEGIN:VCARD\r\n");
        sb.Append("VERSION:3.0\r\n");
        sb.Append("FN:").Append(EscapeText(name)).Append("\r\n");
        sb.Append("N:").Append(EscapeText(name)).Append(";;;;\r\n");
        if (!string.IsNullOrWhiteSpace(card.JobTitle))
        {
            sb.Append("TITLE:").Append(EscapeText(card.JobTitle!)).Append("\r\n");
            // Bilingual title (2026-07-20): Arabic title as a language-tagged
            // second TITLE (RFC 6350 LANGUAGE param); English stays first.
            if (!string.IsNullOrWhiteSpace(card.JobTitleArabic))
            {
                sb.Append("TITLE;LANGUAGE=ar:").Append(EscapeText(card.JobTitleArabic!)).Append("\r\n");
            }
        }
        else if (!string.IsNullOrWhiteSpace(card.JobTitleArabic))
        {
            sb.Append("TITLE:").Append(EscapeText(card.JobTitleArabic!)).Append("\r\n");
        }
        if (!string.IsNullOrWhiteSpace(card.Organisation))
        {
            sb.Append("ORG:").Append(EscapeText(card.Organisation!)).Append("\r\n");
        }
        if (!string.IsNullOrWhiteSpace(card.SaudiMobile))
        {
            sb.Append("TEL;TYPE=CELL:").Append(EscapeText(card.SaudiMobile!)).Append("\r\n");
        }
        if (!string.IsNullOrWhiteSpace(card.InternationalMobile))
        {
            sb.Append("TEL;TYPE=CELL:").Append(EscapeText(card.InternationalMobile!)).Append("\r\n");
        }
        sb.Append("END:VCARD\r\n");
        return sb.ToString();
    }

    // vCard text escaping (RFC 6350 §3.4): backslash, comma, semicolon, newlines.
    private static string EscapeText(string value) => value
        .Replace("\\", "\\\\")
        .Replace(";", "\\;")
        .Replace(",", "\\,")
        .Replace("\r\n", "\\n")
        .Replace("\n", "\\n");
}
