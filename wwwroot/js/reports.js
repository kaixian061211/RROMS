// Uses the locally vendored Chart.js 4.4.9 UMD bundle.
(() => {
    "use strict";

    const dataElement = document.getElementById("report-data");

    if (!dataElement) {
        return;
    }

    const statusElement = document.getElementById("report-chart-status");

    function showChartError() {
        if (!statusElement) {
            return;
        }

        statusElement.textContent =
            "Charts could not be displayed. The tables contain the report figures.";

        statusElement.hidden = false;
    }

    let data;

    try {
        data = JSON.parse(dataElement.textContent);
    } catch {
        showChartError();
        return;
    }

    if (typeof window.Chart !== "function" || !data.sales) {
        showChartError();
        return;
    }

    const amountFormatter = new Intl.NumberFormat("en-MY", {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    });

    const formatMoney = value =>
        `${data.currency} ${amountFormatter.format(value)}`;

    const reducedMotion = window.matchMedia(
        "(prefers-reduced-motion: reduce)"
    ).matches;

    const animation = reducedMotion ? false : { duration: 250 };

    const trendCanvas = document.getElementById("sales-trend-chart");

    if (trendCanvas) {
        const trend = data.sales.trend;

        try {
            new window.Chart(trendCanvas, {
                type: "bar",

                data: {
                    labels: trend.labels,

                    datasets: [
                        {
                            label: "Collections",
                            data: trend.collections,
                            backgroundColor: "#2563eb",
                            borderColor: "#1d4ed8",
                            borderWidth: 1,
                            order: 1
                        },
                        {
                            label: "Refunds",
                            data: trend.refunds,
                            backgroundColor: "#f59e0b",
                            borderColor: "#b45309",
                            borderWidth: 1,
                            order: 1
                        },
                        {
                            type: "line",
                            label: "Net collections",
                            data: trend.net,
                            borderColor: "#15803d",
                            backgroundColor: "#15803d",
                            borderWidth: 2,
                            borderDash: [6, 4],
                            pointStyle: "triangle",
                            pointRadius: 3,
                            tension: 0,
                            fill: false,
                            order: 0
                        }
                    ]
                },

                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation,

                    interaction: {
                        mode: "index",
                        intersect: false
                    },

                    plugins: {
                        legend: {
                            position: "bottom"
                        },

                        tooltip: {
                            callbacks: {
                                label: context =>
                                    `${context.dataset.label}: ${formatMoney(context.parsed.y)}`
                            }
                        }
                    },

                    scales: {
                        x: {
                            grid: {
                                display: false
                            },

                            ticks: {
                                autoSkip: true,
                                maxRotation: 45
                            }
                        },

                        y: {
                            // Negative net collections must remain visible.
                            beginAtZero: true,

                            title: {
                                display: true,
                                text: data.currency
                            },

                            ticks: {
                                callback: value => formatMoney(Number(value))
                            }
                        }
                    }
                }
            });
        } catch {
            showChartError();
        }
    }

    const methodCanvas = document.getElementById("sales-method-chart");

    if (methodCanvas) {
        const methods = data.sales.methods;

        const palette = [
            "#2563eb",
            "#15803d",
            "#f59e0b",
            "#9333ea",
            "#dc2626"
        ];

        try {
            new window.Chart(methodCanvas, {
                type: "doughnut",

                data: {
                    labels: methods.labels,

                    datasets: [
                        {
                            label: "Collections",
                            data: methods.amounts,

                            backgroundColor: methods.labels.map(
                                (_, index) => palette[index % palette.length]
                            ),

                            borderColor: "#ffffff",
                            borderWidth: 2
                        }
                    ]
                },

                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation,

                    plugins: {
                        legend: {
                            position: "bottom"
                        },

                        tooltip: {
                            callbacks: {
                                label: context =>
                                    `${context.label}: ${formatMoney(context.parsed)}`
                            }
                        }
                    }
                }
            });
        } catch {
            showChartError();
        }
    }
    if (data.orders) {
        const orderCountFormatter = new Intl.NumberFormat("en-MY");

        const orderStatusCanvas = document.getElementById(
            "orders-status-chart"
        );

        if (orderStatusCanvas) {
            const status = data.orders.status;

            try {
                new window.Chart(orderStatusCanvas, {
                    type: "bar",

                    data: {
                        labels: status.labels,

                        datasets: [
                            {
                                label: "Orders",
                                data: status.counts,
                                backgroundColor: "#2563eb",
                                borderColor: "#1d4ed8",
                                borderWidth: 1
                            }
                        ]
                    },

                    options: {
                        indexAxis: "y",
                        responsive: true,
                        maintainAspectRatio: false,
                        animation,

                        plugins: {
                            legend: {
                                display: false
                            },

                            tooltip: {
                                callbacks: {
                                    label: context =>
                                        `Orders: ${orderCountFormatter.format(context.parsed.x)}`
                                }
                            }
                        },

                        scales: {
                            x: {
                                beginAtZero: true,

                                ticks: {
                                    precision: 0
                                },

                                title: {
                                    display: true,
                                    text: "Orders"
                                }
                            },

                            y: {
                                grid: {
                                    display: false
                                },

                                ticks: {
                                    autoSkip: false
                                }
                            }
                        }
                    }
                });
            } catch {
                showChartError();
            }
        }

        const orderTrendCanvas = document.getElementById(
            "orders-trend-chart"
        );

        if (orderTrendCanvas) {
            const trend = data.orders.trend;

            try {
                new window.Chart(orderTrendCanvas, {
                    type: "bar",

                    data: {
                        labels: trend.labels,

                        datasets: [
                            {
                                label: "Orders placed",
                                data: trend.counts,
                                yAxisID: "orders",
                                backgroundColor: "#2563eb",
                                borderColor: "#1d4ed8",
                                borderWidth: 1,
                                order: 1
                            },
                            {
                                type: "line",
                                label: "Order value",
                                data: trend.values,
                                yAxisID: "money",
                                borderColor: "#15803d",
                                backgroundColor: "#15803d",
                                borderWidth: 2,
                                borderDash: [6, 4],
                                pointStyle: "triangle",
                                pointRadius: 3,
                                tension: 0,
                                fill: false,
                                order: 0
                            }
                        ]
                    },

                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        animation,

                        interaction: {
                            mode: "index",
                            intersect: false
                        },

                        plugins: {
                            legend: {
                                position: "bottom"
                            },

                            tooltip: {
                                callbacks: {
                                    label: context => {
                                        const value =
                                            context.dataset.yAxisID === "money"
                                                ? formatMoney(context.parsed.y)
                                                : orderCountFormatter.format(
                                                    context.parsed.y
                                                );

                                        return `${context.dataset.label}: ${value}`;
                                    }
                                }
                            }
                        },

                        scales: {
                            x: {
                                grid: {
                                    display: false
                                },

                                ticks: {
                                    autoSkip: true,
                                    maxRotation: 45
                                }
                            },

                            orders: {
                                axis: "y",
                                type: "linear",
                                position: "left",
                                beginAtZero: true,

                                title: {
                                    display: true,
                                    text: "Orders"
                                },

                                ticks: {
                                    precision: 0
                                }
                            },

                            money: {
                                axis: "y",
                                type: "linear",
                                position: "right",
                                beginAtZero: true,

                                title: {
                                    display: true,
                                    text: data.currency
                                },

                                ticks: {
                                    callback: value =>
                                        formatMoney(Number(value))
                                },

                                grid: {
                                    drawOnChartArea: false
                                }
                            }
                        }
                    }
                });
            } catch {
                showChartError();
            }
        }
    }
    if (data.reservations) {
        const reservationData = data.reservations;
        const reservationCountFormatter = new Intl.NumberFormat("en-MY");

        const drawReservationBars = (
            canvasId,
            labels,
            datasets,
            horizontal = false
        ) => {
            const canvas = document.getElementById(canvasId);

            if (!canvas) {
                return;
            }

            const countAxis = horizontal ? "x" : "y";
            const labelAxis = horizontal ? "y" : "x";

            try {
                new window.Chart(canvas, {
                    type: "bar",

                    data: {
                        labels,
                        datasets
                    },

                    options: {
                        indexAxis: horizontal ? "y" : "x",
                        responsive: true,
                        maintainAspectRatio: false,
                        animation,

                        interaction: {
                            mode: "index",
                            intersect: false
                        },

                        plugins: {
                            legend: {
                                display: datasets.length > 1,
                                position: "bottom"
                            },

                            tooltip: {
                                callbacks: {
                                    label: context =>
                                        `${context.dataset.label}: ${
                                            reservationCountFormatter.format(
                                                context.parsed[countAxis]
                                            )
                                        }`
                                }
                            }
                        },

                        scales: {
                            [countAxis]: {
                                beginAtZero: true,
                                stacked: false,

                                ticks: {
                                    precision: 0
                                },

                                title: {
                                    display: true,
                                    text: "Reservations"
                                }
                            },

                            [labelAxis]: {
                                stacked: false,

                                grid: {
                                    display: false
                                },

                                ticks: {
                                    autoSkip: !horizontal,
                                    maxRotation: horizontal ? 0 : 45
                                }
                            }
                        }
                    }
                });
            } catch {
                showChartError();
            }
        };

        drawReservationBars(
            "reservations-trend-chart",
            reservationData.trend.labels,
            [
                {
                    label: "Scheduled reservations",
                    data: reservationData.trend.scheduled,
                    backgroundColor: "#2563eb",
                    borderColor: "#1d4ed8",
                    borderWidth: 1
                },
                {
                    label: "Currently cancelled",
                    data: reservationData.trend.cancelled,
                    backgroundColor: "#f59e0b",
                    borderColor: "#b45309",
                    borderWidth: 1
                }
            ]
        );

        drawReservationBars(
            "reservations-status-chart",
            reservationData.status.labels,
            [
                {
                    label: "Reservations",
                    data: reservationData.status.counts,
                    backgroundColor: "#2563eb",
                    borderColor: "#1d4ed8",
                    borderWidth: 1
                }
            ],
            true
        );

        drawReservationBars(
            "reservations-peak-chart",
            reservationData.peak.labels,
            [
                {
                    label: "Non-cancelled reservations",
                    data: reservationData.peak.counts,

                    backgroundColor: reservationData.peak.isPeak.map(
                        peak => peak ? "#15803d" : "#2563eb"
                    ),

                    borderColor: reservationData.peak.isPeak.map(
                        peak => peak ? "#166534" : "#1d4ed8"
                    ),

                    borderWidth: 1
                }
            ]
        );
    }
    if (data.menu) {
        const menuCanvas = document.getElementById("menu-quantity-chart");

        if (menuCanvas) {
            const menuData = data.menu;
            const menuCountFormatter = new Intl.NumberFormat("en-MY");

            try {
                new window.Chart(menuCanvas, {
                    type: "bar",

                    data: {
                        labels: menuData.labels,

                        datasets: [
                            {
                                label: "Quantity purchased",
                                data: menuData.quantities,
                                backgroundColor: "#2563eb",
                                borderColor: "#1d4ed8",
                                borderWidth: 1
                            }
                        ]
                    },

                    options: {
                        indexAxis: "y",
                        responsive: true,
                        maintainAspectRatio: false,
                        animation,

                        plugins: {
                            legend: {
                                display: false
                            },

                            tooltip: {
                                callbacks: {
                                    title: items => items.length > 0
                                        ? menuData.labels[items[0].dataIndex]
                                        : "",

                                    label: context =>
                                        `Quantity purchased: ${menuCountFormatter.format(context.parsed.x)}`
                                }
                            }
                        },

                        scales: {
                            x: {
                                beginAtZero: true,

                                ticks: {
                                    precision: 0
                                },

                                title: {
                                    display: true,
                                    text: "Quantity purchased"
                                }
                            },

                            y: {
                                grid: {
                                    display: false
                                },

                                ticks: {
                                    autoSkip: false,

                                    callback: function (value) {
                                        const label = this.getLabelForValue(value);
                                        const characters = Array.from(label);

                                        return characters.length > 32
                                            ? `${characters.slice(0, 29).join("")}...`
                                            : label;
                                    }
                                }
                            }
                        }
                    }
                });
            } catch {
                showChartError();
            }
        }
    }
})();
