namespace SIMF.Common.Enums;

/// <summary>
/// Lifecycle state of an
/// <c>Invitation</c> row. Brand-new enum; no existing wire contract
/// to preserve. Persisted as the underlying <c>int</c> via the
/// default EF mapping on <c>InvitationConfiguration</c>.
/// </summary>
public enum InvitationState
{
    /// <summary>The invitation has been sent and is awaiting the recipient's response.</summary>
    Pending = 0,

    /// <summary>The recipient has confirmed attendance.</summary>
    Confirmed = 1,

    /// <summary>The recipient has declined.</summary>
    Declined = 2,
}
