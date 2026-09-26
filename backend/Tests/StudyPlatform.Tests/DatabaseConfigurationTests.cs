using Microsoft.EntityFrameworkCore;
using StudyPlatform.Api.Data;
using Xunit;

namespace StudyPlatform.Tests;

public sealed class DatabaseConfigurationTests
{
    [Fact]
    public void ApplicationDbContextUsesPostgreSqlProvider()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=trabalho_cae;Username=test;Password=test")
            .Options;

        using var context = new ApplicationDbContext(options);

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
    }
}
