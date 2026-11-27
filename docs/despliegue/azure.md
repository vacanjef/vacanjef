# Despliegue de VacanJef en Azure (TT-003)

Guía del entorno de producción: qué recursos hay, cómo se despliega y cómo se verifica.
Issue: #43. Responsables: Juan Camilo Sarrazola (suscripción, base de datos y firewall), Laura Henao (App Service, identidad federada y workflow) y Carlos Santiz (prueba de la URL pública y esta guía).

> Esta guía no contiene contraseñas, cadenas de conexión ni identificadores de la suscripción. Los secretos viven solo en Azure y en los secrets de GitHub.

## 1. Arquitectura

```
GitHub (rama main) ──► GitHub Actions "Despliegue en Azure"
                         │  OIDC (identidad federada, sin contraseñas)
                         ▼
                 Azure App Service (Linux, .NET 10)
                 app-vacanjef-3169901 · Spain Central
                         │  cadena "VacanJef" (SQLAzure)
                         ▼
                 Azure SQL Database (serverless)
                 sql-vacanjef-3169901 / VacanJefDB · Canada Central
```

- **URL pública:** https://app-vacanjef-3169901-gdc3aaa2afgjgcfj.spaincentral-01.azurewebsites.net
- **Salud:** `/salud` responde `{"estado":"Saludable", ...}` con el componente `base-de-datos`.

## 2. Recursos (grupo `rg-vacanjef`)

| Recurso | Nombre | Región | Notas |
| --- | --- | --- | --- |
| Grupo de recursos | `rg-vacanjef` | — | Etiquetas `proyecto=vacanjef`, `ficha=3169901` |
| Servidor SQL | `sql-vacanjef-3169901` | Canada Central | TLS 1.2, TDE, acceso público solo para redes seleccionadas, sin "Permitir servicios de Azure" |
| Base de datos | `VacanJefDB` | Canada Central | Oferta gratuita, serverless (se pausa sin uso) |
| Plan de App Service | `asp-vacanjef` | Spain Central | F1 (gratuito), Linux |
| App Service | `app-vacanjef-3169901` | Spain Central | .NET 10 LTS |
| Identidad administrada | `id-vacanjef-github` | Spain Central | Usada por GitHub Actions para desplegar |

**Suscripción:** Azure for Students (crédito de USD 100; si se agota, la suscripción se desactiva, no genera cobros).

**Permisos:** el titular de la suscripción es Juan Camilo. Jeferson, Laura y Carlos son Propietarios del grupo `rg-vacanjef` con la condición recomendada: pueden crear y modificar recursos del grupo, pero no registrar proveedores de recursos ni pedir cupo (eso solo lo hace el titular).

### ¿Por qué dos regiones?

La suscripción tiene una directiva que solo permite estas regiones: Switzerland North, Spain Central, North Central US, Canada Central y Germany West Central.

- **Azure SQL** quedó en **Canada Central** porque North Central US no tenía cupo para SQL.
- **App Service** quedó en **Spain Central** porque la suscripción tiene cupo 0 para el plan F1 en Canada Central ("Current Limit (F1 App Service instances): 0").

Funciona, pero cada consulta cruza el Atlántico, así que hay más latencia. Alternativas evaluadas y descartadas por ahora: plan B1 en Canada Central (unos USD 13 al mes del crédito) o pedir cupo F1 en Canada Central.

## 3. Configuración del App Service

**Configuración general:** solo HTTPS, TLS mínimo 1.2 (sitio y SCM), FTP deshabilitado, credenciales básicas de publicación (SCM y FTP) desactivadas, HTTP 1.1. Implementación continua del portal deshabilitada y sin Application Insights.

**Cadena de conexión** (Configuración → Cadenas de conexión):

| Nombre | Tipo | Valor |
| --- | --- | --- |
| `VacanJef` | SQLAzure | Solo en el portal. Nunca en el repositorio. |

Azure la entrega como `SQLAZURECONNSTR_VacanJef` y .NET la expone como `ConnectionStrings:VacanJef`, que es lo que lee `Program.cs`.

**Variables de entorno** (Configuración de la aplicación):

| Variable | Valor | Para qué |
| --- | --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Modo producción |
| `Fotos__Ruta` | `/home/data/fotos-mascotas` | Fotos de mascotas (HU-004). `/home` es lo único que persiste entre reinicios. |
| `Registros__Ruta` | `/home/LogFiles/vacanjef` | Archivos de Serilog (TT-008) |
| `VACANJEF_ADMIN_CORREO` | (correo del primer administrador) | Semilla del primer administrador |
| `VACANJEF_ADMIN_CONTRASENA` | (no se documenta) | Semilla del primer administrador. **Se borra** antes de la entrega final. |

Variables opcionales de reservas (HU-005), si la guardería define otros valores: `Reservas__CapacidadDiaria`, `Reservas__TarifaBasico`, `Reservas__TarifaPremium`, `Reservas__TarifaSpa`.

### Primer administrador

Al arrancar, si no existe ningún administrador y están las dos variables `VACANJEF_ADMIN_*`, la aplicación crea uno. Así se creó el primero en producción. Antes de la entrega final:

1. Ese administrador cambia su contraseña en **Mis datos → Cambiar contraseña**.
2. Se borra `VACANJEF_ADMIN_CONTRASENA` del App Service, porque queda en texto plano y la pueden ver los Propietarios del grupo.

Los demás administradores y cuidadores se crean desde `/Usuarios` (HU-015).

### Limitaciones del plan F1

- Sin "Siempre activado": la aplicación se duerme tras unos 20 minutos sin uso y **la primera visita tarda**. La base serverless también se pausa: el error 40613 significa que se está despertando.
- Sin "Comprobación de estado" de Azure (requiere B1): `/salud` se verifica desde el workflow.

## 4. Firewall de Azure SQL

El servidor solo acepta conexiones de:

- Las **31 direcciones IP de salida** del App Service (reglas `appservice-1` a `appservice-31`), creadas con un bucle de `az sql server firewall-rule create` en Cloud Shell. Se ven en App Service → Redes → Configuración del tráfico saliente → Direcciones IPv4 salientes.
- Temporalmente, la IP del PC de Juan Camilo, para el Editor de consultas y para aplicar migraciones. Se quita antes de cerrar el issue #43.

**Si el App Service cambia de plan o de región, cambian sus IP de salida** y hay que actualizar estas reglas; si no, `/salud` mostrará la base de datos como no disponible.

## 5. Despliegue automático

Workflow `.github/workflows/despliegue-azure.yml`. Se ejecuta con cada push a `main` (es decir, al unir una release) o a mano desde Actions (solo desde `main`).

Pasos: compilar y probar en Release → `dotnet publish` → iniciar sesión en Azure con OIDC → desplegar en `app-vacanjef-3169901` → consultar `/salud` hasta 20 veces cada 15 s hasta recibir `"Saludable"` → cerrar sesión.

### Identidad federada (sin contraseñas)

- Identidad administrada `id-vacanjef-github`, con rol **Website Contributor** solo sobre el App Service (no sobre el grupo ni la base de datos).
- Credencial federada `github-main`, entidad *Rama* `main`, audiencia `api://AzureADTokenExchange`, con el **sujeto inmutable** que GitHub usa en los repositorios creados después del 15/07/2026: `repo:vacanjef@<id organización>/vacanjef@<id repositorio>:ref:refs/heads/main`.
- Secrets de GitHub Actions (cuenta `vacanjef`): `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`.

Como la credencial solo confía en `main`, el despliegue no funciona desde otras ramas, a propósito.

### Migraciones

**El workflow no aplica migraciones.** Cuando una historia agrega una migración, se aplica en Azure **antes** de unir la release a `main`. La vía recomendada es el **Editor de consultas** del portal con la cuenta Entra del titular:

1. Generar el script: `dotnet ef migrations script <última-aplicada> <nueva> -p src/VacanJef.Infrastructure -s src/VacanJef.Infrastructure`
2. Revisar que no tenga `DROP` ni `DELETE`.
3. Pegarlo en el Editor de consultas sin las líneas `GO` y ejecutarlo una sola vez.
4. Verificar la tabla `__EFMigrationsHistory`.

Aplicadas a la fecha: `Inicial` y `HU009Notificaciones`. HU-005, HU-006, HU-008, HU-013 y HU-014 no agregaron migraciones.

## 6. Prueba de la URL pública

Fecha de la prueba: __ · Responsable: Carlos Santiz · Navegador: __

| # | Prueba | Resultado esperado | Resultado | Evidencia |
| --- | --- | --- | --- | --- |
| 1 | `curl -s $URL/salud` | `"estado":"Saludable"` y `base-de-datos` Saludable | __ | `img/azure-02-salud.png` |
| 2 | `curl -sI http://...` (sin s) | Redirección 301/308 a HTTPS | __ | — |
| 3 | `curl -sI $URL` | Cabeceras `Strict-Transport-Security`, `Content-Security-Policy`, `X-Frame-Options`, `X-Content-Type-Options`, `Referrer-Policy` | __ | — |
| 4 | Abrir la portada | Carga con candado HTTPS | __ | `img/azure-01-portada.png` |
| 5 | Registrar un dueño de prueba | Cuenta creada | __ | `img/azure-03-registro.png` |
| 6 | Iniciar sesión | Menú de dueño | __ | `img/azure-04-sesion.png` |
| 7 | Registrar una mascota con foto | Se ve la foto | __ | `img/azure-05-mascota.png` |
| 8 | Mis datos | Datos y política de privacidad | __ | `img/azure-06-mis-datos.png` |
| 9 | 5 intentos con clave incorrecta (cuenta de prueba) | Bloqueo de 15 minutos | __ | `img/azure-07-bloqueo.png` |

Comandos:

```bash
URL=https://app-vacanjef-3169901-gdc3aaa2afgjgcfj.spaincentral-01.azurewebsites.net
curl -s $URL/salud
curl -sI http://${URL#https://} | head -3
curl -sI $URL | grep -i -E "strict-transport|content-security|x-frame|x-content-type|referrer"
```

## 7. Problemas frecuentes

| Síntoma | Causa y solución |
| --- | --- |
| La primera visita tarda casi un minuto | Plan F1 y base serverless dormidos. Esperar y recargar. |
| Error 40613 | La base serverless se está despertando. Reintentar. |
| `/salud` responde 404 | El código todavía no se ha desplegado en el App Service. |
| `/salud` con base de datos "No disponible" | Revisar la cadena `VacanJef`, el firewall (IP de salida) y los registros en `/home/LogFiles/vacanjef`. |
| "Client with IP address … is not allowed" | Agregar la IP en Redes del servidor SQL o en "Permitir IP…" del Editor de consultas. |
| "Login failed for user 'vacanjef_admin'" (18456) al aplicar migraciones | Aplicarlas desde el Editor de consultas con la cuenta Entra (sección 5). |
| "RequestDisallowedByPolicy" | Región no permitida por la directiva de la suscripción. |
| "Current Limit (F1 App Service instances): 0" | Sin cupo F1 en esa región: usar otra región permitida, otro plan o pedir cupo (solo el titular). |
| "Los proveedores de recursos Microsoft.X no están registrados" | Solo el titular de la suscripción puede registrarlos. |
| AADSTS700213 en `azure/login` | El sujeto de la credencial federada no coincide con el que envía GitHub. Revisar el log del paso y crear la credencial con ese sujeto exacto. |
| "Correo o contraseña incorrectos" con el administrador | No insistir (bloqueo al quinto intento). Reiniciar el contador en el Editor de consultas y copiar la contraseña exacta de la variable. |

Nunca tomar capturas de pantallas donde aparezca una contraseña o la cadena de conexión.