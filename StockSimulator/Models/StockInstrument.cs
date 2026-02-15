namespace StockSimulator.Models;

public sealed record StockInstrument(string Symbol, string Name, string Cap, string Region)
{
    public string DisplayName => $"{Symbol} — {Name}";

    public string SegmentText => $"{Region} · {Cap}";
}
