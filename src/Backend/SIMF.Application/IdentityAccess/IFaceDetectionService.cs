namespace SIMF.Application.IdentityAccess;

/// <summary>
/// C7 (D-371) — the server-side human-face gate on the profile image
/// upload. The client runs an on-device check for instant feedback; this
/// service is the authority (a client-side check is bypassable). The
/// implementation must run fully offline (NCA posture — no external
/// face-recognition service).
/// </summary>
public interface IFaceDetectionService
{
    /// <summary>True when at least one human face is detected in
    /// <paramref name="imageBytes"/> (PNG / JPEG / WebP). Infrastructure
    /// failures throw — the gate fails closed, never silently passes.</summary>
    Task<bool> ContainsHumanFaceAsync(
        byte[] imageBytes, CancellationToken cancellationToken = default);
}
