using CapitalPos.Tcg.Api.Contracts.Cpe;

namespace CapitalPos.Tcg.Api.Application.Cpe;

public interface ICpeEmisor
{
    string Nombre { get; }

    Task<CpeEmisionResultado> EmitirAsync(EmitirCpeRequest request, CancellationToken cancellationToken);
}
