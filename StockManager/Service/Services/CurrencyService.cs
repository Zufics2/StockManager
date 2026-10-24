using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;
using StockManager.Abstract;
using StockManager.Models.DTOs;
using StockManager.Models.Exceptions;
using StockManager.Models.Options;

namespace StockManager.Service.Services;

/// <summary>
/// Talks to a public exchange-rate API. The injected <see cref="HttpClient"/> is wrapped by a Polly resilience
/// pipeline (retry with exponential back-off + jitter, circuit breaker, timeout) registered in
/// <c>ServiceCollectionExtensions</c>. On top of that the service caches rates and falls back to the last
/// known good rates when the provider is down.
/// </summary>
public class CurrencyService(
    HttpClient http,
    IMemoryCache cache,
    IOptions<CurrencyOptions> options,
    ILogger<CurrencyService> logger) : ICurrencyService
{
    private sealed record RatesSnapshot(Dictionary<string, decimal> Rates, DateTime RetrievedAtUtc);

    private sealed record RatesApiResponse(
        [property: JsonPropertyName("result")] string? Result,
        [property: JsonPropertyName("rates")] Dictionary<string, decimal>? Rates);

    public async Task<ExchangeRateResult> GetRateAsync(string fromCurrency, string toCurrency, CancellationToken ct = default)
    {
        var from = fromCurrency.Trim().ToUpperInvariant();
        var to = toCurrency.Trim().ToUpperInvariant();

        if (from == to)
            return new ExchangeRateResult(from, to, 1m, DateTime.UtcNow);

        var snapshot = await GetSnapshotAsync(from, ct);

        if (!snapshot.Rates.TryGetValue(to, out var rate))
            throw new BadRequestException($"The currency '{to}' is not supported by the exchange-rate provider.");

        return new ExchangeRateResult(from, to, rate, snapshot.RetrievedAtUtc);
    }

    private async Task<RatesSnapshot> GetSnapshotAsync(string baseCurrency, CancellationToken ct)
    {
        var freshKey = $"rates:fresh:{baseCurrency}";
        var staleKey = $"rates:stale:{baseCurrency}";

        if (cache.TryGetValue(freshKey, out RatesSnapshot? fresh) && fresh is not null)
            return fresh;

        try
        {
            var response = await http.GetFromJsonAsync<RatesApiResponse>($"v6/latest/{baseCurrency}", ct);

            if (response is null || !string.Equals(response.Result, "success", StringComparison.OrdinalIgnoreCase)
                                 || response.Rates is null || response.Rates.Count == 0)
                throw new ExternalServiceException("The exchange-rate provider returned an unexpected response.");

            var snapshot = new RatesSnapshot(response.Rates, DateTime.UtcNow);
            cache.Set(freshKey, snapshot, TimeSpan.FromMinutes(options.Value.CacheMinutes));
            cache.Set(staleKey, snapshot, TimeSpan.FromHours(24));
            return snapshot;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
                                   ex is HttpRequestException or TaskCanceledException or TimeoutRejectedException
                                       or BrokenCircuitException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "Exchange-rate provider unavailable for base {Base}", baseCurrency);
            
            if (cache.TryGetValue(staleKey, out RatesSnapshot? stale) && stale is not null)
                return stale;

            throw new ExternalServiceException(
                "The exchange-rate service is temporarily unavailable. Please try again later.", ex);
        }
    }
}
