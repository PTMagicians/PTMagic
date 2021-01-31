using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Core.Main;
using Core.Helper;
using Core.Main.DataObjects.PTMagicData;

namespace Core.MarketAnalyzer
{
  public class BaseAnalyzer
  {
    public static string GetJsonStringFromURL(string url, LogHelper log, (string header, string value)[] headers = null)
    {
      HttpClient webClient = null;
      if (webClient == null)
      {
        webClient = new HttpClient();

        // Setup the one time conneciton characteristics
        webClient.Timeout = new TimeSpan(0, 0, 30); // 30 second call timeout
        webClient.DefaultRequestHeaders.ConnectionClose = false; // Keep alives        
      }
      else
      {
        webClient.DefaultRequestHeaders.Clear();
      }

      // Accept JSON and Text
      webClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
      webClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

      // Setup the keep alive timeout
      ServicePointManager.FindServicePoint(new Uri(url)).ConnectionLeaseTimeout = 300000; // 5 mins for keep alives     

      // Add any custom headers     
      if (headers != null)
      {
        foreach (var header in headers)
        {
          webClient.DefaultRequestHeaders.Add(header.header, header.value);
        }
      }

      try
      {
        // log.DoLogInfo("Calling URL: " + url);
        var response = webClient.GetAsync(url).Result;
        string repsonseString = response.Content.ReadAsStringAsync().Result;
        if (response.IsSuccessStatusCode)
        {
          return repsonseString;
        }
        else
        {
          // Error
          var message = string.Format("Error whilst calling {0} - {1}", url, repsonseString);
          log.DoLogError(message);
          throw new Exception(message);
        }
      }
      catch (TaskCanceledException tcEx)
      {
        // Conneciton timeout
        log.DoLogError(string.Format("Timeout whilst calling {0} - {1}", url, tcEx.Message));
        throw;
      }
      catch (Exception ex)
      {
        log.DoLogError(string.Format("Error whilst calling {0} \nError: {1}", url, ex.Message));
        throw;
      }
    }

    public static Dictionary<string, dynamic> GetJsonFromURL(string url, LogHelper log, (string header, string value)[] headers = null)
    {
      Dictionary<string, dynamic> jsonObject = null;
      string jsonString = GetJsonStringFromURL(url, log, headers);

      // Convert the response to JSON
      jsonObject = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(jsonString);
      return jsonObject;
    }

    public static Newtonsoft.Json.Linq.JObject GetSimpleJsonObjectFromURL(string url, LogHelper log, (string header, string value)[] headers = null)
    {
      Newtonsoft.Json.Linq.JObject jsonObject = null;
      string jsonString = GetJsonStringFromURL(url, log, headers);
      jsonObject = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(jsonString);
      return jsonObject;
    }

    public static List<dynamic> GetSimpleJsonListFromURL(string url, LogHelper log)
    {
      List<dynamic> jsonObject = null;
      string jsonString = GetJsonStringFromURL(url, log, null);
      jsonObject = JsonConvert.DeserializeObject<List<dynamic>>(jsonString);
      return jsonObject;
    }

    public static Newtonsoft.Json.Linq.JArray GetSimpleJsonArrayFromURL(string url, LogHelper log)
    {
      Newtonsoft.Json.Linq.JArray jsonObject = null;
      string jsonString = GetJsonStringFromURL(url, log, null);
      jsonObject = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JArray>(jsonString);
      return jsonObject;
    }

    public static string GetLatestGitHubRelease(LogHelper log, string defaultVersion)
    {
      string result = defaultVersion;

      try
      {
        string baseUrl = "https://api.github.com/repos/PTMagicians/PTMagic/releases/latest";

        Newtonsoft.Json.Linq.JObject jsonObject = GetSimpleJsonObjectFromURL(baseUrl, log, new (string header, string value)[] { ("User-Agent", "PTMagic.Import") });
        if (jsonObject != null)
        {
          result = jsonObject.GetValue("tag_name").ToString();
        }
      }
      catch (WebException ex)
      {
        log.DoLogDebug("GitHub version check error: " + ex.Message);
      }
      catch (Exception ex)
      {
        log.DoLogDebug("GitHub version check error: " + ex.Message);
      }
      return result;
    }

    public static double GetMainFiatCurrencyRate(string currency, string FreeCurrencyAPI, LogHelper log)
    {
      double result = 1;
      string baseUrl = "http://free.currencyconverterapi.com/api/v5/convert?q=USD_" + currency + "&compact=y&apiKey=" + FreeCurrencyAPI;

      log.DoLogDebug("http://free.currencyconverterapi.com - Getting latest exchange rates...");
      Newtonsoft.Json.Linq.JObject jsonObject = GetSimpleJsonObjectFromURL(baseUrl, log, null);
      if (jsonObject != null)
      {
        log.DoLogDebug("http://free.currencyconverterapi.com - Received latest exchange rates.");

        result = (double)jsonObject["USD_" + currency]["val"];
        log.DoLogInfo("http://free.currencyconverterapi.com - Latest exchange rate for USD to " + currency + " is " + result);
      }
      return result;
    }

    public static Dictionary<string, MarketInfo> GetMarketInfosFromFile(PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      Dictionary<string, MarketInfo> result = new Dictionary<string, MarketInfo>();

      string marketInfoFilePath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + Constants.PTMagicPathExchange + Path.DirectorySeparatorChar + "MarketInfo.json";
      if (File.Exists(marketInfoFilePath))
      {
        try
        {
          result = JsonConvert.DeserializeObject<Dictionary<string, MarketInfo>>(System.IO.File.ReadAllText(marketInfoFilePath));
        }
        catch (Exception ex)
        {
          log.DoLogDebug(ex.Message);
        }
      }
      if (result == null)
      {
        result = new Dictionary<string, MarketInfo>();
      }
      return result;
    }

    public static void SaveMarketInfosToFile(Dictionary<string, MarketInfo> marketInfos, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      FileHelper.WriteTextToFile(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + Constants.PTMagicPathExchange + Path.DirectorySeparatorChar, "MarketInfo.json", JsonConvert.SerializeObject(marketInfos));
    }

    public static Dictionary<string, Market> GetMarketDataFromFile(PTMagicConfiguration systemConfiguration, LogHelper log, string platform, DateTime maxDateTime, string marketCaption)
    {
      Dictionary<string, Market> result = new Dictionary<string, Market>();
      DirectoryInfo dataDirectory = new DirectoryInfo(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + platform + Path.DirectorySeparatorChar);

      // Get market files older than max datetime in descending order (newest file up top)
      List<FileInfo> marketFiles = dataDirectory.EnumerateFiles("MarketData*")
                         .Select(x => { x.Refresh(); return x; })
                         .Where(x => x.LastWriteTimeUtc <= maxDateTime)
                         .ToArray().OrderByDescending(f => f.LastWriteTimeUtc).ToList();

      FileInfo marketFile = null;
      if (marketFiles.Count > 0)
      {
        marketFile = marketFiles.First();

        log.DoLogDebug(platform + " - " + marketCaption + " market data loaded (" + marketFile.LastWriteTimeUtc.ToString() + ")");
      }
      else
      {
        log.DoLogDebug(platform + " - Not able to load " + marketCaption + " market data. Loading next youngest market file instead.");

        // Get market files younger than max datetime in ascending order (oldest file up top)
        marketFiles = dataDirectory.EnumerateFiles("MarketData*")
                       .Select(x => { x.Refresh(); return x; })
                       .Where(x => x.LastWriteTimeUtc >= maxDateTime)
                       .ToArray().OrderBy(f => f.LastWriteTimeUtc).ToList();
        if (marketFiles.Count > 0)
        {
          marketFile = marketFiles.First();
          log.DoLogDebug(platform + " - " + marketCaption + " market data loaded (" + marketFile.LastWriteTimeUtc.ToString() + ")");
        }
      }
      try
      {
        // Get JSON object
        result = JsonConvert.DeserializeObject<Dictionary<string, Market>>(marketFile.OpenText().ReadToEnd());
      }
      catch (Exception ex)
      {
        log.DoLogCritical(ex.Message, ex);
      }
      return result;
    }

    public static Dictionary<string, List<MarketTrendChange>> BuildMarketTrends(string platform, string mainMarket, List<string> marketList, string sortBy, bool isGlobal, Dictionary<string, List<MarketTrendChange>> output, PTMagicConfiguration systemConfiguration, LogHelper log)
    {

      try
      {
        List<MarketTrend> marketTrends = systemConfiguration.AnalyzerSettings.MarketAnalyzer.MarketTrends.FindAll(mt => mt.Platform.Equals(platform, StringComparison.InvariantCultureIgnoreCase));
        if (marketTrends.Count > 0)
        {
          Dictionary<string, Market> recentMarkets = BaseAnalyzer.GetMarketDataFromFile(systemConfiguration, log, platform, DateTime.UtcNow, "Recent");

          foreach (MarketTrend marketTrend in marketTrends)
          {
            if (platform.Equals("Exchange", StringComparison.InvariantCultureIgnoreCase))
            {
              log.DoLogInfo(platform + " - Building market trend changes for '" + marketTrend.Name + "' on main market '" + mainMarket + "' with " + marketList.Count.ToString() + " markets...");
            }
            else
            {
              log.DoLogInfo(platform + " - Building market trend changes for '" + marketTrend.Name + "'...");
            }

            Dictionary<string, Market> trendMarkets = BaseAnalyzer.GetMarketDataFromFile(systemConfiguration, log, platform, DateTime.UtcNow.AddMinutes(-marketTrend.TrendMinutes), marketTrend.Name);

            List<MarketTrendChange> marketTrendChanges = BaseAnalyzer.GetMarketTrendChanges(platform, mainMarket, marketTrend, marketList, recentMarkets, trendMarkets, sortBy, isGlobal, systemConfiguration, log);

            output.Add(marketTrend.Name, marketTrendChanges);

            log.DoLogInfo(platform + " - " + marketTrendChanges.Count.ToString() + " Market trend changes built for '" + marketTrend.Name + "'.");
          }
        }

      }
      catch (Exception ex)
      {
        log.DoLogCritical(ex.Message, ex);
      }

      return output;
    }

    public static List<MarketTrendChange> GetMarketTrendChanges(
      string platform,
      string mainMarket,
      MarketTrend marketTrend,
      List<string> marketList,
      Dictionary<string, Market> recentMarkets,
      Dictionary<string, Market> trendMarkets,
      string sortBy,
      bool isGlobal,
      PTMagicConfiguration systemConfiguration,
      LogHelper log)
    {
      List<MarketTrendChange> result = new List<MarketTrendChange>();

      var sortedMarkets = new SortedDictionary<string, Market>(recentMarkets).OrderBy(m => m.Value.Position);
      if (sortBy.Equals("Volume"))
      {
        sortedMarkets = new SortedDictionary<string, Market>(recentMarkets).OrderByDescending(m => m.Value.Volume24h);
      }
      int marketCount = 1;
      foreach (KeyValuePair<string, Market> recentMarketPair in sortedMarkets)
      {
        if (marketList.Count == 0 || marketList.Contains(recentMarketPair.Key))
        {
          bool excludeMainCurrency = systemConfiguration.AnalyzerSettings.MarketAnalyzer.ExcludeMainCurrency;
          if (!marketTrend.ExcludeMainCurrency)
          {
            excludeMainCurrency = marketTrend.ExcludeMainCurrency;
          }

          if (platform.Equals("CoinMarketCap", StringComparison.InvariantCulture) && excludeMainCurrency)
          {
            // Check if this is the main currency (only for CoinMarketCap)
            if (recentMarketPair.Value.Symbol.Equals(mainMarket, StringComparison.InvariantCultureIgnoreCase))
            {

              // If the current market is the main currency, skip it
              continue;
            }
          }

          Market recentMarket;
          if (recentMarkets.TryGetValue(recentMarketPair.Key, out recentMarket))
          {
            List<string> ignoredMarkets = SystemHelper.ConvertTokenStringToList(marketTrend.IgnoredMarkets, ",");
            if (ignoredMarkets.Contains(recentMarketPair.Value.Symbol))
            {
              log.DoLogDebug(platform + " - Market trend '" + marketTrend.Name + "' for '" + recentMarketPair.Key + "' is ignored in this trend.");
              continue;
            }
            List<string> allowedMarkets = SystemHelper.ConvertTokenStringToList(marketTrend.AllowedMarkets, ",");
            if (allowedMarkets.Count > 0 && !allowedMarkets.Contains(recentMarketPair.Value.Symbol))
            {
              log.DoLogDebug(platform + " - Market trend '" + marketTrend.Name + "' for '" + recentMarketPair.Key + "' is not allowed in this trend.");
              continue;
            }
          }
          else
          {
            // No recent market data
            log.DoLogDebug(platform + " - Market trend '" + marketTrend.Name + "' for '" + recentMarketPair.Key + "' has no recent market trend data.");
            continue;
          }

          Market trendMarket;
          if (trendMarkets.TryGetValue(recentMarketPair.Key, out trendMarket))
          {
            double recentMarketPrice = recentMarket.Price;
            double trendMarketPrice = trendMarket.Price;
            if (!platform.Equals("CoinMarketCap", StringComparison.InvariantCulture) && marketTrend.TrendCurrency.Equals("Fiat", StringComparison.InvariantCultureIgnoreCase))
            {
              if (recentMarket.MainCurrencyPriceUSD > 0 && trendMarket.MainCurrencyPriceUSD > 0)
              {
                recentMarketPrice = recentMarketPrice * recentMarket.MainCurrencyPriceUSD;
                trendMarketPrice = trendMarketPrice * trendMarket.MainCurrencyPriceUSD;
              }
            }

            double trendMarketChange = (recentMarketPrice - trendMarketPrice) / trendMarketPrice * 100;

            MarketTrendChange mtc = new MarketTrendChange();
            mtc.MarketTrendName = marketTrend.Name;
            mtc.TrendMinutes = marketTrend.TrendMinutes;
            mtc.TrendChange = trendMarketChange;
            mtc.Market = recentMarket.Name;
            mtc.LastPrice = recentMarket.Price;
            mtc.Volume24h = recentMarket.Volume24h;
            mtc.TrendDateTime = DateTime.UtcNow;

            result.Add(mtc);
            log.DoLogDebug(platform + " - Market trend '" + marketTrend.Name + "' for '" + recentMarketPair.Key + "' (Vol. " + recentMarket.Volume24h.ToString("#,#0.00") + ") is " + trendMarketChange.ToString("#,#0.00") + "% in " + SystemHelper.GetProperDurationTime(marketTrend.TrendMinutes * 60).ToLower() + ".");
            marketCount++;
          }
          else
          {
            // No data market trend data
            log.DoLogDebug(platform + " - Market trend '" + marketTrend.Name + "' for '" + recentMarketPair.Key + "' has no market trend data.");
            continue;
          }
        }
      }
      if (marketTrend.MaxMarkets > 0 && isGlobal)
      {
        int maxMarkets = (marketTrend.MaxMarkets <= result.Count) ? marketTrend.MaxMarkets : result.Count;
        result = result.GetRange(0, maxMarkets);
      }
      return result;
    }

    public static Dictionary<string, double> BuildGlobalMarketTrends(Dictionary<string, List<MarketTrendChange>> globalMarketTrendChanges, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      Dictionary<string, double> result = new Dictionary<string, double>();

      List<MarketTrend> marketTrends = systemConfiguration.AnalyzerSettings.MarketAnalyzer.MarketTrends;
      if (marketTrends.Count > 0)
      {
        foreach (MarketTrend marketTrend in marketTrends)
        {
          log.DoLogInfo("Building market trend average for '" + marketTrend.Name + "'");
          if (globalMarketTrendChanges.ContainsKey(marketTrend.Name))
          {
            List<MarketTrendChange> marketTrendChanges = globalMarketTrendChanges[marketTrend.Name];
            if (marketTrendChanges != null && marketTrendChanges.Count > 0)
            {
            double totalTrendChange = 0;
            int trendChangeCount = marketTrendChanges.Count;
            foreach (MarketTrendChange marketTrendChange in marketTrendChanges) 
              {
                if (marketTrend.IgnoreOutlier != 0) 
                {
                  if ((marketTrendChange.TrendChange > marketTrend.IgnoreOutlier) || (marketTrendChange.TrendChange < (marketTrend.IgnoreOutlier * -1)))
                  {
                    log.DoLogInfo("Market trend '" + marketTrend.Name + "' is ignoring " + marketTrendChange.Market + " for exceeding TrendThreshold.");
                    trendChangeCount += -1;
                  }
                  else
                  {
                    totalTrendChange += marketTrendChange.TrendChange;
                  }
                }
                else
                {
                    totalTrendChange += marketTrendChange.TrendChange;
                }
              }
              double averageTrendChange = totalTrendChange / trendChangeCount;
              result.Add(marketTrend.Name, averageTrendChange);
              log.DoLogInfo("Built average market trend change '" + marketTrend.Name + "' (" + averageTrendChange.ToString("#,#0.00") + "% in " + marketTrend.TrendMinutes.ToString() + " minutes) for " + marketTrendChanges.Count.ToString() + " markets.");
            }
            else
            {
              result.Add(marketTrend.Name, 0);
              log.DoLogWarn("No market trend changes found for  '" + marketTrend.Name + "' - returning 0%");
            }
          }
          else
          {
            result.Add(marketTrend.Name, 0);
            log.DoLogWarn("Market trend '" + marketTrend.Name + "' not found in globalMarketTrendChanges[] - returning 0%");
          }
        }
      }
      return result;
    }
  }
}
