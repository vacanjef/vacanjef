using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using Serilog.Events;
using VacanJef.Core.Entities;
using VacanJef.Infrastructure;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Salud;
using VacanJef.Web.Seguridad;
using VacanJef.Web.Servicios;

var builder = WebApplication.CreateBuilder(args);

// TT-008: registros con Serilog en consola y en un archivo por día (se guardan 14 días).
// Niveles en appsettings.json (sección Serilog). En Docker la carpeta se define con Registros__Ruta.
var carpetaRegistros = builder.Configuration["Registros:Ruta"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "Logs");
builder.Services.AddSerilog((servicios, configuracion) => configuracion
    .ReadFrom.Configuration(builder.Configuration)
    .ReadFrom.Services(servicios)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(carpetaRegistros, "vacanjef-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14));

// Cadena de conexión: nunca en el código. Se toma de la variable de entorno VACANJEF_DB.
var cadenaConexion = builder.Configuration.GetConnectionString("VacanJef")
    ?? builder.Configuration["VACANJEF_DB"]
    ?? throw new InvalidOperationException("Configure la cadena de conexión en la variable de entorno VACANJEF_DB.");

builder.Services.AddInfrastructure(cadenaConexion);

// TT-008: health check en /salud (la aplicación responde y la base de datos acepta conexiones)
builder.Services.AddHealthChecks()
    .AddDbContextCheck<VacanJefDbContext>(RespuestaSalud.ChequeoBaseDeDatos);

// HU-004: carpeta de fotos de mascotas (fuera de wwwroot). En Docker se define con Fotos__Ruta.
var carpetaFotos = builder.Configuration["Fotos:Ruta"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "fotos-mascotas");
builder.Services.AddSingleton(new AlmacenFotosMascotas(carpetaFotos));

builder.Services
    .AddIdentity<Usuario, IdentityRole<int>>(OpcionesIdentity.Configurar)
    .AddEntityFrameworkStores<VacanJefDbContext>()
    .AddErrorDescriber<ErroresIdentityEnEspanol>()
    .AddDefaultTokenProviders();

// HU-015: si el administrador desactiva una cuenta o cambia su rol, sus sesiones se cierran en máximo 1 minuto
builder.Services.Configure<SecurityStampValidatorOptions>(opciones =>
    opciones.ValidationInterval = TimeSpan.FromMinutes(1));

builder.Services.AddScoped<GestionUsuarios>();
builder.Services.AddScoped<ServicioContrasenas>();

builder.Services.ConfigureApplicationCookie(opciones =>
{
    opciones.LoginPath = "/Cuenta/IniciarSesion";
    opciones.AccessDeniedPath = "/Cuenta/AccesoDenegado";
    opciones.Cookie.Name = "VacanJef.Sesion";
    opciones.Cookie.HttpOnly = true;
    opciones.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    opciones.Cookie.SameSite = SameSiteMode.Strict;
    opciones.ExpireTimeSpan = TimeSpan.FromHours(2);
    opciones.SlidingExpiration = true;
});

// Protección CSRF en todos los formularios POST
// HU-016: quien tiene una contraseña asignada por el administrador debe cambiarla antes de seguir
builder.Services.AddControllersWithViews(opciones =>
{
    opciones.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    opciones.Filters.Add<FiltroCambioContrasena>();
});

var app = builder.Build();

await SemillaRoles.CrearAsync(app.Services);
await SemillaAdministrador.CrearAsync(app.Services, app.Configuration);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<CabecerasSeguridadMiddleware>();

// TT-008: una línea por petición (método, ruta, código y tiempo). Nunca registra el contenido
// de los formularios. Las consultas a /salud se omiten para no llenar los registros.
app.UseSerilogRequestLogging(opciones =>
    opciones.GetLevel = (contexto, _, error) =>
        error is not null || contexto.Response.StatusCode >= 500 ? LogEventLevel.Error
        : contexto.Request.Path.StartsWithSegments(RespuestaSalud.Ruta) ? LogEventLevel.Verbose
        : LogEventLevel.Information);

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks(RespuestaSalud.Ruta, new HealthCheckOptions
{
    ResponseWriter = RespuestaSalud.EscribirAsync,
});

app.MapStaticAssets();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
