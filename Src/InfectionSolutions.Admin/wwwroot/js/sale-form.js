// Formulario de nueva venta: agrega/quita lineas y calcula una vista previa
// del subtotal, IVA y total. El servidor recalcula todo al guardar.
(function () {
    const form = document.getElementById('sale-form');
    if (!form) {
        return;
    }

    const tbody = document.getElementById('sale-lines');
    const template = document.getElementById('line-template');
    const taxRate = parseFloat(form.dataset.taxRate || '0');
    const currency = new Intl.NumberFormat('es-CO', { style: 'currency', currency: 'COP', minimumFractionDigits: 2 });

    // Clave unica para Lines.Index: no importa que haya huecos al quitar filas.
    let nextIndex = tbody.querySelectorAll('.sale-line').length;

    function recalculate() {
        let subtotal = 0;

        tbody.querySelectorAll('.sale-line').forEach(function (row) {
            const option = row.querySelector('.line-product').selectedOptions[0];
            const quantityInput = row.querySelector('.line-quantity');
            const quantity = parseInt(quantityInput.value, 10) || 0;
            const price = option && option.dataset.price ? parseFloat(option.dataset.price) : 0;
            const stock = option && option.dataset.stock ? parseInt(option.dataset.stock, 10) : null;
            const lineTotal = Math.round(price * quantity * 100) / 100;

            row.querySelector('.line-price').textContent = price ? currency.format(price) : '—';
            row.querySelector('.line-total').textContent = price ? currency.format(lineTotal) : '—';

            const exceeds = stock !== null && quantity > stock;
            quantityInput.classList.toggle('is-invalid', exceeds);
            quantityInput.title = exceeds ? 'Supera el stock disponible (' + stock + ')' : '';

            subtotal += lineTotal;
        });

        const tax = Math.round(subtotal * taxRate * 100) / 100;
        document.getElementById('sum-subtotal').textContent = currency.format(subtotal);
        document.getElementById('sum-tax').textContent = currency.format(tax);
        document.getElementById('sum-total').textContent = currency.format(subtotal + tax);
    }

    function addLine() {
        const html = template.innerHTML.replaceAll('__i__', String(nextIndex++));
        tbody.insertAdjacentHTML('beforeend', html);
        recalculate();
    }

    document.getElementById('add-line').addEventListener('click', addLine);

    tbody.addEventListener('click', function (event) {
        const button = event.target.closest('.remove-line');
        if (!button) {
            return;
        }

        // Siempre queda al menos una fila para no enviar una venta vacia.
        if (tbody.querySelectorAll('.sale-line').length > 1) {
            button.closest('.sale-line').remove();
        } else {
            const row = button.closest('.sale-line');
            row.querySelector('.line-product').value = '';
            row.querySelector('.line-quantity').value = 1;
        }

        recalculate();
    });

    tbody.addEventListener('change', recalculate);
    tbody.addEventListener('input', recalculate);

    recalculate();
})();
