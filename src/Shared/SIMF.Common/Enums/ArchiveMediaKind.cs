namespace SIMF.Common.Enums;

/// <summary>The kind of media in an archive edition's gallery
/// (Mockup screen 24-01 "الصور والفيديو"). Additive enum (D-219 freeze-lift
/// permits new enums / additive values); persisted by its int value.</summary>
public enum ArchiveMediaKind
{
    Image = 0,
    Video = 1,
}
