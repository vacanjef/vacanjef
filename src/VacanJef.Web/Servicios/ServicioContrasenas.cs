using Microsoft.AspNetCore.Identity;
using VacanJef.Core.Entities;
using VacanJef.Infrastructure.Data;
using VacanJef.Web.Seguridad;

namespace VacanJef.Web.Servicios;

// HU-016: cambio de contraseña por el titular y restablecimiento por el administrador.
public class ServicioContrasenas(UserManager<Usuario> gestorUsuarios, VacanJefDbContext contexto)
{
    public const string AccionCambiar = "CambioContrasena";
    public const string AccionRestablecer = "RestablecerContrasena";

    public async Task<ResultadoGestion> CambiarAsync(Usuario usuario, string actual, string nueva)
    {
        if (actual == nueva)
            return ResultadoGestion.Error("La nueva contraseña debe ser distinta de la actual.");

        // Identity valida la contraseña actual, aplica RNF-003 a la nueva y renueva el sello de seguridad
        var cambio = await gestorUsuarios.ChangePasswordAsync(usuario, actual, nueva);
        if (!cambio.Succeeded)
            return ResultadoGestion.Error(DescribirErrores(cambio));

        await QuitarMarcaAsync(usuario);
        await AuditarAsync(usuario.Id, AccionCambiar, usuario.Id);
        return ResultadoGestion.Ok("Su contraseña se cambió.");
    }

    public async Task<ResultadoGestion> RestablecerAsync(int usuarioId, string nueva, int administradorId)
    {
        if (usuarioId == administradorId)
            return ResultadoGestion.Error("Para cambiar su propia contraseña use la opción Cambiar contraseña de Mis datos.");

        var usuario = await gestorUsuarios.FindByIdAsync(usuarioId.ToString());
        if (usuario is null)
            return ResultadoGestion.Error("El usuario no existe.");

        // Se valida antes de quitar la contraseña anterior, para no dejar la cuenta sin contraseña
        var errores = new List<IdentityError>();
        foreach (var validador in gestorUsuarios.PasswordValidators)
        {
            var validacion = await validador.ValidateAsync(gestorUsuarios, usuario, nueva);
            errores.AddRange(validacion.Errors);
        }
        if (errores.Count > 0)
            return ResultadoGestion.Error(string.Join(" ", errores.Select(e => e.Description).Distinct()));

        var quitar = await gestorUsuarios.RemovePasswordAsync(usuario);
        if (!quitar.Succeeded)
            return ResultadoGestion.Error(DescribirErrores(quitar));

        var agregar = await gestorUsuarios.AddPasswordAsync(usuario, nueva);
        if (!agregar.Succeeded)
            return ResultadoGestion.Error(DescribirErrores(agregar));

        // Debe cambiarla al entrar; además se desbloquea y se cierran sus sesiones abiertas
        if (!(await gestorUsuarios.GetClaimsAsync(usuario)).Any(c => c.Type == ContrasenaObligatoria.TipoClaim))
            await gestorUsuarios.AddClaimAsync(usuario, ContrasenaObligatoria.CrearMarca());
        await gestorUsuarios.SetLockoutEndDateAsync(usuario, null);
        await gestorUsuarios.ResetAccessFailedCountAsync(usuario);
        await gestorUsuarios.UpdateSecurityStampAsync(usuario);

        await AuditarAsync(administradorId, AccionRestablecer, usuario.Id);
        return ResultadoGestion.Ok(
            $"Se restableció la contraseña de {usuario.Nombre}. Deberá cambiarla la próxima vez que inicie sesión.");
    }

    private async Task QuitarMarcaAsync(Usuario usuario)
    {
        var marcas = (await gestorUsuarios.GetClaimsAsync(usuario))
            .Where(c => c.Type == ContrasenaObligatoria.TipoClaim)
            .ToList();
        if (marcas.Count > 0)
            await gestorUsuarios.RemoveClaimsAsync(usuario, marcas);
    }

    private async Task AuditarAsync(int autorId, string accion, int usuarioId)
    {
        contexto.Auditorias.Add(new Auditoria
        {
            UsuarioId = autorId,
            Accion = accion,
            Recurso = $"Usuario #{usuarioId}",
            Resultado = "permitido",
        });
        await contexto.SaveChangesAsync();
    }

    private static string DescribirErrores(IdentityResult resultado) =>
        string.Join(" ", resultado.Errors.Select(e => e.Description).Distinct());
}
