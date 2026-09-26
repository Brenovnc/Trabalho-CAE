using Microsoft.EntityFrameworkCore;

namespace StudyPlatform.Api.Data;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options)
{
}
