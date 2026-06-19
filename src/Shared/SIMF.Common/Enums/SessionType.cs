namespace SIMF.Common.Enums;

/// <summary>D-452 (Figma 883:2308 filter tabs) — the kind of a programme
/// <c>Session</c>, used by the app's "ورش العمل / جلسات / احداث" type tabs.
/// Persisted as an int; append-only — never rename or reorder existing values
/// (the D-110 enum-stability rule). Nullable on the entity ("الكل / All" = the
/// unfiltered view + sessions with no type set).</summary>
public enum SessionType
{
    Workshop = 0,
    Session = 1,
    Event = 2,
}
