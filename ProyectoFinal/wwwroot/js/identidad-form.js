(() => {
    const tipo = document.querySelector('[data-document-type]');
    const ubigeo = document.querySelector('[data-ubigeo-selector]');
    if (!ubigeo) return;
    const departamento = ubigeo.querySelector('[data-departamento]');
    const provincia = ubigeo.querySelector('[data-provincia]');
    const distrito = ubigeo.querySelector('[data-distrito]');
    const estado = ubigeo.querySelector('[data-ubigeo-status]');

    function actualizarRequisitos() {
        const esRuc = !tipo || tipo.selectedOptions[0]?.dataset.codigo === '6';
        distrito.required = esRuc;
        document.querySelector('#Direccion').required = esRuc;
        const texto = esRuc ? '' : '(opcional para DNI)';
        for (const label of document.querySelectorAll('[data-address-optional], [data-ubigeo-optional]')) label.textContent = texto;
        const documento = document.querySelector('#NumeroDocumento');
        if (documento && tipo) documento.maxLength = tipo.selectedOptions[0]?.dataset.codigo === '1' ? 8 : 11;
    }

    function limpiar(select, etiqueta) {
        select.replaceChildren(new Option(etiqueta, ''));
        select.disabled = true;
    }

    async function cargar(parent, select, etiqueta) {
        limpiar(select, etiqueta);
        if (!parent) return;
        estado.hidden = true;
        try {
            const response = await fetch(`${ubigeo.dataset.url}?parent=${encodeURIComponent(parent)}`, { credentials: 'same-origin' });
            if (!response.ok) throw new Error('No se pudo cargar el ubigeo');
            const items = await response.json();
            if ((select === provincia && departamento.value !== parent) || (select === distrito && provincia.value !== parent)) return;
            for (const item of items) select.add(new Option(item.nombre, item.codigo));
            select.disabled = false;
        } catch {
            estado.hidden = false;
        }
    }

    departamento.addEventListener('change', () => {
        limpiar(distrito, 'Selecciona distrito');
        cargar(departamento.value, provincia, 'Selecciona provincia');
    });
    provincia.addEventListener('change', () => cargar(provincia.value, distrito, 'Selecciona distrito'));
    tipo?.addEventListener('change', actualizarRequisitos);
    actualizarRequisitos();
})();
