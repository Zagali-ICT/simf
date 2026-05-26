// SIMF — theme helper.
// Applies and persists the light / dark theme by setting data-theme on the
// document root. The no-flash apply-on-load runs from an inline <head> script
// in each app; this module provides the toggle the SimfThemeToggle calls.
//
// D-098: a third value "grey" is supported by the tokens layer
// (theme.tokens.css [data-theme="grey"]). It is reachable today via
// simfTheme.setNamed("grey"); the UI toggle still cycles only light <-> dark.
// A future commit can swap the toggle for a 3-way cycler when the owner
// confirms the grey theme is approved for general use.
window.simfTheme = (function () {
    "use strict";
    var STORAGE_KEY = "simf-theme";
    var KNOWN = ["light", "dark", "grey"];

    function current() {
        return document.documentElement.getAttribute("data-theme") || "light";
    }

    function isDark() { return current() === "dark"; }

    function setNamed(name) {
        if (KNOWN.indexOf(name) < 0) { name = "light"; }
        if (name === "light") {
            document.documentElement.removeAttribute("data-theme");
        } else {
            document.documentElement.setAttribute("data-theme", name);
        }
        try {
            window.localStorage.setItem(STORAGE_KEY, name);
        } catch (e) {
            // localStorage may be unavailable (private mode); the theme still
            // applies for the current page.
        }
        return name;
    }

    return {
        isDark: isDark,
        current: current,
        setNamed: setNamed,
        toggle: function () { return setNamed(isDark() ? "light" : "dark"); }
    };
})();
