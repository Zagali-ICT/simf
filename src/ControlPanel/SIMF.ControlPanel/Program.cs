using Cropper.Blazor.Extensions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Serilog;
using SIMF.ApiClient;
using SIMF.ControlPanel;
using SIMF.ControlPanel.Components;
using SIMF.ControlPanel.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Production secrets/config arrive as SIMF_-prefixed Machine-scope environment
// variables (deploy/set-env-*.ps1, SIMF-OPS-001 section 6). This source strips
// the prefix, so SIMF_Api__BaseUrl binds to Api:BaseUrl. ASPNETCORE_ENVIRONMENT
// stays un-prefixed (the host reads it before configuration sources load).
builder.Configuration.AddEnvironmentVariables("SIMF_");

// P6 — per-project log files under {Storage:LogDirectory}/SIMF.ControlPanel/log-{Date}.log.
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
        // D-122 — raised from 256 KB (QR SVG render diff) to 10 MB to match
        // V10 ERP's cropper image-transfer limit. The D-116 cropper consumes
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
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
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
        // D-121 — rotate the cookie's stored access_token using the
        // refresh_token before the JWT expires. Without this hook, every
        // /account/api/* BFF call past the 30-minute mark forwards an
        // expired JWT and the API returns 401, even though the cookie is
        // still valid for hours. See SimfCookieRefreshHandler for detail.
        options.Events.OnValidatePrincipal = SimfCookieRefreshHandler.OnValidatePrincipalAsync;
    });
builder.Services.AddAuthorization();
// Issue-1 — per-page/per-action permission policies (perm:<code>). The dynamic
// provider materialises them on demand and the handler matches the required
// code against the principal's `perm` claims (Administrator's wildcard passes
// any). Registered after AddAuthorization so this provider wins for perm: names.
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider,
    SIMF.ControlPanel.Authorization.PermissionPolicyProvider>();
builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler,
    SIMF.ControlPanel.Authorization.PermissionAuthorizationHandler>();
builder.Services.AddCascadingAuthenticationState();

// D-122 — Cropper.Blazor DI registration (was missing in D-116). Without
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

// HttpContext access — the profile page captures the access token from the
// cookie auth result during the initial (prerendered) render, then holds it
// in the circuit for the interactive callbacks that follow.
builder.Services.AddHttpContextAccessor();

// SIMF_Api__AllowSelfSignedCertificate=true → accept the API's self-signed
// certificate on the server-to-server API calls (the API uses a self-signed
// cert whose name does not match the host). Default false → normal TLS
// validation, so dev and any other environment are unaffected.
var allowSelfSignedApiCert =
    builder.Configuration.GetValue<bool>("Api:AllowSelfSignedCertificate");
Func<HttpMessageHandler> apiPrimaryHandler = () =>
{
    var handler = new HttpClientHandler();
    if (allowSelfSignedApiCert)
    {
        handler.ServerCertificateCustomValidationCallback =
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
    }
    return handler;
};

// The typed client for the SIMF Login API. The call is server-to-server, so
// the access token never reaches the browser and there is no cross-origin
// concern.
builder.Services.AddHttpClient<SimfAuthClient>(client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException(
            "Configuration value 'Api:BaseUrl' is required but was not found.");
    var baseUri = new Uri(baseUrl);
    if (!builder.Environment.IsDevelopment() && baseUri.Scheme != Uri.UriSchemeHttps)
    {
        throw new InvalidOperationException(
            "'Api:BaseUrl' must use HTTPS outside the Development environment.");
    }
    client.BaseAddress = baseUri;
})
    .ConfigurePrimaryHttpMessageHandler(apiPrimaryHandler);

builder.Services.AddHttpClient<SimfAccountClient>(client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"]!;
    client.BaseAddress = new Uri(baseUrl);
})
    .ConfigurePrimaryHttpMessageHandler(apiPrimaryHandler);

builder.Services.AddHttpClient<SimfAdminClient>(client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"]!;
    client.BaseAddress = new Uri(baseUrl);
})
    .ConfigurePrimaryHttpMessageHandler(apiPrimaryHandler);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Baseline security response headers. A full Content-Security-Policy is a
// later hardening item (it needs a nonce for the theme bootstrap script).
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
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
