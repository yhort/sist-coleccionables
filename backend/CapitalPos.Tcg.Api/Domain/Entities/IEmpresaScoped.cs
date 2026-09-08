namespace CapitalPos.Tcg.Api.Domain.Entities;

/// <summary>
/// Entidad de negocio acotada a un tenant. El filtro global EF usa <see cref="EmpresaId"/>.
/// </summary>
public interface IEmpresaScoped
{
    Guid EmpresaId { get; }
}
