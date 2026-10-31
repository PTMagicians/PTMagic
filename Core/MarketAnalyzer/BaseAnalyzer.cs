using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Core.Main;
using Core.Helper;
using Core.Main.DataObjects.PTMagicData;

namespace Core.MarketAnalyzer
{
  public class BaseAnalyzer
  {
    private const int MaxHttpRetries = 3;
    private static readonly HttpClient HttpClient = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
      SocketsHttpHandler handler = new SocketsHttpHandler
      {
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
      };

      HttpClient client = new HttpClient(handler)
      {
        Timeout = TimeSpan.FromSeconds(30)
      };

      client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
      client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

      return client;
    }

    private static HttpRequestMessage CreateRequest(string url, (string header, string value)[] headers = null)
    {
      HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);

      if (headers != null)
      {
        foreach (var header in headers)
        {
          if (!request.Headers.TryAddWithoutValidation(header.header, header.value))
          {
            request.Dispose();
            throw new InvalidOperationException($"Unable to add HTTP header '{header.header}' whilst calling {url}.");
          }
        }
      }

      return request;
    }

    private static bool IsRetryableStatusCode(HttpStatusCode statusCode)
    {
      int status = (int)statusCode;
      return status == 408 || status == 429 || status == 500 || status == 502 || status == 503 || status == 504;
    }

    private static bool IsBinanceIpBan(HttpStatusCode statusCode)
    {
      return (int)statusCode == 418;
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int retryNumber)
    {
      if (response?.Headers?.RetryAfter != null)
      {
        if (response.Headers.RetryAfter.Delta.HasValue)
        {
          return response.Headers.RetryAfter.Delta.Value > TimeSpan.Zero ? response.Headers.RetryAfter.Delta.Value : TimeSpan.Zero;
        }

        if (response.Headers.RetryAfter.Date.HasValue)
        {
          TimeSpan delay = response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
          return delay > TimeSpan.Zero ? delay : TimeSpan.Zero;
        }
      }

      return TimeSpan.FromSeconds(Math.Min(8, Math.Pow(2, retryNumber - 1)));
    }

    private static string GetResponseContext(HttpResponseMessage response)
    {
      List<string> context = new List<string>();

      if (response?.Headers?.RetryAfter != null)
      {
        if (response.Headers.RetryAfter.Delta.HasValue)
        {
          context.Add("Retry-After=" + response.Headers.RetryAfter.Delta.Value.TotalSeconds.ToString("0.###") + "s");
        }
        else if (response.Headers.RetryAfter.Date.HasValue)
        {
          context.Add("Retry-After=" + response.Headers.RetryAfter.Date.Value.ToString("O"));
        }
      }

      if (response != null)
      {
        foreach (var header in response.Headers.Where(h => h.Key.StartsWith("X-MBX-", StringComparison.OrdinalIgnoreCase) || h.Key.StartsWith("X-SAPI-", StringComparison.OrdinalIgnoreCase)))
        {
          context.Add(header.Key + "=" + String.Join(",", header.Value));
        }
      }

      return context.Count > 0 ? String.Join("; ", context) : "";
    }

    private static void ValidateResponse(HttpResponseMessage response, string url, string responseString)
    {
      if (response.IsSuccessStatusCode)
      {
        return;
      }

      string context = GetResponseContext(response);
      string message = $"Error whilst calling {url} - HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
      if (!String.IsNullOrEmpty(context))
      {
        message += " - " + context;
      }
      if (!String.IsNullOrEmpty(responseString))
      {
        message += " - " + responseString;
      }

      throw new HttpRequestException(message, null, response.StatusCode);
    }

    private static void LogRetry(LogHelper log, string url, int retryNumber, TimeSpan delay, HttpStatusCode? statusCode = null, string reason = null)
    {
      string status = statusCode.HasValue ? $"HTTP {(int)statusCode.Value} {statusCode.Value}" : reason;
      log.DoLogWarn($"HTTP GET retry {retryNumber}/{MaxHttpRetries} for {url} after {delay.TotalSeconds:0.###} seconds" + (!String.IsNullOrEmpty(status) ? $" ({status})." : "."));
    }

    public static string GetJsonStringFromURL(string url, LogHelper log, (string header, string value)[] headers = null)
    {
      int attempt = 0;
      while (true)
      {
        try
        {
          using HttpRequestMessage request = CreateRequest(url, headers);
          BinanceRateLimiter.WaitIfNeeded(request.RequestUri, log);
          using HttpResponseMessage response = HttpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
          BinanceRateLimiter.ObserveResponse(request.RequestUri, response);
          using Stream responseStream = response.Content.ReadAsStream();
          using StreamReader reader = new StreamReader(responseStream);

          string responseString = reader.ReadToEnd();
          if (response.IsSuccessStatusCode)
          {
            return responseString;
          }

          if (IsBinanceIpBan(response.StatusCode))
          {
            ValidateResponse(response, url, responseString);
          }

          if (IsRetryableStatusCode(response.StatusCode) && attempt < MaxHttpRetries)
          {
            attempt++;
            TimeSpan delay = GetRetryDelay(response, attempt);
            LogRetry(log, url, attempt, delay, response.StatusCode);
            Thread.Sleep(delay);
            continue;
          }

          ValidateResponse(response, url, responseString);
        }
        catch (OperationCanceledException ex)
        {
          if (attempt < MaxHttpRetries)
          {
            attempt++;
            TimeSpan delay = GetRetryDelay(null, attempt);
            LogRetry(log, url, attempt, delay, null, "timeout");
            Thread.Sleep(delay);
            continue;
          }

          log.DoLogError($"Timeout whilst calling {url} - {ex.Message}");
          throw;
        }
        catch (HttpRequestException ex)
        {
          if (!ex.StatusCode.HasValue && attempt < MaxHttpRetries)
          {
            attempt++;
            TimeSpan delay = GetRetryDelay(null, attempt);
            LogRetry(log, url, attempt, delay, null, "network error");
            Thread.Sleep(delay);
            continue;
          }

          log.DoLogError($"HTTP error whilst calling {url}\nError: {ex.Message}");
          throw;
        }
        catch (Exception ex)
        {
          log.DoLogError($"Error whilst calling {url}\nError: {ex.Message}");
          throw;
        }
      }
    }

    public static async Task<string> GetJsonStringFromURLAsync(string url, LogHelper log, (string header, string value)[] headers = null, CancellationToken cancellationToken = default)
    {
      int attempt = 0;
      while (true)
      {
        try
        {
          using HttpRequestMessage request = CreateRequest(url, headers);
          await BinanceRateLimiter.WaitIfNeededAsync(request.RequestUri, log, cancellationToken).ConfigureAwait(false);
          using HttpResponseMessage response = await HttpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
          BinanceRateLimiter.ObserveResponse(request.RequestUri, response);
          string responseString = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

          if (response.IsSuccessStatusCode)
          {
            return responseString;
          }

          if (IsBinanceIpBan(response.StatusCode))
          {
            ValidateResponse(response, url, responseString);
          }

          if (IsRetryableStatusCode(response.StatusCode) && attempt < MaxHttpRetries)
          {
            attempt++;
            TimeSpan delay = GetRetryDelay(response, attempt);
            LogRetry(log, url, attempt, delay, response.StatusCode);
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            continue;
          }

          ValidateResponse(response, url, responseString);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
          log.DoLogError($"Request cancelled whilst calling {url} - {ex.Message}");
          throw;
        }
        catch (OperationCanceledException ex)
        {
          if (attempt < MaxHttpRetries)
          {
            attempt++;
            TimeSpan delay = GetRetryDelay(null, attempt);
            LogRetry(log, url, attempt, delay, null, "timeout");
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            continue;
          }

          log.DoLogError($"Timeout whilst calling {url} - {ex.Message}");
          throw;
        }
        catch (HttpRequestException ex)
        {
          if (!ex.StatusCode.HasValue && attempt < MaxHttpRetries)
          {
            attempt++;
            TimeSpan delay = GetRetryDelay(null, attempt);
            LogRetry(log, url, attempt, delay, null, "network error");
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            continue;
          }

          log.DoLogError($"HTTP error whilst calling {url}\nError: {ex.Message}");
          throw;
        }
        catch (Exception ex)
        {
          log.DoLogError($"Error whilst calling {url}\nError: {ex.Message}");
          throw;
        }
      }
    }

    public static Dictionary<string, dynamic> GetJsonFromURL(string url, LogHelper log, (string header, string value)[] headers = null)
    {
      string jsonString = GetJsonStringFromURL(url, log, headers);
      return JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(jsonString);
    }

    public static async Task<Dictionary<string, dynamic>> GetJsonFromURLAsync(string url, LogHelper log, (string header, string value)[] headers = null, CancellationToken cancellationToken = default)
    {
      string jsonString = await GetJsonStringFromURLAsync(url, log, headers, cancellationToken).ConfigureAwait(false);
      return JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(jsonString);
    }

    public static Newtonsoft.Json.Linq.JObject GetSimpleJsonObjectFromURL(string url, LogHelper log, (string header, string value)[] headers = null)
    {
      string jsonString = GetJsonStringFromURL(url, log, headers);
      return JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(jsonString);
    }

    public static async Task<Newtonsoft.Json.Linq.JObject> GetSimpleJsonObjectFromURLAsync(string url, LogHelper log, (string header, string value)[] headers = null, CancellationToken cancellationToken = default)
    {
      string jsonString = await GetJsonStringFromURLAsync(url, log, headers, cancellationToken).ConfigureAwait(false);
      return JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(jsonString);
    }

    public static List<dynamic> GetSimpleJsonListFromURL(string url, LogHelper log)
    {
      string jsonString = GetJsonStringFromURL(url, log, null);
      return JsonConvert.DeserializeObject<List<dynamic>>(jsonString);
    }

    public static async Task<List<dynamic>> GetSimpleJsonListFromURLAsync(string url, LogHelper log, CancellationToken cancellationToken = default)
    {
      string jsonString = await GetJsonStringFromURLAsync(url, log, null, cancellationToken).ConfigureAwait(false);
      return JsonConvert.DeserializeObject<List<dynamic>>(jsonString);
    }

    public static Newtonsoft.Json.Linq.JArray GetSimpleJsonArrayFromURL(string url, LogHelper log)
    {
      string jsonString = GetJsonStringFromURL(url, log, null);
      return JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JArray>(jsonString);
    }

    public static async Task<Newtonsoft.Json.Linq.JArray> GetSimpleJsonArrayFromURLAsync(string url, LogHelper log, CancellationToken cancellationToken = default)
    {
      string jsonString = await GetJsonStringFromURLAsync(url, log, null, cancellationToken).ConfigureAwait(false);
      return JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JArray>(jsonString);
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
      catch (Exception ex)
      {
        log.DoLogDebug("GitHub version check error: " + ex.Message);
      }
      return result;
    }

    public static async Task<string> GetLatestGitHubReleaseAsync(LogHelper log, string defaultVersion, CancellationToken cancellationToken = default)
    {
      string result = defaultVersion;

      try
      {
        string baseUrl = "https://api.github.com/repos/PTMagicians/PTMagic/releases/latest";

        Newtonsoft.Json.Linq.JObject jsonObject = await GetSimpleJsonObjectFromURLAsync(baseUrl, log, new (string header, string value)[] { ("User-Agent", "PTMagic.Import") }, cancellationToken).ConfigureAwait(false);
        if (jsonObject != null)
        {
          result = jsonObject.GetValue("tag_name").ToString();
        }
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
      string baseUrl = "https://free.currencyconverterapi.com/api/v5/convert?q=USD_" + currency + "&compact=y&apiKey=" + FreeCurrencyAPI;

      log.DoLogDebug("https://free.currencyconverterapi.com - Getting latest exchange rates...");
      Newtonsoft.Json.Linq.JObject jsonObject = GetSimpleJsonObjectFromURL(baseUrl, log, null);
      if (jsonObject != null)
      {
        log.DoLogDebug("https://free.currencyconverterapi.com - Received latest exchange rates.");

        result = (double)jsonObject["USD_" + currency]["val"];
        log.DoLogInfo("https://free.currencyconverterapi.com - Latest exchange rate for USD to " + currency + " is " + result);
      }
      return result;
    }

    public static ConcurrentDictionary<string, MarketInfo> GetMarketInfosFromFile(PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      ConcurrentDictionary<string, MarketInfo> result = new ConcurrentDictionary<string, MarketInfo>();

      string marketInfoFilePath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + Constants.PTMagicPathExchange + Path.DirectorySeparatorChar + "MarketInfo.json";
      if (File.Exists(marketInfoFilePath))
      {
        try
        {
          result = JsonConvert.DeserializeObject<ConcurrentDictionary<string, MarketInfo>>(System.IO.File.ReadAllText(marketInfoFilePath));
        }
        catch (Exception ex)
        {
          log.DoLogDebug(ex.Message);
        }
      }
      if (result == null)
      {
        result = new ConcurrentDictionary<string, MarketInfo>();
      }
      return result;
    }

    public static void SaveMarketInfosToFile(ConcurrentDictionary<string, MarketInfo> marketInfos, PTMagicConfiguration systemConfiguration, LogHelper log)
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
      bool useBinanceFuturesAnalyzerUniverse = platform.Equals("Exchange", StringComparison.InvariantCultureIgnoreCase)
        && systemConfiguration.GeneralSettings.Application.Exchange.Equals("BinanceFutures", StringComparison.InvariantCultureIgnoreCase);

      var sortedMarkets = new SortedDictionary<string, Market>(recentMarkets).OrderBy(m => m.Value.Position);
      if (sortBy.Equals("Volume"))
      {
        sortedMarkets = new SortedDictionary<string, Market>(recentMarkets).OrderByDescending(m => m.Value.Volume24h);
      }
      if (useBinanceFuturesAnalyzerUniverse)
      {
        sortedMarkets = sortedMarkets
          .OrderByDescending(m => BinanceFuturesMarketUniverse.IsMarketIncluded(m.Key, systemConfiguration))
          .ThenByDescending(m => m.Value.Volume24h);
      }

      int marketCount = 1;
      foreach (KeyValuePair<string, Market> recentMarketPair in sortedMarkets)
      {
        bool isTradingMarket = marketList.Count == 0 || marketList.Contains(recentMarketPair.Key);
        bool isAnalyzerMarket = useBinanceFuturesAnalyzerUniverse
          && !systemConfiguration.AnalyzerSettings.MarketAnalyzer.UseProfitTrailerEnabledPairs
          && BinanceFuturesMarketUniverse.IsMarketIncluded(recentMarketPair.Key, systemConfiguration);
        if (isTradingMarket || isAnalyzerMarket)
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
          string market = recentMarketPair.Value.Symbol.Replace(mainMarket, "");
          string exchange = systemConfiguration.GeneralSettings.Application.Exchange.ToLower();
          switch (exchange)
            {
              case "bittrex":
              market = market.Replace("-", "");
              break;
              case "poloniex":
              market = market.Replace("_", "");
              break;
            }
          if (recentMarkets.TryGetValue(recentMarketPair.Key, out recentMarket))
          {
            // Strip main markets from lists, if exists
            string ignored = marketTrend.IgnoredMarkets.ToUpper();
            ignored = ignored.Replace(mainMarket, "");
            switch (exchange)
            {
              case "bittrex":
              ignored = ignored.Replace("-", "");
              break;
              case "poloniex":
              ignored = ignored.Replace("_", "");
              break;
            }
            List<string> ignoredMarkets = SystemHelper.ConvertTokenStringToList(ignored, ",");
            if (ignoredMarkets.Contains(market))
            {
              log.DoLogDebug(platform + " - Market trend '" + marketTrend.Name + "' for '" + recentMarketPair.Key + "' is ignored in this trend.");
              continue;
            }
            // Strip main markets from lists, if exists
            string allowed = marketTrend.AllowedMarkets.ToUpper();
            allowed = allowed.Replace(mainMarket, "");
            switch (exchange)
            {
              case "bittrex":
              allowed = allowed.Replace("-", "");
              break;
              case "poloniex":
              allowed = allowed.Replace("_", "");
              break;
            }
            List<string> allowedMarkets = SystemHelper.ConvertTokenStringToList(allowed, ",");
            if (allowedMarkets.Count > 0 && !allowedMarkets.Contains(market))
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
            if (marketTrendChanges != null
              && marketTrend.Platform.Equals("Exchange", StringComparison.InvariantCultureIgnoreCase)
              && systemConfiguration.GeneralSettings.Application.Exchange.Equals("BinanceFutures", StringComparison.InvariantCultureIgnoreCase))
            {
              marketTrendChanges = marketTrendChanges
                .Where(mtc => BinanceFuturesMarketUniverse.IsMarketIncluded(mtc.Market, systemConfiguration))
                .ToList();
            }

            if (marketTrendChanges != null && marketTrendChanges.Count > 0)
            {
            double totalTrendChange = 0;
            int trendsCount = marketTrendChanges.Count;
            foreach (MarketTrendChange marketTrendChange in marketTrendChanges) 
              {
                if (marketTrend.TrendThreshold != 0) 
                {
                  if ((marketTrendChange.TrendChange > marketTrend.TrendThreshold) || (marketTrendChange.TrendChange < (marketTrend.TrendThreshold * -1)))
                  {
                    log.DoLogInfo("Market trend '" + marketTrend.Name + "' is ignoring " + marketTrendChange.Market + " for exceeding TrendThreshold.");
                    trendsCount += -1;
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
              double averageTrendChange = totalTrendChange / trendsCount;
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