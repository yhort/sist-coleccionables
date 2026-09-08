using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

public class IntegracionWooCommerce : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public string UrlTienda { get; set; } = string.Empty;
    public string ConsumerKeyCifrado { get; set; } = string.Empty;
    public string ConsumerSecretCifrado { get; set; } = string.Empty;
    public Guid SedeOrigenId { get; set; }
    public ModoSincronizacionWoo ModoSincronizacion { get; set; } = ModoSincronizacionWoo.MANUAL;
    public ModoRecepcionPedidosWoo ModoRecepcionPedidos { get; set; } = ModoRecepcionPedidosWoo.WEBHOOK;
    public EstadoConexionWoo EstadoConexion { get; set; } = EstadoConexionWoo.DESCONECTADO;
    public string? MensajeConexion { get; set; }
    public DateTimeOffset? UltimoIntentoConexion { get; set; }
    public bool Activa { get; set; } = true;
    public DateTimeOffset FechaCreacion { get; set; }
    public DateTimeOffset FechaActualizacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
    public Sede SedeOrigen { get; set; } = null!;
    public ICollection<WooCommerceMapeoProducto> Mapeos { get; set; } = new List<WooCommerceMapeoProducto>();
    public ICollection<WooCommerceSyncLog> Logs { get; set; } = new List<WooCommerceSyncLog>();
}
