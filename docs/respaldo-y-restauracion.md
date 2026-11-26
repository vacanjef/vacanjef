# Respaldo y restauración (TT-011)

Cómo se respaldan los datos de VacanJef, cómo se prueba que un respaldo sirve y cómo se recupera cada ambiente. Complementa [ambientes.md](ambientes.md) y [operacion.md](operacion.md).

## Qué se respalda

| Dato | Dónde vive | Cómo se respalda |
| --- | --- | --- |
| Base de datos (usuarios, mascotas, salud, reservas, observaciones, notificaciones, auditoría) | `VacanJefDB` en cada ambiente | Archivo `.bak` (local) o respaldos automáticos de Azure (producción) |
| Fotos de mascotas | `src/VacanJef.Web/App_Data/fotos-mascotas` (local), volumen `fotos-mascotas` (Docker), `/home/data/fotos-mascotas` (Azure) | Archivo `.zip` junto al `.bak` (local) |
| Código, configuración sin secretos y migraciones | GitHub | El repositorio es el respaldo |
| Secretos (cadenas, contraseñas) | Variables de entorno, `.env`, App Service | No se respaldan en archivos: quien los creó los vuelve a generar |

## Reglas (Ley 1581)

- Un respaldo contiene **datos personales y de salud**. Se guarda **fuera del repositorio** (por defecto en `~/respaldos-vacanjef`) y nunca se sube a GitHub, al chat del equipo ni a un correo. El `.gitignore` bloquea `*.bak` por si acaso.
- Si un respaldo se copia a otro equipo o a una memoria USB, debe ir cifrado (BitLocker o un `.zip` con contraseña) y se borra cuando ya no se necesite.
- La herramienta solo muestra nombres de tablas y cantidades de filas, nunca datos.
- Nunca se restaura un respaldo de producción en un PC de desarrollo.

## Cuándo respaldar

| Momento | Ambiente |
| --- | --- |
| Antes de aplicar una migración nueva (`dotnet ef database update`) | Local |
| Antes de preparar una release | Local |
| Una vez por semana mientras se desarrolla | Local |
| Automático y continuo | Azure SQL (producción) |

Se conservan los **7 respaldos más recientes**; al crear uno nuevo, la herramienta borra los más antiguos de la carpeta (solo los archivos `VacanJefDB_*.bak` y su `.zip`).

## Respaldar la base local

Requisitos: SDK de .NET 10 y la variable `VACANJEF_DB` (la misma que usa la aplicación). Funciona con LocalDB y con un SQL Server instalado en el PC. Desde la raíz del repositorio:

```bash
source ~/.bashrc
dotnet run herramientas/respaldo/respaldo.cs -- respaldar
```

La primera vez tarda un poco más porque compila la herramienta. Crea en `~/respaldos-vacanjef`:

- `VacanJefDB_AAAAMMDD_HHMMSS.bak`: respaldo completo `COPY_ONLY` con `CHECKSUM`, verificado con `RESTORE VERIFYONLY`.
- `VacanJefDB_AAAAMMDD_HHMMSS_fotos.zip`: las fotos de mascotas (si hay).

Variables opcionales: `VACANJEF_RESPALDOS` (otra carpeta), `VACANJEF_RESPALDOS_CONSERVAR` (otra cantidad) y `VACANJEF_FOTOS` (otra carpeta de fotos).

Si sale "SQL Server no tiene permiso para usar la carpeta": pasa con un **servicio** SQL Server (no con LocalDB), porque el servicio corre con otra cuenta de Windows. Use una carpeta en la que el servicio pueda escribir, por ejemplo la carpeta `Backup` de la instancia:

```bash
export VACANJEF_RESPALDOS="/c/Program Files/Microsoft SQL Server/MSSQL16.MSSQLSERVER/MSSQL/Backup"
```

## Probar la restauración

Un respaldo que nunca se ha restaurado no está probado. La prueba restaura el `.bak` en una base aparte (`VacanJefDB_PruebaRestauracion`), así que **la base de trabajo no se toca**:

```bash
dotnet run herramientas/respaldo/respaldo.cs -- probar
```

Sin argumentos usa el respaldo más reciente; también se puede indicar un archivo: `... -- probar ~/respaldos-vacanjef/VacanJefDB_20261126_150000.bak`.

La prueba:

1. Restaura el respaldo en la base de prueba.
2. Revisa la integridad con `DBCC CHECKDB`.
3. Compara la cantidad de filas de cada tabla con la base actual y muestra la última migración.
4. Abre el `.zip` y lee cada foto.
5. Mide el tiempo total (tiempo de recuperación).
6. Borra la base de prueba (para conservarla: `VACANJEF_CONSERVAR_PRUEBA=1`).

Si alguna tabla aparece como "distinto", es normal cuando la base se usó después del respaldo.

## Restaurar de verdad (base local)

Solo cuando la base local se dañó o se perdieron datos:

1. Detenga la aplicación (`Ctrl + C`).
2. Respalde el estado actual, aunque esté dañado: `dotnet run herramientas/respaldo/respaldo.cs -- respaldar`.
3. Pruebe el respaldo que va a usar con `-- probar <archivo.bak>`.
4. Restaure con `sqlcmd`, SQL Server Management Studio o la extensión MSSQL de VS Code, conectado a `master`:

   ```sql
   ALTER DATABASE [VacanJefDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
   RESTORE DATABASE [VacanJefDB] FROM DISK = N'C:\Users\<usuario>\respaldos-vacanjef\VacanJefDB_<fecha>.bak'
       WITH REPLACE, RECOVERY, CHECKSUM;
   ALTER DATABASE [VacanJefDB] SET MULTI_USER;
   ```

5. Si el respaldo es de antes de una migración, aplíquela: `dotnet ef database update -p src/VacanJef.Infrastructure -s src/VacanJef.Infrastructure`.
6. Para las fotos, descomprima el `.zip` en `src/VacanJef.Web/App_Data/fotos-mascotas`.

## Docker

El SQL Server del contenedor corre en Linux y no puede escribir en las carpetas de Windows, así que la herramienta no se usa con él. Con los contenedores encendidos (`docker compose up -d`), desde la raíz:

```bash
read -s -p "Contraseña sa: " SA && echo
docker compose exec -e SQLCMDPASSWORD="$SA" sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -C -b \
  -Q "BACKUP DATABASE [VacanJefDB] TO DISK = N'/var/opt/mssql/backup/VacanJefDB.bak' WITH COPY_ONLY, INIT, CHECKSUM"
docker compose cp sql:/var/opt/mssql/backup/VacanJefDB.bak ~/respaldos-vacanjef/VacanJefDB_docker.bak
unset SA
```

La contraseña es la de `VACANJEF_SA_PASSWORD` en `.env`. Si `/var/opt/mssql/backup` no existe, créela con `docker compose exec sql mkdir -p /var/opt/mssql/backup`. Los datos del contenedor viven en el volumen `datos-sql`; `docker compose down -v` los borra.

## Producción (Azure SQL)

- Azure SQL Database hace **respaldos automáticos** (completos, diferenciales y del registro de transacciones). No se pueden crear `.bak` con `BACKUP TO DISK`, por eso la herramienta rechaza las cadenas de Azure.
- La recuperación se hace con **restauración a un punto en el tiempo** desde el portal: SQL database `VacanJefDB` → **Restaurar**, eligiendo la fecha y la hora. La restauración **siempre crea una base nueva**; después se cambia la cadena `VacanJef` del App Service para apuntar a ella.
- La base `VacanJefDB` usa la oferta gratuita, y una base restaurada **no puede ser gratuita**: consume crédito de la suscripción. Por eso la restauración en Azure no se prueba por rutina; solo la decide y la hace Juan Camilo (dueño de la suscripción), y la base restaurada se borra al terminar.
- Pendiente: definir el respaldo de las fotos de producción (`/home/data/fotos-mascotas` en el App Service).

## Objetivos

| Ambiente | Pérdida de datos máxima aceptada (RPO) | Tiempo para recuperar (RTO) |
| --- | --- | --- |
| Local | Desde el último respaldo (máximo una semana, o desde antes de la última migración) | Lo que mida la prueba de restauración (anotar abajo) |
| Producción | Minutos (respaldos automáticos de Azure) | Por medir si se hace una restauración en Azure |

## Registro de pruebas

| Fecha | Quién | Ambiente | Respaldo | Tamaño | Tiempo de la prueba | Resultado |
| --- | --- | --- | --- | --- | --- | --- |
| 26/11/2026 | Laura | Local | VacanJefDB_20261126_143825.bak | 5.9M | 49,5 s | OK: CHECKDB sin errores, 15 tablas, última migración HU009Notificaciones; sin fotos locales |
