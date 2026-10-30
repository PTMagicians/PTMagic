using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Helper;
using Core.Main;
using Core.Main.DataObjects.PTMagicData;
using Newtonsoft.Json;

namespace Core.MarketAnalyzer
{
  public class BinanceFutures : BaseAnalyzer
  {
    private const string ExchangeName = "BinanceFutures";
    private const string ApiBase = "https://fapi.binance.com/fapi/v1";

    public static Task<double> GetMainCurrencyPriceAsync(string mainMarket, PTMagicConfiguration systemConfiguration, LogHelper log, CancellationToken cancellationToken = default)
    {
      return BinanceAnalyzerCommon.GetMainCurrencyPriceAsync(ExchangeName, ApiBase, mainMarket, log, false, cancellationToken);
    }

    public static async Task<List<string>> GetMarketDataAsync(string mainMarket, ConcurrentDictionary<string, MarketInfo> marketInfos, PTMagicConfiguration systemConfiguration, LogHelper log, CancellationToken cancellationToken = default)
    {
      await BinanceFuturesMarketUniverse.RefreshAsync(ApiBase, log, cancellationToken).ConfigureAwait(false);

      List<string> markets = await BinanceAnalyzerCommon.GetMarketDataAsync(ExchangeName, ApiBase, mainMarket, marketInfos, systemConfiguration, log, false, true, true, false, cancellationToken).ConfigureAwait(false);
      if (markets == null)
      {
        return null;
      }

      List<string> filteredMarkets = BinanceFuturesMarketUniverse.FilterMarketNames(markets, systemConfiguration);
      FilterLatestMarketDataFile(systemConfiguration, log);

      if (filteredMarkets.Count != markets.Count)
      {
        log.DoLogInfo(ExchangeName + " - Market Analyzer universe filtered from " + markets.Count + " to " + filteredMarkets.Count + " markets.");
      }

      return filteredMarkets;
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
      return BinanceAnalyzerCommon.CheckForMarketDataRecreationAsync(ExchangeName, ApiBase, mainMarket, markets, systemConfiguration, log, false, false, cancellationToken);
    }

    private static void FilterLatestMarketDataFile(PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      try
      {
        string dataDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), Constants.PTMagicPathData, Constants.PTMagicPathExchange);
        if (!Directory.Exists(dataDirectoryPath))
        {
          return;
        }

        FileInfo latestMarketDataFile = new DirectoryInfo(dataDirectoryPath)
          .EnumerateFiles("MarketData*.json")
          .OrderByDescending(file => file.LastWriteTimeUtc)
          .FirstOrDefault();

        if (latestMarketDataFile == null)
        {
          return;
        }

        Dictionary<string, Market> markets = JsonConvert.DeserializeObject<Dictionary<string, Market>>(File.ReadAllText(latestMarketDataFile.FullName));
        if (markets == null)
        {
          return;
        }

        Dictionary<string, Market> filteredMarkets = BinanceFuturesMarketUniverse.FilterMarkets(markets, systemConfiguration);
        if (filteredMarkets.Count == markets.Count)
        {
          return;
        }

        File.WriteAllText(latestMarketDataFile.FullName, JsonConvert.SerializeObject(filteredMarkets));
        log.DoLogDebug(ExchangeName + " - Filtered latest MarketData file from " + markets.Count + " to " + filteredMarkets.Count + " markets.");
      }
      catch (Exception ex)
      {
        log.DoLogWarn(ExchangeName + " - Unable to filter latest MarketData file: " + ex.Message);
      }
    }
  }
}