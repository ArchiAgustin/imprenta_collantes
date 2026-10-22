(() => {
    const form = document.querySelector('[data-quote-editor]');
    if (!form) return;

    const lines = form.querySelector('#quote-lines');
    const template = form.querySelector('#quote-line-template');
    if (!lines || !template) return;
    const addButton = form.querySelector('[data-add-line]');
    const taxRate = Number(form.dataset.taxRate || 0.18);
    const currency = new Intl.NumberFormat('es-PE', { style: 'currency', currency: 'PEN' });

    const money = (amount) => currency.format(amount);

    function refreshValidation() {
        if (!window.jQuery || !jQuery.validator || !jQuery.validator.unobtrusive) return;
        const $form = jQuery(form);
        $form.removeData('validator').removeData('unobtrusiveValidation');
        jQuery.validator.unobtrusive.parse($form);
    }

    function renumberLines() {
        const rows = Array.from(lines.querySelectorAll('[data-quote-line]'));
        rows.forEach((row, index) => {
            row.querySelector('[data-line-label]').textContent = `Producto ${index + 1}`;
            const removeButton = row.querySelector('[data-remove-line]');
            removeButton.setAttribute('aria-label', `Quitar producto ${index + 1}`);

            row.querySelectorAll('[name], [id], [for], [data-valmsg-for], [aria-describedby]').forEach((element) => {
                for (const attribute of ['name', 'id', 'for', 'data-valmsg-for', 'aria-describedby']) {
                    const value = element.getAttribute(attribute);
                    if (value) element.setAttribute(attribute, value.replace(/Lineas\[\d+\]/g, `Lineas[${index}]`).replace(/Lineas_\d+__/g, `Lineas_${index}__`));
                }
            });
        });

        form.dataset.nextIndex = String(rows.length);
        lines.querySelectorAll('[data-remove-line]').forEach((button) => {
            button.disabled = rows.length === 1;
        });
        refreshValidation();
        recalculate();
    }

    function recalculate() {
        let subtotal = 0;
        lines.querySelectorAll('[data-quote-line]').forEach((row) => {
            const quantity = Number(row.querySelector('.quote-quantity').value) || 0;
            const price = Number(row.querySelector('.quote-price').value) || 0;
            const amount = Math.round((quantity * price + Number.EPSILON) * 100) / 100;
            row.querySelector('[data-line-total]').textContent = money(amount);
            subtotal += amount;
        });

        subtotal = Math.round((subtotal + Number.EPSILON) * 100) / 100;
        const igv = Math.round((subtotal * taxRate + Number.EPSILON) * 100) / 100;
        form.querySelector('[data-subtotal]').textContent = money(subtotal);
        form.querySelector('[data-igv]').textContent = money(igv);
        form.querySelector('[data-total]').textContent = money(subtotal + igv);
    }

    addButton?.addEventListener('click', () => {
        const index = Number(form.dataset.nextIndex || lines.children.length);
        const row = template.content.firstElementChild.cloneNode(true);
        row.querySelectorAll('[name], [id], [for], [data-valmsg-for], [aria-describedby]').forEach((element) => {
                for (const attribute of ['name', 'id', 'for', 'data-valmsg-for', 'aria-describedby']) {
                    const value = element.getAttribute(attribute);
                    if (value) element.setAttribute(attribute, value.replaceAll('___index___', `_${index}__`).replaceAll('__index__', String(index)));
                }
        });
        lines.append(row);
        renumberLines();
        row.querySelector('select')?.focus();
    });

    lines.addEventListener('click', (event) => {
        const button = event.target.closest('[data-remove-line]');
        if (!button || lines.querySelectorAll('[data-quote-line]').length <= 1) return;
        button.closest('[data-quote-line]').remove();
        renumberLines();
    });

    lines.addEventListener('change', (event) => {
        if (event.target.matches('.quote-product-select')) {
            const selected = event.target.selectedOptions[0];
            const price = selected?.dataset.unitPrice;
            if (price !== undefined) event.target.closest('[data-quote-line]').querySelector('.quote-price').value = Number(price).toFixed(2);
        }
        recalculate();
    });

    lines.addEventListener('input', (event) => {
        if (event.target.matches('.quote-quantity, .quote-price')) recalculate();
    });

    renumberLines();
})();
