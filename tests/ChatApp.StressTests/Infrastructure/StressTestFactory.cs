using ChatApp.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ChatApp.StressTests.Infrastructure;

/// <summary>
/// Boots the real ASP.NET Core application in-process against a dedicated
/// "chatapp_stress" Postgres database and the real Redis at localhost:6379.
/// Isolates stress tests from the development database.
/// </summary>
public class StressTestFactory : WebApplicationFactory<Program>
{
    public const string ConnectionString =
        "Host=localhost;Port=5432;Database=chatapp_stress;" +
        "Username=admin;Password=password123;" +
        "MaxPoolSize=50;CommandTimeout=30;Include Error Detail=true";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("StressTests");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Reduce log noise during stress runs.
            // DB is overridden in ConfigureServices; JWT comes from base appsettings.json.
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:LogLevel:Default"]              = "Warning",
                ["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning",
                ["Serilog:MinimumLevel"]                  = "Warning",
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace the DbContext registration with the stress-test DB
            services.RemoveAll<DbContextOptions<ChatDbContext>>();
            services.RemoveAll<ChatDbContext>();

            services.AddDbContext<ChatDbContext>(options =>
                options.UseNpgsql(ConnectionString));
        });
    }

    /// <summary>
    /// Drops and recreates the stress-test schema. Call once per test class
    /// in InitializeAsync() to guarantee a clean slate.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();
    }
}
