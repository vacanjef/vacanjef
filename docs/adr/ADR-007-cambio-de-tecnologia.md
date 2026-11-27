# ADR-007 — Cambio de tecnología: de React + Node.js + MySQL a .NET 10 + SQL Server + Azure

- Estado: Aceptado
- Fecha: __/11/2026 (decisión "opción B", antes de empezar el código de TT-002)
- Responsables: equipo VacanJef. Líder técnico: Juan Camilo Sarrazola Agudelo.
- Relacionado: TT-002, TT-003, RNF-003, RNF-006, RF-007, RF-010, ADR-005
- Reemplaza: la pila tecnológica de la versión 1.0 de Formulación, Alcance, Arquitectura y Manual Técnico

> Revisión del equipo: confirmar la fecha y completar las razones de la sección "Decisión" si hubo otras, por ejemplo la formación del programa ADSO.

## Contexto
La versión 1.0 de la documentación proponía una aplicación de página única en **React**, una API en **Node.js** y una base **MySQL**. Antes de escribir el código, el equipo revisó qué pila le permitía cumplir el alcance del MVP en 14 semanas, con cuatro integrantes y con las exigencias de seguridad y de datos personales del proyecto (RNF-003, RNF-006 y Ley 1581).

## Decisión
Se adopta la siguiente pila (opción B):

| Capa | Tecnología |
| --- | --- |
| Lenguaje y plataforma | C# 14 sobre **.NET 10 LTS**, fijado en `global.json` |
| Aplicación web | **ASP.NET Core MVC con vistas Razor** y Bootstrap 5.3 (un solo proyecto para servidor e interfaz) |
| Acceso a datos | **Entity Framework Core 10** con migraciones versionadas |
| Base de datos | **SQL Server**: LocalDB o SQL Server en desarrollo, **Azure SQL Database** en producción |
| Autenticación y seguridad | **ASP.NET Core Identity**: hash de contraseñas PBKDF2, bloqueo por intentos (RN-005), roles, cookie segura, protección CSRF |
| Tiempo real | **SignalR** (notificaciones, HU-009) |
| Pruebas | xUnit, EF Core InMemory y `WebApplicationFactory` (pruebas de integración) |
| Entrega | Docker, GitHub Actions y **Azure App Service** con OIDC |

Razones:
1. **Un solo lenguaje y una sola aplicación.** Las vistas se generan en el servidor, así que no hay que mantener por separado una SPA y una API, ni coordinar dos despliegues.
2. **Seguridad incluida en el framework.** Identity, la protección CSRF y las cookies seguras evitan depender de librerías de terceros para lo más delicado. Por eso RNF-003 pasa de bcrypt a **PBKDF2**, que es lo que usa Identity.
3. **Tiempo real sin infraestructura adicional.** SignalR viene con ASP.NET Core y negocia solo WebSockets, SSE o long polling.
4. **Base de datos y migraciones.** EF Core con SQL Server da migraciones versionadas en el repositorio y reintentos automáticos para Azure SQL serverless.
5. **Nube disponible para el equipo.** La suscripción Azure for Students cubre App Service y Azure SQL (oferta gratuita), y GitHub Actions despliega con identidad federada, sin contraseñas.
6. **Soporte a largo plazo.** .NET 10 es una versión LTS, con soporte hasta noviembre de 2028.

## Alternativas consideradas
| Alternativa | Motivo para descartarla |
| --- | --- |
| React + Node.js + MySQL (versión 1.0) | Dos aplicaciones que construir, asegurar y desplegar. La autenticación, el bloqueo por intentos, CSRF y el tiempo real dependen de varias librerías externas que habría que integrar y mantener. |
| API en ASP.NET Core + interfaz en React | Conserva la seguridad de .NET, pero mantiene dos aplicaciones y la complejidad de una SPA, que el alcance del MVP no necesita. |

## Consecuencias
- Formulación, Alcance, Arquitectura y Manual Técnico pasan a la versión 1.1 con la pila nueva, y RNF-003 queda con PBKDF2.
- La interfaz se genera en el servidor con Razor. La interactividad en el navegador se limita a JavaScript propio servido desde `wwwroot` (notificaciones y monitoreo). La CSP es estricta: solo `'self'`, sin CDN ni scripts o estilos en línea.
- El modelo de datos se crea con migraciones de EF Core (TT-002), y cada migración nueva se aplica en Azure antes de cada release.
- Producción quedó en dos regiones por los cupos de la suscripción: App Service en Spain Central y Azure SQL en Canada Central (ver `docs/despliegue/azure.md`).
- El equipo trabaja con el SDK de .NET 10 en Windows con Git Bash. Visual Studio o VS Code sirven por igual.

## Evidencia
`global.json`, `Directory.Build.props`, `src/`, `tests/`, `.github/workflows/`, `docs/ambientes.md` y `docs/despliegue/azure.md`.
