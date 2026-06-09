// SIMF — generic per-page UI preference store over localStorage (D-353).
// Holds the user's custom display config keyed per page (today: the CRUD
// add/edit/view presentation mode — dialog vs full page; later: grid page
// size and column widths). Values are JSON blobs; the C# CpPreferences
// service owns the shape and the key namespace. Mirrors the resilience of
// simf-theme.js: localStorage may be unavailable (private mode / quota), in
// which case the preference simply isn't persisted for next time.
window.simfPrefs = (function () {
    "use strict";

    // Returns the parsed object stored under key, or null if absent / unreadable.
    function get(key) {
        try {
            var raw = window.localStorage.getItem(key);
            return raw ? JSON.parse(raw) : null;
        } catch (e) {
            return null;
        }
    }

    // Stores value (any JSON-serialisable object) under key.
    function set(key, value) {
        try {
            window.localStorage.setItem(key, JSON.stringify(value));
        } catch (e) {
            // private mode / quota — the preference just won't be remembered.
        }
    }

    // Removes every key that starts with prefix; returns how many were cleared.
    function clearAll(prefix) {
        try {
            var doomed = [];
            for (var i = 0; i < window.localStorage.length; i++) {
                var k = window.localStorage.key(i);
                if (k && k.indexOf(prefix) === 0) {
                    doomed.push(k);
                }
            }
            doomed.forEach(function (k) { window.localStorage.removeItem(k); });
            return doomed.length;
        } catch (e) {
            return 0;
        }
    }

    return { get: get, set: set, clearAll: clearAll };
})();
