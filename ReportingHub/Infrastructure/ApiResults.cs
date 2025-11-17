namespace ReportingHub.Api.Infrastructure;

public sealed class ApiResults(ICorrelationIdAccessor accessor)
{
    private string Cid => accessor.CorrelationId;

    public IResult Bad(string code, string message) =>
        Results.BadRequest(Envelope(code, message));

    public IResult NotFound(string code, string message) =>
        Results.NotFound(Envelope(code, message));

    public IResult Forbidden(string code = "Forbidden", string message = "Access denied") =>
        Results.Json(Envelope(code, message), statusCode: StatusCodes.Status403Forbidden);

    public IResult Internal(string code = "InternalError", string message = "An unexpected error occurred") =>
        Results.Json(Envelope(code, message), statusCode: StatusCodes.Status500InternalServerError);

    private object Envelope(string code, string message) => new
    {
        error = new { code, message, correlationId = Cid }
    };
}
