# TT-006: imágenes de VacanJef
#   docker build --target web .          → aplicación web
#   docker build --target migraciones .  → crea/actualiza la base de datos

# --- Compilación ---------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS compilacion
WORKDIR /src

# Primero solo los proyectos, para aprovechar la caché de la restauración
COPY global.json Directory.Build.props ./
COPY src/VacanJef.Core/VacanJef.Core.csproj src/VacanJef.Core/
COPY src/VacanJef.Infrastructure/VacanJef.Infrastructure.csproj src/VacanJef.Infrastructure/
COPY src/VacanJef.Web/VacanJef.Web.csproj src/VacanJef.Web/
RUN dotnet restore src/VacanJef.Web/VacanJef.Web.csproj

COPY src/ src/
RUN dotnet publish src/VacanJef.Web/VacanJef.Web.csproj -c Release -o /app/web --no-restore

# --- Paquete de migraciones (EF Core bundle) ------------------------------
FROM compilacion AS paquete-migraciones
RUN dotnet tool install --global dotnet-ef --version 10.0.10
ENV PATH="$PATH:/root/.dotnet/tools"
RUN dotnet ef migrations bundle \
      -p src/VacanJef.Infrastructure \
      -s src/VacanJef.Infrastructure \
      --configuration Release \
      -o /app/efbundle

# --- Migraciones: aplica las migraciones y termina ------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS migraciones
WORKDIR /app
COPY --from=paquete-migraciones /app/efbundle .
USER $APP_UID
# Usa la variable de entorno VACANJEF_DB (VacanJefDbContextFactory)
ENTRYPOINT ["./efbundle"]

# --- Web (imagen final por defecto) ---------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS web
WORKDIR /app
COPY --from=compilacion /app/web .
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "VacanJef.Web.dll"]
