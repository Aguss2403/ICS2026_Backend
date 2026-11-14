using System.Security.Cryptography;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dsw2025Tpi.IntegrationTests;

internal static class TestCredentials
{
    internal static string Password { get; } = Environment.GetEnvironmentVariable("ICS_TEST_ADMIN_PASSWORD")
        ?? "Tp4-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
}

public static class TestData
{
    public static Product Product(string sku = "TEST-SKU", decimal price = 12.35m, int stock = 10) =>
        new(sku, "TEST-CODE", "Test product", "Test description", price, stock);

    // Adds a persisted-user graph suitable for order tests. The factory saves it after the callback.
    public static async Task<Customer> AddCustomerAsync(Dsw2025TpiContext context, string username = "tp4-user")
    {
        var role = await context.Roles.SingleAsync(r => r.Name == Role.User);
        var user = new User(username, username + "@example.invalid", TestCredentials.Password, role);
        var customer = new Customer(user, "Test", "Customer", "Test address", "123456");
        context.Customers.Add(customer);
        return customer;
    }
}
