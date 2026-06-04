namespace SIMF.Application.Abstractions;

/// <summary>
/// Runs a unit of work inside a database transaction. Used where several writes
/// must commit together or not at all — for example, creating a user and its
/// first verification code during sign-up.
/// </summary>
public interface ITransactionRunner
{
    Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default);
}
