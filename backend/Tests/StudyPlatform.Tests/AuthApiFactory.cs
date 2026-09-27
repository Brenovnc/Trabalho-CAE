using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using StudyPlatform.Api.Data;

namespace StudyPlatform.Tests;

public sealed class AuthApiFactory : WebApplicationFactory<Program>
{
    private const string TestDatabaseName = "trabalho_cae_auth_test";
    private readonly string _connectionString = CreateTestConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["Frontend:Origin"] = "http://localhost:5173",
            }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(_connectionString));
        });
    }

    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (dbContext.Database.GetDbConnection().Database != TestDatabaseName)
        {
            throw new InvalidOperationException(
                "Refusing to clean any database other than the dedicated authentication test database.");
        }

        await dbContext.Database.MigrateAsync();
        await dbContext.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE \"Teachers\", \"Classrooms\", \"Students\" CASCADE");
    }

    private static string CreateTestConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable("STUDYPLATFORM_TEST_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=trabalho_cae_auth_test;Username=trabalho_cae;Password=local_dev_password";
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (builder.Database != TestDatabaseName)
        {
            throw new InvalidOperationException(
                $"Integration tests require the dedicated {TestDatabaseName} database.");
        }

        return builder.ConnectionString;
    }
}
