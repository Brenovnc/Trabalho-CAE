using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.Data;
using StudyPlatform.Api.DTOs.Auth;
using StudyPlatform.Api.Middleware;
using StudyPlatform.Api.Models;
using StudyPlatform.Api.Services.Auth;
using StudyPlatform.Api.Services.Learning;
using StudyPlatform.Api.Services.Modules;
using StudyPlatform.Api.Services.Classrooms;
using StudyPlatform.Api.Services.Students;
using StudyPlatform.Api.Validators;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("TrabalhoCAE");
if (builder.Environment.IsDevelopment())
{
    dataProtection.UseEphemeralDataProtectionProvider();
}
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string 'DefaultConnection' was not configured.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ClassroomService>();
builder.Services.AddScoped<StudentManagementService>();
builder.Services.AddScoped<TemporaryStudentAccessCodeService>();
builder.Services.AddScoped<CsvStudentParser>();
builder.Services.AddScoped<StudentCsvImportService>();
builder.Services.AddScoped<ModuleService>();
builder.Services.AddScoped<ModuleTransferService>();
builder.Services.AddScoped<PrerequisiteService>();
builder.Services.AddScoped<FsrsService>();
builder.Services.AddScoped<PerformanceRatingService>();
builder.Services.AddScoped<LearningProgressionService>();
builder.Services.AddScoped<ConceptEligibilityService>();
builder.Services.AddScoped<ActivityCompatibilityService>();
// Stage 10 session services
builder.Services.AddScoped<ActivitySelectionService>();
builder.Services.AddScoped<StudySessionService>();
builder.Services.AddScoped<IPasswordHasher<Teacher>, PasswordHasher<Teacher>>();
builder.Services.AddScoped<IPasswordHasher<Student>, PasswordHasher<Student>>();
builder.Services.AddScoped<ApiCookieAuthenticationEvents>();

var secureCookiePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "CAE-XSRF";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = secureCookiePolicy;
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "CAE.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = secureCookiePolicy;
        options.Cookie.MaxAge = TimeSpan.FromHours(8);
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = false;
        options.EventsType = typeof(ApiCookieAuthenticationEvents);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.Authenticated, policy => policy.RequireAuthenticatedUser());
    options.AddPolicy(AuthPolicies.Teacher, policy => policy.RequireRole(AuthRoles.Teacher));
    options.AddPolicy(AuthPolicies.Student, policy => policy.RequireRole(AuthRoles.Student));
});

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var response = ModelStateErrorMapper.CreateResponse(context.ModelState);
            return new ObjectResult(response) { StatusCode = response.Status };
        };
    });

const string developmentCorsPolicy = "DevelopmentFrontend";
if (builder.Environment.IsDevelopment())
{
    var frontendOrigin = builder.Configuration["Frontend:Origin"] ?? "http://localhost:5173";
    builder.Services.AddCors(options => options.AddPolicy(developmentCorsPolicy, policy =>
        policy.WithOrigins(frontendOrigin)
            .AllowCredentials()
            .AllowAnyHeader()
            .AllowAnyMethod()));
}

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseCors(developmentCorsPolicy);
}
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CsrfValidationMiddleware>();

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
