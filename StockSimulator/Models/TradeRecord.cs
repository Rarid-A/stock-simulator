using System.Globalization;

namespace StockSimulator.Models;

public sealed record TradeRecord(string Side, string Symbol, int Quantity, decimal Price, DateTimeOffset Timestamp)
{
    public string Summary => $"{Side} {Symbol} x{Quantity}";

    public string ValueText => (Price * Quantity).ToString("C2", CultureInfo.CurrentCulture);

    public string TimeText => Timestamp.ToString("HH:mm:ss", CultureInfo.CurrentCulture);
}
