// Dark mode toggle. Plain JS, not a Blazor component, because the toggle
// lives in MainLayout which wraps the static-SSR login page (no
// interactive circuit there to attach a C# click handler to) as well as
// the InteractiveServer pages. Click handling uses event delegation on
// document rather than binding to the button directly, since Blazor's
// enhanced navigation can replace the button element between page loads
// without a full script reload.

(function () {
    function systemPrefersDark() {
        return window.matchMedia('(prefers-color-scheme: dark)').matches;
    }

    function currentTheme() {
        var explicit = document.documentElement.getAttribute('data-theme');
        if (explicit === 'light' || explicit === 'dark') return explicit;
        return systemPrefersDark() ? 'dark' : 'light';
    }

    function updateToggleLabel() {
        var btn = document.getElementById('theme-toggle');
        if (!btn) return;
        var isDark = currentTheme() === 'dark';
        // Label names the action (what clicking switches TO), not the
        // current state, matching common toggle-button convention.
        btn.textContent = isDark ? 'Light' : 'Dark';
        btn.setAttribute('aria-pressed', String(isDark));
    }

    function toggleTheme() {
        var next = currentTheme() === 'dark' ? 'light' : 'dark';
        document.documentElement.setAttribute('data-theme', next);
        try {
            localStorage.setItem('theme', next);
        } catch (e) { /* storage unavailable; theme still applies for this load */ }
        updateToggleLabel();
    }

    document.addEventListener('click', function (event) {
        if (event.target.closest('#theme-toggle')) {
            toggleTheme();
        }
    });

    document.addEventListener('DOMContentLoaded', updateToggleLabel);
    // Re-sync after Blazor's enhanced navigation swaps in fresh server-
    // rendered markup (a new button with the default label/aria-pressed).
    if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
        window.Blazor.addEventListener('enhancedload', updateToggleLabel);
    }
})();
