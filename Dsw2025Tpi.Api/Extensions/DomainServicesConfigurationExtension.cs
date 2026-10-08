using Dsw2025Tpi.Data;
using Microsoft.EntityFrameworkCore;

namespace Dsw2025Tpi.Api.Utils;

public static class DomainServicesConfigurationExtension
{
    public static IServiceCollection AddDomainServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<Dsw2025TpiContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
            var enabledValue = configuration["Seed:Admin:Enabled"];
            var enabled = false;
            if (enabledValue is not null && !bool.TryParse(enabledValue, out enabled))
            {
                throw new InvalidOperationException("Seed:Admin:Enabled debe ser true o false.");
            }
            var admin = new SeedAdminOptions
            {
                Enabled = enabled,
                Username = configuration["Seed:Admin:Username"],
                Email = configuration["Seed:Admin:Email"],
                Password = configuration["Seed:Admin:Password"]
            };
            // EF tooling uses the synchronous callback; async migrations use the other.
            // EF 9 executes these callbacks under its migration lock.
            options.UseSeeding((context, _) => DataSeeder.Seed((Dsw2025TpiContext)context, admin));
            options.UseAsyncSeeding((context, _, ct) => DataSeeder.SeedAsync((Dsw2025TpiContext)context, admin, ct));
        });
        return services;

    }
}
