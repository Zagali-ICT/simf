namespace SIMF.Common.Enums;

/// <summary>The seat TIER: real data on the hall seat
/// layout, not a display label. A tier is a property of a whole ROW (the layout
/// already addresses seats as row + 1-based number, and the row CSVs
/// <c>RowLabels</c> / <c>SeatCounts</c> are the established shape), so every seat
/// in a row carries its row's tier.
/// <list type="bullet">
/// <item><see cref="Normal"/> — the general audience row. Any approved visitor
/// may self-reserve a seat in it.</item>
/// <item><see cref="Vip"/> — reserved for the VIP audience tiers. Only a visitor
/// whose <c>ProfileType.AllowsVipMeetingSlots</c> is set (the seeded VVIP + VIP
/// rows — the same flag the app already surfaces as <c>isVip</c>) may
/// self-reserve; everyone else is rejected with
/// <c>SEAT_TIER_NOT_ELIGIBLE</c>.</item>
/// <item><see cref="Vvip"/> — protocol seating. <b>Never</b> self-reservable by
/// anyone (there is no registration for a VVIP seat): an administrator blocks the
/// seat from the Control Panel and types the guest's name as a free-text hint
/// (e.g. "هذا المقعد محجوز لمعالي الوزير"). Rejected with
/// <c>SEAT_TIER_RESERVED</c>.</item>
/// </list>
/// Stored as an int inside the layout's <c>SeatTiers</c> CSV. <see cref="Normal"/>
/// = 0 so a legacy layout written before tiers existed (null CSV) reads as an
/// all-Normal grid and keeps its shipped behaviour exactly.</summary>
public enum SeatTier
{
    Normal = 0,
    Vip = 1,
    Vvip = 2,
}
