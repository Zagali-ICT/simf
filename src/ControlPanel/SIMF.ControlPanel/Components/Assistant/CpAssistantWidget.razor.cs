using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using SIMF.Common;
using SIMF.Contracts.Ai;

namespace SIMF.ControlPanel.Components.Assistant;

/// <summary>Code-behind for the Control Panel help assistant. Posts the operator's
/// question to the CP proxy (which builds the grounding directory server-side and
/// calls the <c>cp-assistant</c> prompt) and shows the answer. Visible only to a
/// user holding <see cref="PermissionCatalog.Assistant.Use"/> (or the Administrator
/// wildcard); the shell renders it once, so every signed-in page gets it.</summary>
public partial class CpAssistantWidget
{
    [Inject] private IStringLocalizer<Strings> L { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthState { get; set; } = default!;

    private sealed record ChatLine(bool IsUser, string Text);

    private readonly List<ChatLine> _messages = new();
    private string _input = string.Empty;
    private bool _open;
    private bool _busy;
    private bool _canUse;
    private bool _scrollPending;
    private bool _focusPending;
    private ElementReference _logEl;
    private ElementReference _inputEl;
    private ElementReference _launcherEl;

    protected override async Task OnInitializedAsync()
    {
        var auth = await AuthState.GetAuthenticationStateAsync();
        var permissions = auth.User.FindAll(PermissionCatalog.ClaimType)
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.Ordinal);
        _canUse = permissions.Contains(PermissionCatalog.Wildcard)
            || permissions.Contains(PermissionCatalog.Assistant.Use);

        if (_canUse)
        {
            _messages.Add(new ChatLine(false, L["Assistant.Greeting"].Value));
        }
    }

    private void Toggle()
    {
        _open = !_open;
        // Move focus where the user expects it (see OnAfterRenderAsync): into the
        // input when opening — which also makes the panel's Esc handler reachable —
        // and back to the launcher when closing.
        _focusPending = true;
        if (_open)
        {
            _scrollPending = true;
        }
    }

    private void Close()
    {
        if (_open)
        {
            _open = false;
            _focusPending = true;
        }
    }

    private void OnKeyDown(KeyboardEventArgs args)
    {
        if (string.Equals(args.Key, "Escape", StringComparison.Ordinal))
        {
            Close();
        }
    }

    private async Task SendAsync()
    {
        var question = _input.Trim();
        if (_busy || question.Length == 0)
        {
            return;
        }

        _messages.Add(new ChatLine(true, question));
        _input = string.Empty;
        _busy = true;
        _scrollPending = true;

        try
        {
            var envelope = await JS.InvokeAsync<ApiResult<AiCallResult>>(
                "simfAccount.postJson", "/account/api/admin/ai/assistant",
                new { Question = question });

            string answer;
            if (envelope?.Success == true
                && !string.IsNullOrWhiteSpace(envelope.Data?.OutputText))
            {
                answer = envelope.Data!.OutputText!;
            }
            else
            {
                var error = envelope?.Error?.MessageForCurrentCulture();
                answer = string.IsNullOrWhiteSpace(error) ? L["Assistant.Error"].Value : error;
            }

            _messages.Add(new ChatLine(false, answer));
        }
        catch
        {
            // A transport/JS failure surfaces as the same bilingual error bubble.
            _messages.Add(new ChatLine(false, L["Assistant.Error"].Value));
        }
        finally
        {
            _busy = false;
            _scrollPending = true;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusPending)
        {
            _focusPending = false;
            // Open → focus the input; closed → return focus to the launcher so a
            // keyboard user is never stranded.
            var target = _open ? _inputEl : _launcherEl;
            try
            {
                await target.FocusAsync();
            }
            catch
            {
                // Best-effort — the element may be gone before the render flushed.
            }
        }
        if (_scrollPending && _open)
        {
            _scrollPending = false;
            try
            {
                await JS.InvokeVoidAsync("simfAccount.scrollToEnd", _logEl);
            }
            catch (JSException)
            {
                // Best-effort — the panel may have closed before the render flushed.
            }
        }
    }
}
