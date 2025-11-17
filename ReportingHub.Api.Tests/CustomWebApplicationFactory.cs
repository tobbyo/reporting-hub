using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace ReportingHub.Api.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // If your Program.cs registers auth/filters/middleware,
        // you can override/disable them here for tests if needed, e.g.:
        // builder.ConfigureServices(services => { ... });

        return base.CreateHost(builder);
    }
}
