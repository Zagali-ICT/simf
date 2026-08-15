namespace SIMF.Domain.Operations;

/// <summary>The registration-open gate: a single fixed-id row, updated in place. Sign-up is rejected outright while <see cref="IsOpen"/> is false.</summary>
public class RegistrationGate
{
    public static readonly Guid SingletonId =
        Guid.Parse("00000000-0000-0000-0000-000000000001");

    public Guid Id { get; set; } = SingletonId;

    /// <summary>An admin toggles it, and the background worker flips it false once <see cref="AutoClose"/> passes.</summary>
    public bool IsOpen { get; set; } = true;

    /// <summary>Saudi local time; null schedules no close.</summary>
    public DateTime? AutoClose { get; set; }

    public DateTime LastChangedAt { get; set; }

    /// <summary>Null when the auto-close worker made the change.</summary>
    public Guid? LastChangedByUserId { get; set; }
}

/// <summary>The archive-visible switch: a single fixed-id row. Its read endpoint is public, so an unauthenticated caller can check the state.</summary>
public class ArchiveVisibility
{
    public static readonly Guid SingletonId =
        Guid.Parse("00000000-0000-0000-0000-000000000002");

    public Guid Id { get; set; } = SingletonId;

    public bool IsVisible { get; set; } = true;

    public DateTime LastChangedAt { get; set; }
    public Guid? LastChangedByUserId { get; set; }
}
