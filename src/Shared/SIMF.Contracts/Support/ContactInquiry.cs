namespace SIMF.Contracts.Support;

// "تواصل معنا / Contact us" message contracts (app submit + CP inbox).

/// <summary>The app contact form's submit payload (Figma 1388:7567). Validated
/// by <c>SubmitContactInquiryValidator</c> (Name ≤120, Email valid ≤256,
/// Message ≤4000).</summary>
public sealed class SubmitContactInquiryRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

/// <summary>One inquiry row in the Control Panel inbox grid.</summary>
public sealed record AdminContactInquiryRow(
    Guid Id,
    string Name,
    string Email,
    string Message,
    bool IsHandled,
    Guid? SubmittedByUserId,
    DateTime CreatedAt,
    DateTime? HandledAt);
