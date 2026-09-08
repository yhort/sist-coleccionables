using CapitalPos.Tcg.Api.Contracts.Cpe;

namespace CapitalPos.Tcg.Api.Application.Cpe;

public interface ICpePdfGenerator
{
    byte[] Generar(EmitirCpeRequest payload, string? hashFirma, CpePdfFormato formato);
}
