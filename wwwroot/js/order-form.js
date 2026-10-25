// The order page's "Your order" docket and running total. The − and + buttons are stepper.js.
// Both are only for display. The form sends dish ids and quantities, and the server takes the prices from the
// menu and works out the real total.
document.querySelectorAll('[data-order-form]').forEach(function (form) {
    const quantityInputs = form.querySelectorAll('input[data-price]');
    const lines = form.querySelector('[data-order-lines]');
    const nothingChosen = form.querySelector('[data-order-empty]');
    if (!lines) {
        return;
    }

    // A whole number between 0 and the input's max, whatever was typed.
    function quantityOf(input) {
        const quantity = Math.floor(Number(input.value)) || 0;
        return Math.min(Math.max(quantity, 0), Number(input.max));
    }

    // Amounts are added up in sen (cents), so 3 x 3.80 is exactly 11.40.
    function money(sen) {
        return 'RM ' + (sen / 100).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    // One docket line: "2 ×", the dish, its amount. textContent, so a dish name is never read as HTML.
    function docketLine(quantity, name, sen) {
        const line = document.createElement('li');
        line.className = 'rr-docket__line';
        [['rr-docket__qty', quantity + ' ×'], ['', name], ['rr-docket__amount', money(sen)]].forEach(function (part) {
            const span = document.createElement('span');
            span.className = part[0];
            span.textContent = part[1];
            line.append(span);
        });
        return line;
    }

    function update() {
        let totalSen = 0;
        let count = 0;
        lines.replaceChildren();

        quantityInputs.forEach(function (input) {
            const quantity = quantityOf(input);
            const sen = quantity * Math.round(Number(input.dataset.price) * 100);
            input.closest('[data-dish]').classList.toggle('is-chosen', quantity > 0);
            if (quantity > 0) {
                lines.append(docketLine(quantity, input.dataset.name, sen));
                totalSen += sen;
                count += quantity;
            }
        });

        nothingChosen.hidden = count > 0;
        form.querySelectorAll('[data-order-total]').forEach(function (element) {
            element.textContent = money(totalSen);
        });
        form.querySelectorAll('[data-order-count]').forEach(function (element) {
            element.textContent = count === 1 ? '1 item' : count + ' items';
        });
    }

    quantityInputs.forEach(function (input) {
        input.addEventListener('input', update);
    });

    // The docket and totals only make sense with this script running, so they start hidden.
    form.querySelectorAll('[data-needs-script]').forEach(function (element) {
        element.hidden = false;
    });
    update();
});
