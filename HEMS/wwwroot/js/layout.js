$(document).ready(function () {
    "use strict";

    // 1. Sidebar Toggle Logic
    $(document).on('click', '#sidebarCollapse', function () {
        $('#sidebar, #content').toggleClass('active');
    });

    // Auto-close sidebar on mobile when clicking a link
    $('#sidebar ul li a').on('click', function () {
        if (window.innerWidth <= 992) {
            $('#sidebar, #content').addClass('active');
        }
    });

    // Close sidebar when the custom close button is clicked (mobile)
    // On mobile the "active" class opens the sidebar, so remove it to close
    $(document).on('click', '#sidebarClose', function (e) {
        e.preventDefault();
        if (window.innerWidth <= 992) {
            $('#sidebar, #content').removeClass('active');
        }
    });

    // 2. Logout Confirmation (Updated for AccountController)
    $(document).on('click', '.logout-btn', function (e) {
        e.preventDefault();

        // Get the specific form related to this button
        const form = $(this).closest('form');

        Swal.fire({
            title: 'Logout?',
            text: "Are you sure you want to end your session?",
            icon: 'question',
            showCancelButton: true,
            confirmButtonColor: '#dc3545',
            cancelButtonColor: '#6c757d',
            confirmButtonText: '<i class="bi bi-box-arrow-right"></i> Yes, Logout',
            cancelButtonText: 'Cancel',
            reverseButtons: true
        }).then((result) => {
            if (result.isConfirmed) {
                // Use the native DOM submit to bypass jQuery validation issues
                form[0].submit();
            }
        });
    });

    // 3. Handle SweetAlert Notifications
    if (typeof msgs !== 'undefined') {
        // Only show success toasts here. Error messages are rendered as page banners in the layout
        if (msgs.success && msgs.success.trim() !== "" && msgs.success !== '@TempData["SuccessMessage"]') {
            const Toast = Swal.mixin({
                toast: true,
                position: 'top-end',
                showConfirmButton: false,
                timer: 3000,
                timerProgressBar: true,
                didOpen: (toast) => {
                    toast.addEventListener('mouseenter', Swal.stopTimer)
                    toast.addEventListener('mouseleave', Swal.resumeTimer)
                }
            });

            Toast.fire({
                icon: 'success',
                title: msgs.success
            });
        }
    }
});