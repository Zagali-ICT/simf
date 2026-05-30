namespace SIMF.Application.Media;

/// <summary>D-199 — module-local stable error codes for the Media module.
/// Kept here (not in <c>SIMF.Common.ErrorCodes</c>) because this increment
/// is delivered as new-files-only and must not edit the shared
/// <c>ErrorCodes</c> file. Values follow the same snake_case convention and
/// are append-only. Central integration MAY later move these onto
/// <c>ErrorCodes</c> verbatim.</summary>
public static class MediaErrorCodes
{
    public const string MediaNotFound = "media_not_found";
    public const string MediaInvalid = "media_invalid";
}

/// <summary>D-199 — module-local audit event identifiers for the Media
/// module, mirroring the <c>admin.&lt;entity&gt;.&lt;verb&gt;</c> convention in
/// <c>SIMF.Application.Auditing.AuditEvents</c>. Kept module-local for the
/// same new-files-only reason; append-only.</summary>
public static class MediaAuditEvents
{
    public const string MediaCreated = "admin.media.created";
    public const string MediaUpdated = "admin.media.updated";
    public const string MediaDeactivated = "admin.media.deactivated";
    public const string MediaImageSet = "admin.media.image.set";
}
