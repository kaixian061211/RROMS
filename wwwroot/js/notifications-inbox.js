(() => {
    const page = document.querySelector("[data-notification-inbox]");
    if (!page) return;

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
    window.addEventListener("pageshow", () => {
        page.querySelectorAll("form[data-submitting]").forEach(form => {
            delete form.dataset.submitting;
            form.removeAttribute("aria-busy");
            const button = form.querySelector("button[type=submit]");
            if (button) {
                button.textContent = button.dataset.originalText ?? button.textContent;
                button.disabled = false;
            }
        });
    });
})();
