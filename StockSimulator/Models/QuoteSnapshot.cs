namespace StockSimulator.Models;

public sealed record QuoteSnapshot(
    string Symbol,
    decimal Price,
    decimal ChangePercent,
    DateTimeOffset Timestamp,
    bool IsLive);
