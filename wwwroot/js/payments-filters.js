// Payment list and report filters: only show the From/To dates when "Custom range" is selected.
(function () {
    var preset = document.querySelector('[data-preset-select]');
    var boxes = document.querySelectorAll('[data-custom-range]');
    if (!preset) { return; }

    function toggle() {
        boxes.forEach(function (box) { box.style.display = preset.value === 'Custom' ? '' : 'none'; });
    }

    preset.addEventListener('change', toggle);
    toggle();
})();