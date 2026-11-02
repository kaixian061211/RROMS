// Local payment-date filters. Values remain available when switching back to Custom.
(function () {
    'use strict';
    var root = document.querySelector('.payment-page');
    var preset = root && root.querySelector('[data-preset-select]');
    if (!preset) return;
    var ranges = root.querySelectorAll('[data-custom-range]');
    function toggle() {
        var custom = preset.value === 'Custom';
        ranges.forEach(function (range) {
            range.hidden = !custom;
            range.querySelectorAll('input').forEach(function (input) { input.disabled = !custom; });
        });
    }
    preset.addEventListener('change', toggle);
    toggle();
})();
