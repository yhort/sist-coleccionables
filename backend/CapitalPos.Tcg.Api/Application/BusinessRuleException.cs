namespace CapitalPos.Tcg.Api.Application;

public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException(string message, int statusCode = 400) : base(message)
    {
        StatusCode = statusCode;
    }

    public BusinessRuleException(string message, Exception innerException, int statusCode = 400)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
