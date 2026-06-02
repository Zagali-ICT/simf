namespace SIMF.Common.Enums;

/// <summary>
/// SIMF-FDS-013 (owner, 2026-06-03) — the category of an admin-arranged business
/// meeting. Set explicitly by the admin on the meeting (OI-10); participants may
/// be any mix of companies and visitors.
/// </summary>
public enum BusinessMeetingType
{
    /// <summary>Business-to-business — typically company ↔ company.</summary>
    B2B = 0,

    /// <summary>Business-to-consumer — typically company/sponsor ↔ visitor.</summary>
    B2C = 1,
}
