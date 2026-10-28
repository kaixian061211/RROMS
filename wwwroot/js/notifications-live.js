(() => {
    const root = document.getElementById("notification-live");
    if (!root || !window.signalR) return;

    const badge = document.getElementById("notification-unread-count");
    const toast = document.getElementById("notification-toast");
    const title = document.getElementById("notification-toast-title");
    const message = document.getElementById("notification-toast-message");
    const trackingBanner = document.getElementById("tracking-updates-banner");
    const trackingRefreshButton = document.getElementById("tracking-refresh-button");
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
    const toastQueue = [];
    let toastShowing = false;
    const toastInstance = window.bootstrap?.Toast && toast
        ? window.bootstrap.Toast.getOrCreateInstance(toast, { delay: 8000 })
        : null;

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
        if (toastShowing || toastQueue.length > 0) return;

        clearTimeout(reloadTimer);

        reloadTimer = setTimeout(() => {
            if (!submitting) window.location.reload();
        }, 500);
    }

    function showNextToast() {
        if (toastShowing || toastQueue.length === 0)
            return;

        if (!toastInstance) {
            toastQueue.length = 0;
            refreshPageSoon();
            return;
        }

        const notification = toastQueue.shift();
        title.textContent = notification.title;
        message.textContent = notification.message;
        toastShowing = true;
        toastInstance.show();
    }

    function queueToast(notification) {
        clearTimeout(reloadTimer);
        toastQueue.push(notification);
        showNextToast();
    }

    function showTrackingUpdates() {
        if (trackingBanner) trackingBanner.hidden = false;
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
        queueToast(notification);

        void updateUnreadCount();
        showTrackingUpdates();
        refreshPageSoon();
    });

    toast?.addEventListener("hidden.bs.toast", () => {
        toastShowing = false;
        showNextToast();
        refreshPageSoon();
    });

    trackingRefreshButton?.addEventListener("click", () => {
        window.location.reload();
    });

    connection.onreconnected(() => {
        void updateUnreadCount();
        showTrackingUpdates();
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
                showTrackingUpdates();
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