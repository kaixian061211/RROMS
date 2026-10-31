// Report-only Chart.js 4.4.9 presentation. Values come from DashboardReport.
(() => {
    "use strict";
    const element = document.getElementById("report-data");
    if (!element) return;
    const showError = () => {
        const status = document.getElementById("report-chart-status");
        if (!status) return;
        status.textContent = "A chart could not be displayed. Its exact figures are available in the table below.";
        status.hidden = false;
    };
    let data;
    try { data = JSON.parse(element.textContent); } catch { showError(); return; }
    if (typeof window.Chart !== "function") { showError(); return; }

    const styles = getComputedStyle(document.documentElement);
    const tone = name => styles.getPropertyValue(`--rr-${name}`).trim();
    const palette = ["pandan", "teh", "kaya", "kopi", "muted"];
    const bodyFont = styles.getPropertyValue("--rr-font-body").trim();
    const displayFont = styles.getPropertyValue("--rr-font-display").trim();
    const count = new Intl.NumberFormat("en-MY");
    const money = new Intl.NumberFormat("en-MY", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    const formatMoney = value => `RM ${money.format(value)}`;
    const motion = window.matchMedia("(prefers-reduced-motion: reduce)");
    const animation = () => motion.matches ? false : { duration: 450, easing: "easeOutQuart" };
    const charts = [];

    const focusGuide = {
        id: "report-focus",
        beforeDatasetsDraw(chart) {
            const active = chart.getActiveElements();
            if (!active.length || chart.scales.x?.type !== "category") return;
            const { ctx, chartArea } = chart;
            const x = active[0].element.x;
            ctx.save();
            ctx.strokeStyle = tone("line-strong");
            ctx.setLineDash([3, 4]);
            ctx.beginPath(); ctx.moveTo(x, chartArea.top); ctx.lineTo(x, chartArea.bottom); ctx.stroke();
            ctx.restore();
        }
    };
    const valueLabels = {
        id: "report-values",
        afterDatasetsDraw(chart, args, options) {
            if (!options.enabled) return;
            const { ctx } = chart;
            ctx.save();
            ctx.font = `600 12px ${bodyFont}`;
            ctx.fillStyle = tone("kopi");
            ctx.textAlign = "left"; ctx.textBaseline = "middle";
            chart.data.datasets.forEach((dataset, index) => {
                if (!chart.isDatasetVisible(index)) return;
                chart.getDatasetMeta(index).data.forEach((bar, i) => {
                    if (dataset.data[i] > 0) ctx.fillText(count.format(dataset.data[i]), bar.x + 8, bar.y);
                });
            });
            ctx.restore();
        }
    };
    const ringTotal = {
        id: "report-total",
        afterDraw(chart) {
            if (chart.config.type !== "doughnut" || data.sales.methods.total == null) return;
            const { ctx, chartArea } = chart;
            const x = (chartArea.left + chartArea.right) / 2;
            const y = (chartArea.top + chartArea.bottom) / 2;
            const radius = chart.getDatasetMeta(0).data[0]?.innerRadius ?? 70;
            const text = formatMoney(data.sales.methods.total);
            ctx.save();
            let size = 25;
            do { ctx.font = `700 ${size--}px ${displayFont}`; }
            while (ctx.measureText(text).width > radius * 1.7 && size > 12);
            ctx.fillStyle = tone("kopi"); ctx.textAlign = "center";
            ctx.fillText(text, x, y - 1);
            ctx.font = `12px ${bodyFont}`; ctx.fillStyle = tone("muted");
            ctx.fillText("All collections", x, y + 22);
            ctx.restore();
        }
    };

    function axis({ currency = false, horizontal = false, title = "", categorical = false } = {}) {
        return {
            beginAtZero: !categorical,
            border: { display: false },
            grid: { display: !categorical, color: tone("line"), drawTicks: false },
            ticks: {
                color: tone("muted"), padding: 10, font: { family: bodyFont, size: 11 },
                maxRotation: 0, autoSkip: !horizontal, maxTicksLimit: horizontal ? undefined : 7,
                ...(categorical ? {} : { precision: 0, callback: value => currency ? formatMoney(Number(value)) : count.format(value) })
            },
            title: { display: !!title, text: title, color: tone("muted"), font: { family: bodyFont, size: 11 } }
        };
    }

    function options(currency = false) {
        return {
            responsive: true, maintainAspectRatio: false, animation: animation(),
            color: tone("muted"), font: { family: bodyFont, size: 12 },
            interaction: { mode: "index", intersect: false },
            layout: { padding: { top: 12, right: 12 } },
            elements: {
                bar: { borderRadius: 4, borderSkipped: false },
                line: { borderWidth: 2.5, tension: 0.22, cubicInterpolationMode: "monotone", fill: false },
                point: { radius: 0, hoverRadius: 5, hitRadius: 15, hoverBorderWidth: 2, hoverBorderColor: tone("enamel") }
            },
            plugins: {
                legend: { display: false },
                tooltip: {
                    backgroundColor: tone("kopi"), titleColor: tone("enamel"), bodyColor: tone("enamel"),
                    titleFont: { family: bodyFont, size: 12, weight: "600" },
                    bodyFont: { family: bodyFont, size: 12 }, padding: 14, cornerRadius: 8,
                    boxPadding: 5, bodySpacing: 7, usePointStyle: true,
                    callbacks: { label: context => `${context.dataset.label}: ${currency ? formatMoney(context.parsed.y) : count.format(context.parsed.y)}` }
                }
            },
            scales: { x: axis({ categorical: true }), y: axis({ currency, title: currency ? "RM" : "Count" }) }
        };
    }

    function bars(label, values, color, extra = {}) {
        return { label, data: values, backgroundColor: tone(color), borderColor: tone(color),
            maxBarThickness: 28, categoryPercentage: 0.7, barPercentage: 0.85, ...extra };
    }

    function controls(chart, tones, switchable) {
        const wrapper = document.createElement("div");
        wrapper.className = "report-chart-controls";
        const legend = document.createElement("div");
        legend.className = chart.config.type === "doughnut" ? "report-chart-legend report-chart-legend--methods" : "report-chart-legend";
        legend.setAttribute("role", "group"); legend.setAttribute("aria-label", "Show or hide chart series");
        const ring = chart.config.type === "doughnut";
        const labels = ring ? chart.data.labels : chart.data.datasets.map(dataset => dataset.label);
        const buttons = [];
        const modes = [];
        const originalTypes = chart.data.datasets.map(dataset => dataset.type || chart.config.type);
        let mode = "bar";
        const reset = document.createElement("button");
        reset.type = "button"; reset.className = "btn btn-outline-secondary btn-sm report-chart-reset";
        reset.textContent = "Reset chart"; reset.hidden = true;
        function sync() {
            buttons.forEach((button, i) => button.setAttribute("aria-pressed", String(ring ? chart.getDataVisibility(i) : chart.isDatasetVisible(i))));
            modes.forEach(button => button.setAttribute("aria-pressed", String(button.dataset.mode === mode)));
            reset.hidden = mode === "bar" && buttons.every(button => button.getAttribute("aria-pressed") === "true");
        }
        labels.forEach((label, index) => {
            const button = document.createElement("button");
            button.type = "button"; button.className = "report-legend-button";
            button.dataset.tone = tones[index % tones.length];
            button.setAttribute("aria-controls", chart.canvas.id);
            const swatch = document.createElement("span"); swatch.className = "report-legend-swatch";
            swatch.setAttribute("aria-hidden", "true");
            const name = document.createElement("span"); name.textContent = label;
            button.append(swatch, name);
            if (ring) {
                const amount = document.createElement("span"); amount.className = "report-legend-value";
                amount.textContent = formatMoney(chart.data.datasets[0].data[index]); button.append(amount);
            }
            button.addEventListener("click", () => {
                if (ring) chart.toggleDataVisibility(index);
                else chart.setDatasetVisibility(index, !chart.isDatasetVisible(index));
                sync(); chart.update(motion.matches ? "none" : undefined);
            });
            buttons.push(button); legend.append(button);
        });
        wrapper.append(legend);
        if (switchable) {
            const group = document.createElement("div"); group.className = "report-chart-switch";
            group.setAttribute("role", "group"); group.setAttribute("aria-label", "Chart display");
            ["bar", "line"].forEach(type => {
                const button = document.createElement("button");
                button.type = "button"; button.dataset.mode = type;
                button.className = "report-mode-button"; button.textContent = type === "bar" ? "Bars" : "Lines";
                button.setAttribute("aria-controls", chart.canvas.id);
                button.addEventListener("click", () => {
                    mode = type;
                    chart.data.datasets.forEach((dataset, i) => dataset.type = type === "line" ? "line" : originalTypes[i]);
                    sync(); chart.update(motion.matches ? "none" : undefined);
                });
                modes.push(button); group.append(button);
            });
            wrapper.append(group);
        }
        reset.addEventListener("click", () => {
            buttons.forEach((button, i) => {
                if (ring) { if (!chart.getDataVisibility(i)) chart.toggleDataVisibility(i); }
                else chart.setDatasetVisibility(i, true);
            });
            chart.data.datasets.forEach((dataset, i) => dataset.type = originalTypes[i]);
            mode = "bar"; sync(); chart.update(motion.matches ? "none" : undefined);
        });
        wrapper.append(reset); chart.canvas.parentElement.after(wrapper); sync();
    }

    function draw(id, config, tones, switchable = false) {
        const canvas = document.getElementById(id);
        if (!canvas) return;
        try {
            const chart = new window.Chart(canvas, { ...config, plugins: [focusGuide, valueLabels, ringTotal] });
            charts.push(chart);
            if (tones) controls(chart, tones, switchable);
        } catch { showError(); }
    }

    const sales = data.sales;
    if (sales) {
        draw("sales-trend-chart", {
            type: "bar", data: { labels: sales.trend.labels, datasets: [
                bars("Collections", sales.trend.collections, "pandan", { order: 1 }),
                bars("Refunds", sales.trend.refunds, "kaya", { order: 1 }),
                bars("Net collections", sales.trend.net, "kopi", { type: "line", order: 0, borderDash: [5, 4] })
            ] }, options: options(true)
        }, ["pandan", "kaya", "kopi"], true);
        const ringOptions = options(); delete ringOptions.scales;
        ringOptions.cutout = "76%"; ringOptions.radius = "90%";
        ringOptions.interaction = { mode: "nearest", intersect: true };
        ringOptions.plugins.tooltip.callbacks = {
            title: items => items[0]?.label ?? "",
            label: context => `Collections: ${formatMoney(context.parsed)}`,
            afterLabel: context => sales.methods.shares?.[context.dataIndex] == null ? "" : `${sales.methods.shares[context.dataIndex].toFixed(1)}% of all collections`
        };
        draw("sales-method-chart", { type: "doughnut", data: { labels: sales.methods.labels,
            datasets: [{ data: sales.methods.amounts, backgroundColor: palette.map(tone),
                borderColor: tone("enamel"), borderWidth: 4, hoverOffset: motion.matches ? 0 : 5, spacing: 2 }] },
            options: ringOptions }, palette);
    }

    function horizontal(id, labels, values, colors, label) {
        const settings = options();
        settings.indexAxis = "y"; settings.layout.padding.right = 44;
        settings.scales = { x: axis({ title: label }), y: axis({ categorical: true, horizontal: true }) };
        settings.scales.y.ticks.callback = function (value) {
            const name = this.getLabelForValue(value);
            const characters = Array.from(name);
            return characters.length > 28 ? characters.slice(0, 25).join("") + "…" : name;
        };
        settings.plugins["report-values"] = { enabled: true };
        settings.plugins.tooltip.callbacks.label = context => `${label}: ${count.format(context.parsed.x)}`;
        draw(id, { type: "bar", data: { labels, datasets: [bars(label, values, "pandan",
            { backgroundColor: colors.map(tone), maxBarThickness: 23 })] }, options: settings });
    }
    const statusTone = label => ({ Pending: "kaya", Preparing: "teh", Ready: "pandan", Confirmed: "pandan",
        Served: "kopi", Completed: "kopi", Cancelled: "muted" }[label] || "muted");
    if (data.orders) {
        horizontal("orders-status-chart", data.orders.status.labels, data.orders.status.counts,
            data.orders.status.labels.map(statusTone), "Orders");
        const settings = options();
        settings.scales = { x: axis({ categorical: true }), orders: { ...axis({ title: "Orders" }), position: "left", axis: "y" },
            money: { ...axis({ currency: true, title: "RM" }), position: "right", axis: "y", grid: { display: false } } };
        settings.plugins.tooltip.callbacks.label = context => `${context.dataset.label}: ${context.dataset.yAxisID === "money" ? formatMoney(context.parsed.y) : count.format(context.parsed.y)}`;
        draw("orders-trend-chart", { type: "bar", data: { labels: data.orders.trend.labels, datasets: [
            bars("Orders placed", data.orders.trend.counts, "pandan", { yAxisID: "orders", order: 1 }),
            bars("Order value", data.orders.trend.values, "teh", { type: "line", yAxisID: "money", order: 0 })
        ] }, options: settings }, ["pandan", "teh"], true);
    }
    if (data.reservations) {
        const reservations = data.reservations;
        const settings = options(); settings.scales.y.title.text = "Reservations";
        draw("reservations-trend-chart", { type: "bar", data: { labels: reservations.trend.labels, datasets: [
            bars("Scheduled reservations", reservations.trend.scheduled, "pandan"),
            bars("Currently cancelled", reservations.trend.cancelled, "muted")
        ] }, options: settings }, ["pandan", "muted"], true);
        horizontal("reservations-status-chart", reservations.status.labels, reservations.status.counts,
            reservations.status.labels.map(statusTone), "Reservations");
        const peakSettings = options();
        peakSettings.scales.y.title.text = "Non-cancelled reservations";
        draw("reservations-peak-chart", { type: "bar", data: { labels: reservations.peak.labels,
            datasets: [bars("Non-cancelled reservations", reservations.peak.counts, "pandan", {
                backgroundColor: reservations.peak.isPeak.map(peak => tone(peak ? "pandan" : "line-strong")), maxBarThickness: 42 })] },
            options: peakSettings });
    }
    if (data.menu) {
        horizontal("menu-quantity-chart", data.menu.labels, data.menu.quantities,
            data.menu.labels.map(() => "pandan"), "Quantity purchased");
        const chart = charts.find(chart => chart.canvas.id === "menu-quantity-chart");
        if (chart) {
            chart.options.plugins.tooltip.callbacks.afterLabel = context => {
                const i = context.dataIndex;
                return [data.menu.categories?.[i], data.menu.amounts?.[i] == null ? "" : `Item amount: ${formatMoney(data.menu.amounts[i])}`].filter(Boolean);
            };
            chart.update("none");
        }
    }

    // Summary before detail, with native, keyboard-accessible disclosures.
    document.querySelectorAll(".report-page table.table-hover").forEach(table => {
        const card = table.closest(".card");
        const heading = card?.querySelector("h3");
        if (!card || !heading) return;
        const details = document.createElement("details");
        details.className = `${card.className} report-records`;
        const summary = document.createElement("summary");
        summary.className = "report-records-summary";
        summary.append(heading);
        details.append(summary);
        while (card.firstChild) details.append(card.firstChild);
        card.replaceWith(details);
    });

    // Keep the real table headings and provide a readable card layout on phones.
    document.querySelectorAll(".report-page table").forEach(table => {
        table.classList.add("report-responsive-table"); table.setAttribute("role", "table");
        const headings = [...table.querySelectorAll("thead th")].map(cell => cell.textContent.trim());
        table.querySelectorAll("tbody tr").forEach(row => {
            row.setAttribute("role", "row");
            [...row.children].forEach((cell, i) => {
                cell.dataset.label = headings[i] || "";
                cell.setAttribute("role", cell.tagName === "TH" ? "rowheader" : "cell");
            });
        });
    });
    motion.addEventListener("change", () => charts.forEach(chart => {
        chart.stop(); chart.options.animation = animation(); chart.update("none");
    }));
    document.fonts?.ready.then(() => charts.forEach(chart => { chart.resize(); chart.update("none"); }));
})();
