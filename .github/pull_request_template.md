<!-- Plantilla de PR según la Guía de Estructura y Evidencias del Repositorio (sección 6.3) -->

## Qué cambia
<!-- Resumen corto de lo que hace este PR -->

## Requisitos relacionados
<!-- Ej.: HU-002, RF-002, RN-005. Agregue la línea "Closes #N" con el número del issue -->

Closes #

## Tipo de cambio
- [ ] feat: nueva funcionalidad
- [ ] fix: corrección de un defecto
- [ ] refactor: mejora interna sin cambiar el comportamiento
- [ ] test: pruebas
- [ ] docs: documentación
- [ ] ci / chore: pipeline o configuración

## Cómo probarlo
<!-- Pasos para que el revisor lo verifique. Incluya los casos de prueba (CP-###) si aplican -->
1. `dotnet build && dotnet test`
2.

## Capturas / evidencia
<!-- Arrastre aquí las capturas. Indique la evidencia (EV-###) si aplica -->

## Riesgos
<!-- Qué podría fallar o afectar a otros módulos. Escriba "Ninguno" si no aplica -->

## Migraciones
- [ ] No incluye migraciones de base de datos
- [ ] Incluye migración: <!-- nombre de la migración y comando para aplicarla -->

## Lista de verificación (definición de listo)
- [ ] Cumple los criterios de aceptación del issue
- [ ] Compila sin errores (`dotnet build`)
- [ ] Las pruebas pasan (`dotnet test`) y se agregaron o actualizaron pruebas
- [ ] La documentación afectada está actualizada
- [ ] No incluye contraseñas, secretos, datos sensibles ni archivos de bin/, obj/ o .vs/
- [ ] Deuda o limitaciones registradas cuando aplica
- [ ] Lo revisó otro integrante
