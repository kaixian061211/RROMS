// Observe saved status without overlapping requests. Stripe verification remains explicit.
(function () {
    'use strict';
    var poll = document.querySelector('.payment-page #poll');
    if (!poll) return;
    var note = document.getElementById('poll-note');
    var spinner = document.querySelector('[data-payment-spinner]');
    var attempts = 0;
    var timer;
    var active;
    var stopped = false;
    function stop(message) {
        stopped = true;
        clearTimeout(timer);
        if (active) active.abort();
        if (spinner) spinner.hidden = true;
        if (note && message) note.textContent = message;
    }
    async function check() {
        if (stopped) return;
        if (document.hidden) { timer = setTimeout(check, 3000); return; }
        attempts++;
        active = new AbortController();
        var timeout = setTimeout(function () { if (active) active.abort(); }, 8000);
        try {
            var response = await fetch(poll.dataset.url, { signal: active.signal, cache: 'no-store', headers: { 'Accept': 'application/json' } });
            if (response.redirected || response.status === 401 || response.status === 403) {
                stop('Your session needs attention. Sign in again to check this payment before paying again.');
                return;
            }
            if (response.status === 404) { stop('This payment could not be found. Open your orders to check it.'); return; }
            if (response.ok && (response.headers.get('content-type') || '').includes('application/json')) {
                var data = await response.json();
                if (['paid', 'failed', 'refunded'].includes(data.status)) { stop(); location.reload(); return; }
            }
        } catch (_) { /* A later check can recover a temporary connection failure. */ }
        finally { clearTimeout(timeout); active = null; }
        if (attempts >= 40) { stop('Payment is still unconfirmed. Use Check payment status or contact the restaurant before paying again.'); return; }
        if (!stopped) timer = setTimeout(check, 3000);
    }
    window.addEventListener('pagehide', function () { stop(); });
    window.addEventListener('pageshow', function (event) { if (event.persisted) location.reload(); });
    timer = setTimeout(check, 3000);
})();
