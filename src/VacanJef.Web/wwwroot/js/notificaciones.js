// HU-009 / CA-HU-009-03: notificaciones en tiempo real con SignalR.
// Si no hay conexión, la campana se actualiza al recargar la página.
// Los textos se ponen con textContent (nunca innerHTML) y sin estilos en línea (CSP).
(function () {
    "use strict";

    if (typeof signalR === "undefined") {
        return;
    }

    const contador = document.getElementById("vj-contador-notificaciones");

    function actualizarContador(noLeidas) {
        if (!contador) {
            return;
        }
        contador.textContent = noLeidas > 99 ? "99+" : String(noLeidas);
        contador.classList.toggle("d-none", noLeidas === 0);
    }

    function contenedorAvisos() {
        let contenedor = document.getElementById("vj-avisos");
        if (!contenedor) {
            contenedor = document.createElement("div");
            contenedor.id = "vj-avisos";
            contenedor.className = "toast-container position-fixed bottom-0 end-0 p-3";
            document.body.appendChild(contenedor);
        }
        return contenedor;
    }

    function mostrarAviso(notificacion) {
        const aviso = document.createElement("div");
        aviso.className = "toast";
        aviso.setAttribute("role", "status");
        aviso.setAttribute("aria-live", "polite");

        const cabecera = document.createElement("div");
        cabecera.className = "toast-header";
        const titulo = document.createElement("strong");
        titulo.className = "me-auto";
        titulo.textContent = notificacion.titulo;
        const cerrar = document.createElement("button");
        cerrar.type = "button";
        cerrar.className = "btn-close";
        cerrar.setAttribute("data-bs-dismiss", "toast");
        cerrar.setAttribute("aria-label", "Cerrar");
        cabecera.append(titulo, cerrar);

        const cuerpo = document.createElement("a");
        cuerpo.className = "toast-body d-block text-reset text-decoration-none";
        cuerpo.href = "/Notificaciones";
        cuerpo.textContent = notificacion.mensaje;

        aviso.append(cabecera, cuerpo);
        contenedorAvisos().appendChild(aviso);

        if (window.bootstrap && window.bootstrap.Toast) {
            aviso.addEventListener("hidden.bs.toast", function () { aviso.remove(); });
            new window.bootstrap.Toast(aviso, { delay: 8000 }).show();
        }
    }

    const conexion = new signalR.HubConnectionBuilder()
        .withUrl("/hubs/notificaciones")
        .withAutomaticReconnect()
        .build();

    conexion.on("notificacion", function (datos) {
        actualizarContador(datos.noLeidas);
        if (datos.notificacion) {
            mostrarAviso(datos.notificacion);
        }
    });

    conexion.start().catch(function () {
        // Sin tiempo real: no se muestra error al usuario
    });
})();
