(() => {
    const form = document.querySelector('[data-quote-editor]');
    if (!form) return;

    const lines = form.querySelector('#quote-lines');
    const template = form.querySelector('#quote-line-template');
    if (!lines || !template) return;
    const addButton = form.querySelector('[data-add-line]');
    const taxRate = Number(form.dataset.taxRate || 0.18);
    const currency = new Intl.NumberFormat('es-PE', { style: 'currency', currency: 'PEN' });
    const advice = form.querySelector('[data-price-guidance]');
    function markAdviceOutdated() {
        if (!advice) return;
        advice.querySelector('[data-price-guidance-stale]').hidden = false;
        advice.classList.add('is-outdated');
    }

    const money = (amount) => currency.format(amount);

    function refreshValidation() {
        if (!window.jQuery || !jQuery.validator || !jQuery.validator.unobtrusive) return;
        const $form = jQuery(form);
        $form.removeData('validator').removeData('unobtrusiveValidation');
        jQuery.validator.unobtrusive.parse($form);
    }

    function presentationLabel(row) {
        const product = row.querySelector('.quote-product-select')?.selectedOptions[0];
        const presentation = row.querySelector('.quote-presentation-select')?.selectedOptions[0];
        if (!product?.value) return 'presentación';
        if (product.dataset.esServicio === 'true') return 'servicio';
        return presentation?.value ? presentation.dataset.label || 'presentación'
            : product.dataset.baseUnit || 'unidad base';
    }

    function syncPresentations(row, chooseDefault = false) {
        const productSelect = row.querySelector('.quote-product-select');
        const presentationSelect = row.querySelector('.quote-presentation-select');
        if (!productSelect || !presentationSelect) return;
        const product = productSelect.selectedOptions[0];
        const productId = productSelect.value;
        const isService = product?.dataset.esServicio === 'true';
        const options = Array.from(presentationSelect.options);
        options.forEach(option => {
            const applies = !option.dataset.productId || option.dataset.productId === productId;
            option.hidden = !applies;
            option.disabled = !applies;
        });
        presentationSelect.disabled = !productId || isService;
        if (!productId || isService) {
            presentationSelect.value = '';
        } else {
            const current = options.find(option => option.value === presentationSelect.value && !option.disabled);
            if (chooseDefault || !current) {
                const base = options.find(option => !option.disabled && option.dataset.productId === productId
                    && option.dataset.isBase === 'true')
                    || options.find(option => !option.disabled && option.dataset.productId === productId
                        && Number(option.dataset.factor) === 1);
                presentationSelect.value = base?.value ?? '';
            }
        }
        const label = isService ? 'servicio' : presentationLabel(row);
        const quantityLabel = row.querySelector('[data-quantity-label]');
        const priceLabel = row.querySelector('[data-price-label]');
        if (quantityLabel) quantityLabel.textContent = `Cantidad (${label})`;
        if (priceLabel) priceLabel.textContent = `Precio por ${label} sin IGV`;
    }

    function setCatalogPresentationPrice(row) {
        const product = row.querySelector('.quote-product-select')?.selectedOptions[0];
        const price = Number(product?.dataset.unitPrice);
        const factor = Number(row.querySelector('.quote-presentation-select')?.selectedOptions[0]?.dataset.factor || 1);
        if (Number.isFinite(price) && Number.isFinite(factor))
            row.querySelector('.quote-price').value = (price * factor).toFixed(2);
        syncPresentations(row);
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
            syncPresentations(row);
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
        markAdviceOutdated();
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
        row.querySelector('.quote-product-select')?.focus();
    });

    lines.addEventListener('click', (event) => {
        const button = event.target.closest('[data-remove-line]');
        if (!button || lines.querySelectorAll('[data-quote-line]').length <= 1) return;
        markAdviceOutdated();
        button.closest('[data-quote-line]').remove();
        renumberLines();
    });

    lines.addEventListener('change', (event) => {
        if (event.target.matches('.quote-product-select')) {
            markAdviceOutdated();
            syncPresentations(event.target.closest('[data-quote-line]'), true);
            setCatalogPresentationPrice(event.target.closest('[data-quote-line]'));
        }
        if (event.target.matches('.quote-presentation-select')) {
            markAdviceOutdated();
            setCatalogPresentationPrice(event.target.closest('[data-quote-line]'));
        }
        recalculate();
    });

    lines.addEventListener('input', (event) => {
        if (event.target.matches('.quote-quantity, .quote-price')) { markAdviceOutdated(); recalculate(); }
    });
    form.querySelector('[name="ClienteId"]')?.addEventListener('change', markAdviceOutdated);

    renumberLines();
})();
