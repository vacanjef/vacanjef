using System.Security.Claims;
using VacanJef.Core.Constantes;

namespace VacanJef.Web.Navegacion;

public record ElementoMenu(string Texto, string Ruta);

// Menú principal según el rol del usuario (TT-003).
// Las rutas son provisionales: cada responsable ajusta la suya al implementar su historia.
public static class MenuNavegacion
{
    private static readonly IReadOnlyList<ElementoMenu> MenuDueno =
    [
        new("Mis mascotas", "/Mascotas"),
        new("Reservas", "/Reservas"),
        new("Monitoreo", "/Monitoreo"),
        new("Notificaciones", "/Notificaciones"),
    ];

    private static readonly IReadOnlyList<ElementoMenu> MenuCuidador =
    [
        new("Estadías", "/Estadias"),
        new("Observaciones", "/Observaciones"),
        new("Monitoreo", "/Monitoreo"),
    ];

    private static readonly IReadOnlyList<ElementoMenu> MenuAdministrador =
    [
        new("Usuarios", "/Usuarios"),
        new("Reservas", "/Reservas"),
        new("Cámaras", "/Camaras"),
        new("Observaciones", "/Observaciones"),
        new("Auditoría", "/Auditoria"),
    ];

    // Si un usuario tiene varios roles, se muestra el de mayor nivel.
    public static IReadOnlyList<ElementoMenu> Para(ClaimsPrincipal usuario)
    {
        if (usuario.Identity?.IsAuthenticated != true)
        {
            return [];
        }

        if (usuario.IsInRole(Roles.Administrador))
        {
            return MenuAdministrador;
        }

        if (usuario.IsInRole(Roles.Cuidador))
        {
            return MenuCuidador;
        }

        if (usuario.IsInRole(Roles.Dueno))
        {
            return MenuDueno;
        }

        return [];
    }

    // Activo si la ruta actual es la del elemento o una subruta (/Mascotas/Crear).
    public static bool EstaActivo(ElementoMenu elemento, string? rutaActual)
    {
        if (string.IsNullOrEmpty(rutaActual))
        {
            return false;
        }

        return rutaActual.Equals(elemento.Ruta, StringComparison.OrdinalIgnoreCase)
            || rutaActual.StartsWith(elemento.Ruta + "/", StringComparison.OrdinalIgnoreCase);
    }
}
