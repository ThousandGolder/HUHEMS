/**
 * Exam Performance Chart Handler
 * @param {Array} labels - Exam Titles
 * @param {Array} dataPoints - Average Scores
 */
function initGeneralReportChart(labels, dataPoints) {
    const canvas = document.getElementById('examChart');
    if (!canvas) return;

    const ctx = canvas.getContext('2d');

    // Destroy existing chart instance if it exists (prevents ghosting)
    if (window.myExamChart) {
        window.myExamChart.destroy();
    }

    window.myExamChart = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: 'Average Score (%)',
                data: dataPoints,
                backgroundColor: 'rgba(13, 110, 253, 0.7)',
                borderColor: 'rgb(13, 110, 253)',
                borderWidth: 1,
                borderRadius: 6
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: false, // IMPORTANT for printing
            scales: {
                y: {
                    beginAtZero: true,
                    max: 100,
                    ticks: {
                        callback: value => value + "%"
                    }
                }
            },
            plugins: {
                legend: {
                    display: true
                },
                tooltip: {
                    callbacks: {
                        label: context => `Avg: ${context.parsed.y}%`
                    }
                }
            }
        }
    });
}

/* ================= PRINT SUPPORT (CRITICAL FIX) ================= */

window.addEventListener("beforeprint", () => {
    const canvas = document.getElementById("examChart");
    if (!canvas) return;

    // Convert chart to image for print
    const img = document.createElement("img");
    img.src = canvas.toDataURL("image/png");
    img.style.maxWidth = "100%";
    img.style.display = "block";
    img.id = "chart-print-image";

    canvas.style.display = "none";
    canvas.parentNode.appendChild(img);
});

window.addEventListener("afterprint", () => {
    const img = document.getElementById("chart-print-image");
    const canvas = document.getElementById("examChart");

    if (img) img.remove();
    if (canvas) canvas.style.display = "block";
});
