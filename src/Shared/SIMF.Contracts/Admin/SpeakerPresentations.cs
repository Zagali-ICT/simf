namespace SIMF.Contracts.Admin;

/// <summary>P2.3 — D-228 (FR-407): one speaker-presentation row in the admin
/// management list. <see cref="SessionTitle"/> is resolved from the same
/// DbContext (Session is a real FK on SimfAppDbContext). The file itself is
/// fetched from the download endpoint, not embedded here.</summary>
public sealed record AdminSpeakerPresentationRow(
    Guid Id,
    Guid SpeakerId,
    Guid SessionId,
    string SessionTitle,
    string SessionTitleArabic,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAt);
