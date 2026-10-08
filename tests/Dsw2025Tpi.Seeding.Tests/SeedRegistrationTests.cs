using Dsw2025Tpi.Api.Utils;
using Dsw2025Tpi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dsw2025Tpi.Seeding.Tests;

public class SeedRegistrationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("false")]
    public void DisabledIsDefaultInActualRegistration(string? enabled)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Seed:Admin:Enabled"] = enabled,
            ["Seed:Admin:Email"] = "invalid",
            ["ConnectionStrings:DefaultConnection"] = "Server=fallback;Database=seed-tests"
        }).Build();
        using var provider = new ServiceCollection().AddDomainServices(config).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<Dsw2025TpiContext>>();
        var core = options.FindExtension<CoreOptionsExtension>()!;
        Assert.NotNull(core.Seeder);
        Assert.NotNull(core.AsyncSeeder);
        Assert.Contains("fallback", scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>().Database.GetConnectionString());
        using var db = new SeedTestDatabase();
        using var context = db.Open();
        core.Seeder(context, false);
        Assert.Equal(2, context.Roles.Count());
        Assert.Empty(context.Users);
    }

    [Fact]
    public void RejectsMalformedEnabledWithoutEchoingValue()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["Seed:Admin:Enabled"] = "invalid-synthetic-value" }).Build();
        using var provider = new ServiceCollection().AddDomainServices(config).BuildServiceProvider();
        using var scope = provider.CreateScope();
        var error = Assert.Throws<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>());
        Assert.Contains("Seed:Admin:Enabled", error.Message);
        Assert.DoesNotContain("invalid-synthetic-value", error.Message);
    }

    [Fact]
    public void UsesDefaultConnectionWhenBothConnectionNamesExist()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Dsw2025TpiEntities"] = "Server=development;Database=seed-tests",
            ["ConnectionStrings:DefaultConnection"] = "Server=fallback;Database=seed-tests"
        }).Build();
        var services = new ServiceCollection().AddDomainServices(config);
        Assert.Single(services, s => s.ServiceType == typeof(IDbContextOptionsConfiguration<Dsw2025TpiContext>));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.Contains("fallback", scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>().Database.GetConnectionString());
    }
}
