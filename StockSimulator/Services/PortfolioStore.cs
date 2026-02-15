using SQLite;

namespace StockSimulator.Services;

public sealed class PortfolioStore
{
	private const int SingletonId = 1;
	private readonly SemaphoreSlim _gate = new(1, 1);
	private readonly SQLiteAsyncConnection _connection;
	private bool _initialized;

	public PortfolioStore()
	{
		var dbPath = Path.Combine(FileSystem.AppDataDirectory, "portfolio.db3");
		_connection = new SQLiteAsyncConnection(dbPath);
	}

	public async Task InitializeAsync()
	{
		if (_initialized)
		{
			return;
		}

		await _gate.WaitAsync();
		try
		{
			if (_initialized)
			{
				return;
			}

			await _connection.CreateTableAsync<PortfolioStateRow>();
			await _connection.CreateTableAsync<PortfolioPositionRow>();
			await _connection.CreateTableAsync<PortfolioTradeRow>();
			_initialized = true;
		}
		finally
		{
			_gate.Release();
		}
	}

	public async Task<PortfolioPersistedState> LoadAsync(decimal defaultStartingBalance)
	{
		await InitializeAsync();

		await _gate.WaitAsync();
		try
		{
			var stateRow = await _connection.FindAsync<PortfolioStateRow>(SingletonId)
				?? new PortfolioStateRow
				{
					Id = SingletonId,
					Cash = defaultStartingBalance,
					RealizedPnL = 0m,
					RecoveryUsed = false,
					IsTradingHalted = false
				};

			var positionRows = await _connection.Table<PortfolioPositionRow>()
				.OrderByDescending(row => row.Id)
				.ToListAsync();

			var tradeRows = await _connection.Table<PortfolioTradeRow>()
				.OrderByDescending(row => row.TimestampUnixMilliseconds)
				.ThenByDescending(row => row.Id)
				.ToListAsync();

			return new PortfolioPersistedState(
				stateRow.Cash,
				stateRow.RealizedPnL,
				stateRow.RecoveryUsed,
				stateRow.IsTradingHalted,
				positionRows.Select(row => new PersistedPosition(
					row.Symbol,
					row.Quantity,
					row.AverageCost,
					row.MarketPrice)).ToList(),
				tradeRows.Select(row => new PersistedTrade(
					row.Side,
					row.Symbol,
					row.Quantity,
					row.Price,
					DateTimeOffset.FromUnixTimeMilliseconds(row.TimestampUnixMilliseconds))).ToList());
		}
		finally
		{
			_gate.Release();
		}
	}

	public async Task SaveAsync(PortfolioPersistedState state)
	{
		await InitializeAsync();

		await _gate.WaitAsync();
		try
		{
			await _connection.RunInTransactionAsync(transaction =>
			{
				transaction.InsertOrReplace(new PortfolioStateRow
				{
					Id = SingletonId,
					Cash = state.Cash,
					RealizedPnL = state.RealizedPnL,
					RecoveryUsed = state.RecoveryUsed,
					IsTradingHalted = state.IsTradingHalted
				});

				transaction.DeleteAll<PortfolioPositionRow>();
				foreach (var position in state.Positions)
				{
					transaction.Insert(new PortfolioPositionRow
					{
						Symbol = position.Symbol,
						Quantity = position.Quantity,
						AverageCost = position.AverageCost,
						MarketPrice = position.MarketPrice
					});
				}

				transaction.DeleteAll<PortfolioTradeRow>();
				foreach (var trade in state.Trades.Take(100))
				{
					transaction.Insert(new PortfolioTradeRow
					{
						Side = trade.Side,
						Symbol = trade.Symbol,
						Quantity = trade.Quantity,
						Price = trade.Price,
						TimestampUnixMilliseconds = trade.Timestamp.ToUnixTimeMilliseconds()
					});
				}
			});
		}
		finally
		{
			_gate.Release();
		}
	}

	public async Task ResetAsync()
	{
		await InitializeAsync();

		await _gate.WaitAsync();
		try
		{
			await _connection.RunInTransactionAsync(transaction =>
			{
				transaction.DeleteAll<PortfolioPositionRow>();
				transaction.DeleteAll<PortfolioTradeRow>();
				transaction.DeleteAll<PortfolioStateRow>();
			});
		}
		finally
		{
			_gate.Release();
		}
	}

	private sealed class PortfolioStateRow
	{
		[PrimaryKey]
		public int Id { get; set; }

		public decimal Cash { get; set; }

		public decimal RealizedPnL { get; set; }

		public bool RecoveryUsed { get; set; }

		public bool IsTradingHalted { get; set; }
	}

	private sealed class PortfolioPositionRow
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }

		public string Symbol { get; set; } = string.Empty;

		public int Quantity { get; set; }

		public decimal AverageCost { get; set; }

		public decimal MarketPrice { get; set; }
	}

	private sealed class PortfolioTradeRow
	{
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }

		public string Side { get; set; } = string.Empty;

		public string Symbol { get; set; } = string.Empty;

		public int Quantity { get; set; }

		public decimal Price { get; set; }

		public long TimestampUnixMilliseconds { get; set; }
	}
}

public sealed record PersistedPosition(string Symbol, int Quantity, decimal AverageCost, decimal MarketPrice);

public sealed record PersistedTrade(string Side, string Symbol, int Quantity, decimal Price, DateTimeOffset Timestamp);

public sealed record PortfolioPersistedState(
	decimal Cash,
	decimal RealizedPnL,
	bool RecoveryUsed,
	bool IsTradingHalted,
	IReadOnlyList<PersistedPosition> Positions,
	IReadOnlyList<PersistedTrade> Trades);