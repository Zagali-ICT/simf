using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using SIMF.Contracts.Authentication;

namespace SIMF.ControlPanel.Components.Pages.Admin;

public partial class VisitorsAddEdit
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;

    // The walk-in wizard reports an AdminCreateUserResponse; the base OnSuccess
    // is typed to the grid-row summary, so map the new account onto a summary
    // shell. The host only uses the callback to close the shell + reload the
    // grid (which re-reads the row from the server), so the unfilled summary
    // fields are never displayed.
    private Task OnAddSucceededAsync(AdminCreateUserResponse created) =>
        OnSuccess.InvokeAsync(new AdminUserSummary(
            created.UserId,
            created.Email,
            string.Empty,
            string.Empty,
            false,
            false,
            DateTimeOffset.UtcNow));

    // EditAccountForm raises a payload-less OnSaved; bubble the row we already
    // hold so the host closes the shell + reloads.
    private Task OnEditSavedAsync() => OnSuccess.InvokeAsync(Initial!);
}
