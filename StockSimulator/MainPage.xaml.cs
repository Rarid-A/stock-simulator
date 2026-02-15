using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using StockSimulator.Models;
using StockSimulator.Services;

namespace StockSimulator;

public partial class MainPage : ContentPage, INotifyPropertyChanged
{
	private const decimal StartingBalance = 10000m;
	private const decimal EmergencyFundAmount = 5000m;

	private readonly MarketDataService _marketDataService = new();
	private readonly PortfolioStore _portfolioStore = new();
	private readonly ObservableCollection<StockInstrument> _symbols = new(StockCatalog.TradeUniverse);

	private readonly ObservableCollection<Position> _positions = [];
	private readonly ObservableCollection<TradeRecord> _trades = [];
	private readonly Dictionary<string, decimal> _latestPrices = new(StringComparer.OrdinalIgnoreCase);

	private IDispatcherTimer? _refreshTimer;
	private bool _isRefreshing;
	private bool _recoveryUsed;
	private decimal _cash = StartingBalance;
	private decimal _realizedPnL;
	private StockInstrument _selectedInstrument = StockCatalog.TradeUniverse.First(item => item.Symbol == "AAPL");
	private string _quantityInput = "1";
	private string _statusMessage = "Connecting to market feed...";
	private Color _statusColor = Colors.Gray;
	private string _quoteText = "Loading latest quote...";
	private DateTimeOffset _lastUpdated = DateTimeOffset.MinValue;
	private bool _isTradingHalted;
	private bool _portfolioReady;

	public MainPage()
	{
		InitializeComponent();
		BindingContext = this;
		_positions.CollectionChanged += OnPositionsCollectionChanged;
		_ = InitializePortfolioAsync();
	}

	public ObservableCollection<StockInstrument> Symbols => _symbols;

	public ObservableCollection<Position> Positions => _positions;

	public ObservableCollection<TradeRecord> Trades => _trades;

	public StockInstrument SelectedInstrument
	{
		get => _selectedInstrument;
		set
		{
			if (!SetProperty(ref _selectedInstrument, value))
			{
				return;
			}

			OnPropertyChanged(nameof(SelectedSymbolText));

			_ = RefreshAllDataAsync();
		}
	}

	public string SelectedSymbolText => SelectedInstrument.DisplayName;

	public string QuantityInput
	{
		get => _quantityInput;
		set => SetProperty(ref _quantityInput, value);
	}

	public string StatusMessage
	{
		get => _statusMessage;
		private set => SetProperty(ref _statusMessage, value);
	}

	public Color StatusColor
	{
		get => _statusColor;
		private set => SetProperty(ref _statusColor, value);
	}

	public string QuoteText
	{
		get => _quoteText;
		private set => SetProperty(ref _quoteText, value);
	}

	public string CashText => ToCurrency(_cash);

	public string NetWorthText => ToCurrency(NetWorth);

	public string RealizedPnLText => ToSignedCurrency(_realizedPnL);

	public Color RealizedPnLColor => GetPnLColor(_realizedPnL);

	public string TotalPnLText => ToSignedCurrency(TotalPnL);

	public Color TotalPnLColor => GetPnLColor(TotalPnL);

	public string LastUpdatedText => _lastUpdated == DateTimeOffset.MinValue
		? "Waiting for first quote..."
		: $"Updated: {_lastUpdated.LocalDateTime:yyyy-MM-dd HH:mm:ss}";

	public bool CanTrade => _portfolioReady && !_isTradingHalted;

	public bool CanResetPortfolio => _portfolioReady && !_isRefreshing;

	public bool ShowRecoveryBanner => _isTradingHalted;

	public bool CanRecover => _isTradingHalted && !_recoveryUsed;

	public string RecoveryMessage => _recoveryUsed
		? "Portfolio is bankrupt and emergency funds are already used. Restart the app to begin a new simulation."
		: "Portfolio hit $0. Activate one-time emergency paper funds to continue trading.";

	private decimal NetWorth => _cash + _positions.Sum(position => position.MarketValue);

	private decimal UnrealizedPnL => _positions.Sum(position => position.UnrealizedPnL);

	private decimal TotalPnL => _realizedPnL + UnrealizedPnL;

	private void SetupRefreshTimer()
	{
		if (_refreshTimer is not null)
		{
			return;
		}

		_refreshTimer = Dispatcher.CreateTimer();
		_refreshTimer.Interval = TimeSpan.FromSeconds(8);
		_refreshTimer.Tick += async (_, _) => await RefreshAllDataAsync();
		_refreshTimer.Start();
	}

	private async Task InitializePortfolioAsync()
	{
		try
		{
			StatusMessage = "Loading local portfolio...";
			StatusColor = Colors.Gray;

			var state = await _portfolioStore.LoadAsync(StartingBalance);

			_cash = Math.Max(0m, state.Cash);
			_realizedPnL = state.RealizedPnL;
			_recoveryUsed = state.RecoveryUsed;
			_isTradingHalted = state.IsTradingHalted;

			_positions.Clear();
			foreach (var position in state.Positions)
			{
				if (position.Quantity <= 0 || string.IsNullOrWhiteSpace(position.Symbol))
				{
					continue;
				}

				var averageCost = Math.Max(0m, position.AverageCost);
				var marketPrice = position.MarketPrice > 0m ? position.MarketPrice : averageCost;
				if (marketPrice <= 0m)
				{
					marketPrice = 0.01m;
				}

				_positions.Add(new Position(position.Symbol, position.Quantity, averageCost, marketPrice));
				_latestPrices[position.Symbol] = marketPrice;
			}

			_trades.Clear();
			foreach (var trade in state.Trades
				.Where(item => item.Quantity > 0 && item.Price >= 0m && !string.IsNullOrWhiteSpace(item.Symbol))
				.Take(100))
			{
				_trades.Add(new TradeRecord(trade.Side, trade.Symbol, trade.Quantity, trade.Price, trade.Timestamp));
			}

			_portfolioReady = true;
			RaiseSummaryPropertiesChanged();
			OnPropertyChanged(nameof(CanTrade));
			OnPropertyChanged(nameof(CanResetPortfolio));
			OnPropertyChanged(nameof(ShowRecoveryBanner));
			OnPropertyChanged(nameof(CanRecover));
			OnPropertyChanged(nameof(RecoveryMessage));

			SetupRefreshTimer();
			await RefreshAllDataAsync();
		}
		catch (Exception ex)
		{
			StatusMessage = $"Portfolio load failed: {ex.Message}";
			StatusColor = Colors.OrangeRed;
			OnPropertyChanged(nameof(CanTrade));
			OnPropertyChanged(nameof(CanResetPortfolio));
		}
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_ = SavePortfolioAsync();
		if (_refreshTimer is not null)
		{
			_refreshTimer.Stop();
		}
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		if (_refreshTimer is not null && !_refreshTimer.IsRunning)
		{
			_refreshTimer.Start();
		}
	}

	private async Task RefreshAllDataAsync()
	{
		if (_isRefreshing)
		{
			return;
		}

		_isRefreshing = true;
		OnPropertyChanged(nameof(CanResetPortfolio));
		try
		{
			var trackedSymbols = GetTrackedSymbols();

			foreach (var symbol in trackedSymbols)
			{
				var quote = await _marketDataService.GetQuoteAsync(symbol, CancellationToken.None);
				if (quote.Price <= 0m)
				{
					continue;
				}

				_latestPrices[symbol] = quote.Price;

				if (string.Equals(symbol, SelectedInstrument.Symbol, StringComparison.OrdinalIgnoreCase))
				{
					var sign = quote.ChangePercent >= 0 ? "+" : string.Empty;
					QuoteText = $"{SelectedInstrument.Name} ({quote.Symbol}) {ToCurrency(quote.Price)} ({sign}{quote.ChangePercent:F2}%)";
					StatusMessage = quote.IsLive
						? "Live data connected"
						: "Live feed unavailable, using safe fallback estimate";
					StatusColor = quote.IsLive ? Colors.Green : Colors.Orange;
					_lastUpdated = quote.Timestamp;
				}
			}

			RefreshPositionValues();
			EvaluateBankruptcyState();
			RaiseSummaryPropertiesChanged();
		}
		catch (Exception ex)
		{
			StatusMessage = $"Feed issue: {ex.Message}";
			StatusColor = Colors.OrangeRed;
		}
		finally
		{
			_isRefreshing = false;
			OnPropertyChanged(nameof(CanResetPortfolio));
		}
	}

	private IEnumerable<string> GetTrackedSymbols()
	{
		var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { SelectedInstrument.Symbol };
		foreach (var position in _positions)
		{
			set.Add(position.Symbol);
		}

		return set;
	}

	private void RefreshPositionValues()
	{
		foreach (var position in _positions)
		{
			if (_latestPrices.TryGetValue(position.Symbol, out var currentPrice))
			{
				position.UpdateMarketPrice(currentPrice);
			}
		}
	}

	private async void OnBuyClicked(object? sender, EventArgs e)
	{
		await ExecuteTradeAsync(isBuy: true);
	}

	private async void OnSellClicked(object? sender, EventArgs e)
	{
		await ExecuteTradeAsync(isBuy: false);
	}

	private async Task ExecuteTradeAsync(bool isBuy)
	{
		if (!_portfolioReady)
		{
			await DisplayAlertAsync("Portfolio", "Portfolio is still loading. Try again in a second.", "OK");
			return;
		}

		if (!CanTrade)
		{
			await DisplayAlertAsync("Trading Halted", "Activate emergency funds or restart the simulation.", "OK");
			return;
		}

		if (!TryParseQuantity(out var quantity))
		{
			await DisplayAlertAsync("Invalid Quantity", "Enter a whole number greater than 0.", "OK");
			return;
		}

		if (!_latestPrices.TryGetValue(SelectedInstrument.Symbol, out var price))
		{
			await DisplayAlertAsync("No Quote", "Price not available yet. Try again in a moment.", "OK");
			return;
		}

		if (price <= 0m)
		{
			await DisplayAlertAsync("Invalid Quote", "Current price is invalid. Wait for a refreshed quote.", "OK");
			return;
		}

		if (isBuy)
		{
			var cost = price * quantity;
			if (cost > _cash)
			{
				await DisplayAlertAsync("Insufficient Cash", "Not enough cash for this buy order.", "OK");
				return;
			}

			_cash -= cost;
			ApplyBuy(SelectedInstrument.Symbol, quantity, price);
			AddTrade("BUY", SelectedInstrument.Symbol, quantity, price);
		}
		else
		{
			var position = _positions.FirstOrDefault(item => item.Symbol.Equals(SelectedInstrument.Symbol, StringComparison.OrdinalIgnoreCase));
			if (position is null || position.Quantity < quantity)
			{
				await DisplayAlertAsync("Insufficient Shares", "You do not hold enough shares to sell.", "OK");
				return;
			}

			var proceeds = price * quantity;
			_cash += proceeds;
			_realizedPnL += (price - position.AverageCost) * quantity;
			ApplySell(position, quantity);
			AddTrade("SELL", SelectedInstrument.Symbol, quantity, price);
		}

		RefreshPositionValues();
		EvaluateBankruptcyState();
		RaiseSummaryPropertiesChanged();
		await SavePortfolioAsync();
	}

	private void ApplyBuy(string symbol, int quantity, decimal price)
	{
		var position = _positions.FirstOrDefault(item => item.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
		if (position is null)
		{
			_positions.Insert(0, new Position(symbol, quantity, price, price));
			return;
		}

		position.AddShares(quantity, price);
		position.UpdateMarketPrice(price);
	}

	private void ApplySell(Position position, int quantity)
	{
		position.RemoveShares(quantity);
		if (position.Quantity == 0)
		{
			_positions.Remove(position);
		}
	}

	private void AddTrade(string side, string symbol, int quantity, decimal price)
	{
		_trades.Insert(0, new TradeRecord(side, symbol, quantity, price, DateTimeOffset.Now));
		if (_trades.Count > 100)
		{
			_trades.RemoveAt(_trades.Count - 1);
		}
	}

	private bool TryParseQuantity(out int quantity)
	{
		if (int.TryParse(QuantityInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out quantity) && quantity > 0)
		{
			return true;
		}

		quantity = 0;
		return false;
	}

	private void EvaluateBankruptcyState()
	{
		if (NetWorth > 0)
		{
			if (_isTradingHalted)
			{
				_isTradingHalted = false;
				OnPropertyChanged(nameof(CanTrade));
				OnPropertyChanged(nameof(ShowRecoveryBanner));
				OnPropertyChanged(nameof(CanRecover));
				OnPropertyChanged(nameof(RecoveryMessage));
			}

			return;
		}

		if (_isTradingHalted)
		{
			return;
		}

		_isTradingHalted = true;
		StatusMessage = "Portfolio bankrupt";
		StatusColor = Colors.OrangeRed;
		OnPropertyChanged(nameof(CanTrade));
		OnPropertyChanged(nameof(ShowRecoveryBanner));
		OnPropertyChanged(nameof(CanRecover));
		OnPropertyChanged(nameof(RecoveryMessage));
	}

	private async void OnRecoverClicked(object? sender, EventArgs e)
	{
		if (_recoveryUsed)
		{
			return;
		}

		_positions.Clear();
		_cash = EmergencyFundAmount;
		_recoveryUsed = true;
		_isTradingHalted = false;
		AddTrade("RECOVERY", "CASH", 1, EmergencyFundAmount);

		StatusMessage = "Emergency funds activated";
		StatusColor = Colors.DarkSeaGreen;
		RaiseSummaryPropertiesChanged();
		OnPropertyChanged(nameof(CanTrade));
		OnPropertyChanged(nameof(ShowRecoveryBanner));
		OnPropertyChanged(nameof(CanRecover));
		OnPropertyChanged(nameof(RecoveryMessage));
		await SavePortfolioAsync();
	}

	private async Task SavePortfolioAsync()
	{
		if (!_portfolioReady)
		{
			return;
		}

		try
		{
			var state = new PortfolioPersistedState(
				_cash,
				_realizedPnL,
				_recoveryUsed,
				_isTradingHalted,
				_positions.Select(position => new PersistedPosition(
					position.Symbol,
					position.Quantity,
					position.AverageCost,
					position.MarketPrice)).ToList(),
				_trades.Select(trade => new PersistedTrade(
					trade.Side,
					trade.Symbol,
					trade.Quantity,
					trade.Price,
					trade.Timestamp)).ToList());

			await _portfolioStore.SaveAsync(state);
		}
		catch
		{
		}
	}

	private void RaiseSummaryPropertiesChanged()
	{
		OnPropertyChanged(nameof(CashText));
		OnPropertyChanged(nameof(NetWorthText));
		OnPropertyChanged(nameof(RealizedPnLText));
		OnPropertyChanged(nameof(RealizedPnLColor));
		OnPropertyChanged(nameof(TotalPnLText));
		OnPropertyChanged(nameof(TotalPnLColor));
		OnPropertyChanged(nameof(LastUpdatedText));
	}

	private void OnPositionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		if (e.OldItems is not null)
		{
			foreach (Position oldPosition in e.OldItems)
			{
				oldPosition.PropertyChanged -= OnPositionPropertyChanged;
			}
		}

		if (e.NewItems is not null)
		{
			foreach (Position newPosition in e.NewItems)
			{
				newPosition.PropertyChanged += OnPositionPropertyChanged;
			}
		}

		if (e.Action == NotifyCollectionChangedAction.Reset)
		{
			foreach (var position in _positions)
			{
				position.PropertyChanged -= OnPositionPropertyChanged;
				position.PropertyChanged += OnPositionPropertyChanged;
			}
		}

		EvaluateBankruptcyState();
		RaiseSummaryPropertiesChanged();
	}

	private void OnPositionPropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(Position.MarketValue) or nameof(Position.UnrealizedPnL) or nameof(Position.Quantity))
		{
			EvaluateBankruptcyState();
			RaiseSummaryPropertiesChanged();
		}
	}

	private static string ToCurrency(decimal amount) => amount.ToString("C2", CultureInfo.CurrentCulture);

	private static string ToSignedCurrency(decimal amount)
	{
		var sign = amount > 0 ? "+" : string.Empty;
		return $"{sign}{amount.ToString("C2", CultureInfo.CurrentCulture)}";
	}

	private static Color GetPnLColor(decimal value)
	{
		if (value > 0)
		{
			return Colors.Green;
		}

		return value < 0 ? Colors.OrangeRed : Colors.Gray;
	}

	private async void OnResetPortfolioClicked(object? sender, EventArgs e)
	{
		if (!CanResetPortfolio)
		{
			return;
		}

		var confirmed = await DisplayAlertAsync(
			"Reset Portfolio",
			"This clears all positions, trade history, and saved portfolio data. Continue?",
			"Reset",
			"Cancel");

		if (!confirmed)
		{
			return;
		}

		await _portfolioStore.ResetAsync();

		_positions.Clear();
		_trades.Clear();
		_latestPrices.Clear();
		_cash = StartingBalance;
		_realizedPnL = 0m;
		_recoveryUsed = false;
		_isTradingHalted = false;
		_lastUpdated = DateTimeOffset.MinValue;
		QuoteText = "Loading latest quote...";
		StatusMessage = "Portfolio reset";
		StatusColor = Colors.DarkSeaGreen;

		RaiseSummaryPropertiesChanged();
		OnPropertyChanged(nameof(CanTrade));
		OnPropertyChanged(nameof(CanResetPortfolio));
		OnPropertyChanged(nameof(ShowRecoveryBanner));
		OnPropertyChanged(nameof(CanRecover));
		OnPropertyChanged(nameof(RecoveryMessage));

		await RefreshAllDataAsync();
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

	private new void OnPropertyChanged([CallerMemberName] string propertyName = "") => base.OnPropertyChanged(propertyName);
}
