namespace StockManager.Models.Options;

public class JwtOptions
{
    public const string Section = "Jwt";
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}

public class FileStorageOptions
{
    public const string Section = "FileStorage";
    public long MaxFileSizeBytes { get; set; } = 2 * 1024 * 1024;
    public string[] AllowedExtensions { get; set; } = [];
    public string UploadsFolder { get; set; } = "uploads/products";
}

public class CurrencyOptions
{
    public const string Section = "Currency";
    public string BaseUrl { get; set; } = "https://open.er-api.com/";
    public string BaseCurrency { get; set; } = "USD";
    public int CacheMinutes { get; set; } = 60;
    public int TimeoutSeconds { get; set; } = 5;
    public int RetryCount { get; set; } = 3;
}

public class RateLimitOptions
{
    public const string Section = "RateLimiting";
    public int PermitLimit { get; set; } = 100;
    public int AuthPermitLimit { get; set; } = 10;
    public int WindowSeconds { get; set; } = 60;
}

public class SeedOptions
{
    public const string Section = "Seed";
    public string AdminEmail { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string AdminFullName { get; set; } = "System Administrator";
}
