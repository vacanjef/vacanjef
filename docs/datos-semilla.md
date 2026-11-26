# Datos semilla de demostración (TT-009)

Carga datos **ficticios** en tu base local para probar y demostrar VacanJef sin preparar nada a mano.

- Solo se cargan en **Desarrollo** (`dotnet run` local). En Producción (Azure) nunca se cargan, aunque la variable exista.
- Solo se cargan si activas `VACANJEF_DEMO=true`.
- La contraseña de las cuentas de prueba se toma de `VACANJEF_DEMO_CONTRASENA`. Nunca va en el código ni en el repo.
- Si las cuentas de demostración ya existen, no se carga nada de nuevo (no duplica).
- Todo es inventado: nombres con "(demo)" y correos `@ejemplo.com` (Ley 1581, TT-004).

## Cómo cargarlos (Git Bash)

```bash
export VACANJEF_DEMO=true
read -s VACANJEF_DEMO_CONTRASENA && export VACANJEF_DEMO_CONTRASENA
dotnet run --project src/VacanJef.Web
```

`read -s` espera que escribas la contraseña (no se ve). Debe cumplir la política: 8 o más caracteres con mayúscula, minúscula, número y símbolo. En la consola debe aparecer *"Datos de demostración cargados"*.

## Cuentas

| Correo | Rol | Qué tiene |
| --- | --- | --- |
| cuidador.demo@ejemplo.com | Cuidador | Autor de todas las observaciones |
| carolina.demo@ejemplo.com | Dueño | Max (estadía finalizada con observaciones y una corrección; reserva futura confirmada) y Lola (estadía activa, alergia al pollo) |
| andres.demo@ejemplo.com | Dueño | Rocky (estadía activa desde hoy; reserva cancelada) |
| valentina.demo@ejemplo.com | Dueño | Nala (reserva confirmada con ingreso hoy, para probar HU-014) y Simón (sin estadías ni observaciones) |

El administrador no se crea aquí: sigue saliendo de `VACANJEF_ADMIN_CORREO` y `VACANJEF_ADMIN_CONTRASENA`.

Las fechas son relativas al día en que se cargan: siempre hay una estadía activa y un ingreso pendiente para ese día.

## Qué se puede probar

| Historia | Cómo |
| --- | --- |
| HU-008 Observaciones | Entrar como cuidador → Observaciones → Lola o Rocky |
| HU-013 Historial | Entrar como Carolina → Mis mascotas → Historial de Max (con corrección) |
| CA-HU-013-03 | Entrar como Valentina → Historial de Simón (sin observaciones) |
| HU-014 Ingreso y salida | Nala (ingreso hoy) y Lola o Rocky (salida) |

## Volver a empezar

Los datos no se borran solos. Para dejar la base local limpia y volver a cargarlos:

```bash
dotnet ef database drop -p src/VacanJef.Infrastructure -s src/VacanJef.Infrastructure
dotnet ef database update -p src/VacanJef.Infrastructure -s src/VacanJef.Infrastructure
```

Esto borra **toda** la base local (también tus datos de prueba manuales). Nunca lo ejecutes con `VACANJEF_DB` apuntando a Azure.
