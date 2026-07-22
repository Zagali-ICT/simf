using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class DelegatesPage
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // Single-delegate walk-in: the success modal shows the minted badge/QR, then
    // resets the form via a fresh @key so the desk can register the next person.
    // The bulk badge generator moved into its own reusable component
    // (BulkBadgeGenerator), rendered below the single form.
    private AdminWalkInRegistrationResponse? _lastResponse;
    private Guid _formKey = Guid.NewGuid();

    private void OnSingleSuccess(AdminWalkInRegistrationResponse response) => _lastResponse = response;

    private void OnSuccessModalClose() => _lastResponse = null;

    private async Task OnPrintAsync() => await JS.InvokeVoidAsync("window.print");

    private void OnRegisterAnother()
    {
        _lastResponse = null;
        _formKey = Guid.NewGuid();
        StateHasChanged();
    }
}
