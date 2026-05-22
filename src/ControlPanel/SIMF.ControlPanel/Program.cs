using SIMF.ApiClient;
using SIMF.ControlPanel.Components;

var builder = WebApplication.CreateBuilder(args);

// Razor components with interactive Server rendering.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// The signed-in session is held per Blazor circuit.
builder.Services.AddScoped<SimfAuthSession>();

// The typed client for the SIMF Login API. The call is server-to-server
// (Blazor Server), so the access token never reaches the browser and there
// is no cross-origin concern.
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

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
