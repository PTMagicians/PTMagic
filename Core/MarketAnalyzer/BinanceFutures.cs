﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;
using Core.Main;
using Core.Helper;
using Core.Main.DataObjects.PTMagicData;
using Newtonsoft.Json;
using System.Net;
using System.Threading.Tasks;

namespace Core.MarketAnalyzer
{
  public class BinanceFutures : BaseAnalyzer
  {
    public static double GetMainCurrencyPrice(string mainMarket, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      double result = 0;

      try
      {
        string baseUrl = "https://fapi.binance.com/fapi/v1/ticker/24hr?symbol=" + mainMarket + "USDT";

        log.DoLogInfo("BinanceFutures - Getting main market price...");
        Newtonsoft.Json.Linq.JObject jsonObject = GetSimpleJsonObjectFromURL(baseUrl, log, null);
        if (jsonObject != null)
        {
          log.DoLogInfo("BinanceFutures - Market data received for " + mainMarket + "USDT");

          result = (double)jsonObject.GetValue("lastPrice");
          log.DoLogInfo("BinanceFutures - Current price for " + mainMarket + "USDT: " + result.ToString("#,#0.00") + " USD");
        }
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
      Newtonsoft.Json.Linq.JObject lastTicker = null;
      try
      {
        string baseUrl = "https://fapi.binance.com/fapi/v1/ticker/24hr";

        log.DoLogInfo("BinanceFutures - Getting market data...");
        Newtonsoft.Json.Linq.JArray jsonArray = GetSimpleJsonArrayFromURL(baseUrl, log);
        if (jsonArray.Count > 0)
        {
          double mainCurrencyPrice = 1;
          if (!mainMarket.Equals("USDT", StringComparison.InvariantCultureIgnoreCase))
          {
            mainCurrencyPrice = BinanceFutures.GetMainCurrencyPrice(mainMarket, systemConfiguration, log);
          }

          log.DoLogInfo("BinanceFutures - Market data received for " + jsonArray.Count.ToString() + " currencies");

          if (mainCurrencyPrice > 0)
          {
            Dictionary<string, Market> markets = new Dictionary<string, Market>();
            foreach (Newtonsoft.Json.Linq.JObject currencyTicker in jsonArray)
            {
              string marketName = currencyTicker["symbol"].ToString();
              //New variables for filtering out bad markets
              float marketLastPrice = currencyTicker["lastPrice"].ToObject<float>();
              float marketVolume = currencyTicker["volume"].ToObject<float>();
              if (marketLastPrice > 0 && marketVolume > 0 && marketName.EndsWith(mainMarket))
                {

                  // Set last values in case any error occurs
                  lastMarket = marketName;
                  lastTicker = currencyTicker;

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
                else
                {
                  //Let the user know that a problem market was ignored.
                  if (!marketName.EndsWith(mainMarket))
                  {
                    log.DoLogInfo("BinanceFutures - Incorrect base currency: " + marketName + " ignored");
                  }
                  else
                  {
                  log.DoLogInfo("BinanceFutures - Ignoring bad market data for " + marketName);
                  }
                }
            }

            BinanceFutures.CheckFirstSeenDates(markets, ref marketInfos, systemConfiguration, log);

            BaseAnalyzer.SaveMarketInfosToFile(marketInfos, systemConfiguration, log);

            BinanceFutures.CheckForMarketDataRecreation(mainMarket, markets, systemConfiguration, log);

            DateTime fileDateTime = DateTime.UtcNow;

            FileHelper.WriteTextToFile(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + Constants.PTMagicPathExchange + Path.DirectorySeparatorChar, "MarketData_" + fileDateTime.ToString("yyyy-MM-dd_HH.mm") + ".json", JsonConvert.SerializeObject(markets), fileDateTime, fileDateTime);

            log.DoLogInfo("BinanceFutures - Market data saved for " + markets.Count.ToString() + " markets with " + mainMarket + ".");

            FileHelper.CleanupFiles(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + Constants.PTMagicPathExchange + Path.DirectorySeparatorChar, systemConfiguration.AnalyzerSettings.MarketAnalyzer.StoreDataMaxHours);
            log.DoLogInfo("BinanceFutures - Market data cleaned.");
          }
          else
          {
            log.DoLogError("BinanceFutures - Failed to get main market price for " + mainMarket + ".");
            result = null;
          }
        }
      }
      catch (WebException ex)
      {
        if (ex.Response != null)
        {
          using (HttpWebResponse errorResponse = (HttpWebResponse)ex.Response)
          {
            using (StreamReader reader = new StreamReader(errorResponse.GetResponseStream()))
            {
              Dictionary<string, string> errorData = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
              if (errorData != null)
              {
                string errorMessage = "Unable to get data from BinanceFutures with URL '" + errorResponse.ResponseUri + "'!";
                if (errorData.ContainsKey("code"))
                {
                  errorMessage += " - Code: " + errorData["code"];
                }

                if (errorData.ContainsKey("msg"))
                {
                  errorMessage += " - Message: " + errorData["msg"];
                }

                log.DoLogError(errorMessage);
              }
            }
          }
        }
        result = null;
      }
      catch (Exception ex)
      {
        log.DoLogCritical("Exception while getting data for '" + lastMarket + "': " + ex.Message, ex);
        result = null;
      }

      return result;
    }

    public static void CheckFirstSeenDates(Dictionary<string, Market> markets, ref ConcurrentDictionary<string, MarketInfo> marketInfos, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      log.DoLogInfo("BinanceFutures - Checking first seen dates for " + markets.Count + " markets. This may take a while...");

      int marketsChecked = 0;

      foreach (string key in markets.Keys)
      {
        // Save market info
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
          marketInfo.FirstSeen = BinanceFutures.GetFirstSeenDate(key, systemConfiguration, log);
        }
        else
        {
          if (marketInfo.FirstSeen == Constants.confMinDate)
          {
            marketInfo.FirstSeen = BinanceFutures.GetFirstSeenDate(key, systemConfiguration, log);
          }
        }
        marketInfo.LastSeen = DateTime.UtcNow;

        marketsChecked++;

        if ((marketsChecked % 20) == 0)
        {
          log.DoLogInfo("BinanceFutures - Yes, I am still checking first seen dates... " + marketsChecked + "/" + markets.Count + " markets done...");
        }
      }
    }

    public static DateTime GetFirstSeenDate(string marketName, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      DateTime result = Constants.confMinDate;

      string baseUrl = "https://fapi.binance.com/fapi/v1/klines?interval=1d&symbol=" + marketName + "&limit=100";

      log.DoLogDebug("BinanceFutures - Getting first seen date for '" + marketName + "'...");

      Newtonsoft.Json.Linq.JArray jsonArray = GetSimpleJsonArrayFromURL(baseUrl, log);
      if (jsonArray.Count > 0)
      {
        result = Constants.Epoch.AddMilliseconds((Int64)jsonArray[0][0]);
        log.DoLogDebug("BinanceFutures - First seen date for '" + marketName + "' set to " + result.ToString());
      }

      return result;
    }

    public static List<MarketTick> GetMarketTicks(string marketName, int ticksNeeded, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      List<MarketTick> result = new List<MarketTick>();

      try
      {
        Int64 endTime = (Int64)Math.Ceiling(DateTime.UtcNow.Subtract(Constants.Epoch).TotalMilliseconds);
        int ticksLimit = 500;
        string baseUrl = "";
        int ticksFetched = 0;

        if (ticksNeeded < ticksLimit)
        {
          ticksLimit = ticksNeeded;
        }

        bool go = true;
        while (ticksFetched < ticksNeeded && go)
        {
          baseUrl = "https://fapi.binance.com/fapi/v1/klines?interval=1m&symbol=" + marketName + "&endTime=" + endTime.ToString() + "&limit=" + ticksLimit.ToString();

          log.DoLogDebug("BinanceFutures - Getting " + ticksLimit.ToString() + " ticks for '" + marketName + "'...");
          Newtonsoft.Json.Linq.JArray jsonArray = GetSimpleJsonArrayFromURL(baseUrl, log);
          if (jsonArray.Count > 0)
          {
            log.DoLogDebug("BinanceFutures - " + jsonArray.Count.ToString() + " ticks received.");

            foreach (Newtonsoft.Json.Linq.JArray marketTick in jsonArray)
            {

              MarketTick tick = new MarketTick();
              tick.Price = (double)marketTick[4];
              tick.Volume24h = (double)marketTick[7];
              tick.Time = Constants.Epoch.AddMilliseconds((Int64)marketTick[0]);

              result.Add(tick);
            }

            ticksFetched = ticksFetched + jsonArray.Count;
            endTime = endTime - ticksLimit * 60 * 1000;
            if (ticksNeeded - ticksFetched < ticksLimit)
            {
              ticksLimit = ticksNeeded - ticksFetched;
            }
          }
          else
          {
            log.DoLogDebug("BinanceFutures - No ticks received.");
            go = false;
          }
        }
      }
      catch (WebException ex)
      {
        if (ex.Response != null)
        {
          using (HttpWebResponse errorResponse = (HttpWebResponse)ex.Response)
          {
            using (StreamReader reader = new StreamReader(errorResponse.GetResponseStream()))
            {
              Dictionary<string, string> errorData = JsonConvert.DeserializeObject<Dictionary<string, string>>(reader.ReadToEnd());
              if (errorData != null)
              {
                string errorMessage = "Unable to get data from BinanceFutures with URL '" + errorResponse.ResponseUri + "'!";
                if (errorData.ContainsKey("code"))
                {
                  errorMessage += " - Code: " + errorData["code"];
                }

                if (errorData.ContainsKey("msg"))
                {
                  errorMessage += " - Message: " + errorData["msg"];
                }

                log.DoLogError(errorMessage);
              }
            }
          }
        }
        result = null;
      }
      catch (Exception ex)
      {
        log.DoLogCritical(ex.Message, ex);
      }

      return result;
    }

    public static void CheckForMarketDataRecreation(string mainMarket, Dictionary<string, Market> markets, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      string binanceFuturesDataDirectoryPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + Constants.PTMagicPathExchange + Path.DirectorySeparatorChar;

      if (!Directory.Exists(binanceFuturesDataDirectoryPath))
      {
        Directory.CreateDirectory(binanceFuturesDataDirectoryPath);
      }

      DirectoryInfo dataDirectory = new DirectoryInfo(binanceFuturesDataDirectoryPath);

      // Check for existing market files
      DateTime latestMarketDataFileDateTime = Constants.confMinDate;
      List<FileInfo> marketFiles = dataDirectory.EnumerateFiles("MarketData*").ToList();
      FileInfo latestMarketDataFile = null;
      if (marketFiles.Count > 0)
      {
        latestMarketDataFile = marketFiles.OrderByDescending(mdf => mdf.LastWriteTimeUtc).First();
        latestMarketDataFileDateTime = latestMarketDataFile.LastWriteTimeUtc;
      }

      if (latestMarketDataFileDateTime < DateTime.UtcNow.AddMinutes(-20))
      {
        int lastMarketDataAgeInSeconds = (int)Math.Ceiling(DateTime.UtcNow.Subtract(latestMarketDataFileDateTime).TotalSeconds);

        // Go back in time and create market data
        DateTime startDateTime = DateTime.UtcNow;
        DateTime endDateTime = DateTime.UtcNow.AddHours(-systemConfiguration.AnalyzerSettings.MarketAnalyzer.StoreDataMaxHours);
        if (latestMarketDataFileDateTime != Constants.confMinDate && latestMarketDataFileDateTime > endDateTime)
        {
          // Existing market files too old => Recreate market data for configured timeframe
          log.DoLogInfo("BinanceFutures - Recreating market data for " + markets.Count + " markets over " + SystemHelper.GetProperDurationTime(lastMarketDataAgeInSeconds) + ". This may take a while...");
          endDateTime = latestMarketDataFileDateTime;
        }
        else
        {
          // No existing market files found => Recreate market data for configured timeframe
          log.DoLogInfo("BinanceFutures - Recreating market data for " + markets.Count + " markets over " + systemConfiguration.AnalyzerSettings.MarketAnalyzer.StoreDataMaxHours + " hours. This may take a while...");
        }

        int totalTicks = (int)Math.Ceiling(startDateTime.Subtract(endDateTime).TotalMinutes);

        // Get Ticks for main market
        List<MarketTick> mainMarketTicks = new List<MarketTick>();
        if (!mainMarket.Equals("USDT", StringComparison.InvariantCultureIgnoreCase))
        {
          mainMarketTicks = BinanceFutures.GetMarketTicks(mainMarket + "USDT", totalTicks, systemConfiguration, log);
        }

        // Get Ticks for all markets
        log.DoLogDebug("BinanceFutures - Getting ticks for '" + markets.Count + "' markets");
        ConcurrentDictionary<string, List<MarketTick>> marketTicks = new ConcurrentDictionary<string, List<MarketTick>>();

        int ParallelThrottle = 2;
        if (systemConfiguration.AnalyzerSettings.MarketAnalyzer.StoreDataMaxHours > 6)
        {
          ParallelThrottle = 1;
          log.DoLogInfo("----------------------------------------------------------------------------");
          log.DoLogInfo("StoreDataMaxHours is greater than 6.  Historical data requests will be");
          log.DoLogInfo("throttled to avoid exceeding exchange data request limits.  This initial ");
          log.DoLogInfo("run could take more than 30 minutes.  Please go outside for a walk...");
          log.DoLogInfo("----------------------------------------------------------------------------");
        }

        Parallel.ForEach(markets.Keys,
                          new ParallelOptions { MaxDegreeOfParallelism = ParallelThrottle},
                          (key) =>
        {
          if (!marketTicks.TryAdd(key, GetMarketTicks(key, totalTicks, systemConfiguration, log)))
          {
            // Failed to add ticks to dictionary
            throw new Exception("Failed to add ticks for " + key + " to the memory dictionary, results may be incorrectly calculated!");
          }

          if ((marketTicks.Count % 10) == 0)
          {
            log.DoLogInfo("BinanceFutures - No worries, I am still alive... " + marketTicks.Count + "/" + markets.Count + " markets done...");
          }
        });

        log.DoLogInfo("BinanceFutures - Ticks completed.");

        log.DoLogInfo("BinanceFutures - Creating initial market data ticks. This may take another while...");

        // Go back in time and create market data
        int completedTicks = 0;
        if (marketTicks.Count > 0)
        {
          for (DateTime tickTime = startDateTime; tickTime >= endDateTime; tickTime = tickTime.AddMinutes(-1))
          {
            completedTicks++;

            double mainCurrencyPrice = 1;
            if (mainMarketTicks.Count > 0)
            {
              List<MarketTick> mainCurrencyTickRange = mainMarketTicks.FindAll(t => t.Time <= tickTime);
              if (mainCurrencyTickRange.Count > 0)
              {
                MarketTick mainCurrencyTick = mainCurrencyTickRange.OrderByDescending(t => t.Time).First();
                mainCurrencyPrice = mainCurrencyTick.Price;
              }
            }

            Dictionary<string, Market> tickMarkets = new Dictionary<string, Market>();
            foreach (string key in markets.Keys)
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
                //market.Volume24h = marketTick.Volume24h;
                market.MainCurrencyPriceUSD = mainCurrencyPrice;

                tickMarkets.Add(market.Name, market);
              }
            }

            DateTime fileDateTime = new DateTime(tickTime.ToLocalTime().Year, tickTime.ToLocalTime().Month, tickTime.ToLocalTime().Day, tickTime.ToLocalTime().Hour, tickTime.ToLocalTime().Minute, 0).ToUniversalTime();

            FileHelper.WriteTextToFile(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + Constants.PTMagicPathExchange + Path.DirectorySeparatorChar, "MarketData_" + fileDateTime.ToString("yyyy-MM-dd_HH.mm") + ".json", JsonConvert.SerializeObject(tickMarkets), fileDateTime, fileDateTime);

            log.DoLogDebug("BinanceFutures - Market data saved for tick " + fileDateTime.ToString() + " - MainCurrencyPrice=" + mainCurrencyPrice.ToString("#,#0.00") + " USD.");

            if ((completedTicks % 100) == 0)
            {
              log.DoLogInfo("BinanceFutures - Our magicbots are still at work, hang on... " + completedTicks + "/" + totalTicks + " ticks done...");
            }
          }
        }

        log.DoLogInfo("BinanceFutures - Initial market data created. Ready to go!");
      }

    }
  }
}