// D-443 (NCA finding) — token-driven session-expiry guard.
//
// The API access token is short-lived (Jwt:AccessTokenMinutes, 5 min) and the
// refresh-token chain is capped at an absolute 24h from sign-in. This guard
// keeps an ACTIVE user signed in without interruption — it silently asks the
// server to rotate the token before it lapses — and warns an IDLE user with a
// countdown modal ("Stay signed in" / "Sign out") just before the token would
// expire, signing them out if they do not respond. When the 24h cap is reached
// the silent refresh fails and the user is sent to sign-in.
//
// On the Website (SSR) the signed-in layout renders the localized config into
// window.__simfSessionGuardCfg and this module auto-starts; the Control Panel
// (interactive) calls start() / stop() through JS interop instead. All UI text
// + thresholds come from config so one module serves both apps, EN and AR (RTL).
window.simfSessionGuard = (function () {
    var cfg = null;
    var lastActivity = 0;
    var expiresAt = 0;          // epoch ms of the current access-token expiry
    var ticker = null;
    var countdownTimer = null;
    var modal = null;
    var ending = false;
    var refreshing = false;     // single-flight guard for fetchStatus
    var running = false;        // idempotency guard for start()
    var ACTIVITY_EVENTS = ['mousemove', 'mousedown', 'keydown', 'scroll', 'touchstart', 'click'];

    function now() { return Date.now(); }
    function onActivity() { lastActivity = now(); }

    // Ask the server for the current expiry. Because the request is
    // authenticated, the cookie-validate hook rotates the token first when it is
    // near expiry — so this doubles as the silent refresh. Returns true when a
    // fresh expiry was obtained, false otherwise; ends the session on 401 /
    // redirect-to-login (revoked, or the 24h cap reached).
    async function fetchStatus() {
        // Single-flight: never run two refreshes at once. Two concurrent
        // /session/status calls would both present the same refresh token, and
        // once the first rotates it the second trips the server's rotation
        // reuse-detection (which signs the user out of every session). A 5-min
        // token refreshes often, so this guard is essential (D-443 review).
        if (refreshing) return false;
        refreshing = true;
        try {
            var res = await fetch(cfg.statusUrl, {
                credentials: 'same-origin',
                headers: { 'X-Requested-With': 'fetch' },
                cache: 'no-store',
            });
            if (res.status === 401 || res.redirected) { endSession(); return false; }
            if (!res.ok) return false;
            var data = await res.json();
            if (data && data.expiresAt) {
                expiresAt = new Date(data.expiresAt).getTime();
                hideModal();
                return true;
            }
            return false;
        } catch (e) {
            // Network blip — leave the existing schedule in place and retry next tick.
            return false;
        } finally {
            refreshing = false;
        }
    }

    function endSession() {
        if (ending) return;
        ending = true;
        stop();
        // POST sign-out (SameSite=Lax cookie makes this CSRF-safe). A full
        // navigation lands the user on /login whether or not the cookie is live.
        var form = document.createElement('form');
        form.method = 'post';
        form.action = cfg.signOutUrl;
        form.style.display = 'none';
        document.body.appendChild(form);
        form.submit();
    }

    function tick() {
        if (!expiresAt) return;
        var remaining = expiresAt - now();
        if (remaining <= 0) { endSession(); return; }
        if (remaining > cfg.warnMs) return;

        var recentlyActive = (now() - lastActivity) < cfg.activeWindowMs;
        if (recentlyActive) {
            // Active user: silently refresh so they are never interrupted.
            fetchStatus();
        } else {
            // Idle user: warn with a live countdown.
            showModal();
        }
    }

    function buildModal() {
        modal = document.createElement('div');
        modal.className = 'simf-modal simf-session-modal';
        modal.setAttribute('role', 'alertdialog');
        modal.setAttribute('aria-modal', 'true');
        modal.dir = cfg.rtl ? 'rtl' : 'ltr';
        modal.style.display = 'none';

        var panel = document.createElement('div');
        panel.className = 'simf-modal__panel';

        var head = document.createElement('div');
        head.className = 'simf-modal__head';
        var title = document.createElement('h2');
        title.className = 'simf-modal__title';
        title.textContent = cfg.title;
        head.appendChild(title);

        var body = document.createElement('div');
        body.className = 'simf-modal__body';
        var count = document.createElement('strong');
        count.className = 'simf-session-modal__count';
        // "{body} {N} {seconds}" — N is a live countdown.
        body.appendChild(document.createTextNode(cfg.body + ' '));
        body.appendChild(count);
        body.appendChild(document.createTextNode(' ' + cfg.secondsLabel));

        var footer = document.createElement('div');
        footer.className = 'simf-modal__footer';
        var signOutBtn = document.createElement('button');
        signOutBtn.type = 'button';
        signOutBtn.className = 'simf-button simf-button--secondary';
        signOutBtn.textContent = cfg.signOutLabel;
        signOutBtn.addEventListener('click', endSession);
        var stayBtn = document.createElement('button');
        stayBtn.type = 'button';
        stayBtn.className = 'simf-button simf-button--primary';
        stayBtn.textContent = cfg.stayLabel;
        stayBtn.addEventListener('click', async function () {
            lastActivity = now();
            await fetchStatus();
        });
        footer.appendChild(signOutBtn);
        footer.appendChild(stayBtn);

        panel.appendChild(head);
        panel.appendChild(body);
        panel.appendChild(footer);
        modal.appendChild(panel);
        document.body.appendChild(modal);
    }

    function renderCountdown() {
        if (!modal) return;
        var secs = Math.max(0, Math.ceil((expiresAt - now()) / 1000));
        var el = modal.querySelector('.simf-session-modal__count');
        if (el) el.textContent = secs;
    }

    function showModal() {
        if (!modal) buildModal();
        modal.style.display = 'flex';
        renderCountdown();
        if (!countdownTimer) {
            countdownTimer = setInterval(function () {
                if ((expiresAt - now()) <= 0) { endSession(); return; }
                renderCountdown();
            }, 1000);
        }
    }

    function hideModal() {
        if (modal) modal.style.display = 'none';
        if (countdownTimer) { clearInterval(countdownTimer); countdownTimer = null; }
    }

    async function start(config) {
        cfg = config;
        // Idempotent: the Website emits the start call inline AND auto-starts
        // from the module tail, and an interactive reconnect can re-invoke it.
        // Only the first call wires up timers + listeners; later calls just
        // refresh the config (D-443 review).
        if (running) return;
        running = true;
        ending = false;
        lastActivity = now();
        for (var i = 0; i < ACTIVITY_EVENTS.length; i++) {
            document.addEventListener(ACTIVITY_EVENTS[i], onActivity, { passive: true });
        }
        await fetchStatus();
        ticker = setInterval(tick, cfg.pollMs);
    }

    function stop() {
        running = false;
        if (ticker) { clearInterval(ticker); ticker = null; }
        if (countdownTimer) { clearInterval(countdownTimer); countdownTimer = null; }
        for (var i = 0; i < ACTIVITY_EVENTS.length; i++) {
            document.removeEventListener(ACTIVITY_EVENTS[i], onActivity);
        }
        hideModal();
    }

    return { start: start, stop: stop };
})();

// SSR auto-start: the Website renders the localized config into a global before
// this module loads, so the guard begins as soon as it is available. The
// Control Panel (interactive) instead calls start() through JS interop from
// SessionTimeoutGuard.razor and never sets this global, so the check is a no-op
// there.
if (window.__simfSessionGuardCfg) {
    window.simfSessionGuard.start(window.__simfSessionGuardCfg);
}
