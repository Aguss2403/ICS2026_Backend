using Dsw2025Tpi.Application.Dtos;
using Dsw2025Tpi.Application.Exceptions; // Asegúrate de que esta carpeta exista o usa System.Exception
using Dsw2025Tpi.Application.Interfaces;
using Dsw2025Tpi.Data;
using Dsw2025Tpi.Domain.Entities; // Tus entidades personalizadas
using Microsoft.EntityFrameworkCore;
using System.Security.Authentication;

namespace Dsw2025Tpi.Application.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly Dsw2025TpiContext _context;
    private readonly JwtTokenServices _jwtTokenServices;

    public AuthenticationService(Dsw2025TpiContext context, JwtTokenServices jwtTokenServices)
    {
        _context = context;
        _jwtTokenServices = jwtTokenServices;
    }

    public async Task<ResponseLoginModel> Login(RequestLoginModel request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            throw new ArgumentException("El usuario y la contraseña son obligatorios.");

        var user = await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Customer)
            .FirstOrDefaultAsync(u => u.Username == request.Username);

        if (user == null || user.Password != request.Password)
            throw new InvalidCredentialException("Usuario o contraseña inválidos.");

        var role = user.Role;
        if (role == null)
            throw new InvalidCredentialException("Usuario o contraseña inválidos.");

        var token = _jwtTokenServices.GenerateToken(user.Username, role.Name);

        // 2. CORRECCIÓN: Usar el constructor del record (paréntesis, no llaves)
        // Mapeamos los datos directamente al orden que definiste en el DTO ResponseLoginModel
        return new ResponseLoginModel(
            token,                                  // Token
            user.Id,                                // Id
            user.Username,                          // Username
            user.Email,                             // Email
            role.Name,                              // Role
            user.Customer?.Id ?? Guid.Empty,        // CustomerId (Si es null, enviamos Guid vacío)
            user.Customer?.FirstName ?? "",         // FirstName
            user.Customer?.LastName ?? "",          // LastName
            user.Customer?.Address ?? "",           // Address
            user.Customer?.PhoneNumber ?? ""        // PhoneNumber
        );
    }

    public async Task<string> Register(RegisterModel model)
    {
        // 1. Validaciones básicas
        if (model == null) throw new ArgumentException("Los datos de registro son obligatorios.");
        if (!model.Email.Contains("@")) throw new ArgumentException("Formato de email inválido.");

        // 2. Verificar si el usuario ya existe
        bool userExists = await _context.Users.AnyAsync(u => u.Username == model.Username || u.Email == model.Email);
        if (userExists)
        {
            throw new InvalidOperationException("El nombre de usuario o email ya está en uso.");
        }

        // 3. Obtener el Rol por defecto (ej. "Client")
        // IMPORTANTE: Asegúrate de que este rol exista en tu tabla Roles
        var clientRole = await _context.Roles.FirstOrDefaultAsync(r => r.Name == "user");
        if (clientRole == null)
        {
            throw new InvalidOperationException("El rol 'Client' no existe en la base de datos.");
        }

        // 4. Crear las entidades (User y Customer)
        // Entity Framework es lo suficientemente inteligente para insertar ambos si están vinculados
        var newUser = new User
        {
            Id = Guid.NewGuid(), // Opcional si tu DB lo genera solo, pero seguro ponerlo.
            Username = model.Username,
            Password = model.Password, // Nota: En producción, aquí deberías hashear la contraseña
            Email = model.Email,
            RoleId = clientRole.Id, // Asignamos la FK del rol

            // Creamos el Customer y lo asignamos a la propiedad de navegación
            Customer = new Customer
            {
                Id = Guid.NewGuid(),
                FirstName = model.FirstName,
                LastName = model.LastName,
                Address = model.Address,
                PhoneNumber = model.PhoneNumber
            }
        };

        // 5. Guardar en base de datos
        _context.Users.Add(newUser);
        await _context.SaveChangesAsync();

        // 6. Retornar mensaje de éxito (Aquí solucionas el error del return)
        return $"Usuario {model.Username} registrado exitosamente.";
    }
}
