using Dsw2025Tpi.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Dsw2025Tpi.Seeding.Tests;

internal sealed class SeedTestDatabase : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public DbContextOptions<Dsw2025TpiContext> Options { get; }

    public SeedTestDatabase()
    {
        connection.Open();
        Options = new DbContextOptionsBuilder<Dsw2025TpiContext>()
            .UseSqlite(connection).Options;
        using var context = Open();
        context.Database.EnsureCreated(); // Only this isolated in-memory test database.
    }

    public Dsw2025TpiContext Open() => new(Options);
    public void Dispose() => connection.Dispose();
}
