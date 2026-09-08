namespace CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;

public sealed class TenantProvider : ITenantProvider
{
    public Guid EmpresaId { get; private set; }

    public void SetEmpresa(Guid empresaId) => EmpresaId = empresaId;
}
