namespace SIMF.Common.Enums;

/// <summary>D-199 — kind of a gallery <c>MediaItem</c> (Mockup page 30).
/// Persisted as an int; append-only — never rename or reorder existing
/// values (matches the D-110 enum-stability rule that survived the D-199
/// freeze-lift for new event tables).</summary>
public enum MediaKind
{
    Image = 0,
    Video = 1,
}
