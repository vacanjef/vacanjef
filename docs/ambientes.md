# Ambientes de VacanJef (TT-009)

VacanJef se ejecuta en cuatro ambientes separados. Cada uno tiene su propia base de datos, su propia configuración y sus propios secretos: nada de un ambiente se comparte con otro.

## Resumen

| | Desarrollo local | Docker local | Integración continua | Producción |
| --- | --- | --- | --- | --- |
| Para qué | Programar y probar en el PC | Probar el entorno completo como en un servidor | Verificar cada cambio antes de unirlo | Aplicación publicada para los usuarios |
| Dónde corre | PC de cada integrante (`dotnet run`) | PC con Docker Desktop (`docker compose up`) | GitHub Actions (`ci.yml`, `docker.yml`) | Azure App Service `app-vacanjef-3169901` (Spain Central) |
| `ASPNETCORE_ENVIRONMENT` | Development (launchSettings.json) | Development (docker-compose.yml) | No aplica: solo compila y corre las pruebas | Production (Variables de entorno del App Service) |
| Base de datos | LocalDB o SQL Server local, base `VacanJefDB` | Contenedor `sql` (SQL Server 2022), volumen `datos-sql` | EF Core InMemory dentro de las pruebas | Azure SQL `VacanJefDB` en `sql-vacanjef-3169901` (Canada Central) |
| Cadena de conexión | Variable `VACANJEF_DB` en `~/.bashrc` | Se arma en docker-compose.yml con la contraseña de `.env` | No usa | Cadena `VacanJef` (tipo SQLAzure) en la configuración del App Service |
| Secretos | Solo en el PC (variables de entorno) | Archivo `.env` (ignorado por git; ejemplo en `.env.example`) | Secrets de GitHub (solo los usa el despliegue) | Configuración del App Service y secrets AZURE_* de GitHub |
| Fotos de mascotas | `src/VacanJef.Web/App_Data/fotos-mascotas` | Volumen `fotos-mascotas` | No aplica | `/home/data/fotos-mascotas` |
| Registros (Serilog) | `src/VacanJef.Web/Logs/` | Volumen `registros` | Salida de la ejecución | `/home/LogFiles/vacanjef` |
| URL | `https://localhost:7039` | `http://localhost:8080` | No aplica | `https://app-vacanjef-3169901-gdc3aaa2afgjgcfj.spaincentral-01.azurewebsites.net` |

## Qué cambia según el ambiente

| Comportamiento | Development | Production |
| --- | --- | --- |
| Página de error | Detalle técnico del error (solo en el PC) | Página genérica `/Home/Error`, sin detalles internos |
| HSTS (obliga HTTPS en el navegador) | No | Sí |
| Guía de estilos `/Home/GuiaEstilos` | Disponible | No existe (404) |
| Indicador en el pie de página | "Ambiente: Desarrollo" | No se muestra |
| Archivo de configuración adicional | `appsettings.Development.json` (personal, ignorado por git) | `appsettings.Production.json` (en el repositorio, sin secretos) |

La configuración se lee en este orden, y cada fuente reemplaza a la anterior: `appsettings.json` → `appsettings.{Ambiente}.json` → variables de entorno. Por eso las rutas, la cadena de conexión y el primer administrador se definen siempre con variables de entorno de cada ambiente, nunca en los archivos del repositorio.

## Cómo pasa un cambio de un ambiente a otro

1. **Desarrollo local:** se programa en una rama `feature/...` y se prueba con `dotnet build` y `dotnet test`.
2. **Integración continua:** al abrir el PR hacia `dev`, GitHub Actions compila, corre las pruebas, mide la cobertura y construye las imágenes Docker. Sin el check "Compilar y probar" en verde y una aprobación, el PR no se puede unir.
3. **Release:** una rama `release/vX.Y.Z` lleva lo integrado en `dev` hacia `main`.
4. **Producción:** al unir a `main`, el workflow "Despliegue en Azure" vuelve a probar, publica en el App Service con OIDC y verifica `/salud`.

## Reglas

- Nunca usar la base de producción para desarrollar o probar. La cadena de Azure SQL solo existe en el App Service.
- Nunca copiar datos reales de producción a un ambiente local (Ley 1581).
- Un secreto de un ambiente no se reutiliza en otro: la contraseña de SQL Server en `.env` es distinta de la de Azure SQL.
- Si se necesita un ambiente intermedio de pruebas en la nube (Staging), se crea otro App Service con su propia base y `ASPNETCORE_ENVIRONMENT=Staging`; el indicador del pie de página mostrará "Ambiente: Staging".
