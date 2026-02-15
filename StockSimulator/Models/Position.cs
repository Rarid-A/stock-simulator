using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace StockSimulator.Models;

public sealed class Position : INotifyPropertyChanged
{
    private int _quantity;
    private decimal _averageCost;
    private decimal _marketPrice;

    public event PropertyChangedEventHandler? PropertyChanged;

    public Position(string symbol, int quantity, decimal averageCost, decimal marketPrice)
    {
        Symbol = symbol;
        _quantity = quantity;
        _averageCost = averageCost;
        _marketPrice = marketPrice;
    }

    public string Symbol { get; }

    public int Quantity
    {
        get => _quantity;
        private set => SetProperty(ref _quantity, value);
    }

    public decimal AverageCost
    {
        get => _averageCost;
        private set => SetProperty(ref _averageCost, value);
    }

    public decimal MarketPrice
    {
        get => _marketPrice;
        private set => SetProperty(ref _marketPrice, value);
    }

    public decimal MarketValue => Quantity * MarketPrice;

    public decimal UnrealizedPnL => (MarketPrice - AverageCost) * Quantity;

    public string QuantityText => $"{Quantity} sh";

    public string UnrealizedPnLText
    {
        get
        {
            var sign = UnrealizedPnL > 0 ? "+" : string.Empty;
            return $"{sign}{UnrealizedPnL.ToString("C2", CultureInfo.CurrentCulture)}";
        }
    }

    public Color UnrealizedPnLColor => UnrealizedPnL > 0 ? Colors.Green : UnrealizedPnL < 0 ? Colors.OrangeRed : Colors.Gray;

    public void AddShares(int quantity, decimal price)
    {
        var totalCost = (AverageCost * Quantity) + (price * quantity);
        Quantity += quantity;
        AverageCost = Quantity == 0 ? 0 : totalCost / Quantity;
        OnPositionMetricsChanged();
    }

    public void RemoveShares(int quantity)
    {
        Quantity -= quantity;
        if (Quantity < 0)
        {
            Quantity = 0;
        }

        OnPositionMetricsChanged();
    }

    public void UpdateMarketPrice(decimal price)
    {
        MarketPrice = price;
        OnPositionMetricsChanged();
    }

    private void OnPositionMetricsChanged()
    {
        OnPropertyChanged(nameof(MarketValue));
        OnPropertyChanged(nameof(UnrealizedPnL));
        OnPropertyChanged(nameof(QuantityText));
        OnPropertyChanged(nameof(UnrealizedPnLText));
        OnPropertyChanged(nameof(UnrealizedPnLColor));
    }

    private bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
        {
            return false;
        }

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
