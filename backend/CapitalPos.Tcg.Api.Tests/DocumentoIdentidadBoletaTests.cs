using CapitalPos.Tcg.Api.Application;
using CapitalPos.Tcg.Api.Application.Comercial;
using CapitalPos.Tcg.Api.Domain.Enums;
using Xunit;

namespace CapitalPos.Tcg.Api.Tests;

public sealed class DocumentoIdentidadBoletaTests
{
    [Fact]
    public void ReceptorSunat_sin_documento_usa_guion_sunat()
    {
        var (tipo, numero) = DocumentoIdentidad.ReceptorSunat(
            TipoDocumentoIdentidad.SIN_DOCUMENTO,
            null);

        Assert.Equal("-", tipo);
        Assert.Equal("-", numero);
    }

    [Fact]
    public void ReceptorSunat_dni_vacio_usa_guion_sunat()
    {
        var (tipo, numero) = DocumentoIdentidad.ReceptorSunat(
            TipoDocumentoIdentidad.DNI,
            "");

        Assert.Equal("-", tipo);
        Assert.Equal("-", numero);
    }

    [Fact]
    public void ReceptorSunat_dni_real_conserva_catalogo_06()
    {
        var (tipo, numero) = DocumentoIdentidad.ReceptorSunat(
            TipoDocumentoIdentidad.DNI,
            "12345678");

        Assert.Equal("1", tipo);
        Assert.Equal("12345678", numero);
    }

    [Fact]
    public void ValidarComprobante_boleta_menor_igual_700_sin_dni_ok()
    {
        DocumentoIdentidad.ValidarComprobante(
            TipoComprobanteSunat.BOLETA,
            TipoDocumentoIdentidad.SIN_DOCUMENTO,
            null,
            700m);
    }

    [Fact]
    public void ValidarComprobante_boleta_mayor_700_sin_dni_falla()
    {
        var ex = Assert.Throws<BusinessRuleException>(() =>
            DocumentoIdentidad.ValidarComprobante(
                TipoComprobanteSunat.BOLETA,
                TipoDocumentoIdentidad.SIN_DOCUMENTO,
                null,
                700.01m));

        Assert.Contains("700", ex.Message);
    }

    [Fact]
    public void NormalizarCliente_solo_nombre_queda_sin_documento()
    {
        var (tipo, numero, nombre) = DocumentoIdentidad.NormalizarCliente(
            "Juan Pérez",
            TipoDocumentoIdentidad.DNI,
            "",
            esPublicoGeneral: false);

        Assert.Equal(TipoDocumentoIdentidad.SIN_DOCUMENTO, tipo);
        Assert.True(string.IsNullOrEmpty(numero));
        Assert.Equal("Juan Pérez", nombre);
        Assert.False(DocumentoIdentidad.EsPublicoGeneral(nombre, tipo, numero));
    }

    [Fact]
    public void NormalizarCliente_publico_general_guarda_numero_null()
    {
        var (tipo, numero, nombre) = DocumentoIdentidad.NormalizarCliente(
            "CLIENTES VARIOS",
            TipoDocumentoIdentidad.SIN_DOCUMENTO,
            "00000000",
            esPublicoGeneral: true);

        Assert.Equal(TipoDocumentoIdentidad.SIN_DOCUMENTO, tipo);
        Assert.Null(numero);
        Assert.Equal("CLIENTES VARIOS", nombre);
        Assert.True(DocumentoIdentidad.EsPublicoGeneral(nombre, tipo, numero));
    }

    [Fact]
    public void NumeroParaPersistir_legado_cero_queda_null()
    {
        Assert.Null(DocumentoIdentidad.NumeroParaPersistir("00000000"));
        Assert.Null(DocumentoIdentidad.NumeroParaPersistir("0000000"));
        Assert.Null(DocumentoIdentidad.NumeroParaPersistir(""));
        Assert.Null(DocumentoIdentidad.NumeroParaPersistir(null));
        Assert.Equal("12345678", DocumentoIdentidad.NumeroParaPersistir("12345678"));
    }
}
