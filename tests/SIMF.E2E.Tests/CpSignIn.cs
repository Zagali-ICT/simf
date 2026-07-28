// Signing in to the Control Panel the way a person does: the real form, the
// real second factor. Not a token injected into storage — a shortcut there
// would skip the exact redirect and session behaviour the auth-gate scenarios
// depend on, and would quietly keep passing after the sign-in flow broke.
using Microsoft.Playwright;

namespace SIMF.E2E.Tests;

public static class CpSignIn
{
    public static async Task SignInAsync(IPage page)
    {
        await page.GotoAsync(QaStack.ControlPanel + "/login",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await page.FillAsync("input[type=email], input[name=Email]", QaStack.AdminEmail!);
        await page.FillAsync("input[type=password]", QaStack.AdminPassword!);
        await page.ClickAsync("button[type=submit]");

        // TOTP step. The CP routes here after valid credentials; a deployment
        // with 2FA disabled lands straight on the dashboard, so this is
        // conditional rather than assumed.
        await page.WaitForURLAsync(
            url => !url.EndsWith("/login", StringComparison.OrdinalIgnoreCase),
            new PageWaitForURLOptions { Timeout = 15_000 });

        if (page.Url.Contains("/login/totp", StringComparison.OrdinalIgnoreCase))
        {
            await page.FillAsync(
                "input[inputmode=numeric], input[name=Code]",
                Totp.Now(QaStack.AdminTotpSecret!));
            await page.ClickAsync("button[type=submit]");
            await page.WaitForURLAsync(
                url => !url.Contains("/login", StringComparison.OrdinalIgnoreCase),
                new PageWaitForURLOptions { Timeout = 15_000 });
        }

        if (page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Control Panel sign-in did not complete; still on {page.Url}. "
                + "Check SIMF_QA_ADMIN_* and that the account is Approved with a "
                + "paired authenticator.");
        }
    }
}
