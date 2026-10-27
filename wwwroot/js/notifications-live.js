(() => {
    const root = document.getElementById("notification-live");
    if (!root || !window.signalR) return;

    const badge = document.getElementById("notification-unread-count");
    const toast = document.getElementById("notification-toast");
    const title = document.getElementById("notification-toast-title");
    const message = document.getElementById("notification-toast-message");
    const refreshPage = root.dataset.refreshPage === "true";

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(root.dataset.hubUrl)
        .withAutomaticReconnect()
        .build();

    let submitting = false;
    let reloadTimer;
    let retryTimer;
    let connectedBefore = false;
    let startFailed = false;

    document.addEventListener("submit", event => {
        const form = event.target;

        if (form instanceof HTMLFormElement
            && form.method.toLowerCase() === "post"
            && !event.defaultPrevented) {
            submitting = true;
        }
    });

    function refreshPageSoon() {
        if (!refreshPage || submitting) return;

        clearTimeout(reloadTimer);

        reloadTimer = setTimeout(() => {
            if (!submitting) window.location.reload();
        }, 500);
    }

    async function updateUnreadCount() {
        if (!badge) return;

        try {
            const count = await connection.invoke("GetUnreadCount");

            badge.textContent = String(count);
            badge.hidden = count === 0;
            badge.setAttribute(
                "aria-label",
                `${count} unread notifications`
            );
        } catch {
            // Reconcile the count after reconnection.
        }
    }

    connection.on("NotificationReceived", notification => {
        title.textContent = notification.title;
        message.textContent = notification.message;

        if (window.bootstrap?.Toast) {
            bootstrap.Toast.getOrCreateInstance(
                toast,
                { delay: 8000 }
            ).show();
        }

        void updateUnreadCount();
        refreshPageSoon();
    });

    connection.onreconnected(() => {
        void updateUnreadCount();
        refreshPageSoon();
    });

    function retryStart() {
        clearTimeout(retryTimer);
        retryTimer = setTimeout(start, 5000);
    }

    async function start() {
        try {
            await connection.start();
            void updateUnreadCount();

            if (connectedBefore || startFailed) {
                refreshPageSoon();
            }

            connectedBefore = true;
            startFailed = false;
        } catch {
            startFailed = true;
            retryStart();
        }
    }

    connection.onclose(retryStart);
    void start();
})();