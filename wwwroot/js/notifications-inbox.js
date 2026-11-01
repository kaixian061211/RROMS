(() => {
    const page = document.querySelector("[data-notification-inbox]");
    if (!page) return;

    let busy = false;
    let queued = false;
    let disposed = false;
    let controller;

    function rememberFocus() {
        const active = document.activeElement;
        if (!active || !page.contains(active)) return null;
        if (active.tagName === "A") return { href: active.getAttribute("href") };
        const form = active.closest("form");
        return form ? { notificationId: form.querySelector("input[name=notificationId]")?.value ?? "all" } : null;
    }

    function restoreFocus(focus) {
        if (!focus) return;
        let target;
        if (focus.href) {
            target = [...page.querySelectorAll("a")].find(a => a.getAttribute("href") === focus.href);
        } else {
            const form = [...page.querySelectorAll("form")].find(f =>
                (f.querySelector("input[name=notificationId]")?.value ?? "all") === focus.notificationId);
            target = form?.querySelector("button[type=submit]");
        }
        (target ?? page.querySelector("h1"))?.focus({ preventScroll: true });
    }

    async function refresh() {
        if (disposed || document.hidden || page.querySelector("form[data-submitting=true]")) return;
        if (busy) { queued = true; return; }
        busy = true;
        queued = false;
        controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 10000);
        try {
            const url = new URL(window.location.href);
            url.hash = "";
            const response = await fetch(url.href, {
                credentials: "same-origin", cache: "no-store", signal: controller.signal,
                headers: { Accept: "text/html" }
            });
            if (!response.ok || response.redirected || response.url !== url.href
                || !response.headers.get("Content-Type")?.includes("text/html")) {
                throw new Error("Inbox refresh unavailable.");
            }
            const incoming = new DOMParser().parseFromString(await response.text(), "text/html")
                .querySelector("[data-notification-inbox]");
            if (!incoming || incoming.dataset.notificationOwner !== page.dataset.notificationOwner
                || incoming.dataset.beforeId !== page.dataset.beforeId) {
                throw new Error("The inbox does not match the current recipient or page.");
            }
            if (disposed || document.hidden || page.querySelector("form[data-submitting=true]")) return;
            const focus = rememberFocus();
            page.replaceChildren(...document.importNode(incoming, true).childNodes);
            restoreFocus(focus);
        } catch {
            if (!disposed) {
                const error = page.querySelector("[data-inbox-error]");
                if (error) error.hidden = false;
            }
        } finally {
            clearTimeout(timeout);
            controller = null;
            busy = false;
            if (queued && !disposed) void refresh();
        }
    }

    page.addEventListener("click", event => {
        if (event.target.closest?.("[data-inbox-refresh]")) void refresh();
    });

    page.addEventListener("submit", event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || form.method.toLowerCase() !== "post" || event.defaultPrevented) return;
        if (form.dataset.submitting === "true") {
            event.preventDefault();
            return;
        }
        form.dataset.submitting = "true";
        form.setAttribute("aria-busy", "true");
        const button = form.querySelector("button[type=submit]");
        if (button) {
            button.dataset.originalText = button.textContent;
            button.textContent = "Saving…";
            button.disabled = true;
        }
    });

    // Browsers can restore a submitted page from their back/forward cache.
    window.addEventListener("pageshow", event => {
        disposed = false;
        page.querySelectorAll("form[data-submitting]").forEach(form => {
            delete form.dataset.submitting;
            form.removeAttribute("aria-busy");
            const button = form.querySelector("button[type=submit]");
            if (button) {
                button.textContent = button.dataset.originalText ?? button.textContent;
                button.disabled = false;
            }
        });
        if (event?.persisted) void refresh();
    });

    document.addEventListener("visibilitychange", () => {
        if (!document.hidden) void refresh();
    });
    window.addEventListener("pagehide", () => { disposed = true; controller?.abort(); });
    window.RromsNotificationInbox = { refresh: () => void refresh() };
})();
