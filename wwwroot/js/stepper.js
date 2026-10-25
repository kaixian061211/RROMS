// The − and + buttons of every stepper (rr-stepper, see /Dev/StyleGuide).
// The number box stays a normal field that the form sends; the buttons add or take away one, within its
// min and max, and tell the page the number changed, as if it had been typed.
document.querySelectorAll('[data-stepper]').forEach(function (stepper) {
    const input = stepper.querySelector('input');

    stepper.querySelectorAll('[data-step]').forEach(function (button) {
        button.addEventListener('click', function () {
            const min = input.min === '' ? -Infinity : Number(input.min);
            const max = input.max === '' ? Infinity : Number(input.max);
            const changed = (Math.floor(Number(input.value)) || 0) + Number(button.dataset.step);
            input.value = Math.min(Math.max(changed, min), max);
            input.dispatchEvent(new Event('input', { bubbles: true }));

            // A field that is showing a validation message is checked again straight away.
            if (input.classList.contains('input-validation-error') && window.jQuery && window.jQuery.fn.valid) {
                window.jQuery(input).valid();
            }
        });
    });
});
