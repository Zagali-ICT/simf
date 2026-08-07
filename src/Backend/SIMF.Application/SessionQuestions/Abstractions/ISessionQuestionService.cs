using SIMF.Contracts.Sessions;

namespace SIMF.Application.SessionQuestions.Abstractions;

/// <summary>
/// Public-facing surface for
/// audience question submission. Used by the mobile app via
/// <c>POST /api/v1/sessions/{sessionId}/questions</c>. The geofence
/// gate is **deferred** — when the venue boundary is settled, the check is
/// added here, not at the endpoint, so the gate logic lives in one
/// place.
/// </summary>
public interface ISessionQuestionService
{
    /// <summary>Submit a question to a live session. Validates the
    /// session exists + is active + falls inside its time window.
    /// Returns the new row's id + queue position.</summary>
    Task<SessionQuestionSubmitted> SubmitAsync(
        Guid sessionId,
        Guid submittedByUserId,
        SubmitSessionQuestionRequest request,
        CancellationToken cancellationToken = default);
}
