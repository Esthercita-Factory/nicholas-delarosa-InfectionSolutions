// Interacciones del panel. Sin dependencias: cada bloque se activa solo si
// su marcado existe en la pagina. Todo respeta prefers-reduced-motion.
(function () {
    'use strict';

    const sinMovimiento = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    // ---------- Toasts: los de exito se cierran solos (pausa al pasar el mouse) ----------
    function cerrarToast(toast) {
        if (toast.classList.contains('is-leaving')) return;
        toast.classList.add('is-leaving');
        toast.addEventListener('animationend', function () { toast.remove(); }, { once: true });
        if (sinMovimiento) toast.remove();
    }

    document.querySelectorAll('[data-toast]').forEach(function (toast) {
        toast.querySelector('[data-toast-close]')?.addEventListener('click', function () { cerrarToast(toast); });

        const tiempo = parseInt(toast.dataset.toastTimeout || '0', 10);
        const barra = toast.querySelector('.toast-progress');
        if (tiempo > 0 && barra) {
            toast.style.setProperty('--toast-tiempo', tiempo + 'ms');
            barra.addEventListener('animationend', function () { cerrarToast(toast); });
        }
    });

    // ---------- Barra superior: sombra al desplazarse ----------
    const topbar = document.querySelector('.topbar');
    if (topbar) {
        const marcar = function () { topbar.classList.toggle('is-scrolled', window.scrollY > 4); };
        window.addEventListener('scroll', marcar, { passive: true });
        marcar();
    }

    // ---------- Riel: el indicador se desliza desde la seccion anterior ----------
    const nav = document.querySelector('[data-rail-nav]');
    const indicador = document.querySelector('[data-rail-indicator]');
    const activo = nav?.querySelector('.rail-link.is-active');
    if (nav && indicador && activo) {
        const CLAVE = 'is-rail-anterior';
        const destino = { top: activo.offsetTop, height: activo.offsetHeight };
        let origen = null;
        try { origen = JSON.parse(sessionStorage.getItem(CLAVE) || 'null'); } catch (e) { origen = null; }

        const ubicar = function (pos) {
            indicador.style.transform = 'translateY(' + pos.top + 'px)';
            indicador.style.height = pos.height + 'px';
        };

        if (origen && !sinMovimiento && origen.top !== destino.top) {
            indicador.style.transition = 'none';
            ubicar(origen);
            indicador.classList.add('is-ready');
            requestAnimationFrame(function () {
                requestAnimationFrame(function () {
                    indicador.style.transition = '';
                    ubicar(destino);
                });
            });
        } else {
            ubicar(destino);
            indicador.classList.add('is-ready');
        }

        try { sessionStorage.setItem(CLAVE, JSON.stringify(destino)); } catch (e) { /* almacenamiento no disponible */ }
        window.addEventListener('resize', function () { ubicar({ top: activo.offsetTop, height: activo.offsetHeight }); });
    }

    // ---------- Cifras que cuentan hacia arriba (data-count="12345" data-format="money|int") ----------
    const pesos = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
    function formatear(valor, formato) {
        return formato === 'money' ? '$ ' + pesos.format(valor) : pesos.format(valor);
    }

    document.querySelectorAll('[data-count]').forEach(function (el) {
        const final = parseFloat(el.dataset.count) || 0;
        const formato = el.dataset.format || 'int';
        if (sinMovimiento || final === 0) {
            el.textContent = formatear(final, formato);
            return;
        }

        const duracion = 1100;
        const inicio = performance.now() + 150;
        el.textContent = formatear(0, formato);
        function paso(ahora) {
            const t = Math.min(Math.max((ahora - inicio) / duracion, 0), 1);
            const suave = 1 - Math.pow(1 - t, 4);
            el.textContent = formatear(Math.round(final * suave), formato);
            if (t < 1) requestAnimationFrame(paso);
        }
        requestAnimationFrame(paso);
    });

    // ---------- Grafica: tooltip por barra ----------
    document.querySelectorAll('[data-chart]').forEach(function (chart) {
        const tip = chart.querySelector('[data-chart-tip]');
        const barras = chart.querySelectorAll('.bar');

        function mostrar(hit) {
            const i = hit.dataset.index;
            barras.forEach(function (b) { b.classList.toggle('is-hover', b.dataset.index === i); });
            chart.classList.add('is-hovering');

            tip.querySelector('[data-tip-day]').textContent = hit.dataset.day;
            tip.querySelector('[data-tip-total]').textContent = hit.dataset.total;
            tip.querySelector('[data-tip-count]').textContent = hit.dataset.count;

            const barra = chart.querySelector('.bar[data-index="' + i + '"]');
            const caja = chart.getBoundingClientRect();
            const rect = barra.getBoundingClientRect();
            tip.style.left = (rect.left - caja.left + rect.width / 2) + 'px';
            tip.style.top = (rect.top - caja.top) + 'px';
            tip.classList.add('is-visible');
        }

        function ocultar() {
            chart.classList.remove('is-hovering');
            barras.forEach(function (b) { b.classList.remove('is-hover'); });
            tip.classList.remove('is-visible');
        }

        chart.querySelectorAll('.hit').forEach(function (hit) {
            hit.addEventListener('mouseenter', function () { mostrar(hit); });
            hit.addEventListener('focus', function () { mostrar(hit); });
            hit.addEventListener('blur', ocultar);
        });
        chart.querySelector('svg').addEventListener('mouseleave', ocultar);
    });

    // ---------- Paleta de comandos (Ctrl K) ----------
    const paleta = document.querySelector('[data-palette]');
    if (paleta) {
        const entrada = paleta.querySelector('[data-palette-input]');
        const lista = paleta.querySelector('[data-palette-list]');
        const items = Array.from(lista.querySelectorAll('.palette-item'));
        const grupos = Array.from(lista.querySelectorAll('.palette-group'));
        let visibles = [];
        let seleccionado = 0;
        let ultimoFoco = null;

        const normalizar = function (texto) {
            return texto.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '');
        };

        function filtrar() {
            const termino = entrada.value.trim();
            const buscado = normalizar(termino);

            items.forEach(function (item) {
                let visible;
                if (item.dataset.group === 'search') {
                    visible = termino.length > 0;
                    item.querySelector('[data-term]').textContent = '“' + termino + '”';
                } else {
                    const texto = normalizar(item.textContent + ' ' + (item.dataset.keywords || ''));
                    visible = buscado.length === 0 || texto.includes(buscado);
                }
                item.hidden = !visible;
            });

            // Un titulo de grupo solo se ve si tiene items visibles debajo.
            grupos.forEach(function (grupo) {
                let siguiente = grupo.nextElementSibling;
                let alguno = false;
                while (siguiente && !siguiente.classList.contains('palette-group')) {
                    if (!siguiente.hidden) alguno = true;
                    siguiente = siguiente.nextElementSibling;
                }
                grupo.hidden = !alguno;
            });

            visibles = items.filter(function (i) { return !i.hidden; });
            seleccionar(0);
        }

        function seleccionar(indice) {
            if (visibles.length === 0) return;
            seleccionado = (indice + visibles.length) % visibles.length;
            visibles.forEach(function (item, i) {
                const es = i === seleccionado;
                item.classList.toggle('is-selected', es);
                item.setAttribute('aria-selected', es ? 'true' : 'false');
            });
            visibles[seleccionado].scrollIntoView({ block: 'nearest' });
        }

        function ejecutar(item) {
            if (!item) return;
            if (item.dataset.searchUrl) {
                window.location.href = item.dataset.searchUrl + '?q=' + encodeURIComponent(entrada.value.trim());
            } else if (item.dataset.url) {
                window.location.href = item.dataset.url;
            }
        }

        function abrir() {
            if (!paleta.hidden) return;
            ultimoFoco = document.activeElement;
            paleta.hidden = false;
            paleta.classList.remove('is-closing');
            entrada.value = '';
            filtrar();
            entrada.focus();
            document.body.style.overflow = 'hidden';
        }

        function cerrar() {
            if (paleta.hidden) return;
            const terminar = function () {
                paleta.hidden = true;
                paleta.classList.remove('is-closing');
                document.body.style.overflow = '';
                ultimoFoco?.focus?.();
            };
            if (sinMovimiento) { terminar(); return; }
            paleta.classList.add('is-closing');
            paleta.querySelector('.palette-panel').addEventListener('animationend', terminar, { once: true });
        }

        document.querySelectorAll('[data-palette-open]').forEach(function (b) { b.addEventListener('click', abrir); });
        paleta.querySelectorAll('[data-palette-close]').forEach(function (b) { b.addEventListener('click', cerrar); });

        document.addEventListener('keydown', function (e) {
            if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k') {
                e.preventDefault();
                paleta.hidden ? abrir() : cerrar();
            } else if (e.key === '/' && paleta.hidden && !/input|textarea|select/i.test(document.activeElement.tagName)) {
                e.preventDefault();
                abrir();
            }
        });

        entrada.addEventListener('input', filtrar);
        entrada.addEventListener('keydown', function (e) {
            if (e.key === 'ArrowDown') { e.preventDefault(); seleccionar(seleccionado + 1); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); seleccionar(seleccionado - 1); }
            else if (e.key === 'Enter') { e.preventDefault(); ejecutar(visibles[seleccionado]); }
            else if (e.key === 'Escape') { e.preventDefault(); cerrar(); }
        });

        items.forEach(function (item) {
            item.addEventListener('mousemove', function () {
                const i = visibles.indexOf(item);
                if (i >= 0 && i !== seleccionado) seleccionar(i);
            });
            item.addEventListener('click', function () { ejecutar(item); });
        });
    }

    // ---------- Boton de envio: estado "guardando" para evitar doble clic ----------
    document.querySelectorAll('form[method="post"]').forEach(function (form) {
        form.addEventListener('submit', function () {
            if (window.jQuery && jQuery(form).data('validator') && !jQuery(form).valid()) return;
            const boton = form.querySelector('button[type="submit"].btn-primary');
            if (!boton || boton.dataset.noBusy !== undefined) return;
            boton.disabled = true;
            boton.insertAdjacentHTML('afterbegin', '<span class="spinner-border spinner-border-sm" aria-hidden="true"></span>');
        });
    });
})();
