(() => {
        const content = document.getElementById("tracking-content");
        if (!content) return null;

        const banner = document.getElementById("tracking-updates-banner");
        const button = document.getElementById("tracking-refresh-button");

        const orderId = content.dataset.orderId
            ? Number(content.dataset.orderId) : null;

        const reservationId = content.dataset.reservationId
            ? Number(content.dataset.reservationId) : null;

        const pageUrl = new URL(window.location.href);
        pageUrl.hash = "";
        pageUrl.searchParams.delete("handler");
        const statusUrl = new URL(pageUrl);
        statusUrl.searchParams.set("handler", "Status");

        button?.addEventListener("click", () => window.location.reload());

        function snapshotKey(snapshot) {
            if (!snapshot
                || snapshot.orderId !== orderId
                || snapshot.reservationId !== reservationId) {
                throw new Error("The tracking record is unavailable.");
            }

            return JSON.stringify(snapshot);
        }

        let currentKey;

        try {
            currentKey = snapshotKey(JSON.parse(content.dataset.snapshot));
        } catch {
            if (banner) banner.hidden = false;
            return null;
        }

        let disposed = false;
        let busy = false;
        let queued = false;
        let timer;
        let controller;

        async function readStatus() {
            controller = new AbortController();
            const timeout = setTimeout(() => controller.abort(), 10000);
            try {
                const response = await fetch(statusUrl.href, {
                    credentials: "same-origin", cache: "no-store",
                    headers: { Accept: "application/json" }, signal: controller.signal
                });
                if (!response.ok || response.redirected || response.url !== statusUrl.href
                    || !response.headers.get("Content-Type")?.includes("application/json")) {
                    throw new Error("The tracking status could not be refreshed.");
                }
                return await response.json();
            } finally {
                clearTimeout(timeout);
                controller = null;
            }
        }

        async function renderLatest() {
            controller = new AbortController();

            const timeout = setTimeout(
                () => controller.abort(),
                10000
            );

            try {
                const response = await fetch(pageUrl.href, {
                    credentials: "same-origin",
                    cache: "no-store",
                    headers: { Accept: "text/html" },
                    signal: controller.signal
                });

                if (!response.ok
                    || response.redirected
                    || response.url !== pageUrl.href
                    || !response.headers.get("Content-Type")?.includes("text/html")) {
                    throw new Error("The tracking page could not be refreshed.");
                }

                const html = await response.text();

                const documentCopy = new DOMParser()
                    .parseFromString(html, "text/html");

                const incoming = documentCopy
                    .getElementById("tracking-content");

                if (!incoming
                    || incoming.dataset.orderId !== content.dataset.orderId
                    || incoming.dataset.reservationId !== content.dataset.reservationId) {
                    throw new Error("The tracking page does not match this record.");
                }

                const nextKey = snapshotKey(
                    JSON.parse(incoming.dataset.snapshot)
                );

                if (disposed || document.hidden) return;

                const replacement = document.importNode(incoming, true);

                content.replaceChildren(...replacement.childNodes);
                content.dataset.snapshot = incoming.dataset.snapshot;

                currentKey = nextKey;

                if (banner) banner.hidden = true;
            } finally {
                clearTimeout(timeout);
                controller = null;
            }
        }

        async function check(force = false) {
            if (disposed) return;

            if (document.hidden || busy) {
                queued ||= force;
                return;
            }

            clearTimeout(timer);
            busy = true;

            try {
                const snapshot = await readStatus();

                if (!disposed && !document.hidden
                    && (force || (banner && !banner.hidden) || snapshotKey(snapshot) !== currentKey)) {
                    await renderLatest();
                }
            } catch {
                if (!disposed && !document.hidden && banner) {
                    banner.hidden = false;
                }
            } finally {
                busy = false;

                if (!disposed && !document.hidden) {
                    const runAgain = queued;
                    queued = false;

                    timer = setTimeout(
                        () => void check(runAgain),
                        runAgain ? 0 : 5000
                    );
                }
            }
        }

        document.addEventListener("visibilitychange", () => {
            clearTimeout(timer);

            if (!document.hidden) {
                void check(true);
            }
        });

        window.addEventListener("pagehide", () => {
            disposed = true;
            clearTimeout(timer);
            controller?.abort();
        });

        window.addEventListener("pageshow", event => {
            if (event.persisted) {
                disposed = false;
                void check(true);
            }
        });

        window.RromsTracking = {
            refresh() {
                void check(true);
            }
        };
        void check(true);
})();
