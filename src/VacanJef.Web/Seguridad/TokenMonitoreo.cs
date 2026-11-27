using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace VacanJef.Web.Seguridad;

// HU-007 / RN-004: token temporal emitido por VacanJef para abrir la transmisión.
// Va cifrado y firmado con Data Protection, dura pocos minutos y solo sirve para
// la reserva y el usuario que lo pidieron. No contiene la dirección de la cámara.
public class TokenMonitoreo(IDataProtectionProvider proveedor)
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(5);

    private readonly ITimeLimitedDataProtector protector =
        proveedor.CreateProtector("VacanJef.Monitoreo.v1").ToTimeLimitedDataProtector();

    public string Crear(int reservaId, int usuarioId) =>
        protector.Protect($"{reservaId}:{usuarioId}", Vigencia);

    public bool Validar(string? token, int usuarioId, out int reservaId)
    {
        reservaId = 0;
        if (string.IsNullOrWhiteSpace(token))
            return false;

        try
        {
            var partes = protector.Unprotect(token).Split(':');
            return partes.Length == 2
                && int.TryParse(partes[0], out reservaId)
                && int.TryParse(partes[1], out var usuarioDelToken)
                && usuarioDelToken == usuarioId;
        }
        catch (CryptographicException)
        {
            // Token alterado, de otra aplicación o vencido
            return false;
        }
    }
}
