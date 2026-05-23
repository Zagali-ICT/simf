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
};
