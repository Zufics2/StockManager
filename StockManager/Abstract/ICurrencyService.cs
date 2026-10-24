using StockManager.Models.DTOs;

namespace StockManager.Abstract;

public interface ICurrencyService
{
    /// <summary>Exchange rate from one currency to another, retrieved from an external API.</summary>
    Task<ExchangeRateResult> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken ct = default);
}
