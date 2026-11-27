# Evidencias de pruebas: cobertura y restauración (TT-013)

Registro para el Manual Técnico y el Plan de Pruebas. Los datos se midieron sobre `dev` el 27/11/2026, con la versión 0.4.0 más lo unido después.

## Resultado de las pruebas automatizadas

| Dato | Valor |
| --- | --- |
| Fecha | 27/11/2026, 11:38 |
| Quién | Laura Vanessa Henao López |
| Comando | `bash herramientas/cobertura/medir-cobertura.sh` |
| Pruebas | **268 en total, 268 correctas, 0 con errores, 0 omitidas** (10,1 s) |
| Tipos | Unitarias (servicios, seguridad, navegación, datos) y de integración por HTTP de la aplicación completa (TT-012) |

## Cobertura de código (coverlet + ReportGenerator)

| Métrica | Valor |
| --- | --- |
| **Líneas** | **79,6 %** (660 de 829 líneas medibles) |
| **Ramas** | **57,9 %** (127 de 219) |
| **Métodos** | **74,6 %** (141 de 189) |
| Métodos cubiertos por completo | 70,8 % (134 de 189) |

| Proyecto | Líneas |
| --- | --- |
| VacanJef.Infrastructure | 91,1 % |
| VacanJef.Web | 77,3 % |
| VacanJef.Core | Sin líneas medibles: solo tiene entidades con propiedades automáticas, que no se cuentan (`SkipAutoProps`) |

Configuración en `coverlet.runsettings`: se excluyen las migraciones, el código generado (vistas Razor compiladas) y el propio proyecto de pruebas.

### Lectura de los resultados

- **Lo que más importa está cubierto.** Las reglas de negocio están en los servicios, y casi todos tienen entre el 97 % y el 100 %:
  - reservas;
  - estadías;
  - observaciones;
  - historial;
  - notificaciones;
  - usuarios;
  - contraseñas;
  - cámaras;
  - auditoría.
- La seguridad también tiene entre el 70 % y el 100 %: cabeceras, Identity, proxy, política de datos y filtro de cambio de contraseña.
- **Controladores con 0 %:**
  - Auditoria, Camaras, Contrasena, DatosPersonales, Historial, Observaciones y Usuarios.
  - Sus pruebas verifican directamente el servicio, que es donde está la lógica.
  - TT-012 cubre por HTTP la autorización de esas rutas (redirección sin sesión y acceso denegado por rol), pero solo llega al filtro de autorización, no a la acción.
- **Anomalía de la medición:** `ServicioGestionReservas` y `GestionReservasController` (HU-006) aparecen con 0 %, pero sus 13 pruebas (`GestionReservasTests`) sí se ejecutan y pasan: así lo confirma `dotnet test --filter "FullyQualifiedName~GestionReservas"` (13 correctas, 27/11). Es un problema del registro de coverlet, no falta de pruebas. Por eso la cobertura real de VacanJef.Web es mayor que el 77,3 % reportado. Queda para investigar.
- **Ramas (57,9 %):** las ramas sin cubrir son sobre todo mensajes de error de formularios y validaciones de presentación.

### Cómo reproducir
1. `dotnet tool install --global dotnet-reportgenerator-globaltool` (una sola vez).
2. `bash herramientas/cobertura/medir-cobertura.sh`
3. Abrir el reporte HTML en `coverage/reporte/index.html`. La carpeta `coverage/` no se sube al repositorio.

En cada PR, el CI ("Compilar y probar") publica la tabla de cobertura en el resumen del run y el artefacto `reporte-cobertura`.

## Prueba de restauración (TT-011)

| Dato | Valor |
| --- | --- |
| Fecha | 26/11/2026 |
| Quién | Laura Vanessa Henao López |
| Ambiente | Local (base `VacanJefDB`) |
| Comandos | `dotnet run herramientas/respaldo/respaldo.cs -- respaldar` y `-- probar` |
| Respaldo | `VacanJefDB_20261126_143825.bak` (COPY_ONLY, CHECKSUM, RESTORE VERIFYONLY) |
| Restauración | En una base aparte (`VacanJefDB_PruebaRestauracion`); la base de trabajo no se modifica |
| Integridad | `DBCC CHECKDB` sin errores |
| Contenido | 15 tablas; última migración `20261126165209_HU009Notificaciones` |
| **Tiempo de recuperación medido** | **49,5 s** |
| Resultado | **Correcto** |

Frente a los objetivos de la Arquitectura (sección 8.11), **RPO ≤ 24 h y RTO ≤ 4 h**:
- **Local:** la restauración tardó 49,5 s, muy por debajo del RTO. El RPO se cumple si se respalda al menos una vez al día mientras se desarrolla, o antes de cada migración.
- **Producción (Azure SQL):** los respaldos automáticos de Azure permiten restaurar a un punto en el tiempo, con una pérdida de minutos, dentro del RPO. La restauración en Azure no se probó, porque crea una base que no es gratuita (ver `docs/respaldo-y-restauracion.md`).

Procedimiento completo: `docs/respaldo-y-restauracion.md`.
