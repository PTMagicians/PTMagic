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
    private const int MetadataRefreshMinutes = 15;
    private const string MetadataFileName = "BinanceFuturesMarketMetadata.json";
    private const string EffectiveUnderlyingTypesFileName = "BinanceFuturesAnalyzerUnderlyingTypes.json";

    private static readonly SemaphoreSlim RefreshLock = new SemaphoreSlim(1, 1);
    private static readonly object MetadataLock = new object();
    private static readonly object UnderlyingTypesLock = new object();
    private static Dictionary<string, BinanceFuturesMarketMetadata> _metadata = new Dictionary<string, BinanceFuturesMarketMetadata>(StringComparer.InvariantCultureIgnoreCase);
    private static DateTime _lastRefreshUtc = Constants.confMinDate;
    private static bool _metadataFileLoaded = false;
    private static List<string> _profitTrailerAllowedUnderlyingTypes = null;
    private static bool _profitTrailerAllowedUnderlyingTypesConfiguredInProcess = false;
    private static DateTime _effectiveUnderlyingTypesFileWriteUtc = Constants.confMinDate;
    private static bool _effectiveUnderlyingTypesFileExists = false;

    public static readonly string[] KnownUnderlyingTypes = new string[]
    {
      "COIN",
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

        string typeCounts = String.Join(", ", metadata.Values
          .GroupBy(item => String.IsNullOrWhiteSpace(item.UnderlyingType) ? "(blank)" : item.UnderlyingType, StringComparer.InvariantCultureIgnoreCase)
          .OrderBy(group => group.Key)
          .Select(group => group.Key + "=" + group.Count()));
        log.DoLogInfo("BinanceFutures - Loaded market classification metadata for " + metadata.Count + " symbols. Underlying types: " + typeCounts);
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

    public static void ConfigureProfitTrailerAllowedUnderlyingTypes(List<string> pairsLines, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      Core.Main.DataObjects.PTMagicData.MarketAnalyzer settings = systemConfiguration?.AnalyzerSettings?.MarketAnalyzer;
      if (settings?.UseProfitTrailerAllowedUnderlyingTypes != true)
      {
        lock (UnderlyingTypesLock)
        {
          _profitTrailerAllowedUnderlyingTypes = null;
          _profitTrailerAllowedUnderlyingTypesConfiguredInProcess = false;
        }
        ClearEffectiveUnderlyingTypesFile(log);
        return;
      }

      bool found = false;
      string rawValue = "";
      if (pairsLines != null)
      {
        foreach (string line in pairsLines)
        {
          string trimmedLine = line?.Trim() ?? "";
          if (trimmedLine.StartsWith("#"))
          {
            continue;
          }

          int separatorIndex = trimmedLine.IndexOf('=');
          if (separatorIndex < 0)
          {
            continue;
          }

          string propertyName = trimmedLine.Substring(0, separatorIndex).Trim();
          if (!propertyName.Equals("allowed_underlying_type", StringComparison.InvariantCultureIgnoreCase))
          {
            continue;
          }

          rawValue = trimmedLine.Substring(separatorIndex + 1).Trim();
          found = true;
          break;
        }
      }

      if (!found)
      {
        lock (UnderlyingTypesLock)
        {
          _profitTrailerAllowedUnderlyingTypes = null;
          _profitTrailerAllowedUnderlyingTypesConfiguredInProcess = false;
        }
        ClearEffectiveUnderlyingTypesFile(log);
        log.DoLogWarn("BinanceFutures - UseProfitTrailerAllowedUnderlyingTypes is enabled, but allowed_underlying_type was not found in the active PAIRS settings. Falling back to IncludedUnderlyingTypes.");
        return;
      }

      List<string> configuredTypes = rawValue
        .Split(',', StringSplitOptions.RemoveEmptyEntries)
        .Select(type => type.Trim().ToUpperInvariant())
        .Where(type => !String.IsNullOrWhiteSpace(type))
        .Distinct(StringComparer.InvariantCultureIgnoreCase)
        .ToList();

      lock (UnderlyingTypesLock)
      {
        _profitTrailerAllowedUnderlyingTypes = configuredTypes;
        _profitTrailerAllowedUnderlyingTypesConfiguredInProcess = true;
      }

      SaveEffectiveUnderlyingTypesFile(configuredTypes, log);

      if (configuredTypes.Count == 0)
      {
        log.DoLogInfo("BinanceFutures - Using ProfitTrailer allowed_underlying_type for analyzer universe: blank value (all classified underlying types).");
      }
      else
      {
        log.DoLogInfo("BinanceFutures - Using ProfitTrailer allowed_underlying_type for analyzer universe: " + String.Join(", ", configuredTypes) + ".");
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
          // Fail closed: an explicit analyzer universe should never silently admit an
          // unclassified Binance Futures symbol.
          return false;
        }
      }

      if (String.IsNullOrWhiteSpace(metadata.UnderlyingType))
      {
        return false;
      }

      Core.Main.DataObjects.PTMagicData.MarketAnalyzer settings = systemConfiguration?.AnalyzerSettings?.MarketAnalyzer;
      List<string> includedTypes = null;

      if (settings?.UseProfitTrailerAllowedUnderlyingTypes == true)
      {
        RefreshEffectiveUnderlyingTypesFromFile();

        lock (UnderlyingTypesLock)
        {
          if (_profitTrailerAllowedUnderlyingTypes != null)
          {
            // ProfitTrailer treats a present-but-blank allowed_underlying_type as unrestricted.
            if (_profitTrailerAllowedUnderlyingTypes.Count == 0)
            {
              return true;
            }

            includedTypes = new List<string>(_profitTrailerAllowedUnderlyingTypes);
          }
        }
      }

      if (includedTypes == null)
      {
        includedTypes = settings?.IncludedUnderlyingTypes;
        if (includedTypes == null)
        {
          includedTypes = new List<string> { "COIN" };
        }
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

    private static void SaveEffectiveUnderlyingTypesFile(List<string> underlyingTypes, LogHelper log)
    {
      try
      {
        string path = GetEffectiveUnderlyingTypesFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonConvert.SerializeObject(underlyingTypes ?? new List<string>(), Formatting.Indented));

        lock (UnderlyingTypesLock)
        {
          _effectiveUnderlyingTypesFileExists = true;
          _effectiveUnderlyingTypesFileWriteUtc = File.GetLastWriteTimeUtc(path);
        }
      }
      catch (Exception ex)
      {
        log.DoLogWarn("BinanceFutures - Unable to save effective analyzer underlying types: " + ex.Message);
      }
    }

    private static void ClearEffectiveUnderlyingTypesFile(LogHelper log)
    {
      try
      {
        string path = GetEffectiveUnderlyingTypesFilePath();
        if (File.Exists(path))
        {
          File.Delete(path);
        }

        lock (UnderlyingTypesLock)
        {
          _effectiveUnderlyingTypesFileExists = false;
          _effectiveUnderlyingTypesFileWriteUtc = Constants.confMinDate;
        }
      }
      catch (Exception ex)
      {
        log.DoLogWarn("BinanceFutures - Unable to clear effective analyzer underlying types: " + ex.Message);
      }
    }

    private static void RefreshEffectiveUnderlyingTypesFromFile()
    {
      lock (UnderlyingTypesLock)
      {
        if (_profitTrailerAllowedUnderlyingTypesConfiguredInProcess)
        {
          return;
        }

        string path = GetEffectiveUnderlyingTypesFilePath();
        if (!File.Exists(path))
        {
          _profitTrailerAllowedUnderlyingTypes = null;
          _effectiveUnderlyingTypesFileExists = false;
          _effectiveUnderlyingTypesFileWriteUtc = Constants.confMinDate;
          return;
        }

        DateTime fileWriteUtc = File.GetLastWriteTimeUtc(path);
        if (_effectiveUnderlyingTypesFileExists && fileWriteUtc == _effectiveUnderlyingTypesFileWriteUtc)
        {
          return;
        }

        try
        {
          _profitTrailerAllowedUnderlyingTypes = JsonConvert.DeserializeObject<List<string>>(File.ReadAllText(path)) ?? new List<string>();
          _effectiveUnderlyingTypesFileExists = true;
          _effectiveUnderlyingTypesFileWriteUtc = fileWriteUtc;
        }
        catch
        {
          _profitTrailerAllowedUnderlyingTypes = null;
          _effectiveUnderlyingTypesFileExists = false;
          _effectiveUnderlyingTypesFileWriteUtc = Constants.confMinDate;
        }
      }
    }

    private static string GetMetadataFilePath()
    {
      return Path.Combine(Directory.GetCurrentDirectory(), Constants.PTMagicPathData, Constants.PTMagicPathExchange, MetadataFileName);
    }

    private static string GetEffectiveUnderlyingTypesFilePath()
    {
      return Path.Combine(Directory.GetCurrentDirectory(), Constants.PTMagicPathData, Constants.PTMagicPathExchange, EffectiveUnderlyingTypesFileName);
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
