using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;
using Core.Main;
using Core.Helper;
using Core.Main.DataObjects.PTMagicData;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Core.MarketAnalyzer
{
  public class Binance : BaseAnalyzer
  {
    private const int MetadataConcurrency = 2;
    private const int HistoricalConcurrency = 2;

    public static double GetMainCurrencyPrice(string mainMarket, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      double result = 0;

      try
      {
        string baseUrl = "https://api.binance.com/api/v1/ticker/24hr?symbol=" + mainMarket + "USDT";

        log.DoLogInfo("Binance - Getting main market price...");
        Newtonsoft.Json.Linq.JObject jsonObject = GetSimpleJsonObjectFromURL(baseUrl, log, null);
        if (jsonObject != null)
        {
          log.DoLogInfo("Binance - Market data received for " + mainMarket + "USDT");
          result = (double)jsonObject.GetValue("lastPrice");
          log.DoLogInfo("Binance - Current price for " + mainMarket + "USDT: " + result.ToString("#,#0.00") + " USD");
        }
      }
      catch (Exception ex)
      {
        log.DoLogCritical(ex.Message, ex);
      }

      return result;
    }

    public static async Task<double> GetMainCurrencyPriceAsync(string mainMarket, PTMagicConfiguration systemConfiguration, LogHelper log, CancellationToken cancellationToken = default)
    {
      double result = 0;

      try
      {
        string baseUrl = "https://api.binance.com/api/v1/ticker/24hr?symbol=" + mainMarket + "USDT";

        log.DoLogInfo("Binance - Getting main market price...");
        Newtonsoft.Json.Linq.JObject jsonObject = await GetSimpleJsonObjectFromURLAsync(baseUrl, log, null, cancellationToken).ConfigureAwait(false);
        if (jsonObject != null)
        {
          log.DoLogInfo("Binance - Market data received for " + mainMarket + "USDT");
          result = (double)jsonObject.GetValue("lastPrice");
          log.DoLogInfo("Binance - Current price for " + mainMarket + "USDT: " + result.ToString("#,#0.00") + " USD");
        }
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (Exception ex)
      {
        log.DoLogCritical(ex.Message, ex);
      }

      return result;
    }

    public static List<string> GetMarketData(string mainMarket, ConcurrentDictionary<string, MarketInfo> marketInfos, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      List<string> result = new List<string>();
      string lastMarket = "";

      try
      {
        string baseUrl = "https://api.binance.com/api/v1/ticker/24hr";

        log.DoLogInfo("Binance - Getting market data...");
        Newtonsoft.Json.Linq.JArray jsonArray = GetSimpleJsonArrayFromURL(baseUrl, log);
        if (jsonArray.Count > 0)
        {
          double mainCurrencyPrice = 1;
          if (!mainMarket.Equals("USDT", StringComparison.InvariantCultureIgnoreCase))
          {
            mainCurrencyPrice = Binance.GetMainCurrencyPrice(mainMarket, systemConfiguration, log);
          }

          log.DoLogInfo("Binance - Market data received for " + jsonArray.Count.ToString() + " currencies");

          if (mainCurrencyPrice > 0)
          {
            Dictionary<string, Market> markets = BuildMarkets(mainMarket, mainCurrencyPrice, jsonArray, result, log, ref lastMarket);
            Binance.CheckFirstSeenDates(markets, ref marketInfos, systemConfiguration, log);
            BaseAnalyzer.SaveMarketInfosToFile(marketInfos, systemConfiguration, log);
            Binance.CheckForMarketDataRecreation(mainMarket, markets, systemConfiguration, log);
            SaveCurrentMarketData(mainMarket, markets, systemConfiguration, log);
          }
          else
          {
            log.DoLogError("Binance - Failed to get main market price for " + mainMarket + ".");
            result = null;
          }
        }
      }
      catch (OperationCanceledException ex)
      {
        log.DoLogError("Binance - Market data request timed out or was cancelled: " + ex.Message);
        result = null;
      }
      catch (HttpRequestException ex)
      {
        string status = ex.StatusCode.HasValue ? " HTTP " + (int)ex.StatusCode.Value + " (" + ex.StatusCode.Value + ")" : "";
        log.DoLogError("Binance - HTTP request failed while getting market data." + status + " " + ex.Message);
        result = null;
      }
      catch (Exception ex)
      {
        log.DoLogCritical("Exception while getting data for '" + lastMarket + "': " + ex.Message, ex);
        result = null;
      }

      return result;
    }

    public static async Task<List<string>> GetMarketDataAsync(string mainMarket, ConcurrentDictionary<string, MarketInfo> marketInfos, PTMagicConfiguration systemConfiguration, LogHelper log, CancellationToken cancellationToken = default)
    {
      List<string> result = new List<string>();
      string lastMarket = "";

      try
      {
        string baseUrl = "https://api.binance.com/api/v1/ticker/24hr";

        log.DoLogInfo("Binance - Getting market data...");
        Newtonsoft.Json.Linq.JArray jsonArray = await GetSimpleJsonArrayFromURLAsync(baseUrl, log, cancellationToken).ConfigureAwait(false);
        if (jsonArray.Count > 0)
        {
          double mainCurrencyPrice = 1;
          if (!mainMarket.Equals("USDT", StringComparison.InvariantCultureIgnoreCase))
          {
            mainCurrencyPrice = await Binance.GetMainCurrencyPriceAsync(mainMarket, systemConfiguration, log, cancellationToken).ConfigureAwait(false);
          }

          log.DoLogInfo("Binance - Market data received for " + jsonArray.Count.ToString() + " currencies");

          if (mainCurrencyPrice > 0)
          {
            Dictionary<string, Market> markets = BuildMarkets(mainMarket, mainCurrencyPrice, jsonArray, result, log, ref lastMarket);
            await Binance.CheckFirstSeenDatesAsync(markets, marketInfos, systemConfiguration, log, cancellationToken).ConfigureAwait(false);
            BaseAnalyzer.SaveMarketInfosToFile(marketInfos, systemConfiguration, log);
            await Binance.CheckForMarketDataRecreationAsync(mainMarket, markets, systemConfiguration, log, cancellationToken).ConfigureAwait(false);
            SaveCurrentMarketData(mainMarket, markets, systemConfiguration, log);
          }
          else
          {
            log.DoLogError("Binance - Failed to get main market price for " + mainMarket + ".");
            result = null;
          }
        }
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (OperationCanceledException ex)
      {
        log.DoLogError("Binance - Market data request timed out: " + ex.Message);
        result = null;
      }
      catch (HttpRequestException ex)
      {
        string status = ex.StatusCode.HasValue ? " HTTP " + (int)ex.StatusCode.Value + " (" + ex.StatusCode.Value + ")" : "";
        log.DoLogError("Binance - HTTP request failed while getting market data." + status + " " + ex.Message);
        result = null;
      }
      catch (Exception ex)
      {
        log.DoLogCritical("Exception while getting data for '" + lastMarket + "': " + ex.Message, ex);
        result = null;
      }

      return result;
    }

    private static Dictionary<string, Market> BuildMarkets(string mainMarket, double mainCurrencyPrice, Newtonsoft.Json.Linq.JArray jsonArray, List<string> result, LogHelper log, ref string lastMarket)
    {
      Dictionary<string, Market> markets = new Dictionary<string, Market>();

      foreach (Newtonsoft.Json.Linq.JObject currencyTicker in jsonArray)
      {
        string marketName = currencyTicker["symbol"].ToString();
        float marketLastPrice = currencyTicker["lastPrice"].ToObject<float>();
        float marketVolume = currencyTicker["volume"].ToObject<float>();
        if (marketName.EndsWith(mainMarket, StringComparison.InvariantCultureIgnoreCase) && marketLastPrice > 0 && marketVolume > 0)
        {
          lastMarket = marketName;

          Market market = new Market();
          market.Position = markets.Count + 1;
          market.Name = marketName;
          market.Symbol = currencyTicker["symbol"].ToString();
          market.Price = SystemHelper.TextToDouble(currencyTicker["lastPrice"].ToString(), 0, "en-US");
          market.Volume24h = SystemHelper.TextToDouble(currencyTicker["quoteVolume"].ToString(), 0, "en-US");
          market.MainCurrencyPriceUSD = mainCurrencyPrice;

          markets.Add(market.Name, market);
          result.Add(market.Name);
        }
        else if (marketName.EndsWith(mainMarket, StringComparison.InvariantCultureIgnoreCase))
        {
          log.DoLogInfo("Binance - Ignoring bad market data for " + marketName);
        }
      }

      return markets;
    }

    private static void SaveCurrentMarketData(string mainMarket, Dictionary<string, Market> markets, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      DateTime fileDateTime = DateTime.UtcNow;
      FileHelper.WriteTextToFile(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + Constants.PTMagicPathExchange + Path.DirectorySeparatorChar, "MarketData_" + fileDateTime.ToString("yyyy-MM-dd_HH.mm") + ".json", JsonConvert.SerializeObject(markets), fileDateTime, fileDateTime);

      log.DoLogInfo("Binance - Market data saved for " + markets.Count.ToString() + " markets with " + mainMarket + ".");
      FileHelper.CleanupFiles(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + Constants.PTMagicPathExchange + Path.DirectorySeparatorChar, systemConfiguration.AnalyzerSettings.MarketAnalyzer.StoreDataMaxHours);
      log.DoLogInfo("Binance - Market data cleaned.");
    }

    public static void CheckFirstSeenDates(Dictionary<string, Market> markets, ref ConcurrentDictionary<string, MarketInfo> marketInfos, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      log.DoLogInfo("Binance - Checking first seen dates for " + markets.Count + " markets. This may take a while...");

      int marketsChecked = 0;
      foreach (string key in markets.Keys)
      {
        MarketInfo marketInfo = null;
        if (marketInfos.ContainsKey(key))
        {
          marketInfo = marketInfos[key];
        }

        if (marketInfo == null)
        {
          marketInfo = new MarketInfo();
          marketInfo.Name = key;
          marketInfos.TryAdd(key, marketInfo);
          marketInfo.FirstSeen = Binance.GetFirstSeenDate(key, systemConfiguration, log);
        }
        else if (marketInfo.FirstSeen == Constants.confMinDate)
        {
          marketInfo.FirstSeen = Binance.GetFirstSeenDate(key, systemConfiguration, log);
        }
        marketInfo.LastSeen = DateTime.UtcNow;

        marketsChecked++;
        if ((marketsChecked % 20) == 0)
        {
          log.DoLogInfo("Binance - Yes, I am still checking first seen dates... " + marketsChecked + "/" + markets.Count + " markets done...");
        }
      }
    }

    public static async Task CheckFirstSeenDatesAsync(Dictionary<string, Market> markets, ConcurrentDictionary<string, MarketInfo> marketInfos, PTMagicConfiguration systemConfiguration, LogHelper log, CancellationToken cancellationToken = default)
    {
      log.DoLogInfo("Binance - Checking first seen dates for " + markets.Count + " markets. This may take a while...");

      int marketsChecked = 0;
      await Parallel.ForEachAsync(markets.Keys, new ParallelOptions { MaxDegreeOfParallelism = MetadataConcurrency, CancellationToken = cancellationToken }, async (key, ct) =>
      {
        if (!marketInfos.TryGetValue(key, out MarketInfo marketInfo))
        {
          MarketInfo newMarketInfo = new MarketInfo();
          newMarketInfo.Name = key;
          newMarketInfo.FirstSeen = await Binance.GetFirstSeenDateAsync(key, systemConfiguration, log, ct).ConfigureAwait(false);
          newMarketInfo.LastSeen = DateTime.UtcNow;
          marketInfos.TryAdd(key, newMarketInfo);
        }
        else
        {
          if (marketInfo.FirstSeen == Constants.confMinDate)
          {
            marketInfo.FirstSeen = await Binance.GetFirstSeenDateAsync(key, systemConfiguration, log, ct).ConfigureAwait(false);
          }
          marketInfo.LastSeen = DateTime.UtcNow;
        }

        int completed = Interlocked.Increment(ref marketsChecked);
        if ((completed % 20) == 0)
        {
          log.DoLogInfo("Binance - Yes, I am still checking first seen dates... " + completed + "/" + markets.Count + " markets done...");
        }
      }).ConfigureAwait(false);
    }

    public static DateTime GetFirstSeenDate(string marketName, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      DateTime result = Constants.confMinDate;
      string baseUrl = "https://api.binance.com/api/v1/klines?interval=1d&symbol=" + marketName + "&limit=100";

      log.DoLogDebug("Binance - Getting first seen date for '" + marketName + "'...");
      Newtonsoft.Json.Linq.JArray jsonArray = GetSimpleJsonArrayFromURL(baseUrl, log);
      if (jsonArray.Count > 0)
      {
        result = Constants.Epoch.AddMilliseconds((Int64)jsonArray[0][0]);
        log.DoLogDebug("Binance - First seen date for '" + marketName + "' set to " + result.ToString());
      }

      return result;
    }

    public static async Task<DateTime> GetFirstSeenDateAsync(string marketName, PTMagicConfiguration systemConfiguration, LogHelper log, CancellationToken cancellationToken = default)
    {
      DateTime result = Constants.confMinDate;
      string baseUrl = "https://api.binance.com/api/v1/klines?interval=1d&symbol=" + marketName + "&limit=100";

      log.DoLogDebug("Binance - Getting first seen date for '" + marketName + "'...");
      Newtonsoft.Json.Linq.JArray jsonArray = await GetSimpleJsonArrayFromURLAsync(baseUrl, log, cancellationToken).ConfigureAwait(false);
      if (jsonArray.Count > 0)
      {
        result = Constants.Epoch.AddMilliseconds((Int64)jsonArray[0][0]);
        log.DoLogDebug("Binance - First seen date for '" + marketName + "' set to " + result.ToString());
      }

      return result;
    }

    public static List<MarketTick> GetMarketTicks(string marketName, int ticksNeeded, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      List<MarketTick> result = new List<MarketTick>();

      try
      {
        Int64 endTime = (Int64)Math.Ceiling(DateTime.UtcNow.Subtract(Constants.Epoch).TotalMilliseconds);
        int ticksLimit = Math.Min(500, ticksNeeded);
        int ticksFetched = 0;

        bool go = true;
        while (ticksFetched < ticksNeeded && go)
        {
          string baseUrl = "https://api.binance.com/api/v1/klines?interval=1m&symbol=" + marketName + "&endTime=" + endTime.ToString() + "&limit=" + ticksLimit.ToString();

          log.DoLogDebug("Binance - Getting " + ticksLimit.ToString() + " ticks for '" + marketName + "'...");
          Newtonsoft.Json.Linq.JArray jsonArray = GetSimpleJsonArrayFromURL(baseUrl, log);
          if (jsonArray.Count > 0)
          {
            log.DoLogDebug("Binance - " + jsonArray.Count.ToString() + " ticks received.");
            AddMarketTicks(result, jsonArray);

            ticksFetched += jsonArray.Count;
            endTime -= ticksLimit * 60 * 1000;
            if (ticksNeeded - ticksFetched < ticksLimit)
            {
              ticksLimit = ticksNeeded - ticksFetched;
            }
          }
          else
          {
            log.DoLogDebug("Binance - No ticks received.");
            go = false;
          }
        }
      }
      catch (OperationCanceledException ex)
      {
        log.DoLogError("Binance - Tick request timed out for '" + marketName + "': " + ex.Message);
        result = null;
      }
      catch (HttpRequestException ex)
      {
        string status = ex.StatusCode.HasValue ? " HTTP " + (int)ex.StatusCode.Value + " (" + ex.StatusCode.Value + ")" : "";
        log.DoLogError("Binance - HTTP request failed while getting ticks for '" + marketName + "'." + status + " " + ex.Message);
        result = null;
      }
      catch (Exception ex)
      {
        log.DoLogCritical(ex.Message, ex);
      }

      return result;
    }

    public static async Task<List<MarketTick>> GetMarketTicksAsync(string marketName, int ticksNeeded, PTMagicConfiguration systemConfiguration, LogHelper log, CancellationToken cancellationToken = default)
    {
      List<MarketTick> result = new List<MarketTick>();

      try
      {
        Int64 endTime = (Int64)Math.Ceiling(DateTime.UtcNow.Subtract(Constants.Epoch).TotalMilliseconds);
        int ticksLimit = Math.Min(500, ticksNeeded);
        int ticksFetched = 0;

        bool go = true;
        while (ticksFetched < ticksNeeded && go)
        {
          string baseUrl = "https://api.binance.com/api/v1/klines?interval=1m&symbol=" + marketName + "&endTime=" + endTime.ToString() + "&limit=" + ticksLimit.ToString();

          log.DoLogDebug("Binance - Getting " + ticksLimit.ToString() + " ticks for '" + marketName + "'...");
          Newtonsoft.Json.Linq.JArray jsonArray = await GetSimpleJsonArrayFromURLAsync(baseUrl, log, cancellationToken).ConfigureAwait(false);
          if (jsonArray.Count > 0)
          {
            log.DoLogDebug("Binance - " + jsonArray.Count.ToString() + " ticks received.");
            AddMarketTicks(result, jsonArray);

            ticksFetched += jsonArray.Count;
            endTime -= ticksLimit * 60 * 1000;
            if (ticksNeeded - ticksFetched < ticksLimit)
            {
              ticksLimit = ticksNeeded - ticksFetched;
            }
          }
          else
          {
            log.DoLogDebug("Binance - No ticks received.");
            go = false;
          }
        }
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (OperationCanceledException ex)
      {
        log.DoLogError("Binance - Tick request timed out for '" + marketName + "': " + ex.Message);
        result = null;
      }
      catch (HttpRequestException ex)
      {
        string status = ex.StatusCode.HasValue ? " HTTP " + (int)ex.StatusCode.Value + " (" + ex.StatusCode.Value + ")" : "";
        log.DoLogError("Binance - HTTP request failed while getting ticks for '" + marketName + "'." + status + " " + ex.Message);
        result = null;
      }
      catch (Exception ex)
      {
        log.DoLogCritical(ex.Message, ex);
      }

      return result;
    }

    private static void AddMarketTicks(List<MarketTick> result, Newtonsoft.Json.Linq.JArray jsonArray)
    {
      foreach (Newtonsoft.Json.Linq.JArray marketTick in jsonArray)
      {
        MarketTick tick = new MarketTick();
        tick.Price = (double)marketTick[4];
        tick.Volume24h = (double)marketTick[7];
        tick.Time = Constants.Epoch.AddMilliseconds((Int64)marketTick[0]);
        result.Add(tick);
      }
    }

    public static void CheckForMarketDataRecreation(string mainMarket, Dictionary<string, Market> markets, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      CheckForMarketDataRecreationAsync(mainMarket, markets, systemConfiguration, log).GetAwaiter().GetResult();
    }

    public static async Task CheckForMarketDataRecreationAsync(string mainMarket, Dictionary<string, Market> markets, PTMagicConfiguration systemConfiguration, LogHelper log, CancellationToken cancellationToken = default)
    {
      string binanceDataDirectoryPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + Constants.PTMagicPathExchange + Path.DirectorySeparatorChar;

      if (!Directory.Exists(binanceDataDirectoryPath))
      {
        Directory.CreateDirectory(binanceDataDirectoryPath);
      }

      DirectoryInfo dataDirectory = new DirectoryInfo(binanceDataDirectoryPath);
      DateTime latestMarketDataFileDateTime = Constants.confMinDate;
      List<FileInfo> marketFiles = dataDirectory.EnumerateFiles("MarketData*").ToList();
      if (marketFiles.Count > 0)
      {
        latestMarketDataFileDateTime = marketFiles.OrderByDescending(mdf => mdf.LastWriteTimeUtc).First().LastWriteTimeUtc;
      }

      if (latestMarketDataFileDateTime < DateTime.UtcNow.AddMinutes(-20))
      {
        int lastMarketDataAgeInSeconds = (int)Math.Ceiling(DateTime.UtcNow.Subtract(latestMarketDataFileDateTime).TotalSeconds);
        DateTime startDateTime = DateTime.UtcNow;
        DateTime endDateTime = DateTime.UtcNow.AddHours(-systemConfiguration.AnalyzerSettings.MarketAnalyzer.StoreDataMaxHours);
        if (latestMarketDataFileDateTime != Constants.confMinDate && latestMarketDataFileDateTime > endDateTime)
        {
          log.DoLogInfo("Binance - Recreating market data for " + markets.Count + " markets over " + SystemHelper.GetProperDurationTime(lastMarketDataAgeInSeconds) + ". This may take a while...");
          endDateTime = latestMarketDataFileDateTime;
        }
        else
        {
          log.DoLogInfo("Binance - Recreating market data for " + markets.Count + " markets over " + systemConfiguration.AnalyzerSettings.MarketAnalyzer.StoreDataMaxHours + " hours. This may take a while...");
        }

        int totalTicks = (int)Math.Ceiling(startDateTime.Subtract(endDateTime).TotalMinutes);

        List<MarketTick> mainMarketTicks = new List<MarketTick>();
        if (!mainMarket.Equals("USDT", StringComparison.InvariantCultureIgnoreCase))
        {
          mainMarketTicks = await Binance.GetMarketTicksAsync(mainMarket + "USDT", totalTicks, systemConfiguration, log, cancellationToken).ConfigureAwait(false);
        }

        log.DoLogDebug("Binance - Getting ticks for '" + markets.Count + "' markets");
        ConcurrentDictionary<string, List<MarketTick>> marketTicks = new ConcurrentDictionary<string, List<MarketTick>>();

        int marketsCompleted = 0;
        await Parallel.ForEachAsync(markets.Keys, new ParallelOptions { MaxDegreeOfParallelism = HistoricalConcurrency, CancellationToken = cancellationToken }, async (key, ct) =>
        {
          List<MarketTick> ticks = await GetMarketTicksAsync(key, totalTicks, systemConfiguration, log, ct).ConfigureAwait(false);
          if (!marketTicks.TryAdd(key, ticks))
          {
            throw new Exception("Failed to add ticks for " + key + " to the memory dictionary, results may be incorrectly calculated!");
          }

          int completed = Interlocked.Increment(ref marketsCompleted);
          if ((completed % 10) == 0)
          {
            log.DoLogInfo("Binance - No worries, I am still alive... " + completed + "/" + markets.Count + " markets done...");
          }
        }).ConfigureAwait(false);

        log.DoLogInfo("Binance - Ticks completed.");
        log.DoLogInfo("Binance - Creating initial market data ticks. This may take another while...");

        int completedTicks = 0;
        if (marketTicks.Count > 0)
        {
          for (DateTime tickTime = startDateTime; tickTime >= endDateTime; tickTime = tickTime.AddMinutes(-1))
          {
            cancellationToken.ThrowIfCancellationRequested();
            completedTicks++;

            double mainCurrencyPrice = 1;
            if (mainMarketTicks != null && mainMarketTicks.Count > 0)
            {
              List<MarketTick> mainCurrencyTickRange = mainMarketTicks.FindAll(t => t.Time <= tickTime);
              if (mainCurrencyTickRange.Count > 0)
              {
                mainCurrencyPrice = mainCurrencyTickRange.OrderByDescending(t => t.Time).First().Price;
              }
            }

            ConcurrentDictionary<string, Market> tickMarkets = new ConcurrentDictionary<string, Market>();
            Parallel.ForEach(markets.Keys, (key) =>
            {
              List<MarketTick> tickRange = marketTicks[key] != null ? marketTicks[key].FindAll(t => t.Time <= tickTime) : new List<MarketTick>();
              if (tickRange.Count > 0)
              {
                MarketTick marketTick = tickRange.OrderByDescending(t => t.Time).First();
                Market market = new Market();
                market.Position = markets.Count + 1;
                market.Name = key;
                market.Symbol = key;
                market.Price = marketTick.Price;
                market.MainCurrencyPriceUSD = mainCurrencyPrice;
                tickMarkets.TryAdd(market.Name, market);
              }
            });

            DateTime fileDateTime = new DateTime(tickTime.ToLocalTime().Year, tickTime.ToLocalTime().Month, tickTime.ToLocalTime().Day, tickTime.ToLocalTime().Hour, tickTime.ToLocalTime().Minute, 0).ToUniversalTime();
            FileHelper.WriteTextToFile(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + Constants.PTMagicPathExchange + Path.DirectorySeparatorChar, "MarketData_" + fileDateTime.ToString("yyyy-MM-dd_HH.mm") + ".json", JsonConvert.SerializeObject(tickMarkets), fileDateTime, fileDateTime);

            log.DoLogDebug("Binance - Market data saved for tick " + fileDateTime.ToString() + " - MainCurrencyPrice=" + mainCurrencyPrice.ToString("#,#0.00") + " USD.");
            if ((completedTicks % 100) == 0)
            {
              log.DoLogInfo("Binance - Our magicbots are still at work, hang on... " + completedTicks + "/" + totalTicks + " ticks done...");
            }
          }
        }

        log.DoLogInfo("Binance - Initial market data created. Ready to go!");
      }
    }
  }
}
