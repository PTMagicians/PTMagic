using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Core.Helper;
using Core.Main;
using Core.Main.DataObjects.PTMagicData;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Core.MarketAnalyzer
{
  public static class BinanceFuturesMarketUniverse
  {
    private const string TradFiContractType = "TRADIFI_PERPETUAL";
    private const int MetadataRefreshMinutes = 15;
    private const string MetadataFileName = "BinanceFuturesMarketMetadata.json";

    private static readonly SemaphoreSlim RefreshLock = new SemaphoreSlim(1, 1);
    private static readonly object MetadataLock = new object();
    private static Dictionary<string, BinanceFuturesMarketMetadata> _metadata = new Dictionary<string, BinanceFuturesMarketMetadata>(StringComparer.InvariantCultureIgnoreCase);
    private static DateTime _lastRefreshUtc = Constants.confMinDate;
    private static bool _metadataFileLoaded = false;

    public static readonly string[] KnownTradFiUnderlyingTypes = new string[]
    {
      "EQUITY",
      "KR_EQUITY",
      "HK_EQUITY",
      "CN_EQUITY",
      "COMMODITY",
      "INDEX",
      "PREMARKET",
      "FX"
    };

    public static async Task RefreshAsync(string apiBase, LogHelper log, CancellationToken cancellationToken = default)
    {
      if (HasFreshMetadata())
      {
        return;
      }

      await RefreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
      try
      {
        if (HasFreshMetadata())
        {
          return;
        }

        JObject exchangeInfo = await BaseAnalyzer.GetSimpleJsonObjectFromURLAsync(apiBase + "/exchangeInfo", log, null, cancellationToken).ConfigureAwait(false);
        JArray symbols = exchangeInfo?["symbols"] as JArray;
        if (symbols == null || symbols.Count == 0)
        {
          LoadMetadataFromFile();
          log.DoLogWarn("BinanceFutures - exchangeInfo did not contain symbol metadata. Using cached market classification data when available.");
          return;
        }

        Dictionary<string, BinanceFuturesMarketMetadata> metadata = new Dictionary<string, BinanceFuturesMarketMetadata>(StringComparer.InvariantCultureIgnoreCase);
        foreach (JObject symbol in symbols.OfType<JObject>())
        {
          string symbolName = symbol.Value<string>("symbol") ?? "";
          if (String.IsNullOrWhiteSpace(symbolName))
          {
            continue;
          }

          BinanceFuturesMarketMetadata marketMetadata = new BinanceFuturesMarketMetadata
          {
            Symbol = symbolName,
            ContractType = symbol.Value<string>("contractType") ?? "",
            UnderlyingType = symbol.Value<string>("underlyingType") ?? "",
            UnderlyingSubTypes = symbol["underlyingSubType"] is JArray underlyingSubTypes
              ? underlyingSubTypes.Values<string>().Where(value => !String.IsNullOrWhiteSpace(value)).ToList()
              : new List<string>()
          };

          metadata[symbolName] = marketMetadata;
        }

        lock (MetadataLock)
        {
          _metadata = metadata;
          _lastRefreshUtc = DateTime.UtcNow;
          _metadataFileLoaded = true;
        }

        SaveMetadataToFile(metadata, log);

        int tradFiCount = metadata.Values.Count(m => IsTradFi(m));
        log.DoLogInfo("BinanceFutures - Loaded market classification metadata for " + metadata.Count + " symbols (" + tradFiCount + " TradFi perpetuals).");
      }
      catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
      {
        throw;
      }
      catch (Exception ex)
      {
        LoadMetadataFromFile();
        log.DoLogWarn("BinanceFutures - Unable to refresh market classification metadata. Using cached data when available. " + ex.Message);
      }
      finally
      {
        RefreshLock.Release();
      }
    }

    public static bool IsMarketIncluded(string symbol, PTMagicConfiguration systemConfiguration)
    {
      if (String.IsNullOrWhiteSpace(symbol))
      {
        return false;
      }

      EnsureMetadataLoaded();

      BinanceFuturesMarketMetadata metadata;
      lock (MetadataLock)
      {
        if (!_metadata.TryGetValue(symbol, out metadata))
        {
          // Preserve existing PTMagic behavior when Binance introduces a symbol that
          // is not yet present in the cached exchangeInfo metadata.
          return true;
        }
      }

      if (!IsTradFi(metadata))
      {
        return true;
      }

      MarketAnalyzer settings = systemConfiguration?.AnalyzerSettings?.MarketAnalyzer;
      if (settings == null || !settings.IncludeTradFi)
      {
        return false;
      }

      List<string> includedTypes = settings.IncludedTradFiUnderlyingTypes;
      if (includedTypes == null)
      {
        includedTypes = KnownTradFiUnderlyingTypes.ToList();
      }

      if (includedTypes.Count == 0)
      {
        return false;
      }

      return includedTypes.Any(type => type.Equals(metadata.UnderlyingType, StringComparison.InvariantCultureIgnoreCase));
    }

    public static List<string> FilterMarketNames(IEnumerable<string> markets, PTMagicConfiguration systemConfiguration)
    {
      if (markets == null)
      {
        return new List<string>();
      }

      return markets.Where(market => IsMarketIncluded(market, systemConfiguration)).ToList();
    }

    public static Dictionary<string, Market> FilterMarkets(Dictionary<string, Market> markets, PTMagicConfiguration systemConfiguration)
    {
      if (markets == null || markets.Count == 0)
      {
        return markets ?? new Dictionary<string, Market>();
      }

      return markets
        .Where(market => IsMarketIncluded(market.Key, systemConfiguration))
        .ToDictionary(market => market.Key, market => market.Value, StringComparer.InvariantCultureIgnoreCase);
    }

    private static bool IsTradFi(BinanceFuturesMarketMetadata metadata)
    {
      return metadata != null && metadata.ContractType.Equals(TradFiContractType, StringComparison.InvariantCultureIgnoreCase);
    }

    private static bool HasFreshMetadata()
    {
      lock (MetadataLock)
      {
        return _metadata.Count > 0 && _lastRefreshUtc >= DateTime.UtcNow.AddMinutes(-MetadataRefreshMinutes);
      }
    }

    private static void EnsureMetadataLoaded()
    {
      lock (MetadataLock)
      {
        if (_metadata.Count > 0 || _metadataFileLoaded)
        {
          return;
        }
      }

      LoadMetadataFromFile();
    }

    private static void LoadMetadataFromFile()
    {
      lock (MetadataLock)
      {
        if (_metadataFileLoaded)
        {
          return;
        }

        _metadataFileLoaded = true;
        string path = GetMetadataFilePath();
        if (!File.Exists(path))
        {
          return;
        }

        try
        {
          List<BinanceFuturesMarketMetadata> metadata = JsonConvert.DeserializeObject<List<BinanceFuturesMarketMetadata>>(File.ReadAllText(path));
          if (metadata == null)
          {
            return;
          }

          _metadata = metadata
            .Where(item => item != null && !String.IsNullOrWhiteSpace(item.Symbol))
            .GroupBy(item => item.Symbol, StringComparer.InvariantCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.InvariantCultureIgnoreCase);

          _lastRefreshUtc = File.GetLastWriteTimeUtc(path);
        }
        catch
        {
          _metadata = new Dictionary<string, BinanceFuturesMarketMetadata>(StringComparer.InvariantCultureIgnoreCase);
          _lastRefreshUtc = Constants.confMinDate;
        }
      }
    }

    private static void SaveMetadataToFile(Dictionary<string, BinanceFuturesMarketMetadata> metadata, LogHelper log)
    {
      try
      {
        string path = GetMetadataFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonConvert.SerializeObject(metadata.Values.OrderBy(item => item.Symbol), Formatting.Indented));
      }
      catch (Exception ex)
      {
        log.DoLogWarn("BinanceFutures - Unable to save market classification metadata: " + ex.Message);
      }
    }

    private static string GetMetadataFilePath()
    {
      return Path.Combine(Directory.GetCurrentDirectory(), Constants.PTMagicPathData, Constants.PTMagicPathExchange, MetadataFileName);
    }
  }

  public class BinanceFuturesMarketMetadata
  {
    public string Symbol { get; set; } = "";
    public string ContractType { get; set; } = "";
    public string UnderlyingType { get; set; } = "";
    public List<string> UnderlyingSubTypes { get; set; } = new List<string>();
  }
}
