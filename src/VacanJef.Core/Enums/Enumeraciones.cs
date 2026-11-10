namespace VacanJef.Core.Enums;

// Dominios del diccionario de datos (Arquitectura, sección 8.6)
public enum SexoMascota { Macho, Hembra }

public enum TipoRegistroSalud { Vacuna, Alergia, Medicamento }

// RN-007: el costo depende del tipo de servicio
public enum TipoServicio { Basico, Premium, Spa }

public enum EstadoReserva { Confirmada, Activa, Finalizada, Cancelada, Pagada }

public enum TipoObservacion { Salud, Comportamiento, Alimentacion, Actividad }

public enum CanalNotificacion { Plataforma, Correo }

public enum EstadoNotificacion { Pendiente, Enviada, Fallida }
