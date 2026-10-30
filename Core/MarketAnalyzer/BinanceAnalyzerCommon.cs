using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Core.Helper;
using Core.Main;
using Core.Main.DataObjects.PTMagicData;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Core.MarketAnalyzer
{
  internal static class BinanceAnalyzerCommon
  {
    private const int MetadataConcurrency = 2;
    private const int HistoricalConcurrency = 2;
    private const string LastPriceField = "lastPrice";
    private const string VolumeField = "volume";
    private const string QuoteVolumeField = "quoteVolume";
    private const string SymbolField = "symbol";

    internal static async Task<List<string>> GetMarketDataAsync(string exchangeName, string apiBase, string mainMarket, ConcurrentDictionary<string, MarketInfo> marketInfos, PTMagicConfiguration systemConfiguration, LogHelper log, bool supportsUsdMainMarket, bool logIncorrectBaseCurrency, bool caseSensitiveBaseMatch, bool parallelHistoricalBuild, CancellationToken cancellationToken = default)
    {
      List<string> result = new List<string>();
      string lastMarket = "";

      try
      {
        log.DoLogInfo(exchangeName + " - Getting market data...");
        JArray jsonArray = await BaseAnalyzer.GetSimpleJsonArrayFromURLAsync(apiBase + "/ticker/24hr", log, cancellationToken).ConfigureAwait(false);
        if (jsonArray.Count == 0)
        {
          return result;
        }

        double mainCurrencyPrice = 1;
        if (!mainMarket.Equals("USDT", StringComparison.InvariantCultureIgnoreCase) && !(supportsUsdMainMarket && mainMarket.Equals("USD", StringComparison.InvariantCultureIgnoreCase)))
        {
          mainCurrencyPrice = await GetMainCurrencyPriceAsync(exchangeName, apiBase, mainMarket, log, supportsUsdMainMarket, cancellationToken).ConfigureAwait(false);
        }

        log.DoLogInfo(exchangeName + " - Market data received for " + jsonArray.Count.ToString() + " currencies");
        if (mainCurrencyPrice <= 0)
        {
          log.DoLogError(exchangeName + " - Failed to get main market price for " + mainMarket + ".");
          return null;
        }

        Dictionary<string, Market> markets = BuildMarkets(exchangeName, mainMarket, mainCurrencyPrice, jsonArray, result, log, logIncorrectBaseCurrency, caseSensitiveBaseMatch, ref lastMarket);
        await CheckFirstSeenDatesAsync(exchangeName, apiBase, markets, marketInfos, systemConfiguration, log, cancellationToken).ConfigureAwait(false);
        BaseAnalyzer.SaveMarketInfosToFile(marketInfos, systemConfiguration, log);
        await CheckForMarketDataRecreationAsync(exchangeName, apiBase, mainMarket, markets, systemConfiguration, log, supportsUsdMainMarket, parallelHistoricalBuild, cancellationToken).ConfigureAwait(false);
        SaveCurrentMarketData(exchangeName, mainMarket, markets, systemConfiguration, log);
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (OperationCanceledException ex)
      {
        log.DoLogError(exchangeName + " - Market data request timed out: " + ex.Message);
        return null;
      }
      catch (HttpRequestException ex)
      {
        string status = ex.StatusCode.HasValue ? " HTTP " + (int)ex.StatusCode.Value + " (" + ex.StatusCode.Value + ")" : "";
        log.DoLogError(exchangeName + " - HTTP request failed while getting market data." + status + " " + ex.Message);
        return null;
      }
      catch (Exception ex)
      {
        log.DoLogCritical("Exception while getting data for '" + lastMarket + "': " + ex.Message, ex);
        return null;
      }

      return result;
    }

    internal static async Task<double> GetMainCurrencyPriceAsync(string exchangeName, string apiBase, string mainMarket, LogHelper log, bool supportsUsdMainMarket, CancellationToken cancellationToken = default)
    {
      if (supportsUsdMainMarket && mainMarket.Equals("USD", StringComparison.InvariantCultureIgnoreCase))
      {
        return 1.0;
      }

      try
      {
        string symbol = mainMarket + "USDT";
        log.DoLogInfo(exchangeName + " - Getting main market price...");
        JObject jsonObject = await BaseAnalyzer.GetSimpleJsonObjectFromURLAsync(apiBase + "/ticker/24hr?symbol=" + symbol, log, null, cancellationToken).ConfigureAwait(false);
        if (jsonObject == null)
        {
          return 0;
        }

        log.DoLogInfo(exchangeName + " - Market data received for " + symbol);
        double result = (double)jsonObject.GetValue(LastPriceField);
        log.DoLogInfo(exchangeName + " - Current price for " + symbol + ": " + result.ToString("#,#0.00") + " USD");
        return result;
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (Exception ex)
      {
        log.DoLogCritical(ex.Message, ex);
        return 0;
      }
    }

    internal static async Task<DateTime> GetFirstSeenDateAsync(string exchangeName, string apiBase, string marketName, LogHelper log, CancellationToken cancellationToken = default)
    {
      DateTime result = Constants.confMinDate;
      log.DoLogDebug(exchangeName + " - Getting first seen date for '" + marketName + "'...");
      JArray jsonArray = await BaseAnalyzer.GetSimpleJsonArrayFromURLAsync(apiBase + "/klines?interval=1d&symbol=" + marketName + "&limit=100", log, cancellationToken).ConfigureAwait(false);
      if (jsonArray.Count > 0)
      {
        result = Constants.Epoch.AddMilliseconds((Int64)jsonArray[0][0]);
        log.DoLogDebug(exchangeName + " - First seen date for '" + marketName + "' set to " + result.ToString());
      }

      return result;
    }

    internal static async Task<List<MarketTick>> GetMarketTicksAsync(string exchangeName, string apiBase, string marketName, int ticksNeeded, LogHelper log, bool supportsUsdMainMarket, CancellationToken cancellationToken = default)
    {
      List<MarketTick> result = new List<MarketTick>();
      if (supportsUsdMainMarket && marketName == "USDUSDT")
      {
        return result;
      }

      try
      {
        Int64 endTime = (Int64)Math.Ceiling(DateTime.UtcNow.Subtract(Constants.Epoch).TotalMilliseconds);
        int ticksLimit = Math.Min(500, ticksNeeded);
        int ticksFetched = 0;

        while (ticksFetched < ticksNeeded)
        {
          string url = apiBase + "/klines?interval=1m&symbol=" + marketName + "&endTime=" + endTime.ToString() + "&limit=" + ticksLimit.ToString();
          log.DoLogDebug(exchangeName + " - Getting " + ticksLimit.ToString() + " ticks for '" + marketName + "'...");
          JArray jsonArray = await BaseAnalyzer.GetSimpleJsonArrayFromURLAsync(url, log, cancellationToken).ConfigureAwait(false);
          if (jsonArray.Count == 0)
          {
            log.DoLogDebug(exchangeName + " - No ticks received.");
            break;
          }

          log.DoLogDebug(exchangeName + " - " + jsonArray.Count.ToString() + " ticks received.");
          AddMarketTicks(result, jsonArray);
          ticksFetched += jsonArray.Count;
          endTime -= ticksLimit * 60 * 1000;
          ticksLimit = Math.Min(500, ticksNeeded - ticksFetched);
        }
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (OperationCanceledException ex)
      {
        log.DoLogError(exchangeName + " - Tick request timed out for '" + marketName + "': " + ex.Message);
        return null;
      }
      catch (HttpRequestException ex)
      {
        string status = ex.StatusCode.HasValue ? " HTTP " + (int)ex.StatusCode.Value + " (" + ex.StatusCode.Value + ")" : "";
        log.DoLogError(exchangeName + " - HTTP request failed while getting ticks for '" + marketName + "'." + status + " " + ex.Message);
        return null;
      }
      catch (Exception ex)
      {
        log.DoLogCritical(ex.Message, ex);
        return null;
      }

      return result;
    }

    internal static async Task CheckFirstSeenDatesAsync(string exchangeName, string apiBase, Dictionary<string, Market> markets, ConcurrentDictionary<string, MarketInfo> marketInfos, PTMagicConfiguration systemConfiguration, LogHelper log, CancellationToken cancellationToken = default)
    {
      log.DoLogInfo(exchangeName + " - Checking first seen dates for " + markets.Count + " markets. This may take a while...");
      int marketsChecked = 0;

      await Parallel.ForEachAsync(markets.Keys, new ParallelOptions { MaxDegreeOfParallelism = MetadataConcurrency, CancellationToken = cancellationToken }, async (key, ct) =>
      {
        if (!marketInfos.TryGetValue(key, out MarketInfo marketInfo))
        {
          MarketInfo newMarketInfo = new MarketInfo
          {
            Name = key,
            FirstSeen = await GetFirstSeenDateAsync(exchangeName, apiBase, key, log, ct).ConfigureAwait(false),
            LastSeen = DateTime.UtcNow
          };
          marketInfos.TryAdd(key, newMarketInfo);
        }
        else
        {
          if (marketInfo.FirstSeen == Constants.confMinDate)
          {
            marketInfo.FirstSeen = await GetFirstSeenDateAsync(exchangeName, apiBase, key, log, ct).ConfigureAwait(false);
          }
          marketInfo.LastSeen = DateTime.UtcNow;
        }

        int completed = Interlocked.Increment(ref marketsChecked);
        if ((completed % 20) == 0)
        {
          log.DoLogInfo(exchangeName + " - Yes, I am still checking first seen dates... " + completed + "/" + markets.Count + " markets done...");
        }
      }).ConfigureAwait(false);
    }

    internal static async Task CheckForMarketDataRecreationAsync(string exchangeName, string apiBase, string mainMarket, Dictionary<string, Market> markets, PTMagicConfiguration systemConfiguration, LogHelper log, bool supportsUsdMainMarket, bool parallelHistoricalBuild, CancellationToken cancellationToken = default)
    {
      string dataDirectoryPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + Constants.PTMagicPathExchange + Path.DirectorySeparatorChar;
      Directory.CreateDirectory(dataDirectoryPath);

      DirectoryInfo dataDirectory = new DirectoryInfo(dataDirectoryPath);
      DateTime latestMarketDataFileDateTime = dataDirectory.EnumerateFiles("MarketData*").OrderByDescending(mdf => mdf.LastWriteTimeUtc).FirstOrDefault()?.LastWriteTimeUtc ?? Constants.confMinDate;
      if (latestMarketDataFileDateTime >= DateTime.UtcNow.AddMinutes(-20))
      {
        return;
      }

      DateTime startDateTime = DateTime.UtcNow;
      DateTime endDateTime = DateTime.UtcNow.AddHours(-systemConfiguration.AnalyzerSettings.MarketAnalyzer.StoreDataMaxHours);
      if (latestMarketDataFileDateTime != Constants.confMinDate && latestMarketDataFileDateTime > endDateTime)
      {
        int ageSeconds = (int)Math.Ceiling(DateTime.UtcNow.Subtract(latestMarketDataFileDateTime).TotalSeconds);
        log.DoLogInfo(exchangeName + " - Recreating market data for " + markets.Count + " markets over " + SystemHelper.GetProperDurationTime(ageSeconds) + ". This may take a while...");
        endDateTime = latestMarketDataFileDateTime;
      }
      else
      {
        log.DoLogInfo(exchangeName + " - Recreating market data for " + markets.Count + " markets over " + systemConfiguration.AnalyzerSettings.MarketAnalyzer.StoreDataMaxHours + " hours. This may take a while...");
      }

      int totalTicks = (int)Math.Ceiling(startDateTime.Subtract(endDateTime).TotalMinutes);
      List<MarketTick> mainMarketTicks = new List<MarketTick>();
      if (!mainMarket.Equals("USDT", StringComparison.InvariantCultureIgnoreCase))
      {
        mainMarketTicks = await GetMarketTicksAsync(exchangeName, apiBase, mainMarket + "USDT", totalTicks, log, supportsUsdMainMarket, cancellationToken).ConfigureAwait(false);
      }

      log.DoLogDebug(exchangeName + " - Getting ticks for '" + markets.Count + "' markets");
      ConcurrentDictionary<string, List<MarketTick>> marketTicks = new ConcurrentDictionary<string, List<MarketTick>>();
      int marketsCompleted = 0;

      await Parallel.ForEachAsync(markets.Keys, new ParallelOptions { MaxDegreeOfParallelism = HistoricalConcurrency, CancellationToken = cancellationToken }, async (key, ct) =>
      {
        List<MarketTick> ticks = await GetMarketTicksAsync(exchangeName, apiBase, key, totalTicks, log, supportsUsdMainMarket, ct).ConfigureAwait(false);
        if (!marketTicks.TryAdd(key, ticks))
        {
          throw new Exception("Failed to add ticks for " + key + " to the memory dictionary, results may be incorrectly calculated!");
        }

        int completed = Interlocked.Increment(ref marketsCompleted);
        if ((completed % 10) == 0)
        {
          log.DoLogInfo(exchangeName + " - No worries, I am still alive... " + completed + "/" + markets.Count + " markets done...");
        }
      }).ConfigureAwait(false);

      log.DoLogInfo(exchangeName + " - Ticks completed.");
      log.DoLogInfo(exchangeName + " - Creating initial market data ticks. This may take another while...");
      CreateHistoricalMarketFiles(exchangeName, dataDirectoryPath, mainMarketTicks, marketTicks, markets, startDateTime, endDateTime, totalTicks, log, parallelHistoricalBuild, cancellationToken);
      log.DoLogInfo(exchangeName + " - Initial market data created. Ready to go!");
    }

    private static Dictionary<string, Market> BuildMarkets(string exchangeName, string mainMarket, double mainCurrencyPrice, JArray jsonArray, List<string> result, LogHelper log, bool logIncorrectBaseCurrency, bool caseSensitiveBaseMatch, ref string lastMarket)
    {
      Dictionary<string, Market> markets = new Dictionary<string, Market>();
      foreach (JObject currencyTicker in jsonArray)
      {
        string marketName = currencyTicker[SymbolField].ToString();
        bool correctBase = caseSensitiveBaseMatch ? marketName.EndsWith(mainMarket) : marketName.EndsWith(mainMarket, StringComparison.InvariantCultureIgnoreCase);
        float marketLastPrice = currencyTicker[LastPriceField].ToObject<float>();
        float marketVolume = currencyTicker[VolumeField].ToObject<float>();

        if (correctBase && marketLastPrice > 0 && marketVolume > 0)
        {
          lastMarket = marketName;
          Market market = new Market
          {
            Position = markets.Count + 1,
            Name = marketName,
            Symbol = currencyTicker[SymbolField].ToString(),
            Price = SystemHelper.TextToDouble(currencyTicker[LastPriceField].ToString(), 0, "en-US"),
            Volume24h = SystemHelper.TextToDouble(currencyTicker[QuoteVolumeField].ToString(), 0, "en-US"),
            MainCurrencyPriceUSD = mainCurrencyPrice
          };
          markets.Add(market.Name, market);
          result.Add(market.Name);
        }
        else if (correctBase)
        {
          log.DoLogInfo(exchangeName + " - Ignoring bad market data for " + marketName);
        }
        else if (logIncorrectBaseCurrency)
        {
          log.DoLogInfo(exchangeName + " - Incorrect base currency: " + marketName + " ignored");
        }
      }

      return markets;
    }

    private static void AddMarketTicks(List<MarketTick> result, JArray jsonArray)
    {
      foreach (JArray marketTick in jsonArray)
      {
        result.Add(new MarketTick
        {
          Price = (double)marketTick[4],
          Volume24h = (double)marketTick[7],
          Time = Constants.Epoch.AddMilliseconds((Int64)marketTick[0])
        });
      }
    }

    private static void SaveCurrentMarketData(string exchangeName, string mainMarket, Dictionary<string, Market> markets, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      string dataDirectoryPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + Constants.PTMagicPathExchange + Path.DirectorySeparatorChar;
      DateTime fileDateTime = DateTime.UtcNow;
      FileHelper.WriteTextToFile(dataDirectoryPath, "MarketData_" + fileDateTime.ToString("yyyy-MM-dd_HH.mm") + ".json", JsonConvert.SerializeObject(markets), fileDateTime, fileDateTime);
      log.DoLogInfo(exchangeName + " - Market data saved for " + markets.Count.ToString() + " markets with " + mainMarket + ".");
      FileHelper.CleanupFiles(dataDirectoryPath, systemConfiguration.AnalyzerSettings.MarketAnalyzer.StoreDataMaxHours);
      log.DoLogInfo(exchangeName + " - Market data cleaned.");
    }

    private static void CreateHistoricalMarketFiles(string exchangeName, string dataDirectoryPath, List<MarketTick> mainMarketTicks, ConcurrentDictionary<string, List<MarketTick>> marketTicks, Dictionary<string, Market> markets, DateTime startDateTime, DateTime endDateTime, int totalTicks, LogHelper log, bool parallelHistoricalBuild, CancellationToken cancellationToken)
    {
      int completedTicks = 0;
      for (DateTime tickTime = startDateTime; tickTime >= endDateTime; tickTime = tickTime.AddMinutes(-1))
      {
        cancellationToken.ThrowIfCancellationRequested();
        completedTicks++;

        double mainCurrencyPrice = 1;
        if (mainMarketTicks != null && mainMarketTicks.Count > 0)
        {
          MarketTick mainCurrencyTick = mainMarketTicks.Where(t => t.Time <= tickTime).OrderByDescending(t => t.Time).FirstOrDefault();
          if (mainCurrencyTick != null)
          {
            mainCurrencyPrice = mainCurrencyTick.Price;
          }
        }

        string tickMarketsJson;
        if (parallelHistoricalBuild)
        {
          ConcurrentDictionary<string, Market> tickMarkets = new ConcurrentDictionary<string, Market>();
          Parallel.ForEach(markets.Keys, key =>
          {
            MarketTick marketTick = marketTicks[key]?.Where(t => t.Time <= tickTime).OrderByDescending(t => t.Time).FirstOrDefault();
            if (marketTick != null)
            {
              tickMarkets.TryAdd(key, CreateHistoricalMarket(key, marketTick, markets.Count, mainCurrencyPrice));
            }
          });
          tickMarketsJson = JsonConvert.SerializeObject(tickMarkets);
        }
        else
        {
          Dictionary<string, Market> tickMarkets = new Dictionary<string, Market>();
          foreach (string key in markets.Keys)
          {
            MarketTick marketTick = marketTicks[key]?.Where(t => t.Time <= tickTime).OrderByDescending(t => t.Time).FirstOrDefault();
            if (marketTick != null)
            {
              tickMarkets.Add(key, CreateHistoricalMarket(key, marketTick, markets.Count, mainCurrencyPrice));
            }
          }
          tickMarketsJson = JsonConvert.SerializeObject(tickMarkets);
        }

        DateTime localTickTime = tickTime.ToLocalTime();
        DateTime fileDateTime = new DateTime(localTickTime.Year, localTickTime.Month, localTickTime.Day, localTickTime.Hour, localTickTime.Minute, 0).ToUniversalTime();
        FileHelper.WriteTextToFile(dataDirectoryPath, "MarketData_" + fileDateTime.ToString("yyyy-MM-dd_HH.mm") + ".json", tickMarketsJson, fileDateTime, fileDateTime);

        log.DoLogDebug(exchangeName + " - Market data saved for tick " + fileDateTime.ToString() + " - MainCurrencyPrice=" + mainCurrencyPrice.ToString("#,#0.00") + " USD.");
        if ((completedTicks % 100) == 0)
        {
          log.DoLogInfo(exchangeName + " - Our magicbots are still at work, hang on... " + completedTicks + "/" + totalTicks + " ticks done...");
        }
      }
    }

    private static Market CreateHistoricalMarket(string key, MarketTick marketTick, int marketCount, double mainCurrencyPrice)
    {
      return new Market
      {
        Position = marketCount + 1,
        Name = key,
        Symbol = key,
        Price = marketTick.Price,
        MainCurrencyPriceUSD = mainCurrencyPrice
      };
    }
  }
}