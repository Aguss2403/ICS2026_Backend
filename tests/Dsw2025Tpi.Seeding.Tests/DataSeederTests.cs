using Dsw2025Tpi.Data;
using Dsw2025Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Dsw2025Tpi.Seeding.Tests;

public class DataSeederTests
{
    // Test passwords come from the process environment, never from source-controlled defaults.
    internal static string Password => Environment.GetEnvironmentVariable("ICS_TEST_ADMIN_PASSWORD")
        ?? throw new InvalidOperationException("Set ICS_TEST_ADMIN_PASSWORD to a synthetic test password.");

    internal static SeedAdminOptions Enabled(string? username = "seed-test-admin",
        string? email = "seed-test-admin@example.invalid", string? password = null) => new()
        { Enabled = true, Username = username, Email = email, Password = password ?? Password };

    private static Task Seed(Dsw2025TpiContext context, SeedAdminOptions admin, bool async)
    {
        if (async) return DataSeeder.SeedAsync(context, admin);
        DataSeeder.Seed(context, admin);
        return Task.CompletedTask;
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task CompletesEachMissingRoleAndPreservesExistingIds(bool hasAdmin, bool hasUser, bool async)
    {
        using var db = new SeedTestDatabase();
        using var context = db.Open();
        var existing = new List<Role>();
        if (hasAdmin) existing.Add(new Role { Name = Role.Admin });
        if (hasUser) existing.Add(new Role { Name = Role.User });
        context.Roles.AddRange(existing);
        await context.SaveChangesAsync();
        var before = existing.ToDictionary(r => r.Name, r => r.Id);

        await Seed(context, new(), async);
        await Seed(context, new(), async);
        using var verify = db.Open();
        var roles = await verify.Roles.ToListAsync();
        Assert.Equal(2, roles.Count);
        Assert.Equal(new[] { Role.Admin, Role.User }, roles.Select(r => r.Name).Order());
        foreach (var role in before) Assert.Equal(role.Value, roles.Single(r => r.Name == role.Key).Id);
        Assert.Empty(verify.Users);
        Assert.Empty(verify.Customers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CreatesOneAdminWithoutCustomerAndDoesNotRotatePassword(bool async)
    {
        using var db = new SeedTestDatabase();
        using var context = db.Open();
        await Seed(context, Enabled(), async);
        var original = await context.Users.AsNoTracking().SingleAsync();
        await Seed(context, Enabled(password: Password + "-changed"), async);

        using var verify = db.Open();
        var admin = await verify.Users.Include(u => u.Role).SingleAsync();
        Assert.Equal(original.Id, admin.Id);
        Assert.Equal(original.RoleId, admin.RoleId);
        Assert.True(admin.Password == Password, "Stored password must be preserved.");
        Assert.Equal(Role.Admin, admin.Role!.Name);
        Assert.Empty(verify.Customers);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisabledIgnoresIncompleteAdminConfiguration(bool async)
    {
        using var db = new SeedTestDatabase();
        using var context = db.Open();
        await Seed(context, new() { Email = "invalid" }, async);
        Assert.Equal(2, await context.Roles.CountAsync());
        Assert.Empty(context.Users);
    }

    [Theory]
    [InlineData("username", false)]
    [InlineData("email", false)]
    [InlineData("password", false)]
    [InlineData("username", true)]
    [InlineData("email", true)]
    [InlineData("password", true)]
    public async Task MissingEnabledSettingFailsWithoutWriting(string missing, bool async)
    {
        using var db = new SeedTestDatabase();
        using var context = db.Open();
        var admin = new SeedAdminOptions
        {
            Enabled = true,
            Username = missing == "username" ? null : "seed-test-admin",
            Email = missing == "email" ? null : "seed-test-admin@example.invalid",
            Password = missing == "password" ? null : Password
        };
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Seed(context, admin, async));
        Assert.False(error.Message.Contains(Password, StringComparison.Ordinal), "Error must not contain credentials.");
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.Empty(context.Roles);
        Assert.Empty(context.Users);
    }

    [Theory]
    [InlineData("username-too-long")]
    [InlineData("username-whitespace")]
    [InlineData("email-format")]
    [InlineData("email-too-long")]
    [InlineData("password-too-long")]
    [InlineData("password-whitespace")]
    public void RejectsInvalidValuesWithoutEchoingThem(string invalid)
    {
        using var db = new SeedTestDatabase();
        using var context = db.Open();
        var admin = new SeedAdminOptions
        {
            Enabled = true,
            Username = invalid == "username-too-long" ? new string('u', 51) : invalid == "username-whitespace" ? " admin " : "seed-test-admin",
            Email = invalid == "email-format" ? "invalid" : invalid == "email-too-long" ? new string('e', 90) + "@example.invalid" : "seed-test-admin@example.invalid",
            Password = invalid == "password-too-long" ? new string('p', 101) : invalid == "password-whitespace" ? "  " : Password
        };
        var error = Assert.Throws<InvalidOperationException>(() => DataSeeder.Seed(context, admin));
        Assert.False(error.Message.Contains(Password, StringComparison.Ordinal), "Error must not contain credentials.");
        Assert.Empty(context.Roles);
        Assert.Empty(context.Users);
    }

    [Theory]
    [InlineData("username", false)]
    [InlineData("email", false)]
    [InlineData("wrong-role", false)]
    [InlineData("split-accounts", false)]
    [InlineData("duplicate-email", false)]
    [InlineData("username", true)]
    [InlineData("email", true)]
    [InlineData("wrong-role", true)]
    [InlineData("split-accounts", true)]
    [InlineData("duplicate-email", true)]
    public async Task ConflictNeverChangesOrPromotesAccounts(string conflict, bool async)
    {
        using var db = new SeedTestDatabase();
        using var context = db.Open();
        var role = new Role { Name = conflict == "wrong-role" ? Role.User : Role.Admin };
        context.Roles.Add(role);
        var first = new User(conflict == "email" ? "other-user" : "seed-test-admin",
            conflict is "username" or "split-accounts" ? "other@example.invalid" : "seed-test-admin@example.invalid",
            Password, role);
        context.Users.Add(first);
        if (conflict is "split-accounts" or "duplicate-email")
            context.Users.Add(new User("another-user", "seed-test-admin@example.invalid", Password, role));
        await context.SaveChangesAsync();
        var before = await context.Users.AsNoTracking().OrderBy(u => u.Username).ToListAsync();
        context.ChangeTracker.Clear();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => Seed(context, Enabled(), async));
        Assert.Contains("Conflicto", error.Message);
        Assert.False(error.Message.Contains(Password, StringComparison.Ordinal), "Error must not contain credentials.");
        Assert.DoesNotContain(context.ChangeTracker.Entries(), e => e.State is EntityState.Added or EntityState.Modified);
        using var verify = db.Open();
        var after = await verify.Users.OrderBy(u => u.Username).ToListAsync();
        Assert.Equal(before.Select(u => (u.Id, u.RoleId, u.Username, u.Email)), after.Select(u => (u.Id, u.RoleId, u.Username, u.Email)));
        Assert.True(before.Select(u => u.Password).SequenceEqual(after.Select(u => u.Password)), "Passwords must be unchanged.");
        Assert.Single(verify.Roles); // Even the missing role was not inserted on failure.
        Assert.Empty(verify.Customers);
    }
}
