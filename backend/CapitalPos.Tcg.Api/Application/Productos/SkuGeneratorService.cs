using System.Globalization;
using System.Text.RegularExpressions;
using CapitalPos.Tcg.Api.Domain.Enums;
using CapitalPos.Tcg.Api.Infrastructure.Persistence;
using CapitalPos.Tcg.Api.Infrastructure.Persistence.Tenant;
using Microsoft.EntityFrameworkCore;

namespace CapitalPos.Tcg.Api.Application.Productos;

/// <summary>
/// Genera SKUs internos cortos y únicos por tenant: <c>RCP-{CAT}-{SECUENCIA}</c>
/// (ej. <c>RCP-ALB-00102</c>).
/// </summary>
public sealed class SkuGeneratorService(ApplicationDbContext db, ITenantProvider tenant)
{
    private static readonly Regex PrefijoSku = new(
        @"^RCP-([A-Z0-9]{2,4})-(\d{1,8})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public const string PrefijoTenant = "RCP";
    public const int SecuenciaDigitos = 5;

    /// <summary>
    /// Reserva el siguiente SKU para la categoría dada (único por empresa).
    /// </summary>
    public async Task<string> GenerarAsync(string categoriaCodigo, CancellationToken cancellationToken)
    {
        var cat = NormalizarCategoria(categoriaCodigo);
        var prefijo = $"{PrefijoTenant}-{cat}-";

        var candidatos = await db.Productos
            .AsNoTracking()
            .Where(p =>
                p.EmpresaId == tenant.EmpresaId
                && (p.SkuInterno != null && p.SkuInterno.StartsWith(prefijo)
                    || p.CodigoSku.StartsWith(prefijo)))
            .Select(p => p.SkuInterno ?? p.CodigoSku)
            .ToListAsync(cancellationToken);

        var maxSeq = 0;
        foreach (var sku in candidatos)
        {
            var match = PrefijoSku.Match(sku);
            if (!match.Success || !string.Equals(match.Groups[1].Value, cat, StringComparison.Ordinal))
            {
                continue;
            }

            if (int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var seq)
                && seq > maxSeq)
            {
                maxSeq = seq;
            }
        }

        var siguiente = maxSeq + 1;
        if (siguiente > 99_999_999)
        {
            throw new BusinessRuleException("Se agotó la secuencia de SKU interno para esta categoría.");
        }

        return $"{prefijo}{siguiente.ToString($"D{SecuenciaDigitos}", CultureInfo.InvariantCulture)}";
    }

    /// <summary>Vista previa del próximo SKU sin reservarlo.</summary>
    public Task<string> PrevisualizarAsync(string categoriaCodigo, CancellationToken cancellationToken)
        => GenerarAsync(categoriaCodigo, cancellationToken);

    public static string ResolverCategoria(
        TipoProducto tipoProducto,
        string? codigoCategoriaExplicit,
        TipoSellado? tipoSellado = null,
        string? tipoAccesorio = null)
    {
        if (!string.IsNullOrWhiteSpace(codigoCategoriaExplicit))
        {
            return NormalizarCategoria(codigoCategoriaExplicit);
        }

        if (tipoProducto == TipoProducto.SELLADO && tipoSellado.HasValue)
        {
            return tipoSellado.Value switch
            {
                TipoSellado.SOBRE => "SOB",
                TipoSellado.CAJA => "CAJ",
                TipoSellado.TIN => "TIN",
                TipoSellado.COLECCION => "COL",
                TipoSellado.CASE => "CAS",
                _ => "SEL"
            };
        }

        if (tipoProducto == TipoProducto.CARTA)
        {
            return "FIG";
        }

        var accesorio = (tipoAccesorio ?? string.Empty).Trim().ToUpperInvariant();
        return accesorio switch
        {
            "BINDER" or "ALBUM" or "ÁLBUM" => "ALB",
            "FUNDAS" => "FUN",
            "PLAYMAT" => "PLA",
            "DECKBOX" => "DEC",
            "TOALLA" => "TOA",
            "SOBRE" => "SOB",
            "FIGURITA" or "CROMO" => "CRO",
            _ => "GEN"
        };
    }

    public static string NormalizarCategoria(string categoriaCodigo)
    {
        var limpio = new string(
            (categoriaCodigo ?? string.Empty)
                .Trim()
                .ToUpperInvariant()
                .Where(c => char.IsLetterOrDigit(c))
                .ToArray());

        if (limpio.Length < 2)
        {
            return "GEN";
        }

        return limpio.Length <= 4 ? limpio : limpio[..4];
    }
}
