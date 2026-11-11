namespace Maia.Domain.Common;

/// <summary>
/// Raised when an operation would break a domain rule. <see cref="Code"/> is stable and safe to
/// map to a localized message in the UI; <see cref="Exception.Message"/> is for logs only.
/// </summary>
public sealed class DomainException : Exception
{
    public DomainException()
    {
        Code = "domain.unknown";
    }

    public DomainException(string message)
        : base(message)
    {
        Code = "domain.unknown";
    }

    public DomainException(string message, Exception innerException)
        : base(message, innerException)
    {
        Code = "domain.unknown";
    }

    public DomainException(string code, string message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        Code = code;
    }

    public string Code { get; }
}
