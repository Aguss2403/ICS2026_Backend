using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Text;

namespace Dsw2025Tpi.Api.Utils;

public sealed record RuntimeSettings(
    string ConnectionString,
    string JwtKey,
    string JwtIssuer,
    string JwtAudience,
    int JwtExpireInMinutes);

public static class RuntimeConfiguration
{
    public static RuntimeSettings Read(IConfiguration configuration)
    {
        var connectionString = Required(configuration, "ConnectionStrings:DefaultConnection");
        ValidateConnectionString(connectionString);
        var key = Required(configuration, "Jwt:Key");
        if (Encoding.UTF8.GetByteCount(key) < 32)
        {
            throw new InvalidOperationException("La configuración Jwt:Key debe tener al menos 32 bytes para HS256.");
        }

        var issuer = Required(configuration, "Jwt:Issuer");
        var audience = Required(configuration, "Jwt:Audience");
        var expiration = Required(configuration, "Jwt:ExpireInMinutes");
        if (!int.TryParse(expiration, NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) || minutes <= 0)
        {
            throw new InvalidOperationException("La configuración Jwt:ExpireInMinutes debe ser un entero positivo.");
        }

        return new RuntimeSettings(connectionString, key, issuer, audience, minutes);
    }

    private static string Required(IConfiguration configuration, string name)
    {
        var value = configuration[name];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Falta la configuración obligatoria {name}.");
        }

        return value;
    }

    private static void ValidateConnectionString(string connectionString)
    {
        SqlConnectionStringBuilder connection;
        try
        {
            connection = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            // Do not include the parser exception: it may contain connection values.
            throw new InvalidOperationException("La configuración ConnectionStrings:DefaultConnection no es una cadena SQL válida.");
        }

        if (string.IsNullOrWhiteSpace(connection.DataSource) || string.IsNullOrWhiteSpace(connection.InitialCatalog))
        {
            throw new InvalidOperationException("La configuración ConnectionStrings:DefaultConnection requiere servidor y base de datos.");
        }
    }
}
