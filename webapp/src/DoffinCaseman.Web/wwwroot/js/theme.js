// Theme switching itself is pure CSS (:has() in site.css, reacting to the
// Auto/Light/Dark radios in MainLayout.razor). JS is only responsible for
// what CSS structurally cannot do: persisting the choice across page loads.
//
// The initial-load restore (before first paint) is a separate inline
// script right after the radios in MainLayout.razor -- this file only
// handles re-syncing after Blazor's enhanced navigation swaps in fresh,
// unrestored radios, and saving a new choice when the user picks one.

(function () {
    function restore() {
        var stored;
        try {
            stored = localStorage.getItem('theme');
        } catch (e) {
            return;
        }
        var id = stored === 'light' ? 'theme-light' : stored === 'dark' ? 'theme-dark' : 'theme-auto';
        var el = document.getElementById(id);
        if (el) el.checked = true;
    }

    document.addEventListener('change', function (event) {
        if (event.target.name !== 'theme') return;
        try {
            // 'auto' isn't stored as a string value -- its absence IS the
            // auto state, same convention the inline restore script and
            // App.razor's pre-paint script (data-theme removed; theme.js
            // now owns storage) both read.
            var value = event.target.value === 'auto' ? '' : event.target.value;
            if (value) {
                localStorage.setItem('theme', value);
            } else {
                localStorage.removeItem('theme');
            }
        } catch (e) { /* storage unavailable; choice still applies for this load */ }
    });

    if (window.Blazor && typeof window.Blazor.addEventListener === 'function') {
        window.Blazor.addEventListener('enhancedload', restore);
    }
})();
