(() => {
    const formatter = new Intl.DateTimeFormat(undefined, {
        dateStyle: "medium",
        timeStyle: "short"
    });

    document.querySelectorAll("time[data-notification-time]").forEach(element => {
        const date = new Date(element.dateTime);

        if (Number.isNaN(date.getTime())) return;

        element.textContent = formatter.format(date);
        element.title = date.toISOString();
    });
})();