namespace Dsw2025Tpi.Application.Exceptions;

public class EntityNotFoundException : ApplicationException
{
    public EntityNotFoundException(string message) : base(message)
    {
    }
}
