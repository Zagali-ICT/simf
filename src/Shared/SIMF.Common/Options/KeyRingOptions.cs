namespace SIMF.Common.Options;

/// <summary>Settings for the shared ASP.NET Core Data Protection key ring, bound
/// from the <c>DataProtection</c> configuration section. Named for the key ring
/// rather than for the section, because the framework already owns the type name
/// <c>DataProtectionOptions</c> and the two would collide in any host that
/// configures both.
///
/// <para>Data Protection encrypts the Control Panel's auth cookie (which carries
/// the API tokens) and every antiforgery token the Blazor hosts issue. Left
/// unconfigured, each process generates its own key ring on local disk. That is
/// fine on a single node and silently broken on two: a cookie or antiforgery
/// token minted on one instance cannot be decrypted by the next, so the user is
/// bounced back to the sign-in page at random.</para>
///
/// <para>The remedy is one ring on shared storage that every instance of an app
/// reads. It lives beside the file store on the file server rather than on any
/// application host, so no node owns it.</para>
///
/// <para>The API does not bind this section: it authenticates with bearer tokens
/// and registers no antiforgery middleware and no <c>IDataProtector</c>, so it
/// has nothing to share.</para></summary>
public sealed class KeyRingOptions
{
    public const string SectionName = "DataProtection";

    /// <summary>Directory the key ring is persisted to, shared by every instance
    /// of the app. A UNC path to the file server in a separated estate.
    ///
    /// <para>Empty means "use the framework default", which keeps the keys local
    /// to the process. That is allowed in Development only; outside it the host
    /// refuses to start, because an unset value degrades to a per-node ring that
    /// breaks the moment a second instance is added and gives no warning that it
    /// has.</para></summary>
    public string KeyRingPath { get; set; } = string.Empty;
}
