using System.IdentityModel.Tokens.Jwt;
using System.Security.Authentication;
using System.Security.Claims;
using System.Security.Cryptography;
using Dsw2025Tpi.Api.Controllers;
using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Services;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Dsw2025Tpi.Api.Tests;

public class AuthenticationServiceTests
{
    private const string InvalidCredentialsMessage = "Usuario o contraseña inválidos.";
    private const string Username = "login-test-user";
    private const string Password = "synthetic-login-test-password";

    [Theory]
    [InlineData(Role.Admin)]
    [InlineData(Role.User)]
    public async Task ValidUserKeepsLoginResponseAndSignedTokenRole(string roleName)
    {
        using var context = CreateContext();
        var role = new Role { Name = roleName };
        var user = new User(Username, "login@example.invalid", Password, role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var key = RandomNumberGenerator.GetBytes(32);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = Convert.ToBase64String(key),
            ["Jwt:Issuer"] = "login-tests",
            ["Jwt:Audience"] = "login-test-users",
            ["Jwt:ExpireInMinutes"] = "60"
        }).Build();
        var service = new AuthenticationService(context, new JwtTokenServices(configuration));

        var result = Assert.IsType<OkObjectResult>(await new AuthController(service)
            .Login(new RequestLoginModel(Username, Password)));
        var response = Assert.IsType<ResponseLoginModel>(result.Value);
        Assert.Equal(user.Id, response.Id);
        Assert.Equal(user.Username, response.Username);
        Assert.Equal(user.Email, response.Email);
        Assert.Equal(roleName, response.Role);
        Assert.Equal(Guid.Empty, response.CustomerId);
        Assert.Equal("", response.FirstName);
        Assert.Equal("", response.LastName);
        Assert.Equal("", response.Address);
        Assert.Equal("", response.PhoneNumber);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));

        // Validate the signature and claims, rather than only decoding the JWT payload.
        var principal = new JwtSecurityTokenHandler().ValidateToken(response.Token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "login-tests",
            ValidateAudience = true,
            ValidAudience = "login-test-users",
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(Convert.ToBase64String(key)))
        }, out _);
        Assert.True(principal.IsInRole(roleName));
        Assert.Equal(Username, principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public async Task UserWithoutRoleIsRejectedBeforeTokenGeneration()
    {
        using var context = new MissingRoleContext(new DbContextOptionsBuilder<Dsw2025TpiContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.Users.Add(new User
        {
            Username = Username,
            Email = "no-role@example.invalid",
            Password = Password,
            RoleId = Guid.NewGuid()
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        // The test-only optional relationship lets the query return the user with Role == null.
        var user = await context.Users.Include(u => u.Role).Include(u => u.Customer)
            .SingleAsync(u => u.Username == Username);
        Assert.Null(user.Role);

        var service = CreateServiceWithoutJwtConfiguration(context);
        var request = new RequestLoginModel(Username, Password);
        var error = await Assert.ThrowsAsync<InvalidCredentialException>(() => service.Login(request));
        Assert.Equal(InvalidCredentialsMessage, error.Message);
        await AssertUnauthorizedWithoutToken(service, request);
    }

    [Fact]
    public async Task OrphanWithProductionMappingIsAlsoRejectedWithoutToken()
    {
        using var context = CreateContext();
        context.Users.Add(new User
        {
            Username = Username,
            Email = "orphan@example.invalid",
            Password = Password,
            RoleId = Guid.NewGuid()
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        Assert.True(await context.Users.AnyAsync(u => u.Username == Username));

        await AssertUnauthorizedWithoutToken(CreateServiceWithoutJwtConfiguration(context),
            new RequestLoginModel(Username, Password));
    }

    [Theory]
    [InlineData("unknown-user", Password)]
    [InlineData(Username, "wrong-password")]
    public async Task InvalidCredentialsKeepControlledRejection(string username, string password)
    {
        using var context = CreateContext();
        context.Users.Add(new User(Username, "login@example.invalid", Password, new Role { Name = Role.User }));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var service = CreateServiceWithoutJwtConfiguration(context);
        var request = new RequestLoginModel(username, password);

        var error = await Assert.ThrowsAsync<InvalidCredentialException>(() => service.Login(request));
        Assert.Equal(InvalidCredentialsMessage, error.Message);
        await AssertUnauthorizedWithoutToken(service, request);
    }

    [Theory]
    [InlineData("", Password)]
    [InlineData("   ", Password)]
    [InlineData(Username, "")]
    [InlineData(Username, "   ")]
    public async Task MissingCredentialsKeepBadRequest(string username, string password)
    {
        using var context = CreateContext();
        var service = CreateServiceWithoutJwtConfiguration(context);
        var result = await new AuthController(service).Login(new RequestLoginModel(username, password));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal("El usuario y la contraseña son obligatorios.", badRequest.Value);
    }

    private static Dsw2025TpiContext CreateContext() => new(
        new DbContextOptionsBuilder<Dsw2025TpiContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private sealed class MissingRoleContext(DbContextOptions<Dsw2025TpiContext> options)
        : Dsw2025TpiContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Production's required FK makes Include use an inner join and filter out orphans.
            // Only this isolated model uses a nullable shadow FK to exercise the null-role guard.
            // No production mapping, entity or database schema is changed.
            modelBuilder.Entity<User>().HasOne(u => u.Role).WithMany(r => r.Users)
                .HasForeignKey("OptionalRoleId").IsRequired(false);
        }
    }

    private static AuthenticationService CreateServiceWithoutJwtConfiguration(Dsw2025TpiContext context) =>
        // GenerateToken would throw ArgumentException without Jwt:Key. These rejection tests
        // must instead return InvalidCredentialException/401, proving signing was never reached.
        new(context, new JwtTokenServices(new ConfigurationBuilder().Build()));

    private static async Task AssertUnauthorizedWithoutToken(AuthenticationService service, RequestLoginModel request)
    {
        var result = await new AuthController(service).Login(request);
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorized.StatusCode);
        Assert.Equal(InvalidCredentialsMessage, Assert.IsType<string>(unauthorized.Value));
    }
}
