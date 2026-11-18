using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using VacanJef.Core.Entities;
using VacanJef.Infrastructure;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Seguridad;
using VacanJef.Web.Servicios;

var builder = WebApplication.CreateBuilder(args);

// Cadena de conexión: nunca en el código. Se toma de la variable de entorno VACANJEF_DB.
var cadenaConexion = builder.Configuration.GetConnectionString("VacanJef")
    ?? builder.Configuration["VACANJEF_DB"]
    ?? throw new InvalidOperationException("Configure la cadena de conexión en la variable de entorno VACANJEF_DB.");

builder.Services.AddInfrastructure(cadenaConexion);

// HU-004: carpeta de fotos de mascotas (fuera de wwwroot). En Docker se define con Fotos__Ruta.
var carpetaFotos = builder.Configuration["Fotos:Ruta"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "fotos-mascotas");
builder.Services.AddSingleton(new AlmacenFotosMascotas(carpetaFotos));

builder.Services
    .AddIdentity<Usuario, IdentityRole<int>>(OpcionesIdentity.Configurar)
    .AddEntityFrameworkStores<VacanJefDbContext>()
    .AddErrorDescriber<ErroresIdentityEnEspanol>()
    .AddDefaultTokenProviders();

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
builder.Services.AddControllersWithViews(opciones =>
    opciones.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

var app = builder.Build();

await SemillaRoles.CrearAsync(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<CabecerasSeguridadMiddleware>();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
