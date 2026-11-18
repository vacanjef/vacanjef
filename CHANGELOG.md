# Registro de cambios

Todas las versiones de VacanJef. Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y [versionado semántico](https://semver.org/lang/es/).

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

[0.1.0]: https://github.com/vacanjef/vacanjef/releases/tag/v0.1.0
