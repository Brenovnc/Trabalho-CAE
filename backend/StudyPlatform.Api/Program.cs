using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.Middleware;
using StudyPlatform.Api.Validators;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not configured.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var response = ModelStateErrorMapper.CreateResponse(context.ModelState);
            return new ObjectResult(response) { StatusCode = response.Status };
        };
    });

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.MapGet("/health", (IConfiguration configuration) =>
{
    var configuredConnection = configuration.GetConnectionString("DefaultConnection");

    return Results.Ok(new
    {
        status = "healthy",
        databaseConfigured = !string.IsNullOrWhiteSpace(configuredConnection),
    });
});

app.MapControllers();

app.Run();

public partial class Program { }
