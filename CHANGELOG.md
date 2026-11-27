# Registro de cambios

Todas las versiones de VacanJef. Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y [versionado semántico](https://semver.org/lang/es/).

## [0.4.0] - 2026-11-27 · Estadías, notificaciones y gestión de reservas

Cuarta versión. Completa el ciclo de una estadía: el administrador gestiona las reservas, el cuidador confirma el ingreso y la salida con la cámara asignada, y el dueño recibe notificaciones en tiempo real. Agrega respaldo y restauración probados y pruebas de integración de la aplicación completa.

### Agregado

- **HU-006** Gestión de reservas por el administrador: filtros, cancelación y ocupación por día (PR #65).
- **HU-014** Estadías: el cuidador o el administrador confirman el ingreso (la reserva pasa a Activa) y la salida (Finalizada), y asignan una cámara libre; gestión de cámaras por el administrador. Cada acción queda en Auditoría (PR #71).
- **HU-009** Notificaciones en la plataforma: reserva confirmada (dueño y administradores) y observación registrada o corregida (dueño, sin el detalle), en tiempo real con SignalR, con campana de no leídas y aviso emergente; marcar una o todas como leídas. El envío por correo queda para V1.1 (PR #62).
- **TT-011** Respaldo y restauración: herramienta `herramientas/respaldo/respaldo.cs` que respalda la base local y las fotos y prueba la restauración en una base aparte, y guía `docs/respaldo-y-restauracion.md` con local, Docker y Azure (PR #67).
- **TT-012** Pruebas de integración de la aplicación completa: cabeceras de seguridad, rutas protegidas, CSRF, cookie de sesión, roles y hub de notificaciones (PR #69).
- **TT-003** Guía de despliegue en Azure `docs/despliegue/azure.md` y prueba de la URL pública con evidencias (PR #72).

### Cambiado

- Acciones de GitHub actualizadas a Node.js 24: checkout v6, setup-dotnet v5, upload-artifact v6 y azure/login v3 (PR #63).

### Base de datos

- Migración `HU009Notificaciones` (título, mensaje, enlace y fecha de lectura de las notificaciones). Ya aplicada en Azure SQL antes de esta versión.

### Pendiente para el MVP (Hito 4)

HU-007 monitoreo en vivo.

## [0.3.0] - 2026-11-26 · Reservas, observaciones e historial

Tercera versión. El dueño reserva estadías para su mascota, el personal registra observaciones durante la estadía y el dueño consulta el historial de salud y de cuidado. Sin migraciones nuevas: la base de datos de Azure no requiere cambios.

### Agregado

- **HU-005** Reserva de estadía por el dueño: disponibilidad según el cupo diario (RN-003), costo calculado por noche según el servicio (RN-007) y sin reservas cruzadas para la misma mascota (PR #51).
- **HU-008** Observaciones de salud, comportamiento, alimentación y actividad por el cuidador o el administrador, solo durante una estadía activa; las correcciones no borran la observación original, RN-006 (PR #53).
- **HU-013** Historial de la mascota para el dueño: información de salud y observaciones agrupadas por estadía, con sus correcciones (PR #55).
- **TT-009** Ambientes separados: guía `docs/ambientes.md` y pruebas por ambiente (PR #48).
- **TT-010** Datos semilla ficticios de demostración, solo en Desarrollo y activados por variable de entorno, con guía en `docs/datos-semilla.md` (PR #58).

### Corregido

- Script de la cámara de prueba con tamaño y fps explícitos para webcams USB, TT-001 (PR #49).
- Plurales en Mis mascotas: "1 vacuna", "1 alergia", "1 medicamento" (PR #53).

### Pendiente para el MVP (Hito 4)

HU-014 ingreso y salida (en curso), HU-006, HU-009 notificaciones y HU-007 monitoreo en vivo.

## [0.2.0] - 2026-11-22 · Administración, seguridad y despliegue en Azure

Segunda versión. Agrega la administración de usuarios, el cambio de contraseña, el panel de auditoría, la observabilidad de la aplicación y el despliegue automático en Azure.

### Agregado

- **HU-015** Gestión de usuarios por el administrador: crear cuentas de cuidador y administrador, cambiar rol, activar o desactivar y desbloquear; primer administrador desde variables de entorno (PR #30).
- **HU-016** Cambio de contraseña obligatorio para cuentas nuevas o restablecidas, y restablecimiento por el administrador (PR #36).
- **HU-017** Panel de auditoría con filtros y atención de solicitudes de supresión de datos (Ley 1581) (PR #40).
- **TT-008** Health check en `/salud` (aplicación y base de datos) y registros con Serilog en consola y archivo diario, sin datos personales (PR #33).
- **TT-003** Despliegue en Azure: reintentos automáticos de conexión con Azure SQL (PR #34), reconocimiento de HTTPS e IP real detrás del proxy de App Service (PR #42) y workflow de despliegue en App Service con OIDC, sin contraseñas, que verifica `/salud` (PR #44).
- Plantillas de PR e issues según la Guía de Estructura y Evidencias (PR #32).

### Corregido

- El registro de dueños funciona con los reintentos de Azure SQL (PR #38).
- README con el instructor actual del proyecto (PR #39).

### Pendiente para el MVP (Hito 4)

HU-005 reservas, HU-014, HU-006, HU-008 observaciones, HU-013, HU-009 notificaciones y HU-007 monitoreo en vivo.

## [0.1.0] - 2026-11-17 · Prototipo (Hito 3)

Primera versión entregable. Incluye el acceso de dueños, el registro de mascotas con su salud y foto, la protección de datos personales y el entorno de desarrollo completo.

### Agregado

- **HU-001** Registro de dueño con validaciones y autorización de tratamiento de datos (PR #4).
- **HU-002** Inicio de sesión con bloqueo temporal de 15 minutos tras 5 intentos fallidos, RN-005 (PR #11).
- **HU-003** Registro de mascota con historial de salud (PR #18).
- **HU-004** Foto de la mascota: cargar, ver y cambiar; solo JPG o PNG de hasta 5 MB, RN-008 (PR #25).
- **TT-004** Protocolo de protección de datos personales (Ley 1581 de 2012): página "Mis datos" y política de tratamiento (PR #23).
- **TT-002** Modelo de datos, migración inicial e inmutabilidad de observaciones, RN-006 (PR #2).
- **TT-001** Prueba de concepto de streaming de cámaras y decisión ADR-005 (PR #6).
- Seguridad base: ASP.NET Core Identity, contraseñas robustas, cookie segura, protección CSRF y cabeceras de seguridad con CSP (PR #1, #4).
- Sistema de diseño: paleta con primario `#0B6E7F`, tipografía Inter, layout con menú por rol y guía de estilos (PR #12).
- Integración continua con GitHub Actions: compilación, pruebas y revisión de dependencias vulnerables (PR #3).
- Cobertura de pruebas en el CI con reporte descargable (PR #16).
- Entorno con Docker: web, migraciones y SQL Server con `docker compose` (PR #21).
- README con requisitos, configuración inicial y problemas frecuentes (PR #14).

### Corregido

- La conexión por defecto apunta a la base `VacanJefDB` para no chocar con la del prototipo anterior (PR #8).

### Pendiente para el MVP (Hito 4)

HU-005 reservas, HU-014, HU-006, HU-008 observaciones, HU-013, HU-009 notificaciones, HU-007 monitoreo en vivo y el despliegue en la nube (TT-003).

[0.4.0]: https://github.com/vacanjef/vacanjef/releases/tag/v0.4.0
[0.3.0]: https://github.com/vacanjef/vacanjef/releases/tag/v0.3.0
[0.2.0]: https://github.com/vacanjef/vacanjef/releases/tag/v0.2.0
[0.1.0]: https://github.com/vacanjef/vacanjef/releases/tag/v0.1.0
