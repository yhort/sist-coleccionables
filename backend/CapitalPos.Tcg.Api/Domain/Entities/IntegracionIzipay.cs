using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Domain.Entities;

/// <summary>
/// Credenciales IPN por tenant. <see cref="HmacSha256Clave"/> se persiste cifrada con Data Protection.
/// </summary>
public class IntegracionIzipay : IEmpresaScoped
{
    public Guid Id { get; set; }
    public Guid EmpresaId { get; set; }
    public string ShopId { get; set; } = string.Empty;
    public string HmacSha256Clave { get; set; } = string.Empty;
    public ModoIzipay Modo { get; set; } = ModoIzipay.TEST;
    public bool Activa { get; set; } = true;
    public DateTimeOffset FechaCreacion { get; set; }
    public DateTimeOffset FechaActualizacion { get; set; }

    public Empresa Empresa { get; set; } = null!;
}
