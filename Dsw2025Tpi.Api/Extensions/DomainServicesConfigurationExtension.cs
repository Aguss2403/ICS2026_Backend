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
        });
        return services;

    }
}
