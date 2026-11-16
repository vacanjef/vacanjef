# VacanJef — Guardería canina con monitoreo en tiempo real

Plataforma web para gestionar una guardería canina: dueños y mascotas, historial de salud, reservas con disponibilidad real, observaciones del cuidador, notificaciones y monitoreo privado por cámara.

Proyecto de grado — SENA, ADSO, ficha 3169901. Instructor: Juan Pablo Saldarriaga.

## Equipo

| Integrante | GitHub | Módulo |
| --- | --- | --- |
| Juan Camilo Sarrazola Agudelo | @juan-camilo-sarrazola-agudelo | Base de datos, mascotas y salud |
| Jeferson Estiven Vargas García | @jeferson-estiven-vargas | Usuarios, acceso, seguridad y administración |
| Carlos Alfredo Santiz | @carlosantiz94-prog | Reservas y monitoreo por cámaras |
| Laura Vanessa Henao López | @lvhenao0-hash | Diseño UI, notificaciones, DevOps y pruebas |

## Tecnologías

C# · .NET 10 · ASP.NET Core MVC · Razor · Bootstrap 5 · Entity Framework Core · SQL Server · ASP.NET Core Identity · SignalR · xUnit · Docker · GitHub Actions

## Estructura

    src/VacanJef.Core            Entidades y reglas de negocio
    src/VacanJef.Infrastructure  Base de datos (EF Core)
    src/VacanJef.Web             Controladores, vistas y seguridad
    tests/VacanJef.Tests         Pruebas unitarias

## Ejecutar

    dotnet build
    dotnet test
    dotnet run --project src/VacanJef.Web

## Flujo de trabajo (Guía de Estructura y Evidencias, sección 6)

| Rama | Uso | Sale de | Se integra a |
| --- | --- | --- | --- |
| `main` | Versión estable y entregable. Nadie sube cambios directamente. | — | — |
| `dev` | Integración del trabajo del equipo. | `main` | — |
| `feature/HU-xxx-descripcion` | Una rama por historia de usuario. | `dev` | `dev` |
| `fix/xxx` | Corrección de errores no urgentes. | `dev` | `dev` |
| `release/vX.Y.Z` | Preparar una entrega: se congela y se valida. | `dev` | `main` y `dev` |
| `hotfix/xxx` | Error crítico en `main`. | `main` | `main` y `dev` |

Todo cambio entra por Pull Request, revisado por otro integrante y con el CI en verde.
Commits: `feat`, `fix`, `refactor`, `test`, `docs`, `ci`, `chore`.
