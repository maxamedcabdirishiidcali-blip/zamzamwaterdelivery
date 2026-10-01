// Admin dashboard charts (Chart.js). Data comes from /Admin/Dashboard/Charts.
window.ZamzamDashboard = (function () {
    'use strict';

    const blue = '#0b5ed7';
    const statusColors = ['#ffc107', '#0dcaf0', '#6ea8fe', '#0b5ed7', '#20c997', '#dc3545'];
    const productColors = ['#0b5ed7', '#19b5fe', '#6ea8fe', '#0a3d8f', '#9ec5fe'];

    function gradient(ctx) {
        const g = ctx.createLinearGradient(0, 0, 0, 280);
        g.addColorStop(0, 'rgba(11, 94, 215, 0.35)');
        g.addColorStop(1, 'rgba(11, 94, 215, 0.02)');
        return g;
    }

    async function init(url, currency) {
        Chart.defaults.font.family = 'Poppins, system-ui, sans-serif';
        Chart.defaults.color = '#6c7a89';
        const money = (v) => currency + Number(v).toLocaleString(undefined, { minimumFractionDigits: 0, maximumFractionDigits: 2 });

        let data;
        try {
            const res = await fetch(url, { headers: { 'Accept': 'application/json' } });
            data = await res.json();
        } catch {
            window.showToast && window.showToast('Could not load chart data.', 'error');
            return;
        }

        const salesCtx = document.getElementById('salesChart').getContext('2d');
        const salesChart = new Chart(salesCtx, {
            type: 'line',
            data: {
                labels: data.dailySales.labels,
                datasets: [{ label: 'Sales', data: data.dailySales.values, borderColor: blue, backgroundColor: gradient(salesCtx), fill: true, tension: .35, pointRadius: 3 }]
            },
            options: {
                maintainAspectRatio: false,
                plugins: { legend: { display: false }, tooltip: { callbacks: { label: (c) => money(c.parsed.y) } } },
                scales: { y: { beginAtZero: true, ticks: { callback: money } }, x: { grid: { display: false } } }
            }
        });

        document.querySelectorAll('input[name="salesPeriod"]').forEach(r => r.addEventListener('change', () => {
            const series = r.value === 'monthly' ? data.monthlySales : data.dailySales;
            salesChart.config.type = r.value === 'monthly' ? 'bar' : 'line';
            salesChart.data.labels = series.labels;
            salesChart.data.datasets[0].data = series.values;
            salesChart.data.datasets[0].backgroundColor = r.value === 'monthly' ? blue : gradient(salesCtx);
            salesChart.update();
        }));

        new Chart(document.getElementById('statusChart'), {
            type: 'doughnut',
            data: { labels: data.ordersByStatus.labels, datasets: [{ data: data.ordersByStatus.values, backgroundColor: statusColors, borderWidth: 2 }] },
            options: { maintainAspectRatio: false, cutout: '65%', plugins: { legend: { position: 'bottom', labels: { boxWidth: 12 } } } }
        });

        new Chart(document.getElementById('productsChart'), {
            type: 'bar',
            data: { labels: data.popularProducts.labels, datasets: [{ data: data.popularProducts.values, backgroundColor: productColors, borderRadius: 6 }] },
            options: {
                indexAxis: 'y', maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    x: { beginAtZero: true, ticks: { precision: 0 } },
                    y: {
                        grid: { display: false },
                        ticks: { callback: function (v) { const l = this.getLabelForValue(v); return l.length > 16 ? l.slice(0, 15) + '…' : l; } }
                    }
                }
            }
        });
    }

    return { init };
})();
