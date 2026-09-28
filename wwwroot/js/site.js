// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

document.addEventListener('DOMContentLoaded', () => {
    const hour = new Date().getHours();
    document.querySelectorAll('[data-greeting]').forEach(node => {
        node.textContent = hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening';
    });
    document.querySelectorAll('[data-local-time]').forEach(node => {
        const date = new Date(node.dateTime);
        if (!Number.isNaN(date.getTime())) node.textContent = date.toLocaleString([], { dateStyle: 'medium', timeStyle: 'short' });
    });
    document.querySelectorAll('[data-product-photo]').forEach(img => {
        const fallback = () => { img.hidden = true; };
        img.addEventListener('error', fallback);
        if (img.complete && img.naturalWidth === 0) fallback();
    });

    // Keep submit button values intact for actions with multiple submit choices.
    const clearBusy = form => {
        form.removeAttribute('aria-busy');
        delete form.dataset.submitting;
        form.querySelectorAll('[data-submit-spinner]').forEach(node => node.remove());
    };
    document.addEventListener('submit', event => {
        const form = event.target;
        if (!(form instanceof HTMLFormElement) || event.defaultPrevented) return;
        if (form.dataset.submitting === 'true') { event.preventDefault(); return; }
        if (window.jQuery && window.jQuery.fn.valid && !window.jQuery(form).valid()) return;
        // Allow existing validation and confirmation handlers to cancel first.
        setTimeout(() => {
            if (event.defaultPrevented) return;
            form.dataset.submitting = 'true';
            form.setAttribute('aria-busy', 'true');
            if (event.submitter instanceof HTMLButtonElement) {
                const spinner = document.createElement('span');
                spinner.className = 'spinner-border spinner-border-sm me-2';
                spinner.dataset.submitSpinner = '';
                spinner.setAttribute('role', 'status');
                spinner.setAttribute('aria-label', 'Working');
                event.submitter.prepend(spinner);
            }
            // A failed navigation or file download must not leave the form locked.
            setTimeout(() => clearBusy(form), 15000);
        }, 0);
    });
    window.addEventListener('pageshow', () => document.querySelectorAll('form[aria-busy]').forEach(clearBusy));
});
