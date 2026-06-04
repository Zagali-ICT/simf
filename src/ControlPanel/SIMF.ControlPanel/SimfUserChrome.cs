namespace SIMF.ControlPanel;

/// <summary>
/// Circuit-scoped store for the signed-in user's "chrome" — the bits the top
/// bar shows (today: the avatar URL). The profile page pushes updates here;
/// the shell layout subscribes to <see cref="Changed"/> and re-renders so the
/// top-bar avatar tracks the profile page in real time without a full reload.
/// </summary>
public sealed class SimfUserChrome
{
    /// <summary>
    /// The avatar URL (CP-relative, e.g. <c>/account/api/avatar/{id}?v=…</c>),
    /// or <c>null</c> when not set. The <c>?v=</c> cache-buster moves on every
    /// avatar change so the browser refetches the new bytes.
    /// </summary>
    public string? AvatarUrl { get; private set; }

    /// <summary>Fired when the avatar (or any future chrome field) changes.</summary>
    public event Action? Changed;

    /// <summary>
    /// Sets the avatar URL; no-ops when the value is identical so subscribers
    /// are not re-rendered for nothing.
    /// </summary>
    public void SetAvatar(string? avatarUrl)
    {
        if (AvatarUrl == avatarUrl)
        {
            return;
        }
        AvatarUrl = avatarUrl;
        Changed?.Invoke();
    }
}
