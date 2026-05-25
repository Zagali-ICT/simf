using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Domain.Auditing;

namespace SIMF.Application.Email;

/// <summary>
/// H23 — D-083: the H10 try / catch + EmailEnqueueFailed audit pattern
/// (D-065) was duplicated verbatim at four sites in
/// <c>PasswordService</c> / <c>RegistrationService</c> /
/// <c>SignInService</c>. Each repeat copied the same shape: try the
/// Enqueue, on any exception log + audit with a per-purpose <c>Detail</c>.
/// A fifth caller (admin invite, recovery codes, …) would copy this
/// block by hand and the slight drift (typo in the Purpose tag, swapped
/// email argument, missed audit) becomes the bug.
///
/// <para>This extension consolidates the pattern. Each caller becomes a
/// single line; the H10 invariant ("persist row inside the TX, dispatch
/// outside") stays in the caller, only the failure handling moves.</para>
/// </summary>
public static class EmailQueueExtensions
{
    /// <summary>
    /// Enqueues an email and, on failure, writes a distinct
    /// <c>Email.EnqueueFailed</c> audit row keyed on the subject email +
    /// user id, with the purpose tag and the exception type in
    /// <c>Detail</c>. Failures are swallowed — by contract the email
    /// dispatch is a side-effect on a different scope from the DB write
    /// the caller has already committed; throwing here would propagate
    /// to the response and break the no-enumeration contract on
    /// forgot-password and the no-failure contract on sign-up.
    /// </summary>
    public static async Task TryEnqueueAsync(
        this IEmailQueue queue,
        EmailMessage message,
        string purpose,
        string subjectEmail,
        Guid subjectUserId,
        IAuditLog auditLog,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            queue.Enqueue(message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{Purpose} email enqueue failed for {Email}", purpose, subjectEmail);
            await auditLog.WriteAsync(
                new AuditEntry
                {
                    EventType = AuditEvents.EmailEnqueueFailed,
                    Outcome = AuditOutcome.Failure,
                    SubjectEmail = subjectEmail,
                    SubjectUserId = subjectUserId,
                    ErrorCode = ErrorCodes.InternalError,
                    Detail = $"{purpose}: {ex.GetType().Name}",
                },
                cancellationToken);
        }
    }
}
