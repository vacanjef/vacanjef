// HU-003: agregar y quitar filas de salud. Va en un archivo aparte (no inline)
// para no chocar con la política de seguridad de contenido.
(() => {
    const contenedor = document.getElementById('registros-salud');
    const plantilla = document.getElementById('plantilla-registro-salud');
    const botonAgregar = document.getElementById('agregar-registro-salud');
    if (!contenedor || !plantilla || !botonAgregar) return;

    // Los índices deben quedar seguidos (0, 1, 2...) para que MVC enlace la lista
    const renumerar = () => {
        contenedor.querySelectorAll('.registro-salud').forEach((fila, i) => {
            fila.querySelectorAll('[data-campo]').forEach(campo => {
                campo.name = `RegistrosSalud[${i}].${campo.dataset.campo}`;
            });
        });
    };

    botonAgregar.addEventListener('click', () => {
        contenedor.appendChild(plantilla.content.cloneNode(true));
        renumerar();
    });

    contenedor.addEventListener('click', evento => {
        const boton = evento.target.closest('.quitar-registro-salud');
        if (!boton) return;
        boton.closest('.registro-salud').remove();
        renumerar();
    });

    renumerar();
})();
