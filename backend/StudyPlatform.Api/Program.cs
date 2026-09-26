using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not configured.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

var app = builder.Build();

app.MapGet("/health", (IConfiguration configuration) =>
{
    var configuredConnection = configuration.GetConnectionString("DefaultConnection");

    return Results.Ok(new
    {
        status = "healthy",
        databaseConfigured = !string.IsNullOrWhiteSpace(configuredConnection),
    });
});

app.Run();

public partial class Program { }
