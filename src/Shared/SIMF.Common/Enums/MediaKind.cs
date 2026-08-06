namespace SIMF.Common.Enums;

/// <summary>Kind of a gallery <c>MediaItem</c>.
/// Persisted as an int; append-only — never rename or reorder existing
/// values (matches the enum-stability rule that survived the freeze-lift for
/// the new event tables).</summary>
public enum MediaKind
{
    Image = 0,
    Video = 1,
}
