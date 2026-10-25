namespace Dsw2025Tpi.Application.Exceptions;

public class EntityValidationException : ApplicationException
{
    public IReadOnlyList<string> Errors { get; }

    public EntityValidationException(IReadOnlyList<string> errors) : base(string.Join(" ", errors))
    {
        Errors = errors;
    }
}
