using StockSimulator.Models;

namespace StockSimulator.Services;

public static class StockCatalog
{
    public static IReadOnlyList<StockInstrument> TradeUniverse { get; } =
    [
        new("AAPL", "Apple", "Mega", "US"),
        new("MSFT", "Microsoft", "Mega", "US"),
        new("NVDA", "NVIDIA", "Mega", "US"),
        new("AMZN", "Amazon", "Mega", "US"),
        new("GOOGL", "Alphabet", "Mega", "US"),
        new("TSLA", "Tesla", "Large", "US"),

        new("ATCO-A.ST", "Atlas Copco A", "Large", "Sweden"),
        new("ATCO-B.ST", "Atlas Copco B", "Large", "Sweden"),
        new("VOLV-B.ST", "Volvo B", "Large", "Sweden"),
        new("ERIC-B.ST", "Ericsson B", "Large", "Sweden"),
        new("INVE-B.ST", "Investor B", "Large", "Sweden"),
        new("SEB-A.ST", "SEB A", "Large", "Sweden"),
        new("SWED-A.ST", "Swedbank A", "Large", "Sweden"),
        new("SHB-A.ST", "Handelsbanken A", "Large", "Sweden"),
        new("ASSA-B.ST", "Assa Abloy B", "Large", "Sweden"),
        new("SAND.ST", "Sandvik", "Large", "Sweden"),
        new("SKF-B.ST", "SKF B", "Large", "Sweden"),
        new("EVO.ST", "Evolution", "Large", "Sweden"),

        new("NIBE-B.ST", "Nibe B", "Mid", "Sweden"),
        new("LATO-B.ST", "Latour B", "Mid", "Sweden"),
        new("ALFA.ST", "Alfa Laval", "Mid", "Sweden"),
        new("HEXA-B.ST", "Hexagon B", "Mid", "Sweden"),
        new("TEL2-B.ST", "Tele2 B", "Mid", "Sweden"),
        new("BOL.ST", "Boliden", "Mid", "Sweden"),
        new("SCA-B.ST", "SCA B", "Mid", "Sweden"),
        new("ESSITY-B.ST", "Essity B", "Mid", "Sweden"),
        new("SINCH.ST", "Sinch", "Mid", "Sweden"),

        new("SBB-B.ST", "SBB B", "Small", "Sweden"),
        new("JM.ST", "JM", "Small", "Sweden"),
        new("MYCR.ST", "Mycronic", "Small", "Sweden"),
        new("AAK.ST", "AAK", "Small", "Sweden"),
        new("WIHL.ST", "Wihlborgs", "Small", "Sweden"),
        new("BICO.ST", "BICO Group", "Small", "Sweden"),
        new("CATE.ST", "Catena", "Small", "Sweden")
    ];

    public static IReadOnlyList<StockInstrument> SwedenLarge { get; } = TradeUniverse.Where(item => item.Region == "Sweden" && item.Cap == "Large").ToList();

    public static IReadOnlyList<StockInstrument> SwedenMid { get; } = TradeUniverse.Where(item => item.Region == "Sweden" && item.Cap == "Mid").ToList();

    public static IReadOnlyList<StockInstrument> SwedenSmall { get; } = TradeUniverse.Where(item => item.Region == "Sweden" && item.Cap == "Small").ToList();

    public static IReadOnlyList<StockInstrument> SwedenAll { get; } = TradeUniverse.Where(item => item.Region == "Sweden").ToList();
}
