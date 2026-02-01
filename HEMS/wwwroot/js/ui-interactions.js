document.addEventListener('DOMContentLoaded', function () {
    const confirmModal = document.getElementById('reusableConfirmModal');
    if (!confirmModal) return;

    // Standard Modal Setup for Buttons
    confirmModal.addEventListener('show.bs.modal', function (event) {
        const button = event.relatedTarget;
        if (!button) return; // Ignore if triggered manually via script

        const title = button.getAttribute('data-title');
        const message = button.getAttribute('data-message');
        const target = button.getAttribute('data-target');
        const type = button.getAttribute('data-type');

        updateModalContent(title, message, target, type);
    });

    // Helper to update modal content and button behavior
    window.updateModalContent = function (title, message, target, type) {
        document.getElementById('confirmTitle').innerText = title;
        document.getElementById('confirmMessage').innerText = message;
        // Set icon and header style according to type
        const iconEl = document.getElementById('confirmIcon');
        const headerEl = document.querySelector('#reusableConfirmModal .modal-header');
        // reset header classes
        if (headerEl) {
            headerEl.classList.remove('bg-success','bg-danger','bg-warning','bg-info','bg-primary','text-white');
        }
        if (iconEl) {
            iconEl.className = 'bi bi-info-circle';
            iconEl.style.color = '';
            iconEl.style.fontSize = '2.5rem';
        }
        if (type === 'warning') {
            if (headerEl) headerEl.classList.add('bg-warning','text-dark');
            if (iconEl) { iconEl.className = 'bi bi-exclamation-triangle-fill'; iconEl.style.color = '#b45309'; }
        } else if (type === 'danger') {
            if (headerEl) headerEl.classList.add('bg-danger','text-white');
            if (iconEl) { iconEl.className = 'bi bi-x-circle-fill'; iconEl.style.color = '#dc2626'; }
        } else if (type === 'success') {
            if (headerEl) headerEl.classList.add('bg-success','text-white');
            if (iconEl) { iconEl.className = 'bi bi-check-circle-fill'; iconEl.style.color = '#15803d'; }
        } else if (type === 'info') {
            if (headerEl) headerEl.classList.add('bg-info','text-dark');
            if (iconEl) { iconEl.className = 'bi bi-info-circle-fill'; iconEl.style.color = '#0ea5e9'; }
        } else {
            if (headerEl) headerEl.classList.add('bg-primary','text-white');
            if (iconEl) { iconEl.className = 'bi bi-info-circle-fill'; iconEl.style.color = ''; }
        }

        // Make the message text red for warnings/danger to increase visibility
        const msgEl = document.getElementById('confirmMessage');
        if (msgEl) {
            if (type === 'warning' || type === 'danger') {
                msgEl.style.color = '#dc2626';
            } else {
                msgEl.style.color = '#475569';
            }
        }

        // Do not hide page banners here anymore - layout controls duplicates. Keep modal-only behavior.

        const confirmBtn = document.getElementById('confirmActionButton');
        const newConfirmBtn = confirmBtn.cloneNode(true);
        confirmBtn.parentNode.replaceChild(newConfirmBtn, confirmBtn);

        newConfirmBtn.addEventListener('click', function () {
            if (type === 'form') {
                const form = document.getElementById(target);
                if (form) form.submit();
            } else if (type === 'link') {
                window.location.href = target;
            } else {
                // Just close for warnings/infos
                const modalInstance = bootstrap.Modal.getInstance(confirmModal);
                if (modalInstance) modalInstance.hide();
            }
        });
    }

    // BROWSER/TAB SWITCH DETECTION
    window.addEventListener('blur', function () {
        if (document.getElementById('timer')) {
            const modalElement = document.getElementById('reusableConfirmModal');
            const modalInstance = new bootstrap.Modal(modalElement);

            // Set Warning Content
            updateModalContent(
                "⚠️ Security Warning",
                "You have switched tabs or windows. This action has been logged. Please focus on your exam.",
                null,
                "warning"
            );

            modalInstance.show();
        }
    });
});

/**
 * User-Specific Resume Logic
 */
function handleResumeLogic(examId, currentIndex, userId) {
    if (!userId) return;
    localStorage.setItem(`resume_exam_${userId}_${examId}`, currentIndex);
}