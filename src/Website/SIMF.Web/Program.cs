using Serilog;
using SIMF.ApiClient;
using SIMF.Web.Components;
using SIMF.Web.Content;
using SIMF.Web.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Production secrets/config arrive as SIMF_-prefixed Machine-scope environment
// variables (deploy/set-env.template.ps1). This source strips the prefix, so
// SIMF_Api__BaseUrl binds to Api:BaseUrl. ASPNETCORE_ENVIRONMENT stays
// un-prefixed — the host reads it before configuration sources load.
builder.Configuration.AddEnvironmentVariables("SIMF_");

// Per-project log files under {Storage:LogDirectory}/SIMF.Web/log-{Date}.log.
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

// Razor components — server-side rendered. The public Website is
// information-only, so the single interactive Server island left is the
// anonymous token-addressed /meeting/confirm page. Everything else is SSR.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Localisation — English and Arabic; resources live under Resources/.
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// Backs the cached public-content providers below (ForumDates / HeroMedia /
// PublicEditions).
builder.Services.AddMemoryCache();

// SIMF_Api__AllowSelfSignedCertificate=true installs
// DangerousAcceptAnyServerCertificateValidator, which accepts ANY certificate
// from ANY host - not just a self-signed one. It is only safe against a loopback
// BaseUrl, and SimfApiTransport.Resolve refuses to boot on any other pairing
// outside Development. Default false => ordinary TLS validation.
//
// The setting is read ONCE, here, and both the validation and the handler come
// out of the resulting transport - so this host cannot install a bypass that
// the validation did not see.

// The typed client shares one validated API transport - server-to-server.
var apiTransport = SimfApiTransport.Resolve(
    builder.Configuration["Api:BaseUrl"],
    builder.Environment.IsDevelopment(),
    builder.Configuration.GetValue<bool>("Api:AllowSelfSignedCertificate"));

// The typed client for the SIMF anonymous public-read endpoints.
// Anonymous, so no bearer token; BaseAddress only — the public endpoints do
// not require an X-App-Key header in this build.
builder.Services.AddHttpClient<SimfPublicClient>(client => client.BaseAddress = apiTransport.BaseAddress)
    .ConfigurePrimaryHttpMessageHandler(apiTransport.CreatePrimaryHandler);

// Resolves the forum event dates from the public OrganizationProfile (cached) and
// formats the shared bilingual range for the marketing pages, so the date is
// driven by CP config instead of a hardcoded resx literal.
builder.Services.AddScoped<ForumDates>();

// Resolves the hero-section background video from the public OrganizationProfile
// (cached) so the landing hero plays the CP-configured video instead of the
// bundled hero-video.mp4 asset.
builder.Services.AddScoped<HeroMedia>();

// Resolves the public past editions (cached) shared by the /archive page cards and
// the top-nav Archive dropdown, so the dropdown lists the real editions and can
// never diverge from the page.
builder.Services.AddScoped<PublicEditions>();

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
        // The hero background video may be a YouTube embed (privacy host).
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

app.UseAntiforgery();

app.MapStaticAssets();
app.MapSiteContentEndpoints();
app.MapChatEndpoints();
app.MapCultureEndpoint();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
