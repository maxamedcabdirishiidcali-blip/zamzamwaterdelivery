namespace ZamzamWaterDelivery.Application.Common;

/// <summary>Base type for expected, user-facing application errors.</summary>
public abstract class AppException : Exception
{
    protected AppException(string message) : base(message) { }
}

public sealed class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string entityName, object key)
        : base($"{entityName} '{key}' was not found.") { }
}

/// <summary>Raised when a request breaks a business rule (bad state, insufficient stock, etc.).</summary>
public sealed class BusinessRuleException : AppException
{
    public IReadOnlyList<string> Errors { get; }

    public BusinessRuleException(string message, IEnumerable<string>? errors = null) : base(message)
    {
        Errors = errors?.ToList() ?? new List<string>();
    }
}

public sealed class AuthenticationFailedException : AppException
{
    public AuthenticationFailedException(string message = "Invalid email or password.") : base(message) { }
}

public sealed class ForbiddenAccessException : AppException
{
    public ForbiddenAccessException(string message = "You do not have permission to perform this action.")
        : base(message) { }
}
