namespace CapitalPos.Tcg.Api.Infrastructure.Cpe;

public sealed class CpeApiOptions
{
    public const string SectionName = "CpeApi";

    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    /// <summary>
    /// Local = UBL en proceso. Http (u otro valor) = POST a capitalpos-cpe-api.
    /// </summary>
    public string Modo { get; set; } = "Local";
    public int TimeoutSeconds { get; set; } = 20;
}
