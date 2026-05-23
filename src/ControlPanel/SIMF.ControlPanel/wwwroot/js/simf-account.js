// Small fetch helpers the /account/profile page uses to call the
// Control Panel's proxy endpoints. The browser sends the auth cookie
// automatically (same origin), so the Blazor circuit never has to
// handle the access token directly.

window.simfAccount = {
    async getJson(url) {
        const response = await fetch(url, { credentials: 'same-origin' });
        const text = await response.text();
        return text.length === 0 ? null : JSON.parse(text);
    },

    async postJson(url, body) {
        const response = await fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: body === undefined || body === null ? null : JSON.stringify(body),
        });
        const text = await response.text();
        return text.length === 0 ? null : JSON.parse(text);
    },

    async deleteJson(url) {
        const response = await fetch(url, {
            method: 'DELETE',
            credentials: 'same-origin',
        });
        const text = await response.text();
        return text.length === 0 ? null : JSON.parse(text);
    },

    // Force a server-side sign-out via a hidden form POST — Nav.NavigateTo
    // with a forceLoad GET would hit /auth/sign-out (mapped as POST only)
    // and 404, leaving the cookie alive. Called after password change so
    // the now-stale API tokens can be cleaned up.
    signOut() {
        const form = document.createElement('form');
        form.method = 'POST';
        form.action = '/auth/sign-out';
        document.body.appendChild(form);
        form.submit();
    },

    async uploadFile(url, inputId) {
        const input = document.getElementById(inputId);
        if (!input || !input.files || input.files.length === 0) {
            return { success: false, error: { code: 'AVATAR_FILE_MISSING', message: 'No file selected.', messageArabic: 'لم يتم اختيار ملف.' } };
        }
        const form = new FormData();
        form.append('file', input.files[0]);
        const response = await fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            body: form,
        });
        const text = await response.text();
        return text.length === 0 ? null : JSON.parse(text);
    },

    // Triggers a click on a hidden file input (used by the SimfDataGrid
    // Import button to open the OS file picker, decision D-044 b).
    triggerFileInput(inputId) {
        const input = document.getElementById(inputId);
        if (input) {
            input.value = '';     // ensure the same file fires the change event again
            input.click();
        }
    },

    // POSTs a JSON body to a binary endpoint and saves the response as a
    // file download (used by the SimfDataGrid Export, decision D-044 b).
    async downloadXlsx(url, body) {
        const response = await fetch(url, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body),
        });
        if (!response.ok) return;
        const blob = await response.blob();

        // D-045 H1: tightened filename parsing — accept only filename-safe
        // characters from Content-Disposition. A malicious upstream header
        // can't drop a "..\evil.lnk.xlsx" into the user's Save dialog.
        const disposition = response.headers.get('Content-Disposition') || '';
        const match = /filename="?([A-Za-z0-9._-]+)"?/i.exec(disposition);
        const fileName = match ? match[1] : 'simf-export.xlsx';

        const url2 = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url2;
        a.download = fileName;
        document.body.appendChild(a);
        a.click();
        a.remove();
        // D-045 H1: defer revocation so slow disks have time to start the
        // download before the blob URL is reclaimed.
        setTimeout(() => URL.revokeObjectURL(url2), 60_000);
    },
};

// SimfModal helpers (D-045 H2) — focus-trap, ref-counted body lock,
// document-level Esc, return-focus on close.
// The body lock is ref-counted so two modals open at once do not race
// (a future second modal would not flip body scroll back on under the
// first). The bindings live on the panel element via a WeakMap so the
// component code stays tiny.
window.simfModal = (function () {
    let openCount = 0;
    const bindings = new WeakMap();
    const focusableSelector =
        'a[href], button:not([disabled]), input:not([disabled]), '
        + 'select:not([disabled]), textarea:not([disabled]), '
        + '[tabindex]:not([tabindex="-1"])';

    function focusFirst(panel) {
        const list = panel.querySelectorAll(focusableSelector);
        if (list.length > 0) {
            list[0].focus();
        } else {
            panel.focus();
        }
    }

    return {
        // Bind a modal — adds body lock, returns focus on close, traps Tab,
        // calls dotNetRef.invokeMethodAsync('OnEscPressed') when the user
        // presses Escape (unless allowEsc=false).
        bind(panel, dotNetRef, allowEsc) {
            if (!panel) return;
            if (bindings.has(panel)) return;       // already bound

            const previousActive = document.activeElement;
            openCount++;
            if (openCount === 1) {
                document.body.classList.add('simf-modal-open');
            }
            focusFirst(panel);

            const handler = (event) => {
                if (event.key === 'Escape' && allowEsc) {
                    event.preventDefault();
                    dotNetRef.invokeMethodAsync('OnEscPressed');
                    return;
                }
                if (event.key !== 'Tab') return;
                const focusables = Array.from(
                    panel.querySelectorAll(focusableSelector))
                    .filter(el => el.offsetParent !== null);
                if (focusables.length === 0) {
                    event.preventDefault();
                    return;
                }
                const first = focusables[0];
                const last = focusables[focusables.length - 1];
                if (event.shiftKey && document.activeElement === first) {
                    event.preventDefault();
                    last.focus();
                } else if (!event.shiftKey && document.activeElement === last) {
                    event.preventDefault();
                    first.focus();
                }
            };

            document.addEventListener('keydown', handler, true);
            bindings.set(panel, { handler, previousActive });
        },

        // Unbind — undoes everything bind() did. Idempotent.
        unbind(panel) {
            if (!panel || !bindings.has(panel)) return;
            const { handler, previousActive } = bindings.get(panel);
            document.removeEventListener('keydown', handler, true);
            bindings.delete(panel);

            openCount = Math.max(0, openCount - 1);
            if (openCount === 0) {
                document.body.classList.remove('simf-modal-open');
            }
            if (previousActive && previousActive.focus) {
                try { previousActive.focus(); } catch { /* element gone */ }
            }
        },
    };
})();

// SimfContextMenu helpers (D-045 H2) — keyboard navigation between
// menuitems with arrow keys, focus the first item on open, Esc closes.
// Bound from the grid via JS interop the moment the menu opens.
window.simfContextMenu = (function () {
    const bindings = new WeakMap();

    return {
        bind(menu, dotNetRef) {
            if (!menu || bindings.has(menu)) return;
            const items = menu.querySelectorAll('[role="menuitem"]:not([disabled])');
            if (items.length === 0) return;

            // Roving tabindex — only the focused item is tabbable.
            items.forEach((el, i) => el.tabIndex = i === 0 ? 0 : -1);
            try { items[0].focus(); } catch { /* element gone */ }

            const handler = (event) => {
                const active = document.activeElement;
                const index = Array.from(items).indexOf(active);
                switch (event.key) {
                    case 'ArrowDown':
                        event.preventDefault();
                        focusItem(items, (index + 1) % items.length);
                        break;
                    case 'ArrowUp':
                        event.preventDefault();
                        focusItem(items, (index - 1 + items.length) % items.length);
                        break;
                    case 'Home':
                        event.preventDefault();
                        focusItem(items, 0);
                        break;
                    case 'End':
                        event.preventDefault();
                        focusItem(items, items.length - 1);
                        break;
                    case 'Escape':
                        event.preventDefault();
                        dotNetRef.invokeMethodAsync('OnEscPressed');
                        break;
                }
            };
            menu.addEventListener('keydown', handler);
            bindings.set(menu, handler);
        },

        unbind(menu) {
            if (!menu || !bindings.has(menu)) return;
            menu.removeEventListener('keydown', bindings.get(menu));
            bindings.delete(menu);
        },
    };

    function focusItem(items, index) {
        items.forEach((el, i) => el.tabIndex = i === index ? 0 : -1);
        try { items[index].focus(); } catch { /* element gone */ }
    }
})();
