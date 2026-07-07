namespace SIMF.Common.Enums;

/// <summary>The reach of a <c>RatingType</c> — how many times a single user may
/// submit it and what (if anything) the submission targets. Persisted as an int
/// (EF convention); append-only — never rename or reorder existing values
/// (the D-110 enum-stability rule).</summary>
public enum RatingScope
{
    /// <summary>One submission per user for the whole forum (e.g. "App",
    /// "Rate the Forum"). The response carries no target (<c>TargetId</c> is the
    /// empty-Guid sentinel).</summary>
    Global = 0,

    /// <summary>One submission per user per target entity (e.g. "Session" — the
    /// response's <c>TargetId</c> is the rated <c>Session.Id</c>).</summary>
    PerSession = 1,

    /// <summary>One submission per user per programme day (e.g. "Day" — the
    /// response's <c>TargetId</c> is the rated <c>ProgrammeDay.Id</c>). Fired by
    /// the end-of-day rating prompt to attendees who checked in that day
    /// (D-679).</summary>
    PerDay = 2,
}
