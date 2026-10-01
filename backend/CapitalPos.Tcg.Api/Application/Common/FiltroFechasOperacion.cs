using System.Globalization;

namespace CapitalPos.Tcg.Api.Application.Common;

/// <summary>
/// Parseo de query params <c>fechaDesde</c>/<c>fechaHasta</c> (YYYY-MM-DD) a
/// <see cref="DateTime"/> UTC con límites de día operativo en Perú (UTC-5),
/// aptos para filtros LINQ traducibles por EF Core.
/// </summary>
public static class FiltroFechasOperacion
{
    /// <summary>Zona horaria operativa del negocio (Perú).</summary>
    public static readonly TimeSpan ZonaHorariaOperacion = TimeSpan.FromHours(-5);

    /// <summary>
    /// Inicio inclusivo del día (00:00:00 en UTC-5) como <see cref="DateTime"/> UTC.
    /// </summary>
    public static DateTime? ParseInicioDia(string? valor)
    {
        if (!TryParseFecha(valor, out var fecha))
        {
            return null;
        }

        return new DateTimeOffset(fecha, TimeOnly.MinValue, ZonaHorariaOperacion).UtcDateTime;
    }

    /// <summary>
    /// Fin exclusivo del día (00:00:00 del día siguiente en UTC-5) como <see cref="DateTime"/> UTC.
    /// Usar con comparación <c>&lt;</c> en LINQ.
    /// </summary>
    public static DateTime? ParseFinDiaExclusivo(string? valor)
    {
        if (!TryParseFecha(valor, out var fecha))
        {
            return null;
        }

        return new DateTimeOffset(fecha.AddDays(1), TimeOnly.MinValue, ZonaHorariaOperacion).UtcDateTime;
    }

    private static bool TryParseFecha(string? valor, out DateOnly fecha)
    {
        fecha = default;
        if (string.IsNullOrWhiteSpace(valor))
        {
            return false;
        }

        var texto = valor.Trim();
        if (DateOnly.TryParseExact(
                texto,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out fecha))
        {
            return true;
        }

        return DateOnly.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);
    }
}
