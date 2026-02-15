using System.Globalization;
using System.Text.Json;
using StockSimulator.Models;

namespace StockSimulator.Services;

public sealed class MarketDataService
{
    private static readonly HttpClient HttpClient = CreateClient();
    private readonly Random _random = new();
    private readonly Dictionary<string, decimal> _fallbackPrices = new(StringComparer.OrdinalIgnoreCase);

    public async Task<QuoteSnapshot> GetQuoteAsync(string symbol, CancellationToken cancellationToken)
    {
        try
        {
            var quote = await FetchLiveQuoteAsync(symbol, cancellationToken);
            _fallbackPrices[symbol] = quote.Price;
            return quote;
        }
        catch
        {
            return GenerateFallbackQuote(symbol);
        }
    }

    private async Task<QuoteSnapshot> FetchLiveQuoteAsync(string symbol, CancellationToken cancellationToken)
    {
        var endpoint = $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval=1m&range=1d";
        using var response = await HttpClient.GetAsync(endpoint, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var chart = json.RootElement.GetProperty("chart");

        if (!chart.TryGetProperty("result", out var result) || result.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("No quote data returned.");
        }

        var meta = result[0].GetProperty("meta");
        var marketPrice = TryGetDecimal(meta, "regularMarketPrice");
        if (marketPrice <= 0)
        {
            throw new InvalidOperationException("Price is unavailable.");
        }

        var previousClose = TryGetDecimal(meta, "previousClose");
        if (previousClose <= 0)
        {
            previousClose = marketPrice;
        }

        var changePercent = ((marketPrice - previousClose) / previousClose) * 100m;

        return new QuoteSnapshot(
            symbol,
            marketPrice,
            changePercent,
            DateTimeOffset.Now,
            true);
    }

    private QuoteSnapshot GenerateFallbackQuote(string symbol)
    {
        var normalized = symbol.ToUpperInvariant();
        if (!_fallbackPrices.TryGetValue(normalized, out var basePrice) || basePrice <= 0)
        {
            basePrice = 100m + (decimal)_random.NextDouble() * 200m;
        }

        var jitter = ((decimal)_random.NextDouble() - 0.5m) * 0.02m;
        var nextPrice = Math.Max(0.01m, basePrice * (1m + jitter));
        var pct = ((nextPrice - basePrice) / basePrice) * 100m;
        _fallbackPrices[normalized] = nextPrice;

        return new QuoteSnapshot(normalized, nextPrice, pct, DateTimeOffset.Now, false);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        client.DefaultRequestHeaders.UserAgent.ParseAdd("StockSimulator/1.0");
        return client;
    }

    private static decimal TryGetDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return 0m;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            return property.GetDecimal();
        }

        if (property.ValueKind == JsonValueKind.String &&
            decimal.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return 0m;
    }
}
