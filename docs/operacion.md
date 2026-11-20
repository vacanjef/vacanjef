# Operación: salud de la aplicación y registros (TT-008)

## Health check

La dirección `/salud` indica si la aplicación responde y si la base de datos acepta conexiones.

| Entorno | Dirección |
| --- | --- |
| Local (`dotnet run`) | <https://localhost:7039/salud> |
| Docker | <http://localhost:8080/salud> |

Respuesta de ejemplo:

    {"estado":"Saludable","duracionMs":12,"componentes":{"base-de-datos":"Saludable"}}

- Código **200** cuando el estado es `Saludable` o `Degradado`, y **503** cuando es `No disponible`.
- No requiere iniciar sesión y nunca muestra mensajes de error, cadenas de conexión ni datos personales.
- Sirve para que Docker, un balanceador o el servicio en la nube sepan si la aplicación está viva.

## Registros (Serilog)

Cada evento se escribe en la consola y en un archivo por día.

| Entorno | Carpeta de archivos |
| --- | --- |
| Local | `src/VacanJef.Web/Logs/` (Git la ignora) |
| Docker | volumen `registros`, montado en `/datos/registros` |

- Nombre de los archivos: `vacanjef-AAAAMMDD.log`. Se guardan los últimos 14 días.
- Por cada petición se registra una línea con método, ruta, código de respuesta y tiempo. Nunca se registra el contenido de los formularios (contraseñas o datos personales, Ley 1581).
- Las consultas a `/salud` no se registran, para no llenar los archivos.
- Los niveles se cambian en `appsettings.json`, sección `Serilog` (por ejemplo, `"Default": "Debug"` para ver más detalle).

En Docker:

    docker compose logs -f web                                  # registros en vivo
    docker compose exec web ls /datos/registros                 # archivos guardados
    docker compose exec web tail -n 50 /datos/registros/vacanjef-AAAAMMDD.log
