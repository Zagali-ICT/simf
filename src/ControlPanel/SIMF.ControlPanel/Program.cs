using Microsoft.AspNetCore.Authentication.Cookies;
using SIMF.ApiClient;
using SIMF.ControlPanel;
using SIMF.ControlPanel.Components;
using SIMF.ControlPanel.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Razor components with interactive Server rendering.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Cookie authentication. The cookie carries the signed-in user's identity and
// (encrypted) the SIMF API tokens. An unauthenticated request to a protected
// page is sent to the sign-in page.
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
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();

// The one-time sign-in hand-off between the verification page and the cookie.
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<SignInTicketStore>();

// The signed-in second-factor flow state is held per Blazor circuit.
builder.Services.AddScoped<SimfAuthSession>();

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapAuthEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
