using CapitalPos.Tcg.Api.Application.Cpe;
using CapitalPos.Tcg.Api.Contracts.Cpe;
using CapitalPos.Tcg.Api.Domain.Enums;

namespace CapitalPos.Tcg.Api.Infrastructure.Cpe;

/// <summary>
/// Firma UBL local y genera CDR de aceptación. Usado cuando no hay ejecutable CPE
/// o como paso previo al POST a SUNAT/OSE.
/// </summary>
public sealed class LocalUblCpeEmisor : ICpeEmisor
{
    public string Nombre => "LocalUBL";

    public Task<CpeEmisionResultado> EmitirAsync(EmitirCpeRequest request, CancellationToken cancellationToken)
    {
        var (xml, hash) = UblCpeXmlBuilder.Construir(request);
        var cdr = UblCpeXmlBuilder.CdrAceptacion(request, hash);
        return Task.FromResult(new CpeEmisionResultado
        {
            Estado = EstadoEmisionSunat.ACEPTADO,
            Xml = xml,
            Cdr = cdr,
            HashFirma = hash,
            Mensaje = $"XML UBL 2.1 firmado ({request.Serie}-{request.Correlativo:00000000}). Envío local aceptado.",
            Serie = request.Serie,
            Correlativo = request.Correlativo
        });
    }
}
