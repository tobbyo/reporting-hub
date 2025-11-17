namespace ReportingHub.Api.Endpoints;

public static class HealthEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
           .WithName("Health")
           .WithOpenApi();

        app.MapGet("/info", () =>
        {
            var info = new
            {
                name = "ReportingHub.Api",
                version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "dev"
            };
            return Results.Ok(info);
        })
        .WithName("Info")
        .WithOpenApi();
    }
}

