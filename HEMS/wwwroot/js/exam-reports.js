/**
 * Exam Performance Chart Handler
 * @param {Array} labels - Exam Titles
 * @param {Array} dataPoints - Average Scores
 */
function initGeneralReportChart(labels, dataPoints) {
    const canvas = document.getElementById('examChart');
    if (!canvas) return;

    const ctx = canvas.getContext('2d');

    // Destroy existing chart instance if it exists (prevents ghosting on refreshes)
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
            scales: {
                y: {
                    beginAtZero: true,
                    max: 100,
                    ticks: {
                        callback: function (value) {
                            return value + "%";
                        }
                    }
                }
            },
            plugins: {
                tooltip: {
                    callbacks: {
                        label: function (context) {
                            return `Avg: ${context.parsed.y}%`;
                        }
                    }
                }
            }
        }
    });
}