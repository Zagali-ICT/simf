using Microsoft.AspNetCore.Authentication.Cookies;
using Serilog;
using SIMF.ApiClient;
using SIMF.Web;
using SIMF.Web.Components;
using SIMF.Web.Content;
using SIMF.Web.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Production secrets/config arrive as SIMF_-prefixed Machine-scope environment
// variables (deploy/set-env-*.ps1, SIMF-OPS-001 section 6). This source strips
// the prefix, so SIMF_Api__BaseUrl binds to Api:BaseUrl. ASPNETCORE_ENVIRONMENT
// stays un-prefixed (the host reads it before configuration sources load). (D-355)
builder.Configuration.AddEnvironmentVariables("SIMF_");

// P6 — per-project log files under {Storage:LogDirectory}/SIMF.Web/log-{Date}.log.
builder.Host.UseSerilog((context, configuration) =>
{
    var logDir = context.Configuration["Storage:LogDirectory"] ?? "logs";
    var appName = context.HostingEnvironment.ApplicationName ?? "SIMF.Web";
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

// Razor components — server-side rendered, with interactive Server islands for
// the pages that need them (the authentication pages, the visitor profile).
// Public content pages stay server-side rendered.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        // The user-profile page renders a server-side SVG QR — raise the
        // SignalR receive cap so the diff fits.
        options.MaximumReceiveMessageSize = 256 * 1024;
    });

// Localisation — English and Arabic; resources live under Resources/.
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Cookie authentication for visitors (decision D-046 c, mirrors the CP's
// D-029 setup). The cookie carries the visitor's identity and (encrypted)
// the SIMF API tokens; the access token never reaches the browser.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "simf.web.auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
        // D-121 — rotate the cookie's stored access_token using the
        // refresh_token before the JWT expires. Without this hook, every
        // /account/api/* BFF call past the 30-minute mark forwards an
        // expired JWT and the API returns 401, even though the cookie is
        // still valid for hours. See SimfCookieRefreshHandler for detail.
        options.Events.OnValidatePrincipal = SimfCookieRefreshHandler.OnValidatePrincipalAsync;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// Sign-in ticket store — the one-time hand-off between the verification
// page and the cookie (the cookie can only be written in an HTTP request,
// not from an interactive Blazor circuit).
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<SignInTicketStore>();

// The signed-in second-factor flow state lives per Blazor circuit.
builder.Services.AddScoped<SimfAuthSession>();

// HttpContext access — the visitor profile reads the access token from the
// cookie during the initial (prerendered) render of the proxy endpoints.
builder.Services.AddHttpContextAccessor();

// SIMF_Api__AllowSelfSignedCertificate=true → accept the API's self-signed
// certificate on the server-to-server API calls (the API uses a self-signed
// cert whose name does not match the host). Default false → normal TLS
// validation, so dev and any other environment are unaffected. (D-355)
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

// Typed clients share one validated API base address (SimfApiBaseAddress) —
// server-to-server, so the token never reaches the browser. The self-signed
// cert handler (above) is applied to each so production over a self-signed
// API cert works when SIMF_Api__AllowSelfSignedCertificate=true.
var apiBaseUri = SimfApiBaseAddress.Resolve(
    builder.Configuration["Api:BaseUrl"], builder.Environment.IsDevelopment());

builder.Services.AddHttpClient<SimfAuthClient>(client => client.BaseAddress = apiBaseUri)
    .ConfigurePrimaryHttpMessageHandler(apiPrimaryHandler);
builder.Services.AddHttpClient<SimfAccountClient>(client => client.BaseAddress = apiBaseUri)
    .ConfigurePrimaryHttpMessageHandler(apiPrimaryHandler);
// The typed client for the SIMF anonymous public-read endpoints (D-199).
// Anonymous, so no bearer token; BaseAddress only — the public endpoints do
// not require an X-App-Key header in this build.
builder.Services.AddHttpClient<SimfPublicClient>(client => client.BaseAddress = apiBaseUri)
    .ConfigurePrimaryHttpMessageHandler(apiPrimaryHandler);

// D-755 — resolves the forum event dates from the public OrganizationProfile
// (cached) and formats the shared bilingual range for the marketing pages, so the
// date is driven by CP config instead of a hardcoded resx literal.
builder.Services.AddScoped<ForumDates>();

// D-756 — resolves the hero-section background video from the public
// OrganizationProfile (cached) so the landing hero plays the CP-configured video
// instead of the bundled hero-video.mp4 asset.
builder.Services.AddScoped<HeroMedia>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

// Baseline security response headers (mirrors the CP; NCA App-Sec Standard
// A3-4 / A5-13 / A6-21). frame-ancestors 'none' is enforced; the full content
// policy ships Report-Only first so the owner can confirm in-browser before
// enforcing (see the gap report Wave 4).
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
        // D-756 — the hero background video may be a YouTube embed (privacy host).
        + "frame-src 'self' https://www.youtube-nocookie.com https://www.youtube.com; "
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

// Static assets (bundled Bootstrap, self-hosted fonts, images). "/" is the
// Blazor SSR marketing landing (Landing.razor @page "/") — no default-document
// rewrite, so the request falls through to endpoint routing.
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapAuthEndpoints();
app.MapAccountEndpoints();
app.MapSiteContentEndpoints();
app.MapChatEndpoints();
app.MapCultureEndpoint();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
