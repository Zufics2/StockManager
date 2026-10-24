namespace StockManager.Models.Exceptions;

/// <summary>Base class for expected, "business" errors that map to a specific HTTP status.</summary>
public abstract class AppException(string message, int statusCode, string title, Exception? inner = null)
    : Exception(message, inner)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
}

public class NotFoundException(string entity, object key)
    : AppException($"{entity} with id '{key}' was not found.", StatusCodes.Status404NotFound, "Resource not found");

public class ConflictException(string message)
    : AppException(message, StatusCodes.Status409Conflict, "Conflict");

public class BadRequestException(string message)
    : AppException(message, StatusCodes.Status400BadRequest, "Bad request");

public class UnauthorizedException(string message)
    : AppException(message, StatusCodes.Status401Unauthorized, "Unauthorized");

public class InsufficientStockException(string product, string warehouse, int requested, int available)
    : AppException(
        $"Insufficient stock for product '{product}' in warehouse '{warehouse}': requested {requested}, available {available}.",
        StatusCodes.Status409Conflict,
        "Insufficient stock")
{
    public int Requested { get; } = requested;
    public int Available { get; } = available;
}

public class ExternalServiceException(string message, Exception? inner = null)
    : AppException(message, StatusCodes.Status503ServiceUnavailable, "External service unavailable", inner);
