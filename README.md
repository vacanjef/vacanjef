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

## Flujo de trabajo

Cada integrante trabaja en su rama `feature/...` y une sus cambios a `main` con un Pull Request revisado por otro integrante.
Commits: `feat`, `fix`, `refactor`, `test`, `docs`, `ci`, `chore`.
