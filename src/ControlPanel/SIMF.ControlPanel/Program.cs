using Cropper.Blazor.Extensions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Serilog;
using SIMF.ApiClient;
using SIMF.Common.Options;
using SIMF.ControlPanel;
using SIMF.ControlPanel.Components;
using SIMF.ControlPanel.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Production secrets/config arrive as SIMF_-prefixed Machine-scope environment
// variables (see deploy/set-env-*.ps1). This source strips
// the prefix, so SIMF_Api__BaseUrl binds to Api:BaseUrl. ASPNETCORE_ENVIRONMENT
// stays un-prefixed (the host reads it before configuration sources load).
builder.Configuration.AddEnvironmentVariables("SIMF_");

// Per-project log files under {Storage:LogDirectory}/SIMF.ControlPanel/log-{Date}.log.
builder.Host.UseSerilog((context, configuration) =>
{
    var logDir = context.Configuration["Storage:LogDirectory"] ?? "logs";
    var appName = context.HostingEnvironment.ApplicationName ?? "SIMF.ControlPanel";
    var path = Path.Combine(logDir, appName, "log-.log");
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            path: path,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 31,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} "
                + "[{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");
});

// Razor components with interactive Server rendering. The hub's
// MaximumReceiveMessageSize default (32 KB) is too small for the QR SVG
// render diff on the /account/profile page — raise to 256 KB, which
// comfortably fits the QR and any future large server-rendered payload.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents(options =>
    {
        options.DetailedErrors = builder.Environment.IsDevelopment();
    })
    .AddHubOptions(options =>
    {
        // Raised from 256 KB (QR SVG render diff) to 10 MB to match
        // V10 ERP's cropper image-transfer limit. The cropper consumes
        // base64 data URLs of the source image (up to the 2 MB avatar policy)
        // through JS interop, which travels over the same SignalR transport.
        options.MaximumReceiveMessageSize = 10 * 1024 * 1024;
    });

// Localisation — English and Arabic; resources live under Resources/.
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Cookie authentication. The cookie carries the signed-in user's identity and
// (encrypted) the SIMF API tokens. An unauthenticated request to a protected
// page is sent to the sign-in page; an access-denied event is logged.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/not-permitted";
        // Auth-cookie idle lifetime, in hours. Ops-configurable via
        // SIMF_Session__LifetimeHours; default 8h (the NCA-safe baseline the
        // committed deploy template ships). A larger value cannot extend the
        // real session beyond the API's absolute cap Jwt:SessionLifetimeHours
        // (the NCA-driven 24h ceiling): the refresh token still expires there and forces
        // re-login, so this knob is for shorter idle windows / test overrides.
        var sessionLifetimeHours =
            builder.Configuration.GetValue<int>("Session:LifetimeHours", 8);
        options.ExpireTimeSpan = TimeSpan.FromHours(sessionLifetimeHours);
        options.SlidingExpiration = true;
        options.Cookie.Name = "simf.cp.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // The cookie carries the API tokens — never send it over plain HTTP
        // outside Development.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        options.Events.OnRedirectToAccessDenied = context =>
        {
            AuthLog.Of(context.HttpContext).LogWarning(
                "Control Panel access denied for {User} at {Path}.",
                context.HttpContext.User.Identity?.Name ?? "(unknown)",
                context.Request.Path);
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
        // Rotate the cookie's stored access_token using the
        // refresh_token before the JWT expires. Without this hook, every
        // /account/api/* BFF call past the 30-minute mark forwards an
        // expired JWT and the API returns 401, even though the cookie is
        // still valid for hours. See SimfCookieRefreshHandler for detail.
        options.Events.OnValidatePrincipal = SimfCookieRefreshHandler.OnValidatePrincipalAsync;
    });
builder.Services.AddAuthorization();
// Per-page/per-action permission policies (perm:<code>). The dynamic
// provider materialises them on demand and the handler matches the required
// code against the principal's `perm` claims (Administrator's wildcard passes
// any). Registered after AddAuthorization so this provider wins for perm: names.
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider,
    SIMF.ControlPanel.Authorization.PermissionPolicyProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
    SIMF.ControlPanel.Authorization.PermissionAuthorizationHandler>();
builder.Services.AddCascadingAuthenticationState();

// Cropper.Blazor DI registration. Without
// this call, CropperComponent crashes at runtime with "no registered
// service of type 'ICropperJsInterop'" the moment SimfImageCropperModal
// tries to render. Mirrors V10 ERP's Program.cs line 60.
builder.Services.AddCropper();

// The one-time sign-in hand-off between the verification page and the cookie.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<SignInTicketStore>();

// The signed-in second-factor flow state is held per Blazor circuit.
builder.Services.AddScoped<SimfAuthSession>();

// The top-bar avatar / user chrome — shared per circuit between the shell
// layout (reads to render) and the profile page (writes on load + change).
builder.Services.AddScoped<SimfUserChrome>();

// Per-user, per-page CRUD display preferences (dialog vs full page),
// persisted in the browser's localStorage. Scoped per circuit (it uses the
// circuit's IJSRuntime).
builder.Services.AddScoped<CpPreferences>();

// HttpContext access — the profile page captures the access token from the
// cookie auth result during the initial (prerendered) render, then holds it
// in the circuit for the interactive callbacks that follow.
builder.Services.AddHttpContextAccessor();

// Typed clients share one validated API base address - server-to-server, so the
// token never reaches the browser.
//
// No primary handler is configured, which is the point: these clients get the
// platform's default, with ordinary certificate-chain validation. There is no
// setting that can turn that off. An "accept any certificate" option cannot be
// offered safely here, because these are the clients that carry the admin
// password, the TOTP code and the perm:* bearer token - anything that answers
// for the configured host would receive them. If the API's certificate does not
// validate, that is an outage to fix on the server, not a check to disable.
var apiBaseUri = SimfApiBaseAddress.Resolve(
    builder.Configuration["Api:BaseUrl"], builder.Environment.IsDevelopment());

builder.Services.AddHttpClient<SimfAuthClient>(client => client.BaseAddress = apiBaseUri);
builder.Services.AddHttpClient<SimfAccountClient>(client => client.BaseAddress = apiBaseUri);
builder.Services.AddHttpClient<SimfAdminClient>(client => client.BaseAddress = apiBaseUri);

// The Data Protection key ring. It encrypts the auth cookie - which carries the
// API tokens - and every antiforgery token, so two instances that do not share it
// reject each other's cookies and bounce the admin back to /login at random.
// Persisted to shared storage (the file server in a separated estate) and named,
// so the Website's ring on the same share stays a distinct key set.
var dataProtectionOptions =
    builder.Configuration.GetSection(KeyRingOptions.SectionName).Get<KeyRingOptions>()
    ?? new KeyRingOptions();

if (!string.IsNullOrWhiteSpace(dataProtectionOptions.KeyRingPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionOptions.KeyRingPath))
        .SetApplicationName("SIMF.ControlPanel");
}

var app = builder.Build();

// A per-node key ring is invisible until a second instance appears, and then it
// presents as random sign-outs rather than as a configuration error. Refuse to
// start without it outside Development, the same way the API refuses without its
// reverse-proxy allowlist.
if (!app.Environment.IsDevelopment()
    && !app.Environment.IsEnvironment("Testing")
    && string.IsNullOrWhiteSpace(dataProtectionOptions.KeyRingPath))
{
    throw new InvalidOperationException(
        "DataProtection:KeyRingPath must be configured outside Development — "
        + "without a shared key ring the auth cookie and antiforgery tokens are "
        + "valid on one instance only.");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Baseline security response headers (NCA App-Sec Standard A3-4 / A5-13 / A6-21).
// frame-ancestors 'none' is enforced (the CP is never framed; matches the
// X-Frame-Options DENY). The full content policy ships as Report-Only first so the
// owner can confirm in-browser (Blazor Server's theme-bootstrap inline script + the
// SignalR socket) before flipping it to the enforcing header.
// 'unsafe-inline'/'unsafe-eval' are present only to keep the report-only
// pass quiet until a nonce is wired for the bootstrap script.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Content-Security-Policy"] = "frame-ancestors 'none'";
    headers["Content-Security-Policy-Report-Only"] =
        "default-src 'self'; "
        + "script-src 'self' 'unsafe-inline' 'unsafe-eval'; "
        + "style-src 'self' 'unsafe-inline'; "
        + "img-src 'self' data: blob: https:; "
        + "font-src 'self' data:; "
        + "connect-src 'self' ws: wss: https:; "
        + "object-src 'none'; base-uri 'self'; form-action 'self'; "
        + "frame-ancestors 'none'";
    await next();
});

// Interface language — English or Arabic, chosen by the culture cookie.
var supportedCultures = new[] { "en", "ar" };
app.UseRequestLocalization(new RequestLocalizationOptions()
    .SetDefaultCulture("en")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures));

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapCultureEndpoint();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
