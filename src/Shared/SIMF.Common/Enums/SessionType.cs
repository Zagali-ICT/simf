namespace SIMF.Common.Enums;

/// <summary>The kind of a programme
/// <c>Session</c>, used by the app's "ورش العمل / جلسات / احداث" type tabs.
/// Persisted as an int; append-only — never rename or reorder existing values,
/// per the enum-stability rule. Nullable on the entity ("الكل / All" = the
/// unfiltered view + sessions with no type set).</summary>
public enum SessionType
{
    Workshop = 0,
    Session = 1,
    Event = 2,
}
