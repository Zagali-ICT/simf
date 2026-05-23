using Microsoft.AspNetCore.Authentication.Cookies;
using SIMF.ApiClient;
using SIMF.ControlPanel;
using SIMF.ControlPanel.Components;
using SIMF.ControlPanel.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Razor components with interactive Server rendering.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// The one-time sign-in hand-off between the verification page and the cookie.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<SignInTicketStore>();

// The signed-in second-factor flow state is held per Blazor circuit.
builder.Services.AddScoped<SimfAuthSession>();

// HttpContext access — the profile page captures the access token from the
// cookie auth result during the initial (prerendered) render, then holds it
// in the circuit for the interactive callbacks that follow.
builder.Services.AddHttpContextAccessor();

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
});

builder.Services.AddHttpClient<SimfAccountClient>(client =>
{
    var baseUrl = builder.Configuration["Api:BaseUrl"]!;
    client.BaseAddress = new Uri(baseUrl);
});

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
