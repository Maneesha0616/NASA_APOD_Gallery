// Client-side interactions for NASA APOD Gallery

document.addEventListener('DOMContentLoaded', function () {
    // Preset date range selector buttons
    const startDateInput = document.getElementById('fetchStartDate');
    const endDateInput = document.getElementById('fetchEndDate');

    window.setPresetRange = function (days) {
        if (!startDateInput || !endDateInput) return;

        const end = new Date();
        const start = new Date(end);
        start.setDate(end.getDate() - days);

        endDateInput.value = formatDate(end);
        startDateInput.value = formatDate(start);

        // Auto-submit the fetch form
        const form = startDateInput.closest('form');
        if (form) form.submit();
    };

    window.setPresetDates = function (startStr, endStr) {
        if (!startDateInput || !endDateInput) return;
        startDateInput.value = startStr;
        endDateInput.value = endStr;
        const form = startDateInput.closest('form');
        if (form) form.submit();
    };

    function formatDate(date) {
        const year = date.getFullYear();
        const month = String(date.getMonth() + 1).padStart(2, '0');
        const day = String(date.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    }

    // Details modal dynamic content populate
    const apodModal = document.getElementById('apodModal');
    if (apodModal) {
        apodModal.addEventListener('show.bs.modal', function (event) {
            const button = event.relatedTarget;
            if (!button) return;

            const title = button.getAttribute('data-apod-title') || 'APOD Details';
            const date = button.getAttribute('data-apod-date') || '';
            const mediaType = button.getAttribute('data-apod-mediatype') || 'image';
            const url = button.getAttribute('data-apod-url') || '';
            const hdUrl = button.getAttribute('data-apod-hdurl') || '';
            const explanation = button.getAttribute('data-apod-explanation') || 'No description available.';
            const copyright = button.getAttribute('data-apod-copyright') || '';
            const id = button.getAttribute('data-apod-id') || '';

            document.getElementById('modalTitle').textContent = title;
            document.getElementById('modalDate').textContent = date;
            document.getElementById('modalExplanation').textContent = explanation;

            const modalCopyright = document.getElementById('modalCopyright');
            if (copyright && copyright.trim() !== '') {
                modalCopyright.textContent = `© ${copyright}`;
                modalCopyright.style.display = 'inline-block';
            } else {
                modalCopyright.style.display = 'none';
            }

            const mediaContainer = document.getElementById('modalMediaContainer');
            mediaContainer.innerHTML = '';

            // Force HTTPS to prevent mixed-content blocks on HTTPS pages
            const safeUrl = url.replace(/^http:\/\//i, 'https://');
            const safeHdUrl = hdUrl.replace(/^http:\/\//i, 'https://');

            mediaContainer.innerHTML = `
                <div class="text-center bg-black rounded p-2">
                    <img src="${safeUrl}"
                         alt="${title}"
                         class="img-fluid rounded"
                         loading="lazy"
                         referrerpolicy="no-referrer"
                         style="max-height: 480px; object-fit: contain;">
                </div>`;

            const hdBtn = document.getElementById('modalHdBtn');
            if (hdBtn) {
                if (safeHdUrl && safeHdUrl.trim() !== '') {
                    hdBtn.href = safeHdUrl;
                    hdBtn.style.display = 'inline-flex';
                } else {
                    hdBtn.style.display = 'none';
                }
            }

            const pageLink = document.getElementById('modalFullPageLink');
            if (pageLink && id) {
                pageLink.href = `/Home/Details/${id}`;
            }
        });
    }
});
