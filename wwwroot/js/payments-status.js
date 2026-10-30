// Poll saved payment status.
// Stripe lookups happen on page entry or manual recheck.
(function () {
    var poll = document.getElementById('poll');
    if (!poll) { return; }

    var url = poll.dataset.url;
    var attempts = 0;
    var maxAttempts = 40;

    var timer = setInterval(async function () {
        attempts++;

        try {
            var response = await fetch(url);

            if (response.ok) {
                var data = await response.json();

                if (data.status && data.status !== 'pending') {
                    clearInterval(timer);
                    location.reload();
                    return;
                }
            }
        } catch (e) {
            // Retry a temporary connection failure on the next tick.
        }

        if (attempts >= maxAttempts) {
            clearInterval(timer);

            var note = document.getElementById('poll-note');

            if (note) {
                note.textContent =
                    'Payment is still unconfirmed. Use Check payment status or contact the restaurant before paying again.';
            }
        }
    }, 3000);
})();
