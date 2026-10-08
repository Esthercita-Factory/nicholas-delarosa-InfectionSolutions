// Modo claro/oscuro. El tema inicial lo pone _ThemeHead antes de pintar; aqui
// solo se cambia y se recuerda. Con View Transitions, el tema nuevo se expande
// en un circulo desde el boton; sin ellas (o con movimiento reducido) cambia directo.
(function () {
    'use strict';

    const raiz = document.documentElement;
    const sinMovimiento = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    function temaActual() {
        return raiz.getAttribute('data-theme') === 'dark' ? 'dark' : 'light';
    }

    function etiquetar() {
        const texto = temaActual() === 'dark' ? 'Cambiar a modo claro' : 'Cambiar a modo oscuro';
        document.querySelectorAll('[data-theme-toggle]').forEach(function (b) {
            b.setAttribute('aria-label', texto);
            b.setAttribute('aria-pressed', temaActual() === 'dark' ? 'true' : 'false');
        });
    }

    function aplicar(tema) {
        raiz.setAttribute('data-theme', tema);
        raiz.setAttribute('data-bs-theme', tema);
        try { localStorage.setItem('is-tema', tema); } catch (e) { /* sin almacenamiento: solo esta visita */ }
        etiquetar();
    }

    function alternar(evento) {
        const siguiente = temaActual() === 'dark' ? 'light' : 'dark';

        if (!document.startViewTransition || sinMovimiento) {
            aplicar(siguiente);
            return;
        }

        // El circulo crece desde el centro del boton hasta cubrir la esquina mas lejana.
        const boton = evento.currentTarget.getBoundingClientRect();
        const x = boton.left + boton.width / 2;
        const y = boton.top + boton.height / 2;
        const radio = Math.hypot(Math.max(x, window.innerWidth - x), Math.max(y, window.innerHeight - y));

        raiz.classList.add('is-theme-switch');
        const transicion = document.startViewTransition(function () { aplicar(siguiente); });

        transicion.ready.then(function () {
            raiz.animate(
                { clipPath: ['circle(0px at ' + x + 'px ' + y + 'px)', 'circle(' + radio + 'px at ' + x + 'px ' + y + 'px)'] },
                { duration: 650, easing: 'cubic-bezier(.16, 1, .3, 1)', pseudoElement: '::view-transition-new(root)' }
            );
        });

        transicion.finished.finally(function () { raiz.classList.remove('is-theme-switch'); });
    }

    document.querySelectorAll('[data-theme-toggle]').forEach(function (b) { b.addEventListener('click', alternar); });
    etiquetar();

    // Si nunca eligio un tema, sigue al sistema cuando este cambia.
    window.matchMedia('(prefers-color-scheme: dark)').addEventListener('change', function (e) {
        let guardado = null;
        try { guardado = localStorage.getItem('is-tema'); } catch (err) { guardado = null; }
        if (!guardado) {
            raiz.setAttribute('data-theme', e.matches ? 'dark' : 'light');
            raiz.setAttribute('data-bs-theme', e.matches ? 'dark' : 'light');
            etiquetar();
        }
    });
})();
