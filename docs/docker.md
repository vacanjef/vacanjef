# Ejecutar VacanJef con Docker (TT-006)

Levanta SQL Server, aplica las migraciones y arranca la web, sin instalar SQL Server ni el SDK de .NET.

## Requisitos

- Docker Desktop abierto (en Windows, con WSL 2).

## Primera vez

Desde la raíz del repositorio, en Git Bash:

    cp .env.example .env

Abra `.env` y cambie `VACANJEF_SA_PASSWORD` por una contraseña propia (mínimo 8 caracteres con mayúscula, minúscula, número y símbolo; sin `;`, `$`, `#` ni comillas).

## Levantar el entorno

    docker compose up --build

Orden de arranque:

1. `sql`: SQL Server 2022. Espera hasta estar listo (healthcheck).
2. `migraciones`: crea o actualiza la base `VacanJefDB` y termina.
3. `web`: la aplicación en <http://localhost:8080>. Al iniciar crea los roles.

En desarrollo también está la guía de estilos: <http://localhost:8080/Home/GuiaEstilos>.

Para detener: `Ctrl + C` y luego `docker compose down`.

## Comandos útiles

| Acción | Comando |
| --- | --- |
| Levantar en segundo plano | `docker compose up --build -d` |
| Ver los registros de la web | `docker compose logs -f web` |
| Detener | `docker compose down` |
| Borrar también la base de datos | `docker compose down -v` |
| Volver a aplicar migraciones | `docker compose run --rm migraciones` |

## Conectarse a la base desde SSMS o Azure Data Studio

- Servidor: `localhost,14333`
- Usuario: `sa`
- Contraseña: la del archivo `.env`
- Marcar "Confiar en el certificado del servidor".

## Notas

- La web dentro de Docker usa HTTP en el puerto 8080. Use `http://localhost:8080` (no la IP), porque las cookies de sesión son seguras y los navegadores solo las aceptan sin HTTPS en `localhost`.
- Las llaves de sesión se guardan dentro del contenedor: al recrearlo hay que volver a iniciar sesión.
- El puerto de SQL Server es 14333 para no chocar con un SQL Server instalado en el equipo.
