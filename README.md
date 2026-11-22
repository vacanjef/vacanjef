# VacanJef — Guardería canina con monitoreo en tiempo real

Plataforma web para gestionar una guardería canina: dueños y mascotas, historial de salud, reservas con disponibilidad real, observaciones del cuidador, notificaciones y monitoreo privado por cámara.

Proyecto de grado — SENA, ADSO, ficha 3169901. Instructor: Mateo Arroyave.

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
    src/VacanJef.Web             Controladores, vistas, seguridad y sistema de diseño
    tests/VacanJef.Tests         Pruebas unitarias
    herramientas/streaming       Cámara de prueba (FFmpeg + MediaMTX)
    docs/                        Pruebas de concepto y decisiones de arquitectura (ADR)

## Requisitos

| Herramienta | Versión | Cómo instalarla (Windows) |
| --- | --- | --- |
| Git (incluye Git Bash) | Reciente | <https://git-scm.com/download/win> |
| SDK de .NET | **10** (10.0.100 o superior de la línea 10) | `winget install Microsoft.DotNet.SDK.10` |
| SQL Server | LocalDB o Express | Viene con Visual Studio (carga "Almacenamiento y procesamiento de datos") o con SQL Server Express |
| dotnet-ef | **10.0.10** | `dotnet tool install --global dotnet-ef --version 10.0.10` |

Importante sobre el SDK:

- El repositorio fija la versión en `global.json` (SDK 10.0.100 con `rollForward: latestFeature`). Con el SDK 11 solo **no** compila.
- Instale el SDK 10 **aparte de Visual Studio**. Las actualizaciones de Visual Studio pueden quitar el SDK 10 y dejar solo el 11.
- Para revisar qué SDK tiene: `dotnet --list-sdks`. Debe aparecer una línea que empiece por `10.0.`.

Paquetes NuGet de Microsoft: instálelos **siempre** con la versión 10 (`--version 10.0.10`). Sin versión, NuGet instala la 11, que no es compatible con `net10.0`.

## Configuración inicial (una sola vez)

Todos los comandos se ejecutan en **Git Bash**, desde la raíz del repositorio.

**1. Clonar fuera de OneDrive.** Las carpetas sincronizadas con OneDrive pueden bloquear los archivos compilados y hacer fallar las pruebas.

    mkdir -p /c/dev && cd /c/dev
    git clone https://github.com/vacanjef/vacanjef.git
    cd vacanjef
    git switch dev

**2. Revisar SQL Server LocalDB.**

    sqllocaldb info

Debe aparecer `MSSQLLocalDB`.

**3. Configurar la cadena de conexión.** Nunca va en el código: se lee de la variable de entorno `VACANJEF_DB`.

    echo "export VACANJEF_DB='Server=(localdb)\MSSQLLocalDB;Database=VacanJefDB;Trusted_Connection=True;TrustServerCertificate=True'" >> ~/.bashrc
    source ~/.bashrc
    echo $VACANJEF_DB

Si usa SQL Server Express en lugar de LocalDB, cambie `(localdb)\MSSQLLocalDB` por `localhost\SQLEXPRESS`.

**4. Crear la base de datos.**

    dotnet ef database update -p src/VacanJef.Infrastructure -s src/VacanJef.Infrastructure

Al final debe decir `Done.`

**5. Confiar en el certificado de desarrollo HTTPS.**

    dotnet dev-certs https --trust

## Ejecutar

    dotnet build
    dotnet test
    dotnet run --project src/VacanJef.Web --launch-profile https

Abra la dirección que aparece en "Now listening on" (por ejemplo `https://localhost:7039`). Al iniciar, la aplicación crea los roles Dueño, Cuidador y Administrador si no existen.

En desarrollo está disponible la guía de estilos en `/Home/GuiaEstilos`.

## Sistema de diseño

- Colores, tipografía y componentes: `src/VacanJef.Web/wwwroot/css/vacanjef.css` (color primario `#0B6E7F`, fuente Inter alojada en `wwwroot/lib/inter`).
- Use las clases de Bootstrap y las del sistema (`vj-tarjeta`, `vj-titulo-pagina`, `vj-texto-suave`). No escriba colores sueltos.
- La política de seguridad de contenido (CSP) solo permite recursos del propio sitio: **no** use `style="..."` en las vistas ni librerías desde CDN.
- El menú de cada rol se define en `src/VacanJef.Web/Navegacion/MenuNavegacion.cs`. Las rutas son provisionales: cada responsable ajusta la suya al implementar su historia.

## Problemas frecuentes

| Mensaje | Causa y solución |
| --- | --- |
| `A compatible .NET SDK was not found` | Solo tiene el SDK 11. Ejecute `winget install Microsoft.DotNet.SDK.10`. |
| `Configure la cadena de conexión en la variable de entorno VACANJEF_DB` | Falta la variable. Repita el paso 3 de la configuración inicial y abra una nueva ventana de Git Bash. |
| `There is already an object named 'Usuarios'` | La variable no está configurada y se usó la base `VacanJef` del prototipo viejo. Configure `VACANJEF_DB` con `Database=VacanJefDB`. |
| `dotnet ef` no se reconoce | Instale la herramienta: `dotnet tool install --global dotnet-ef --version 10.0.10`. |
| `FileLoadException 0x800711C7` al correr las pruebas | El repositorio está dentro de OneDrive. Clónelo en `C:\dev\vacanjef`. |
| El navegador dice que el sitio no es seguro | Ejecute `dotnet dev-certs https --trust` y recargue. |
| `Committing is not possible because you have unmerged files` | Hay un conflicto sin resolver. Corrija el archivo, haga `git add` y luego `git commit --no-edit`. Revise siempre con `git status`. |

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

Pasos para cada historia:

1. Crear el issue con la historia, los requisitos y los criterios de aceptación, y asignarlo al responsable.
2. `git switch dev && git pull` y `git switch -c feature/HU-xxx-descripcion`.
3. Trabajar con commits pequeños. Antes de cada commit: `dotnet build` y `dotnet test`.
4. Si `dev` avanzó mientras tanto: `git fetch origin` y `git merge origin/dev`, resolver conflictos y probar de nuevo.
5. `git push -u origin feature/HU-xxx-descripcion` y abrir el PR hacia `dev` con `Closes #N` (el número del issue **propio**).
6. Otro integrante revisa, aprueba y une el PR.
