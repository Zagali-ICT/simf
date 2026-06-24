namespace SIMF.Common.Enums;

/// <summary>D-495 — the lifecycle state of the current forum edition, shown as a
/// status badge on the app + website and editable on the CP Organization Profile
/// page. Brand-new enum (outside the D-110 freeze); persisted as an int —
/// append-only, never rename or reorder existing values.</summary>
public enum ForumStatus
{
    /// <summary>The edition is announced but not yet open ("coming soon").</summary>
    Soon = 0,

    /// <summary>The edition is currently active / open.</summary>
    Open = 1,

    /// <summary>The edition has finished — closed / archived.</summary>
    Archived = 2,
}
