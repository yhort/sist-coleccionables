namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;

public interface ITenantProvider
{
    Guid EmpresaId { get; }
    void SetEmpresa(Guid empresaId);
}
