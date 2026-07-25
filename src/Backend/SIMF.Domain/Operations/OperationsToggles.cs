namespace SIMF.Domain.Operations;

/// <summary>
/// D-166 (gap doc G4, PDF §2.3) — singleton "registration open" gate.
/// One row, fixed Id (Guid.Empty), updated in place by admins.
/// Sign-up reads this; sign-up is rejected with REGISTRATION_CLOSED when
/// <see cref="IsOpen"/> is false.
/// </summary>
public class RegistrationGate
{
    public static readonly Guid SingletonId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");

    public Guid Id { get; set; } = SingletonId;

    /// <summary>True while sign-up is accepted. Admins toggle via the
    /// admin endpoint; the background worker flips it to false when
    /// <see cref="AutoClose"/> passes.</summary>
    public bool IsOpen { get; set; } = true;

    /// <summary>Optional UTC moment after which the worker auto-closes
    /// the gate. Null means "no scheduled close".</summary>
    public DateTimeOffset? AutoClose { get; set; }

    public DateTimeOffset LastChangedAt { get; set; }

    /// <summary>Admin user id of the last manual toggle. Null when the
    /// auto-close worker did the flip.</summary>
    public Guid? LastChangedByUserId { get; set; }
}

/// <summary>
/// D-166 (gap doc G4, PDF §2.4) — singleton "archive visible" switch.
/// Drives whether the Flutter app + Website surface the past-events
/// archive. Public GET endpoint lets unauthenticated callers check the
/// current state without authentication.
/// </summary>
public class ArchiveVisibility
{
    public static readonly Guid SingletonId =
        Guid.Parse("00000000-0000-0000-0000-000000000002");

    public Guid Id { get; set; } = SingletonId;

    /// <summary>True while the archive is visible. Admins toggle via
    /// the admin endpoint.</summary>
    public bool IsVisible { get; set; } = true;

    public DateTimeOffset LastChangedAt { get; set; }
    public Guid? LastChangedByUserId { get; set; }
}
