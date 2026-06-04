namespace SIMF.Domain.Common;

/// <summary>
/// Base type for every SIMF domain exception — a violated domain rule or an
/// illegal state transition. Specific exceptions derive from this as the
/// features that throw them are built.
/// </summary>
public abstract class DomainException(string message) : Exception(message);
