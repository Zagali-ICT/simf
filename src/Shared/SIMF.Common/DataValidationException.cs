namespace SIMF.Common;

/// <summary>
/// A validation failure — HTTP 400 with the <c>VALIDATION_FAILED</c> code
/// (SIMF-SES-001 section 2). Carries the field-level details.
/// </summary>
public sealed class DataValidationException : ApiException
{
    public DataValidationException(
        string message,
        IReadOnlyList<ApiErrorDetail>? details = null)
        : base(ErrorCodes.ValidationFailed, 400, message, details)
    {
    }
}
