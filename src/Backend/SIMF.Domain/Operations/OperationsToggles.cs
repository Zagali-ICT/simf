namespace SIMF.Domain.Operations;

/// <summary>
/// The "registration open" gate. A single row with a fixed id, updated in place.
/// Sign-up reads it and is rejected outright while <see cref="IsOpen"/> is false.
/// </summary>
public class RegistrationGate
{
    public static readonly Guid SingletonId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");

    public Guid Id { get; set; } = SingletonId;

    /// <summary>True while sign-up is accepted. An admin toggles it, and the
    /// background worker flips it to false once <see cref="AutoClose"/>
    /// passes.</summary>
    public bool IsOpen { get; set; } = true;

    /// <summary>The Saudi-local moment after which the worker closes the gate on
    /// its own. Null schedules no close.</summary>
    public DateTime? AutoClose { get; set; }

    public DateTime LastChangedAt { get; set; }

    /// <summary>The admin behind the last manual toggle. Null when the
    /// auto-close worker made the change.</summary>
    public Guid? LastChangedByUserId { get; set; }
}

/// <summary>
/// The "archive visible" switch, deciding whether the app and website surface
/// the past-events archive at all. A single row with a fixed id. Its read
/// endpoint is public, so an unauthenticated caller can check the current state.
/// </summary>
public class ArchiveVisibility
{
    public static readonly Guid SingletonId =
        Guid.Parse("00000000-0000-0000-0000-000000000002");

    public Guid Id { get; set; } = SingletonId;

    public bool IsVisible { get; set; } = true;

    public DateTime LastChangedAt { get; set; }
    public Guid? LastChangedByUserId { get; set; }
}
