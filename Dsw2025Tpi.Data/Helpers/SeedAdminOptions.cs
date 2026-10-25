using System.Net.Mail;

namespace Dsw2025Tpi.Data;

public sealed class SeedAdminOptions
{
    public bool Enabled { get; init; }
    public string? Username { get; init; }
    public string? Email { get; init; }
    public string? Password { get; init; }

    public void Validate()
    {
        if (!Enabled) return;

        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(Username) || Username.Length > 50 ||
            Username != Username.Trim() || Username.Any(char.IsControl))
            errors.Add("Seed:Admin:Username es obligatorio, sin espacios en los extremos ni caracteres de control, y admite hasta 50 caracteres.");

        if (string.IsNullOrWhiteSpace(Email) || Email.Length > 100 ||
            !MailAddress.TryCreate(Email, out var address) || address.Address != Email ||
            Email != Email.Trim() || Email.Any(char.IsControl))
            errors.Add("Seed:Admin:Email debe ser un correo válido de hasta 100 caracteres.");

        if (string.IsNullOrWhiteSpace(Password) || Password.Length > 100)
            errors.Add("Seed:Admin:Password es obligatorio y admite hasta 100 caracteres.");

        // Report setting names, never supplied values or credentials.
        if (errors.Count > 0)
            throw new InvalidOperationException(string.Join(" ", errors));
    }
}
