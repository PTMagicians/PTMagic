using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Core.Helper;
using Core.Main;
using Core.Main.DataObjects.PTMagicData;

namespace Core.MarketAnalyzer
{
  public class Binance : BaseAnalyzer
  {
    private const string ExchangeName = "Binance";
    private const string ApiBase = "https://api.binance.com/api/v1";

    public static Task<double> GetMainCurrencyPriceAsync(string mainMarket, PTMagicConfiguration systemConfiguration, LogHelper log, CancellationToken cancellationToken = default)
    {
      return BinanceAnalyzerCommon.GetMainCurrencyPriceAsync(ExchangeName, ApiBase, mainMarket, log, false, cancellationToken);
    }

    public static Task<List<string>> GetMarketDataAsync(string mainMarket, ConcurrentDictionary<string, MarketInfo> marketInfos, PTMagicConfiguration systemConfiguration, LogHelper log, CancellationToken cancellationToken = default)
    {
      return BinanceAnalyzerCommon.GetMarketDataAsync(ExchangeName, ApiBase, mainMarket, marketInfos, systemConfiguration, log, false, false, false, true, cancellationToken);
    }

    public static Task CheckFirstSeenDatesAsync(Dictionary<string, Market> markets, ConcurrentDictionary<string, MarketInfo> marketInfos, PTMagicConfiguration systemConfiguration, LogHelper log, CancellationToken cancellationToken = default)
    {
      return BinanceAnalyzerCommon.CheckFirstSeenDatesAsync(ExchangeName, ApiBase, markets, marketInfos, systemConfiguration, log, cancellationToken);
    }

    public static Task<DateTime> GetFirstSeenDateAsync(string marketName, PTMagicConfiguration systemConfiguration, LogHelper log, CancellationToken cancellationToken = default)
    {
      return BinanceAnalyzerCommon.GetFirstSeenDateAsync(ExchangeName, ApiBase, marketName, log, cancellationToken);
    }

    public static Task<List<MarketTick>> GetMarketTicksAsync(string marketName, int ticksNeeded, PTMagicConfiguration systemConfiguration, LogHelper log, CancellationToken cancellationToken = default)
    {
      return BinanceAnalyzerCommon.GetMarketTicksAsync(ExchangeName, ApiBase, marketName, ticksNeeded, log, false, cancellationToken);
    }

    public static Task CheckForMarketDataRecreationAsync(string mainMarket, Dictionary<string, Market> markets, PTMagicConfiguration systemConfiguration, LogHelper log, CancellationToken cancellationToken = default)
    {
      return BinanceAnalyzerCommon.CheckForMarketDataRecreationAsync(ExchangeName, ApiBase, mainMarket, markets, systemConfiguration, log, false, true, cancellationToken);
    }
  }
}