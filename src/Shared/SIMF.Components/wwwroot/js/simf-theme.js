// SIMF — theme helper.
// Applies and persists the light / dark theme by setting data-theme on the
// document root. The no-flash apply-on-load runs from an inline <head> script
// in each app; this module provides the toggle the SimfThemeToggle calls.
window.simfTheme = (function () {
    "use strict";
    var STORAGE_KEY = "simf-theme";

    function isDark() {
        return document.documentElement.getAttribute("data-theme") === "dark";
    }

    function set(dark) {
        if (dark) {
            document.documentElement.setAttribute("data-theme", "dark");
        } else {
            document.documentElement.removeAttribute("data-theme");
        }
        try {
            window.localStorage.setItem(STORAGE_KEY, dark ? "dark" : "light");
        } catch (e) {
            // localStorage may be unavailable (private mode); the theme still
            // applies for the current page.
        }
        return dark;
    }

    return {
        isDark: isDark,
        toggle: function () { return set(!isDark()); }
    };
})();
