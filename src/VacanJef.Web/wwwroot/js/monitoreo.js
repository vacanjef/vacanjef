// HU-007: monitoreo en vivo por WebRTC.
// El navegador arma la oferta, la envía a VacanJef (/Monitoreo/Senal con el token temporal) y VacanJef
// la reenvía a MediaMTX. El video llega directo de MediaMTX. Si no hay transmisión, se muestra el respaldo
// (mensaje y última fotografía, CA-HU-007-02). Sin scripts en línea, por la CSP 'self'.
(() => {
    'use strict';

    const contenedor = document.getElementById('monitoreo');
    if (!contenedor) return;

    const video = document.getElementById('video-en-vivo');
    const estado = document.getElementById('estado-monitoreo');
    const respaldo = document.getElementById('respaldo-monitoreo');
    const urlSenal = contenedor.dataset.senal;
    const tiempoMaximoMs = 10000;

    let conectado = false;
    let conexion = null;

    function mostrarRespaldo(mensaje) {
        if (conectado) return;
        estado.textContent = mensaje;
        video.hidden = true;
        respaldo.hidden = false;
        if (conexion) conexion.close();
    }

    function esperarCandidatos(pc) {
        if (pc.iceGatheringState === 'complete') return Promise.resolve();
        return new Promise((resolver) => {
            const limite = setTimeout(resolver, 2000);
            pc.addEventListener('icegatheringstatechange', () => {
                if (pc.iceGatheringState === 'complete') {
                    clearTimeout(limite);
                    resolver();
                }
            });
        });
    }

    async function iniciar() {
        const inicio = performance.now();
        conexion = new RTCPeerConnection();
        conexion.addTransceiver('video', { direction: 'recvonly' });

        conexion.addEventListener('track', (evento) => {
            if (!video.srcObject) {
                video.srcObject = evento.streams[0] || new MediaStream([evento.track]);
            }
        });

        video.addEventListener('playing', () => {
            conectado = true;
            const segundos = ((performance.now() - inicio) / 1000).toFixed(1);
            estado.textContent = `En vivo. La transmisión inició en ${segundos} s.`;
        }, { once: true });

        conexion.addEventListener('connectionstatechange', () => {
            if (conexion.connectionState === 'failed') {
                conectado = false;
                mostrarRespaldo('Se perdió la conexión con la cámara.');
            }
        });

        const oferta = await conexion.createOffer();
        await conexion.setLocalDescription(oferta);
        await esperarCandidatos(conexion);

        const respuesta = await fetch(urlSenal, {
            method: 'POST',
            headers: { 'Content-Type': 'application/sdp' },
            body: conexion.localDescription.sdp,
            credentials: 'same-origin',
            redirect: 'error',
        });

        if (!respuesta.ok) {
            mostrarRespaldo(respuesta.status === 403
                ? 'El acceso a esta cámara ya no está permitido. Recargue la página.'
                : 'La cámara no está disponible en este momento.');
            return;
        }

        await conexion.setRemoteDescription({ type: 'answer', sdp: await respuesta.text() });
    }

    setTimeout(() => mostrarRespaldo('La cámara no respondió a tiempo.'), tiempoMaximoMs);
    iniciar().catch(() => mostrarRespaldo('No fue posible abrir la transmisión.'));
})();
