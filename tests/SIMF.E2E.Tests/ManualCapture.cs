// The screenshot pass behind the Control Panel operations manual.
//
// It lives in this project rather than in a tool of its own because everything
// it needs is already here: the Playwright package reference, a provisioned
// Chromium, the real TOTP generator and the sign-in helper. A separate tool
// would mean a second package reference and a second browser download for one
// purpose.
//
// It is NOT a test of the product. It asserts only that a capture actually
// happened, so that a silently empty run fails instead of producing a manual
// with holes in it. Every run also records the console errors, failed requests
// and horizontal overflow for each page, which is the evidence the manual's own
// verification gate asks for.
//
// Driven by environment, so one runner serves both language volumes:
//   SIMF_MANUAL_CP_URL        Control Panel base URL
//   SIMF_MANUAL_EMAIL         the account to sign in as
//   SIMF_MANUAL_PASSWORD      its password
//   SIMF_MANUAL_TOTP_SECRET   its base32 authenticator secret (blank = enrol)
//   SIMF_MANUAL_OUT           directory the PNGs are written to
//   SIMF_MANUAL_LANG          "en" or "ar" - switches the CP before capturing
//   SIMF_MANUAL_ROUTES        text file, one tab-separated "slug<TAB>route" per line
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace SIMF.E2E.Tests;

public sealed class ManualCapture : IAsyncLifetime
{
    private const int ViewportWidth = 1920;
    private const int ViewportHeight = 950;

    private static string Cp => Env("SIMF_MANUAL_CP_URL", "http://localhost:5158");
    private static string Email => Env("SIMF_MANUAL_EMAIL", "superadmin@simrsnf.com");
    private static string Password => Env("SIMF_MANUAL_PASSWORD", "");

    /// <summary>What a forced password change sets the account to. The
    /// seeded password is a bootstrap credential the server insists is
    /// replaced, so every run after the first signs in with THIS value.</summary>
    private static string NewPassword => Env("SIMF_MANUAL_NEW_PASSWORD", "");
    private static string TotpSecret => Env("SIMF_MANUAL_TOTP_SECRET", "");
    private static string OutDir => Env("SIMF_MANUAL_OUT", "");
    private static string Lang => Env("SIMF_MANUAL_LANG", "en");
    private static string RoutesFile => Env("SIMF_MANUAL_ROUTES", "");

    private static string Env(string key, string fallback) =>
        Environment.GetEnvironmentVariable(key) is { Length: > 0 } value ? value : fallback;

    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private IBrowserContext _context = null!;
    private IPage _page = null!;
    private readonly List<string> _consoleErrors = [];
    private readonly List<string> _failedRequests = [];
    private readonly List<object> _report = [];

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        // Drive the browser already installed on the machine rather than a
        // Playwright-managed build. The provisioned browsers here are build 1208
        // while this project's Playwright 1.49 asks for 1148, and a channel
        // launch sidesteps that mismatch without downloading a second Chromium
        // for one screenshot pass. SIMF_MANUAL_BROWSER_CHANNEL overrides it
        // ("msedge"), and an empty value falls back to the bundled build.
        var channel = Env("SIMF_MANUAL_BROWSER_CHANNEL", "chrome");
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = channel.Length > 0 ? channel : null,
        });
        _context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = ViewportWidth, Height = ViewportHeight },
            Locale = Lang == "ar" ? "ar-SA" : "en-GB",
        });
        _page = await _context.NewPageAsync();
        _page.Console += (_, message) =>
        {
            if (message.Type == "error") { _consoleErrors.Add(message.Text); }
        };
        _page.RequestFailed += (_, request) =>
            _failedRequests.Add(request.Method + " " + request.Url + " - " + request.Failure);
    }

    public async Task DisposeAsync()
    {
        await _context.CloseAsync();
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    // ---------------------------------------------------------------- shots --

    private async Task ShotAsync(string slug, string state)
    {
        Directory.CreateDirectory(OutDir);
        var suffix = Lang == "ar" ? "-ar" : string.Empty;
        var path = Path.Combine(OutDir, "cp-" + slug + "-" + state + suffix + ".png");

        // A Blazor Server circuit paints after the document settles, so a shot
        // taken on load alone catches an empty grid. Wait for the network to go
        // quiet, then give the render a beat.
        try
        {
            await _page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                new PageWaitForLoadStateOptions { Timeout = 8_000 });
        }
        catch (TimeoutException) { /* a page holding a poll open never idles */ }
        await Task.Delay(700);
        await _page.ScreenshotAsync(new PageScreenshotOptions { Path = path, FullPage = false });

        var overflows = await _page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth > document.documentElement.clientWidth");

        _report.Add(new
        {
            slug,
            state,
            lang = Lang,
            url = _page.Url,
            file = Path.GetFileName(path),
            consoleErrors = _consoleErrors.ToArray(),
            failedRequests = _failedRequests.ToArray(),
            horizontalOverflow = overflows,
        });
        _consoleErrors.Clear();
        _failedRequests.Clear();
    }

    private void WriteReport(string name)
    {
        Directory.CreateDirectory(OutDir);
        var path = Path.Combine(OutDir, "capture-report-" + name + "-" + Lang + ".json");
        File.WriteAllText(path,
            JsonSerializer.Serialize(_report, new JsonSerializerOptions { WriteIndented = true }));
    }

    // ------------------------------------------------------------ sign-in ---

    /// <summary>Signs in, handling the two gates a freshly seeded account meets
    /// that the E2E sign-in helper does not: a forced password change, and the
    /// mandatory Control Panel 2FA enrolment. Both are one-shot screens - they
    /// render once per account, ever - so this is the only chance to photograph
    /// them, which is why the capture happens here rather than in a later pass.</summary>
    private async Task<string?> SignInCapturingFirstRunAsync(bool capture)
    {
        var pairedSecret = TotpSecret.Length > 0 ? TotpSecret : null;
        var password = Password;

        await _page.GotoAsync(Cp + "/login",
            new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
        if (capture) { await ShotAsync("login", "empty"); }

        // The gates a fresh account meets do not arrive in a fixed order, and a
        // forced password change arrives as a MODAL OVER the login page rather
        // than as its own route - so the flow is driven as a state machine over
        // what is actually on screen, not as a fixed sequence of steps. Filling
        // "every password box on the page" here would fill the login form behind
        // the modal and then click the Sign in button underneath it.
        for (var step = 0; step < 8; step++)
        {
            if (!_page.Url.Contains("/login", StringComparison.OrdinalIgnoreCase))
            {
                return pairedSecret;
            }

            var modal = _page.Locator(".simf-modal").First;
            var modalOpen = await modal.CountAsync() > 0 && await modal.IsVisibleAsync();

            if (modalOpen)
            {
                if (capture) { await ShotAsync("login-password-change", "empty"); }

                var replacement = NewPassword.Length > 0 ? NewPassword : password;
                var boxes = modal.Locator("input[type=password]");
                var count = await boxes.CountAsync();
                for (var i = 0; i < count; i++)
                {
                    await boxes.Nth(i).FillAsync(replacement);
                }
                if (capture) { await ShotAsync("login-password-change", "filled"); }

                await modal.Locator("button[type=submit]").First.ClickAsync();
                await SettleAsync();
                password = replacement;
                continue;
            }

            if (_page.Url.Contains("/login/enrol-2fa", StringComparison.OrdinalIgnoreCase))
            {
                if (capture) { await ShotAsync("login-enrol-2fa", "qr"); }
                pairedSecret = await ReadPairingSecretAsync()
                    ?? throw new InvalidOperationException(
                        "The enrolment page did not expose a manual-entry secret to pair with.");
                await SubmitCodeAsync(pairedSecret);
                if (capture) { await ShotAsync("login-enrol-2fa", "recovery-codes"); }
                await ClickFirstAsync("button[type=submit]", "button.simf-button");
                await SettleAsync();
                continue;
            }

            if (_page.Url.Contains("/login/totp", StringComparison.OrdinalIgnoreCase))
            {
                if (capture) { await ShotAsync("login-totp", "empty"); }
                await SubmitCodeAsync(pairedSecret
                    ?? throw new InvalidOperationException("A TOTP step needs a secret."));
                continue;
            }

            if (_page.Url.Contains("/login/recovery", StringComparison.OrdinalIgnoreCase))
            {
                if (capture) { await ShotAsync("login-recovery", "empty"); }
                throw new InvalidOperationException(
                    "Landed on the recovery-code step, which this run has no code for.");
            }

            // Plain credential form.
            await FillSettledAsync("input[type=email], input[name=Email]", Email);
            await FillSettledAsync("input[type=password]", password);
            if (capture && step == 0) { await ShotAsync("login", "filled"); }
            await _page.ClickAsync("button[type=submit]");
            await SettleAsync();
        }

        var shown = await FirstTextAsync(".simf-alert--error", ".simf-field__error", "[role=alert]");
        throw new InvalidOperationException(
            "Sign-in did not complete; still on " + _page.Url
            + ". Page says: " + (shown ?? "(nothing)"));
    }

    /// <summary>Fills a field only once the Blazor circuit has stopped
    /// re-rendering it. A Blazor Server page renders statically first and then
    /// swaps in the interactive circuit; filling across that swap detaches the
    /// node mid-keystroke, which Playwright reports as "element was detached
    /// from the DOM" after retrying to its own timeout.</summary>
    private async Task FillSettledAsync(string selector, string value)
    {
        var field = _page.Locator(selector).First;
        await field.WaitForAsync(new LocatorWaitForOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 30_000,
        });
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                await field.FillAsync(value, new LocatorFillOptions { Timeout = 10_000 });
                return;
            }
            catch (PlaywrightException) when (attempt < 3)
            {
                await Task.Delay(1_000);
            }
        }
    }

    private async Task SubmitCodeAsync(string secret)
    {
        var field = _page.Locator("input.simf-code__input, input[inputmode=numeric]").First;
        await field.WaitForAsync(new LocatorWaitForOptions { Timeout = 15_000 });
        for (var attempt = 1; attempt <= 2; attempt++)
        {
            if (attempt > 1) { await WaitForNextWindowAsync(); }
            // Fill, then re-type only if the bound model did not take it. The
            // key-by-key path is slower and, on the enrolment screen, waits on
            // an element the circuit re-renders underneath it - which times out
            // on a field that is present and perfectly fillable.
            var code = Totp.Now(secret);
            await field.FillAsync(code, new LocatorFillOptions { Timeout = 15_000 });
            if (await field.InputValueAsync() != code)
            {
                await field.PressSequentiallyAsync(code,
                    new LocatorPressSequentiallyOptions { Delay = 30, Timeout = 15_000 });
            }
            await field.BlurAsync();
            await _page.ClickAsync("button[type=submit]");
            await SettleAsync();
            var stillOnCode = _page.Url.Contains("/totp", StringComparison.OrdinalIgnoreCase)
                || _page.Url.Contains("/enrol-2fa", StringComparison.OrdinalIgnoreCase);
            if (!stillOnCode) { return; }
        }

        // Both attempts were refused. Returning here would let the caller shoot
        // the still-showing code screen and file it under the name of the screen
        // that never appeared.
        throw new InvalidOperationException(
            "The verification code was refused twice; still on " + _page.Url
            + ". The secret being used does not match the account's authenticator.");
    }

    /// <summary>The base32 the enrolment page prints for manual entry. Read from
    /// the page rather than assumed, because the server mints it.</summary>
    private async Task<string?> ReadPairingSecretAsync()
    {
        // Read the element that holds it rather than scanning the page. A
        // whole-body regex for base32 groups can bleed into neighbouring
        // uppercase text, and only matches multiples of four - so a shorter
        // secret would be silently truncated and every generated code refused,
        // with nothing to say why.
        var box = _page.Locator("code.simf-totp-secret").First;
        if (await box.CountAsync() == 0) { return null; }
        var secret = Regex.Replace(await box.InnerTextAsync(), "\\s+", string.Empty).ToUpperInvariant();
        return Regex.IsMatch(secret, "^[A-Z2-7]{16,}$") ? secret : null;
    }

    private static async Task WaitForNextWindowAsync()
    {
        var into = DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 30;
        await Task.Delay(TimeSpan.FromSeconds(30 - into + 1));
    }

    private async Task SettleAsync()
    {
        for (var waited = 0; waited < 15_000; waited += 500)
        {
            await Task.Delay(500);
            try
            {
                await _page.WaitForLoadStateAsync(LoadState.NetworkIdle,
                    new PageWaitForLoadStateOptions { Timeout = 1_000 });
                return;
            }
            catch (TimeoutException) { }
        }
    }

    private async Task ClickFirstAsync(params string[] selectors)
    {
        foreach (var selector in selectors)
        {
            var locator = _page.Locator(selector).First;
            if (await locator.CountAsync() > 0 && await locator.IsVisibleAsync())
            {
                await locator.ClickAsync();
                return;
            }
        }
    }

    private async Task<string?> FirstTextAsync(params string[] selectors)
    {
        foreach (var selector in selectors)
        {
            var locator = _page.Locator(selector).First;
            if (await locator.CountAsync() == 0) { continue; }
            var text = (await locator.InnerTextAsync()).Trim();
            if (text.Length > 0) { return text.ReplaceLineEndings(" "); }
        }
        return null;
    }

    // --------------------------------------------------------------- runs ---

    /// <summary>First sign-in on a freshly seeded account: the forced password
    /// change, the 2FA enrolment QR and the recovery codes. Run once per account,
    /// because those screens never render again.</summary>
    [SkippableFact]
    public async Task Capture_first_sign_in()
    {
        Skip.If(OutDir.Length == 0, "SIMF_MANUAL_OUT is not set.");
        var paired = await SignInCapturingFirstRunAsync(capture: true);
        await _page.GotoAsync(Cp + "/", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await ShotAsync("dashboard", "default");
        WriteReport("first-sign-in");
        // The paired secret is written beside the throwaway environment's other
        // credentials, NEVER into the screenshot directory: that directory is
        // committed, and a secret - even a local, throwaway one - does not
        // belong in a repository.
        var secretSink = Env("SIMF_MANUAL_SECRET_SINK", "");
        if (paired is not null && secretSink.Length > 0)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(secretSink)!);
            File.WriteAllText(secretSink, paired);
        }
        Assert.NotEmpty(_report);
    }

    /// <summary>The account-creation flows, step by step.</summary>
    /// <remarks>The route sweep photographs each page as it opens, which shows
    /// a list but never shows the act the manual is actually about. This walks
    /// the create forms: the empty form, a rejected submission, a filled one,
    /// and the result - because creating a user is a sequence, not a page.
    /// <para>Every step is guarded on its own. A selector that moves should
    /// cost the manual one figure, not the whole chapter.</para></remarks>
    [SkippableFact]
    public async Task Capture_account_flows()
    {
        Skip.If(OutDir.Length == 0, "SIMF_MANUAL_OUT is not set.");
        await SignInCapturingFirstRunAsync(capture: false);
        if (Lang == "ar")
        {
            await _page.GotoAsync(Cp + "/culture?culture=ar&redirectUri=%2F",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await SettleAsync();
        }

        var add = Env("SIMF_MANUAL_ADD_LABEL", "Add");
        var addVip = Env("SIMF_MANUAL_ADD_VIP_LABEL", "New VIP");
        var submit = Env("SIMF_MANUAL_SUBMIT_LABEL", "Create user");
        var stamp = Env("SIMF_MANUAL_STAMP", "manual");

        await StepAsync("admins-add", async () =>
        {
            await _page.GotoAsync(Cp + "/admin/admins",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await SettleAsync();
            await ClickToolbarAsync(add);
            await ShotAsync("admin-admins", "add-empty");

            // A rejected submission, so the manual can show what a refusal looks
            // like rather than only describing one.
            await FillSettledAsync("input[type=email]", "not-an-email");
            var names = _page.Locator("input[type=text]:visible");
            if (await names.CountAsync() > 0) { await names.First.FillAsync("X"); }
            await ClickButtonAsync(submit);
            await Task.Delay(1_500);
            await ShotAsync("admin-admins", "add-validation");

            await FillSettledAsync("input[type=email]", $"naval.ops.{stamp}@simf.test");
            if (await names.CountAsync() > 0)
            {
                await names.First.FillAsync("Naval Operations Lead");
            }
            await ShotAsync("admin-admins", "add-filled");

            await ClickButtonAsync(submit);
            await Task.Delay(3_000);
            await ShotAsync("admin-admins", "add-result");
        });

        await StepAsync("admins-after", async () =>
        {
            await _page.GotoAsync(Cp + "/admin/admins",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await SettleAsync();
            await ShotAsync("admin-admins", "after-create");
        });

        // The visitor wizard is far taller than one screen, so it is captured in
        // three overlapping views rather than as one unreadable full-page image.
        await StepAsync("visitors-add", async () =>
        {
            await _page.GotoAsync(Cp + "/admin/visitors",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await SettleAsync();
            await ClickToolbarAsync(add);
            await Task.Delay(1_500);
            await ShotAsync("admin-visitors", "add-top");
            await _page.Mouse.WheelAsync(0, 700);
            await Task.Delay(900);
            await ShotAsync("admin-visitors", "add-middle");
            await _page.Mouse.WheelAsync(0, 900);
            await Task.Delay(900);
            await ShotAsync("admin-visitors", "add-lower");
            await _page.Mouse.WheelAsync(0, 1200);
            await Task.Delay(900);
            await ShotAsync("admin-visitors", "add-bottom");
        });

        await StepAsync("others-add", async () =>
        {
            await _page.GotoAsync(Cp + "/admin/others",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await SettleAsync();
            await ClickToolbarAsync(add);
            await Task.Delay(1_500);
            await ShotAsync("admin-others", "add-top");
        });

        await StepAsync("vip-add", async () =>
        {
            await _page.GotoAsync(Cp + "/admin/visitors/vip",
                new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await SettleAsync();
            await ClickToolbarAsync(addVip);
            await Task.Delay(1_500);
            await ShotAsync("admin-visitors-vip", "add-top");
        });

        WriteReport("account-flows");
        Assert.NotEmpty(_report);
    }

    /// <summary>Whether the browser is still on the route it was sent to,
    /// ignoring a trailing slash and letter case.</summary>
    private static bool SamePath(string landed, string requested)
    {
        var a = landed.TrimEnd('/');
        var b = requested.Split('?')[0].TrimEnd('/');
        return a.Equals(b, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Throws unless the Control Panel shell is actually on screen.
    /// "Not on /login" is not the same as "signed in": an account awaiting
    /// approval is redirected to /auth/pending from every page, which would
    /// satisfy a URL-only check and fill the whole manual with 114 identical
    /// pictures of the pending banner - a run that passes every assertion and
    /// produces a worthless book.</summary>
    private async Task AssertSignedInAsync()
    {
        var shell = _page.Locator("nav a[href='/admin/admins'], .simf-shell__nav, [data-testid=cp-shell]").First;
        if (await shell.CountAsync() > 0)
        {
            return;
        }
        var signOut = _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Sign out" });
        var signOutAr = _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "تسجيل الخروج" });
        if (await signOut.CountAsync() > 0 || await signOutAr.CountAsync() > 0)
        {
            return;
        }
        throw new InvalidOperationException(
            "Sign-in left the browser on " + _page.Url + ", which is not the Control Panel shell. "
            + "An account that is not Approved lands on the pending page from every route.");
    }

    /// <summary>Clicks a button by the label the page gave it, preferring one
    /// inside an open dialog. Without the dialog preference a submit selector
    /// matches a button on the page BEHIND the dialog, which the overlay then
    /// refuses to let through - the click waits out its timeout on a control
    /// that was never the target.</summary>
    private async Task ClickButtonAsync(string label)
    {
        var modal = _page.Locator(".simf-modal").First;
        if (await modal.CountAsync() > 0 && await modal.IsVisibleAsync())
        {
            var inModal = modal.GetByRole(AriaRole.Button,
                new LocatorGetByRoleOptions { Name = label }).First;
            if (await inModal.CountAsync() > 0)
            {
                await inModal.ClickAsync(new LocatorClickOptions { Timeout = 20_000 });
                return;
            }
        }
        await _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = label })
            .First.ClickAsync(new LocatorClickOptions { Timeout = 20_000 });
    }

    /// <summary>Clicks a grid toolbar button by the label the page gave it, so
    /// the same code drives the English and the Arabic interface.</summary>
    private async Task ClickToolbarAsync(string label)
    {
        var button = _page.Locator($"button.simf-tbbtn[title=\"{label}\"]").First;
        if (await button.CountAsync() == 0)
        {
            button = _page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = label }).First;
        }
        await button.WaitForAsync(new LocatorWaitForOptions { Timeout = 20_000 });
        await button.ClickAsync();
        await Task.Delay(1_200);
    }

    /// <summary>Runs one capture step, recording a failure instead of raising it.</summary>
    private async Task StepAsync(string name, Func<Task> step)
    {
        try
        {
            await step();
        }
        catch (Exception exception)
        {
            _report.Add(new { slug = name, state = "flow", lang = Lang, error = exception.Message });
        }
    }

    /// <summary>Walks every route in SIMF_MANUAL_ROUTES and photographs it.</summary>
    [SkippableFact]
    public async Task Capture_route_sweep()
    {
        Skip.If(OutDir.Length == 0 || RoutesFile.Length == 0,
            "SIMF_MANUAL_OUT and SIMF_MANUAL_ROUTES must both be set.");

        await SignInCapturingFirstRunAsync(capture: false);
        await _page.GotoAsync(Cp + "/", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
        await SettleAsync();
        await AssertSignedInAsync();

        if (Lang == "ar")
        {
            await _page.GotoAsync(Cp + "/culture?culture=ar&redirectUri=%2F",
                new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
            await SettleAsync();
        }

        var captured = 0;
        foreach (var line in await File.ReadAllLinesAsync(RoutesFile))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("#", StringComparison.Ordinal)) { continue; }
            var parts = trimmed.Split('\t', 2);
            if (parts.Length != 2) { continue; }
            var slug = parts[0].Trim();
            var route = parts[1].Trim();

            try
            {
                await _page.GotoAsync(Cp + route,
                    new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded, Timeout = 30_000 });
                await SettleAsync();

                // Photograph the page only if the browser is still ON it. Five
                // routes redirect for a signed-in caller - the two account-state
                // pages go to the dashboard, and the three mid-sign-in pages go
                // back to /login - so a shot taken without this check is a
                // picture of the wrong page filed under the right name. The
                // manual build then passes, because the FILE exists; it just
                // shows something else. Recording the redirect and taking no
                // shot lets the build's missing-screenshot guard speak instead.
                var landed = new Uri(_page.Url).AbsolutePath;
                if (!SamePath(landed, route))
                {
                    _report.Add(new
                    {
                        slug,
                        state = "default",
                        lang = Lang,
                        url = _page.Url,
                        requested = route,
                        redirected = true,
                    });
                    continue;
                }

                await ShotAsync(slug, "default");
                captured++;
            }
            catch (Exception exception)
            {
                // One unreachable route must not abandon the other hundred; the
                // report is what says which ones failed.
                _report.Add(new
                {
                    slug,
                    state = "default",
                    lang = Lang,
                    url = Cp + route,
                    error = exception.Message,
                });
            }
        }

        WriteReport("sweep");
        Assert.True(captured > 0, "The sweep captured nothing at all.");
    }
}
