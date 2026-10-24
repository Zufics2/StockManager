namespace StockManager.Service.Extensions;

public static class DateTimeExtensions
{
    /// <summary>PostgreSQL "timestamp with time zone" only accepts UTC values.</summary>
    public static DateTime ToUtc(this DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
