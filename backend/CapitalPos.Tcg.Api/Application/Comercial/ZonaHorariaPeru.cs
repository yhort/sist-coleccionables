namespace CapitalPos.Tcg.Api.Application.Comercial;

public static class ZonaHorariaPeru
{
    public static TimeZoneInfo Zona
    {
        get
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("America/Lima");
            }
            catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
            {
                return TimeZoneInfo.CreateCustomTimeZone("PET", TimeSpan.FromHours(-5), "Peru", "Peru");
            }
        }
    }

    public static DateTime ToLocal(DateTimeOffset instante) =>
        TimeZoneInfo.ConvertTime(instante, Zona).DateTime;

    public static DateOnly FechaLocal(DateTimeOffset instante) => DateOnly.FromDateTime(ToLocal(instante));

    public static DateTime FechaEmisionLocal(DateTimeOffset instante)
    {
        var local = ToLocal(instante);
        if (local.Date > DateTime.Now.Date)
        {
            local = DateTime.Now;
        }

        return DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
    }

    public static (DateTimeOffset Inicio, DateTimeOffset Fin) RangoUtcDelDia(DateOnly fechaLocal)
    {
        var inicioLocal = DateTime.SpecifyKind(fechaLocal.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        var inicio = new DateTimeOffset(inicioLocal, Zona.GetUtcOffset(inicioLocal)).ToUniversalTime();
        return (inicio, inicio.AddDays(1));
    }
}
