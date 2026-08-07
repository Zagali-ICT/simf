using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class CreateOtherForm
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private WalkInRegistrationForm? _form;
    private AdminWalkInRegistrationResponse? _lastResponse;
    private Guid _formKey = Guid.NewGuid();

    [Parameter] public EventCallback<AdminCreateUserResponse> OnSuccess { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }
    [Parameter] public string? CancelLabel { get; set; }

    private void OnRegistrationSuccessAsync(AdminWalkInRegistrationResponse response)
    {
        _lastResponse = response;
    }

    private async Task OnSuccessModalClose()
    {
        var response = _lastResponse;
        _lastResponse = null;
        if (response is not null)
        {
            await OnSuccess.InvokeAsync(new AdminCreateUserResponse(
                response.UserId, response.Email, 0));
        }
    }

    private async Task OnPrintAsync() =>
        await JS.InvokeVoidAsync("window.print");

    private async Task OnRegisterAnother()
    {
        var previous = _lastResponse;
        _lastResponse = null;
        if (previous is not null)
        {
            await OnSuccess.InvokeAsync(new AdminCreateUserResponse(
                previous.UserId, previous.Email, 0));
        }
        _formKey = Guid.NewGuid();
        StateHasChanged();
    }

    private Task OnCancelInternal() => OnCancel.InvokeAsync();
}
