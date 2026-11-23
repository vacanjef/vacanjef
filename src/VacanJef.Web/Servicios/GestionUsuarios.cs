using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VacanJef.Core.Constantes;
using VacanJef.Core.Entities;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Seguridad;

namespace VacanJef.Web.Servicios;

public record ResultadoGestion(bool Exito, string Mensaje)
{
    public static ResultadoGestion Ok(string mensaje) => new(true, mensaje);
    public static ResultadoGestion Error(string mensaje) => new(false, mensaje);
}

public record UsuarioResumen(
    int Id, string Nombre, string Correo, string Rol, bool Activo, bool Bloqueado, DateTime FechaRegistro);

// HU-015: el administrador gestiona las cuentas (crear personal, cambiar rol, activar, desactivar y desbloquear).
// Cada acción queda en Auditorias con el administrador que la hizo.
public class GestionUsuarios(UserManager<Usuario> gestorUsuarios, VacanJefDbContext contexto)
{
    public const string AccionCrear = "CrearUsuario";
    public const string AccionCambiarRol = "CambiarRol";
    public const string AccionActivar = "ActivarUsuario";
    public const string AccionDesactivar = "DesactivarUsuario";
    public const string AccionDesbloquear = "DesbloquearUsuario";

    // Del rol de mayor nivel al de menor nivel
    public static readonly IReadOnlyList<string> RolesDelSistema = [Roles.Administrador, Roles.Cuidador, Roles.Dueno];

    // Los dueños se registran solos (HU-001): el administrador crea cuentas de personal
    public static readonly IReadOnlyList<string> RolesCreables = [Roles.Cuidador, Roles.Administrador];

    public static string NombreRol(string rol) => rol == Roles.Dueno ? "Dueño" : rol;

    public async Task<IReadOnlyList<UsuarioResumen>> ListarAsync(string? busqueda = null, string? rol = null)
    {
        var rolesPorUsuario = (await (
                from usuarioRol in contexto.UserRoles
                join r in contexto.Roles on usuarioRol.RoleId equals r.Id
                select new { usuarioRol.UserId, r.Name })
            .ToListAsync())
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => RolesDelSistema.FirstOrDefault(rolSistema => g.Any(x => x.Name == rolSistema)) ?? string.Empty);

        var consulta = contexto.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var texto = busqueda.Trim();
            consulta = consulta.Where(u => u.Nombre.Contains(texto) || (u.Email != null && u.Email.Contains(texto)));
        }

        var usuarios = await consulta.OrderBy(u => u.Nombre).ToListAsync();
        var ahora = DateTimeOffset.UtcNow;

        return usuarios
            .Select(u => new UsuarioResumen(
                u.Id,
                u.Nombre,
                u.Email ?? string.Empty,
                rolesPorUsuario.GetValueOrDefault(u.Id, string.Empty),
                u.Activo,
                u.LockoutEnd is { } fin && fin > ahora,
                u.FechaRegistro))
            .Where(u => string.IsNullOrEmpty(rol) || u.Rol == rol)
            .ToList();
    }

    public async Task<ResultadoGestion> CrearAsync(
        string nombre, string correo, string telefono, string contrasena, string rol, int administradorId)
    {
        if (!RolesCreables.Contains(rol))
            return ResultadoGestion.Error("Solo se pueden crear cuentas de cuidador o de administrador.");

        var usuario = new Usuario
        {
            UserName = correo.Trim(),
            Email = correo.Trim(),
            Nombre = nombre.Trim(),
            PhoneNumber = telefono.Trim(),
            EmailConfirmed = true,
        };

        var creacion = await gestorUsuarios.CreateAsync(usuario, contrasena);
        if (!creacion.Succeeded)
            return ResultadoGestion.Error(DescribirErrores(creacion));

        var asignacion = await gestorUsuarios.AddToRoleAsync(usuario, rol);
        if (!asignacion.Succeeded)
        {
            // Sin rol la cuenta no sirve: se elimina para no dejarla a medias
            await gestorUsuarios.DeleteAsync(usuario);
            return ResultadoGestion.Error(DescribirErrores(asignacion));
        }

        // HU-016: la contraseña inicial la conoce el administrador, así que debe cambiarla al entrar
        await gestorUsuarios.AddClaimAsync(usuario, ContrasenaObligatoria.CrearMarca());

        await AuditarAsync(administradorId, AccionCrear, usuario.Id);
        return ResultadoGestion.Ok($"Se creó la cuenta de {usuario.Nombre} con el rol {NombreRol(rol)}.");
    }

    public async Task<ResultadoGestion> CambiarRolAsync(int usuarioId, string rol, int administradorId)
    {
        if (usuarioId == administradorId)
            return ResultadoGestion.Error("No puede cambiar su propio rol.");

        if (!RolesDelSistema.Contains(rol))
            return ResultadoGestion.Error("El rol no es válido.");

        var usuario = await gestorUsuarios.FindByIdAsync(usuarioId.ToString());
        if (usuario is null)
            return ResultadoGestion.Error("El usuario no existe.");

        var rolesActuales = await gestorUsuarios.GetRolesAsync(usuario);
        if (rolesActuales.Count == 1 && rolesActuales[0] == rol)
            return ResultadoGestion.Ok($"{usuario.Nombre} ya tiene el rol {NombreRol(rol)}.");

        var quitar = await gestorUsuarios.RemoveFromRolesAsync(usuario, rolesActuales);
        if (!quitar.Succeeded)
            return ResultadoGestion.Error(DescribirErrores(quitar));

        var agregar = await gestorUsuarios.AddToRoleAsync(usuario, rol);
        if (!agregar.Succeeded)
            return ResultadoGestion.Error(DescribirErrores(agregar));

        // Cierra sus sesiones abiertas para que entre con el rol nuevo
        await gestorUsuarios.UpdateSecurityStampAsync(usuario);
        await AuditarAsync(administradorId, AccionCambiarRol, usuario.Id);
        return ResultadoGestion.Ok($"{usuario.Nombre} ahora tiene el rol {NombreRol(rol)}.");
    }

    public async Task<ResultadoGestion> CambiarEstadoAsync(int usuarioId, bool activo, int administradorId)
    {
        if (usuarioId == administradorId)
            return ResultadoGestion.Error("No puede desactivar su propia cuenta.");

        var usuario = await gestorUsuarios.FindByIdAsync(usuarioId.ToString());
        if (usuario is null)
            return ResultadoGestion.Error("El usuario no existe.");

        usuario.Activo = activo;
        var actualizacion = await gestorUsuarios.UpdateAsync(usuario);
        if (!actualizacion.Succeeded)
            return ResultadoGestion.Error(DescribirErrores(actualizacion));

        if (!activo)
            await gestorUsuarios.UpdateSecurityStampAsync(usuario); // cierra sus sesiones abiertas

        await AuditarAsync(administradorId, activo ? AccionActivar : AccionDesactivar, usuario.Id);
        return ResultadoGestion.Ok(activo
            ? $"Se activó la cuenta de {usuario.Nombre}."
            : $"Se desactivó la cuenta de {usuario.Nombre}. Ya no puede iniciar sesión.");
    }

    public async Task<ResultadoGestion> DesbloquearAsync(int usuarioId, int administradorId)
    {
        var usuario = await gestorUsuarios.FindByIdAsync(usuarioId.ToString());
        if (usuario is null)
            return ResultadoGestion.Error("El usuario no existe.");

        await gestorUsuarios.SetLockoutEndDateAsync(usuario, null);
        await gestorUsuarios.ResetAccessFailedCountAsync(usuario);

        await AuditarAsync(administradorId, AccionDesbloquear, usuario.Id);
        return ResultadoGestion.Ok($"Se desbloqueó la cuenta de {usuario.Nombre}.");
    }

    private async Task AuditarAsync(int administradorId, string accion, int usuarioId)
    {
        contexto.Auditorias.Add(new Auditoria
        {
            UsuarioId = administradorId,
            Accion = accion,
            Recurso = $"Usuario #{usuarioId}",
            Resultado = "permitido",
        });
        await contexto.SaveChangesAsync();
    }

    private static string DescribirErrores(IdentityResult resultado) =>
        string.Join(" ", resultado.Errors.Select(e => e.Description).Distinct());
}
