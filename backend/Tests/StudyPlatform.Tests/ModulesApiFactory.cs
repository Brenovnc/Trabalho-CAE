using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using StudyPlatform.Api.Data;

namespace StudyPlatform.Tests;

public sealed class ModulesApiFactory : WebApplicationFactory<Program>
{
    private const string DatabaseName = "trabalho_cae_modules_test";
    private readonly string connectionString = CreateConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["Frontend:Origin"] = "http://localhost:5173",
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (db.Database.GetDbConnection().Database != DatabaseName)
            throw new InvalidOperationException("Refusing to clean a non-test database.");
        await db.Database.MigrateAsync();
        await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Teachers\", \"Classrooms\", \"Students\" CASCADE");
    }

    private static string CreateConnectionString()
    {
        var raw = Environment.GetEnvironmentVariable("STUDYPLATFORM_MODULES_TEST_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=trabalho_cae_modules_test;Username=trabalho_cae;Password=local_dev_password";
        var builder = new NpgsqlConnectionStringBuilder(raw);
        if (builder.Database != DatabaseName)
            throw new InvalidOperationException($"Tests require the dedicated {DatabaseName} database.");
        return builder.ConnectionString;
    }
}
