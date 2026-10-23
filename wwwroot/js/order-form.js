// The order page's + and − buttons and running total.
// The total here is only for display. The form sends dish ids and quantities, and the server
// takes the prices from the menu and works out the real total.
document.querySelectorAll('[data-order-form]').forEach(function (form) {
    const totalElement = form.querySelector('[data-order-total]');
    const quantityInputs = form.querySelectorAll('input[data-price]');
    if (!totalElement) {
        return;
    }

    // A whole number between 0 and the input's max, whatever was typed.
    function quantityOf(input) {
        const quantity = Math.floor(Number(input.value)) || 0;
        return Math.min(Math.max(quantity, 0), Number(input.max));
    }

    function updateTotal() {
        // Add up in sen (cents), so 3 x 3.80 is exactly 11.40.
        let totalSen = 0;
        quantityInputs.forEach(function (input) {
            totalSen += quantityOf(input) * Math.round(Number(input.dataset.price) * 100);
        });
        const total = totalSen / 100;
        totalElement.textContent = 'RM ' + total.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    form.querySelectorAll('[data-step]').forEach(function (button) {
        button.addEventListener('click', function () {
            const input = button.parentElement.querySelector('input[data-price]');
            const changed = quantityOf(input) + Number(button.dataset.step);
            input.value = Math.min(Math.max(changed, 0), Number(input.max));
            updateTotal();
        });
    });

    quantityInputs.forEach(function (input) {
        input.addEventListener('input', updateTotal);
    });

    totalElement.parentElement.classList.remove('d-none');
    updateTotal();
});
