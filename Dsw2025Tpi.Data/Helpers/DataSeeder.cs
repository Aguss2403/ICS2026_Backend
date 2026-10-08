using Dsw2025Tpi.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dsw2025Tpi.Data;

public static class DataSeeder
{
    public static void Seed(Dsw2025TpiContext context, SeedAdminOptions admin)
    {
        admin.Validate();
        var adminRole = context.Roles.SingleOrDefault(r => r.Name == Role.Admin);
        var userRole = context.Roles.SingleOrDefault(r => r.Name == Role.User);
        var matches = admin.Enabled
            ? context.Users.Include(u => u.Role)
                .Where(u => u.Username == admin.Username || u.Email == admin.Email).ToList()
            : new List<User>();

        AddMissingEntities(context, admin, adminRole, userRole, matches);
        context.SaveChanges();
    }

    public static async Task SeedAsync(Dsw2025TpiContext context, SeedAdminOptions admin,
        CancellationToken cancellationToken = default)
    {
        admin.Validate();
        var adminRole = await context.Roles.SingleOrDefaultAsync(r => r.Name == Role.Admin, cancellationToken);
        var userRole = await context.Roles.SingleOrDefaultAsync(r => r.Name == Role.User, cancellationToken);
        var matches = admin.Enabled
            ? await context.Users.Include(u => u.Role)
                .Where(u => u.Username == admin.Username || u.Email == admin.Email).ToListAsync(cancellationToken)
            : new List<User>();

        AddMissingEntities(context, admin, adminRole, userRole, matches);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static void AddMissingEntities(Dsw2025TpiContext context, SeedAdminOptions admin,
        Role? adminRole, Role? userRole, List<User> matches)
    {
        // Email has no unique index: inspect every matching account before making changes.
        if (admin.Enabled && matches.Count > 0 &&
            (matches.Count != 1 ||
             !string.Equals(matches[0].Username, admin.Username, StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(matches[0].Email, admin.Email, StringComparison.OrdinalIgnoreCase) ||
             matches[0].Role?.Name != Role.Admin))
        {
            throw new InvalidOperationException(
                "Conflicto en Seed:Admin: Username o Email identifica otra cuenta o un rol distinto de admin. No se modificaron cuentas.");
        }

        // Stage changes only after validation and conflict detection succeed.
        if (adminRole is null)
        {
            adminRole = new Role { Name = Role.Admin };
            context.Roles.Add(adminRole);
        }

        if (userRole is null)
            context.Roles.Add(new Role { Name = Role.User });

        if (admin.Enabled && matches.Count == 0)
        {
            // AuthenticationService currently stores and compares the password verbatim.
            // Hashing and migrating passwords belong to the separate TP1 task.
            context.Users.Add(new User(admin.Username!, admin.Email!, admin.Password!, adminRole));
        }
    }
}
