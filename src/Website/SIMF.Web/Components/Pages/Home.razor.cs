using Microsoft.AspNetCore.Components;
using SIMF.ApiClient;

namespace SIMF.Web.Components.Pages;

// Website — post-sign-in landing placeholder (/account). Confirms the sign-in
// and offers sign-out; an unauthenticated visitor is redirected to /login.
// Markup lives in Home.razor.
public partial class Home
{
    [Inject] private SimfAuthClient Api { get; set; } = default!;
    [Inject] private SimfAuthSession Session { get; set; } = default!;
    [Inject] private NavigationManager Nav { get; set; } = default!;

    private bool _signingOut;

    protected override void OnInitialized()
    {
        if (!Session.IsSignedIn)
        {
            Nav.NavigateTo("/login");
        }
    }

    private async Task SignOutAsync()
    {
        if (_signingOut)
        {
            return;
        }

        _signingOut = true;
        try
        {
            var accessToken = Session.Tokens?.AccessToken;
            if (accessToken is not null)
            {
                await Api.SignOutAsync(accessToken);
            }
            Session.Clear();
            Nav.NavigateTo("/login");
        }
        finally
        {
            _signingOut = false;
        }
    }
}
