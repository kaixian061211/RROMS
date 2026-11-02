// Payment-only confirmation and submission handling. The server remains authoritative.
(function () {
    'use strict';
    var root = document.querySelector('.payment-page');
    if (!root) return;
    var modalElement = root.querySelector('#payment-confirm');
    var modal = modalElement && window.bootstrap ? new bootstrap.Modal(modalElement) : null;
    var pending = null;

    function valid(form) {
        if (!form.checkValidity()) { form.reportValidity(); return false; }
        return !window.jQuery || !jQuery.fn.valid || jQuery(form).valid();
    }
    function resetSubmissions() {
        root.querySelectorAll('form[data-payment-submit]').forEach(function (form) {
            delete form.dataset.paymentSubmitting;
            delete form.dataset.paymentConfirmed;
            form.removeAttribute('aria-busy');
            form.querySelectorAll('button[data-payment-original]').forEach(function (button) {
                button.disabled = false;
                button.textContent = button.dataset.paymentOriginal;
                delete button.dataset.paymentOriginal;
            });
        });
    }
    root.addEventListener('submit', function (event) {
        var form = event.target;
        if (!form.matches('form[data-payment-submit]') || event.defaultPrevented) return;
        if (form.dataset.paymentSubmitting === 'true') { event.preventDefault(); return; }
        if (!valid(form)) { event.preventDefault(); return; }
        if (form.dataset.paymentConfirm && form.dataset.paymentConfirmed !== 'true') {
            event.preventDefault();
            if (!modal) {
                if (!window.confirm(form.dataset.paymentConfirm + '\n\n' + (form.dataset.paymentConfirmDescription || ''))) return;
                form.dataset.paymentConfirmed = 'true';
                form.requestSubmit(event.submitter || undefined);
                return;
            }
            // One pending confirmation at a time; opening another form replaces no submitted action.
            if (pending) return;
            pending = { form: form, button: event.submitter };
            modalElement.querySelector('#payment-confirm-title').textContent = form.dataset.paymentConfirm;
            modalElement.querySelector('#payment-confirm-description').textContent = form.dataset.paymentConfirmDescription || '';
            var confirmButton = modalElement.querySelector('[data-payment-confirm-action]');
            confirmButton.textContent = form.dataset.paymentConfirmLabel || 'Confirm';
            confirmButton.className = 'btn ' + (form.dataset.paymentDanger === 'true' ? 'btn-danger' : 'btn-primary');
            modal.show(event.submitter);
            return;
        }
        delete form.dataset.paymentConfirmed;
        form.dataset.paymentSubmitting = 'true';
        form.setAttribute('aria-busy', 'true');
        form.querySelectorAll('button[type=submit]').forEach(function (button) {
            button.dataset.paymentOriginal = button.textContent;
            button.disabled = true;
            button.textContent = form.dataset.paymentBusy || 'Saving…';
        });
    });
    if (modal) {
        modalElement.querySelector('[data-payment-confirm-action]').addEventListener('click', function () {
            if (!pending || !valid(pending.form)) return;
            var action = pending;
            pending = null;
            action.form.dataset.paymentConfirmed = 'true';
            modal.hide();
            action.form.requestSubmit(action.button || undefined);
        });
        modalElement.addEventListener('hidden.bs.modal', function () {
            if (pending && pending.button) pending.button.focus();
            pending = null;
        });
    }
    window.addEventListener('pageshow', resetSubmissions);

    var checkout = root.querySelector('[data-payment-checkout]');
    if (checkout) {
        var button = checkout.querySelector('[data-payment-checkout-button]');
        var note = checkout.querySelector('[data-payment-method-note]');
        var resuming = button.textContent.trim() === 'Resume previous checkout';
        function updateMethod() {
            if (checkout.dataset.paymentSubmitting === 'true') return;
            var selected = checkout.querySelector('input[name=Method]:checked');
            var counter = selected && selected.value === 'pay_at_counter';
            button.textContent = resuming ? 'Resume previous checkout' : counter ? 'Confirm counter payment' : 'Continue to Stripe';
            checkout.dataset.paymentBusy = counter ? 'Confirming counter payment…' : 'Opening Stripe…';
            note.textContent = counter ? 'No online charge is made. Staff will confirm payment after receiving the money.' : 'Stripe opens next. Payment is confirmed only after verification.';
        }
        checkout.addEventListener('change', updateMethod);
        updateMethod();
    }
})();
