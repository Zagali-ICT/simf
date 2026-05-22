using SIMF.ApiClient;
using SIMF.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Razor components — server-side rendered, with interactive Server islands for
// the pages that need them (the authentication pages). Public content pages
// stay server-side rendered.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// The signed-in session is held per Blazor circuit.
builder.Services.AddScoped<SimfAuthSession>();

// The typed client for the SIMF Login API. The call is server-to-server, so
// the access token never reaches the browser.
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

// Serve the imported static marketing site (wwwroot/index.html) at "/".
// UseStaticFiles runs as middleware, so the default-document rewrite is served
// regardless of endpoint-routing order.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
