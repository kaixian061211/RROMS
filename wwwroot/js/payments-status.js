// Payment status page: Stripe tells our server the result by webhook. Ask the server every
// 3 seconds until the status changes, then reload to show the final state.
(function () {
    var poll = document.getElementById('poll');
    if (!poll) { return; }

    var url = poll.dataset.url;
    var attempts = 0;
    var maxAttempts = 40; // about 2 minutes

    var timer = setInterval(async function () {
        attempts++;
        try {
            var response = await fetch(url);
            var data = await response.json();
            if (data.status !== 'pending') {
                clearInterval(timer);
                location.reload();
                return;
            }
        } catch (e) { /* try again on the next tick */ }

        if (attempts >= maxAttempts) {
            clearInterval(timer);
            document.getElementById('poll-note').textContent =
                'This is taking longer than usual. Check your payment history in a moment - you will not be charged twice.';
        }
    }, 3000);
})();