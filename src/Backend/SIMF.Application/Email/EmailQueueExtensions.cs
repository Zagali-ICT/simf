using Microsoft.Extensions.Logging;
using SIMF.Application.Auditing;
using SIMF.Common;
using SIMF.Common.Enums;
using SIMF.Domain.Auditing;

namespace SIMF.Application.Email;

/// <summary>
/// The try / catch + EmailEnqueueFailed audit pattern
/// was duplicated verbatim at four sites in
/// <c>PasswordService</c> / <c>RegistrationService</c> /
/// <c>SignInService</c>. Each repeat copied the same shape: try the
/// Enqueue, on any exception log + audit with a per-purpose <c>Detail</c>.
/// A fifth caller (admin invite, recovery codes, …) would copy this
/// block by hand and the slight drift (typo in the Purpose tag, swapped
/// email argument, missed audit) becomes the bug.
///
/// <para>This extension consolidates the pattern. Each caller becomes a
/// single line; the invariant ("persist row inside the TX, dispatch
/// outside") stays in the caller, only the failure handling moves.</para>
/// </summary>
public static class EmailQueueExtensions
{
    /// <summary>The <c>Detail</c> marker for the second failure mode: the queue
    /// did not throw, it refused the message because it is at capacity. Kept
    /// distinct from an exception type name so the audit trail says which of the
    /// two happened.</summary>
    private const string QueueFullDetail = "QueueFull";

    /// <summary>
    /// Enqueues an email and, on failure, writes a distinct
    /// <c>Email.EnqueueFailed</c> audit row keyed on the subject email +
    /// user id, with the purpose tag and the cause in <c>Detail</c>.
    /// Failures are swallowed — by contract the email
    /// dispatch is a side-effect on a different scope from the DB write
    /// the caller has already committed; throwing here would propagate
    /// to the response and break the no-enumeration contract on
    /// forgot-password and the no-failure contract on sign-up.
    ///
    /// <para>There are two failure modes and both must land in the audit trail.
    /// A throwing queue is the obvious one. The quiet one is a queue that is
    /// simply FULL: it returns false rather than throwing, so for as long as this
    /// method only caught exceptions, a saturated queue discarded credential
    /// emails and audited nothing at all.</para>
    /// </summary>
    public static async Task TryEnqueueAsync(
        this IEmailQueue queue,
        EmailMessage message,
        string purpose,
        string subjectEmail,
        // Null when the recipient has no account yet - a badge holder being
        // emailed the code that will create one. The audit row already allows it.
        Guid? subjectUserId,
        IAuditLog auditLog,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        string? failure = null;

        try
        {
            if (!queue.Enqueue(message))
            {
                logger.LogError(
                    "{Purpose} email enqueue was refused for {Email}: the email queue is full",
                    purpose, subjectEmail);
                failure = QueueFullDetail;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "{Purpose} email enqueue failed for {Email}", purpose, subjectEmail);
            failure = ex.GetType().Name;
        }

        if (failure is null)
        {
            return;
        }

        // Outside the try on purpose: an audit write that fails is not another
        // enqueue failure, and catching it here would retry it into the same
        // handler that produced it.
        await auditLog.WriteAsync(
            new AuditEntry
            {
                EventType = AuditEvents.EmailEnqueueFailed,
                Outcome = AuditOutcome.Failure,
                SubjectEmail = subjectEmail,
                SubjectUserId = subjectUserId,
                ErrorCode = ErrorCodes.InternalError,
                Detail = $"{purpose}: {failure}",
            },
            cancellationToken);
    }
}
