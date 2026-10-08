// Cierra solas las alertas de exito despues de unos segundos.
document.addEventListener('DOMContentLoaded', function () {
    document.querySelectorAll('.alert-success.alert-dismissible').forEach(function (alerta) {
        setTimeout(function () {
            bootstrap.Alert.getOrCreateInstance(alerta).close();
        }, 6000);
    });
});
