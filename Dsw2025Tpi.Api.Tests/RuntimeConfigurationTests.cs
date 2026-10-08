using Dsw2025Tpi.Api.Utils;
using Dsw2025Tpi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dsw2025Tpi.Api.Tests;

public class RuntimeConfigurationTests
{
    private const string TestKey = "synthetic-test-key-with-at-least-32-bytes";

    private static Dictionary<string, string?> ValidValues() => new()
    {
        ["ConnectionStrings:DefaultConnection"] = "Server=localhost;Database=ConfigurationTests;Integrated Security=True",
        ["Jwt:Key"] = TestKey,
        ["Jwt:Issuer"] = "test-issuer",
        ["Jwt:Audience"] = "test-audience",
        ["Jwt:ExpireInMinutes"] = "60"
    };

    private static IConfiguration Build(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void CompleteConfigurationIsAccepted()
    {
        var settings = RuntimeConfiguration.Read(Build(ValidValues()));
        Assert.Equal(TestKey, settings.JwtKey);
        Assert.Equal("test-issuer", settings.JwtIssuer);
        Assert.Equal("test-audience", settings.JwtAudience);
        Assert.Equal(60, settings.JwtExpireInMinutes);
    }

    public static IEnumerable<object?[]> MissingValues()
    {
        foreach (var name in ValidValues().Keys)
        {
            foreach (var value in new string?[] { null, "", "   " })
            {
                yield return new object?[] { name, value };
            }
        }
    }

    [Theory]
    [MemberData(nameof(MissingValues))]
    public void MissingRequiredValueIdentifiesTheSetting(string name, string? value)
    {
        var values = ValidValues();
        values[name] = value;
        var error = Assert.Throws<InvalidOperationException>(() => RuntimeConfiguration.Read(Build(values)));
        Assert.Contains(name, error.Message);
        Assert.DoesNotContain(TestKey, error.ToString());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("not-a-number")]
    public void InvalidExpirationIsRejectedBeforeServingRequests(string value)
    {
        var values = ValidValues();
        values["Jwt:ExpireInMinutes"] = value;
        var error = Assert.Throws<InvalidOperationException>(() => RuntimeConfiguration.Read(Build(values)));
        Assert.Contains("Jwt:ExpireInMinutes", error.Message);
    }

    [Fact]
    public void ShortSigningKeyIsRejectedWithoutExposingIt()
    {
        var values = ValidValues();
        values["Jwt:Key"] = "short-secret-sentinel";
        var error = Assert.Throws<InvalidOperationException>(() => RuntimeConfiguration.Read(Build(values)));
        Assert.Contains("Jwt:Key", error.Message);
        Assert.DoesNotContain("short-secret-sentinel", error.ToString());
    }

    [Theory]
    [InlineData("Server=localhost;Password=private-password-sentinel")]
    [InlineData("Database=Test;Password=private-password-sentinel")]
    [InlineData("UnknownOption=private-password-sentinel")]
    public void InvalidSqlConfigurationDoesNotExposeConnectionValues(string connection)
    {
        var values = ValidValues();
        values["ConnectionStrings:DefaultConnection"] = connection;
        var error = Assert.Throws<InvalidOperationException>(() => RuntimeConfiguration.Read(Build(values)));
        Assert.Contains("ConnectionStrings:DefaultConnection", error.Message);
        Assert.DoesNotContain("private-password-sentinel", error.ToString());
    }

    [Fact]
    public void ContextUsesOnlyDefaultConnectionEvenWhenLegacyKeyExists()
    {
        var values = ValidValues();
        values["ConnectionStrings:Dsw2025TpiEntities"] = "Server=wrong-server;Database=WrongDatabase;Integrated Security=True";
        var services = new ServiceCollection().AddDomainServices(Build(values));
        Assert.Single(services, service => service.ServiceType == typeof(Dsw2025TpiContext));
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<Dsw2025TpiContext>();
        Assert.Equal(values["ConnectionStrings:DefaultConnection"], context.Database.GetConnectionString());
    }
}
