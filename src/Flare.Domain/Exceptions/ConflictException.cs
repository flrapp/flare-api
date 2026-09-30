namespace Flare.Domain.Exceptions;

/// Thrown when a request would create a duplicate of a uniquely named entity (maps to HTTP 409).
public class ConflictException : DomainException
{
    public ConflictException(string message) : base(message)
    {
    }
}
