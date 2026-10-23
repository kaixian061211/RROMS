// Counter "collect payment" page: works out the change for cash and hides the cash box for other methods.
(function () {
    var tendered = document.getElementById('tendered');
    var change = document.getElementById('change');
    var cashBox = document.getElementById('cash-box');
    var radios = document.querySelectorAll('input[name="Method"]');
    if (!tendered || !change || !cashBox) { return; }

    var due = parseFloat(tendered.dataset.amount);

    function updateChange() {
        var given = parseFloat(tendered.value);
        var diff = isNaN(given) ? 0 : Math.max(given - due, 0);
        change.textContent = 'RM ' + diff.toFixed(2);
    }

    function updateVisibility() {
        var selected = document.querySelector('input[name="Method"]:checked');
        cashBox.style.display = selected && selected.value === 'cash' ? '' : 'none';
    }

    tendered.addEventListener('input', updateChange);
    radios.forEach(function (r) { r.addEventListener('change', updateVisibility); });
    updateVisibility();
})();