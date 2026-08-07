// Signing in to the Control Panel the way a person does: the real form, the
// real second factor. Not a token injected into storage — a shortcut there
// would skip the exact redirect and session behaviour the auth-gate scenarios
// depend on, and would quietly keep passing after the sign-in flow broke.
using Microsoft.Playwright;

namespace SIMF.E2E.Tests;

public static class CpSignIn
{
    /// <param name="email">Defaults to the sweep's super-admin. The restricted
    /// fixture (QaStack.RestrictedAdminEmail) shares its password and TOTP secret
    /// by construction, so only the email differs.</param>
    public static async Task SignInAsync(IPage page, string? email = null)
    {
        await page.GotoAsync(QaStack.ControlPanel + "/login",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

        await page.FillAsync("input[type=email], input[name=Email]", email ?? QaStack.AdminEmail!);
        await page.FillAsync("input[type=password]", QaStack.AdminPassword!);
        await page.ClickAsync("button[type=submit]");

        // Blazor Server does not always perform a full document navigation
        // between these steps, so `WaitForURLAsync` (which waits for a Load
        // event) can time out on a sign-in that in fact succeeded. Poll the URL
        // instead — it is what the redirect actually changes.
        await WaitUntilAwayFromAsync(page, "/login", exact: true);

        // TOTP step, conditional: a deployment with 2FA disabled lands straight
        // on the dashboard.
        if (page.Url.Contains("/login/totp", StringComparison.OrdinalIgnoreCase))
        {
            var field = page.Locator("input.simf-code__input, input[inputmode=numeric]");
            await field.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });

            // Submit, and if the code is refused, WAIT FOR THE NEXT 30-SECOND
            // WINDOW AND TRY ONCE MORE.
            //
            // Two fixtures now sign in per run (the super-admin sweep account and
            // the restricted deny-path account), they share a TOTP secret by
            // construction, and xUnit starts their classes in parallel. When the
            // two submissions land in the same window one can be refused, and the
            // restricted fixture then records a setup failure and every deny-path
            // test SKIPS — silently losing the only coverage of the permission
            // system's deny half. That is worse than a red test, and it was
            // observed: one full run reported 6 skips where the next reported 2.
            // A single retry on a fresh window removes the whole class; a code is
            // valid for one window, so waiting past the boundary guarantees a new
            // one rather than resubmitting the same digits.
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                if (attempt > 1)
                {
                    await WaitForNextTotpWindowAsync();
                    await field.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
                }

                // Typed key by key, not FillAsync. The field is a Blazor `InputText`
                // on an interactive circuit; a single value-set + synthetic change
                // can land before the circuit is listening, leaving the bound model
                // empty while the box looks filled.
                await field.ClearAsync();
                await field.PressSequentiallyAsync(
                    Totp.Now(QaStack.AdminTotpSecret!), new LocatorPressSequentiallyOptions { Delay = 30 });
                await field.BlurAsync();

                await page.ClickAsync("button[type=submit]");
                await WaitUntilAwayFromAsync(page, "/login", exact: false);

                if (!page.Url.Contains("/login/totp", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }
        }

        if (page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase))
        {
            // Report what the PAGE says, not what we assume. A rejected code and
            // an unbound field fail identically from the outside, and a message
            // that only blames the credentials sends the reader to the wrong
            // place — it did exactly that for two runs.
            var onScreen = await ReadFirstTextAsync(
                page, ".simf-alert--error", ".simf-field__error", "[role=alert]");
            throw new InvalidOperationException(
                $"Control Panel sign-in did not complete; still on {page.Url}."
                + (onScreen is null
                    ? " The page shows no error — the submitted value may not have"
                      + " reached the model, rather than having been rejected."
                    : $" The page reports: \"{onScreen}\"")
                + " Check SIMF_QA_ADMIN_*, that the account is Approved, and that"
                + " its seeded AuthenticatorKey matches SIMF_QA_ADMIN_TOTP_SECRET.");
        }
    }

    /// <summary>Blocks until the current 30-second TOTP window has rolled over,
    /// so the next generated code is guaranteed to differ from the one just
    /// refused. Sleeps to the boundary plus a small margin rather than a flat
    /// delay, so the wait is as short as it can be (never more than ~31s).</summary>
    private static async Task WaitForNextTotpWindowAsync()
    {
        const int WindowSeconds = 30;
        var secondsIntoWindow =
            DateTimeOffset.UtcNow.ToUnixTimeSeconds() % WindowSeconds;
        var untilBoundary = WindowSeconds - (int)secondsIntoWindow;
        await Task.Delay(TimeSpan.FromSeconds(untilBoundary + 1));
    }

    /// <summary>The first non-empty text among the given selectors, or null.</summary>
    private static async Task<string?> ReadFirstTextAsync(IPage page, params string[] selectors)
    {
        foreach (var selector in selectors)
        {
            var locator = page.Locator(selector).First;
            if (await locator.CountAsync() == 0)
            {
                continue;
            }
            var text = (await locator.InnerTextAsync()).Trim();
            if (text.Length > 0)
            {
                return text.ReplaceLineEndings(" ");
            }
        }
        return null;
    }

    /// <summary>Polls until the path leaves <paramref name="path"/>. Cheaper and
    /// more reliable here than waiting on a navigation event, which an
    /// interactive Blazor circuit may never raise.
    ///
    /// <para><paramref name="exact"/> is the whole point. After the credential
    /// step we are waiting to leave <c>/login</c> itself, and <c>/login/totp</c>
    /// counts as having left. After the TOTP step we are waiting to leave the
    /// <c>/login</c> area entirely, and <c>/login/totp</c> does NOT count. One
    /// helper that treated both the same returned instantly from the second
    /// call and reported sign-in as stuck on the page it had just submitted —
    /// 97 identical failures with a message that blamed the credentials.</para></summary>
    private static async Task WaitUntilAwayFromAsync(IPage page, string path, bool exact)
    {
        bool StillThere()
        {
            var current = new Uri(page.Url).AbsolutePath.TrimEnd('/');
            return exact
                ? current.Equals(path, StringComparison.OrdinalIgnoreCase)
                : current.StartsWith(path, StringComparison.OrdinalIgnoreCase);
        }

        for (var waited = 0; waited < 20_000 && StillThere(); waited += 250)
        {
            await Task.Delay(250);
        }
    }
}
