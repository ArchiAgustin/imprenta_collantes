(() => {
    const chartContainer = document.querySelector("[data-analytics-chart]");
    const canvas = document.getElementById("ventas-confirmadas-chart");
    if (!chartContainer || !canvas || typeof window.Chart === "undefined") return;

    try {
        const labels = JSON.parse(canvas.dataset.labels || "[]");
        const values = JSON.parse(canvas.dataset.values || "[]");
        if (!Array.isArray(labels) || !Array.isArray(values) || labels.length === 0 || labels.length !== values.length) return;
        if (values.some(value => !Number.isFinite(value) || value < 0)) return;

        const tokens = getComputedStyle(document.documentElement);
        const color = (name, fallback) => tokens.getPropertyValue(name).trim() || fallback;
        const action = color("--action", "#196369");
        const hover = color("--action-hover", "#104b50");
        const text = color("--subtle", "#425c5c");
        const rule = color("--rule", "#cdd3c9");
        const locale = document.documentElement.lang || "es-PE";
        const currency = value => new Intl.NumberFormat(locale, {
            style: "currency", currency: "PEN", minimumFractionDigits: 2, maximumFractionDigits: 2
        }).format(value);
        const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;

        new window.Chart(canvas, {
            type: "bar",
            data: {
                labels,
                datasets: [{
                    label: "Ventas confirmadas con IGV",
                    data: values,
                    backgroundColor: action,
                    hoverBackgroundColor: hover,
                    borderWidth: 0,
                    borderRadius: 1,
                    maxBarThickness: 44
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                animation: reducedMotion ? false : { duration: 220 },
                interaction: { intersect: false, mode: "index" },
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            label: item => ` ${currency(item.parsed.y)}`
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false },
                        ticks: { color: text, maxRotation: 0, autoSkip: true, maxTicksLimit: 13 }
                    },
                    y: {
                        beginAtZero: true,
                        grid: { color: rule },
                        ticks: {
                            color: text,
                            callback: value => new Intl.NumberFormat(locale, {
                                style: "currency", currency: "PEN", maximumFractionDigits: 0
                            }).format(value)
                        }
                    }
                }
            }
        });

        chartContainer.dataset.chartReady = "true";
    } catch {
        // Keep the server-rendered bars and the data table available if chart setup fails.
    }
})();
