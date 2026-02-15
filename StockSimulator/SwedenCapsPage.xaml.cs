using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using StockSimulator.Models;
using StockSimulator.Services;

namespace StockSimulator;

public partial class SwedenCapsPage : ContentPage, INotifyPropertyChanged
{
    private readonly MarketDataService _marketDataService = new();
    private readonly ObservableCollection<QuoteRow> _quotes = [];
    private readonly ObservableCollection<string> _segments = ["Large", "Mid", "Small", "All Sweden"];

    private bool _isRefreshing;
    private string _selectedSegment = "Large";
    private DateTimeOffset _lastUpdated = DateTimeOffset.MinValue;

    public SwedenCapsPage()
    {
        InitializeComponent();
        BindingContext = this;
        _ = RefreshAsync();
    }

    public ObservableCollection<QuoteRow> Quotes => _quotes;

    public ObservableCollection<string> Segments => _segments;

    public string SelectedSegment
    {
        get => _selectedSegment;
        set
        {
            if (!SetProperty(ref _selectedSegment, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HeaderText));
            _ = RefreshAsync();
        }
    }

    public string HeaderText => SelectedSegment == "All Sweden"
        ? "All Swedish Caps"
        : $"Sweden {SelectedSegment} Caps";

    public string UpdatedText => _lastUpdated == DateTimeOffset.MinValue
        ? "Updating..."
        : $"Updated: {_lastUpdated.LocalDateTime:yyyy-MM-dd HH:mm:ss}";

    private async void OnRefreshClicked(object? sender, EventArgs e)
    {
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            var list = GetSelectedList();
            _quotes.Clear();

            foreach (var instrument in list)
            {
                var quote = await _marketDataService.GetQuoteAsync(instrument.Symbol, CancellationToken.None);
                _quotes.Add(new QuoteRow(instrument, quote.Price, quote.ChangePercent));
            }

            _lastUpdated = DateTimeOffset.Now;
            OnPropertyChanged(nameof(UpdatedText));
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private IReadOnlyList<StockInstrument> GetSelectedList() => SelectedSegment switch
    {
        "Large" => StockCatalog.SwedenLarge,
        "Mid" => StockCatalog.SwedenMid,
        "Small" => StockCatalog.SwedenSmall,
        _ => StockCatalog.SwedenAll
    };

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

    private new void OnPropertyChanged([CallerMemberName] string propertyName = "") => base.OnPropertyChanged(propertyName);

    public sealed class QuoteRow
    {
        public QuoteRow(StockInstrument instrument, decimal price, decimal changePercent)
        {
            Symbol = instrument.Symbol;
            Name = instrument.Name;
            PriceText = price.ToString("C2", CultureInfo.CurrentCulture);
            var sign = changePercent > 0 ? "+" : string.Empty;
            ChangeText = $"{sign}{changePercent:F2}%";
            ChangeColor = changePercent > 0 ? Colors.Green : changePercent < 0 ? Colors.OrangeRed : Colors.Gray;
        }

        public string Symbol { get; }

        public string Name { get; }

        public string PriceText { get; }

        public string ChangeText { get; }

        public Color ChangeColor { get; }
    }
}
