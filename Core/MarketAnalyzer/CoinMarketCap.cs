﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Core.Main;
using Core.Helper;
using Core.Main.DataObjects.PTMagicData;
using Newtonsoft.Json;

namespace Core.MarketAnalyzer
{
  public class CoinMarketCap : BaseAnalyzer
  {
    public static string GetMarketData(PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      string result = "";
      try
      {
        string baseUrl = "https://pro-api.coinmarketcap.com/v1/cryptocurrency/listings/latest?limit=200";
        string cmcAPI = systemConfiguration.GeneralSettings.Application.CoinMarketCapAPIKey;

        log.DoLogInfo("CoinMarketCap - Getting market data...");

        Dictionary<string, dynamic> jsonObject = GetJsonFromURL(baseUrl, log, new (string header, string value)[] { ("X-CMC_PRO_API_KEY", cmcAPI) });

        if (jsonObject.Count > 0)
        {
          if (jsonObject["data"] != null)
          {
            log.DoLogInfo("CoinMarketCap - Market data received for " + jsonObject["data"].Count + " currencies");

            Dictionary<string, Market> markets = new Dictionary<string, Market>();

            for (int i = 0; i < jsonObject["data"].Count; i++)
            {
              if (jsonObject["data"][i]["quote"]["USD"] != null)
              {
                Market market = new Market();
                market.Position = markets.Count + 1;
                market.Name = jsonObject["data"][i]["name"].ToString();
                market.Symbol = jsonObject["data"][i]["symbol"].ToString();
                market.Price = (double)jsonObject["data"][i]["quote"]["USD"]["price"];
                market.Volume24h = (double)jsonObject["data"][i]["quote"]["USD"]["volume_24h"];
                if (!String.IsNullOrEmpty(jsonObject["data"][i]["quote"]["USD"]["percent_change_24h"].ToString()))
                {
                  market.TrendChange24h = (double)jsonObject["data"][i]["quote"]["USD"]["percent_change_24h"];
                }

                markets.Add(market.Name, market);
              }
            }

            CoinMarketCap.CheckForMarketDataRecreation(markets, systemConfiguration, log);

            // Save the data
            DateTime fileDateTime = DateTime.UtcNow;
            FileHelper.WriteTextToFile(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + Constants.PTMagicPathCoinMarketCap + Path.DirectorySeparatorChar, "MarketData_" + fileDateTime.ToString("yyyy-MM-dd_HH.mm") + ".json", JsonConvert.SerializeObject(markets), fileDateTime, fileDateTime);

            log.DoLogInfo("CoinMarketCap - Market data saved.");

            FileHelper.CleanupFiles(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + Constants.PTMagicPathCoinMarketCap + Path.DirectorySeparatorChar, systemConfiguration.AnalyzerSettings.MarketAnalyzer.StoreDataMaxHours);
            log.DoLogInfo("CoinMarketCap - Market data cleaned.");
          }
        }
      }
      catch (Exception ex)
      {
        log.DoLogCritical(ex.Message, ex);
        result = ex.Message;
      }

      return result;
    }

    public static void CheckForMarketDataRecreation(Dictionary<string, Market> markets, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      string coinMarketCapDataDirectoryPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + Constants.PTMagicPathCoinMarketCap + Path.DirectorySeparatorChar;

      if (!Directory.Exists(coinMarketCapDataDirectoryPath))
      {
        Directory.CreateDirectory(coinMarketCapDataDirectoryPath);
      }

      DirectoryInfo dataDirectory = new DirectoryInfo(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + Constants.PTMagicPathCoinMarketCap + Path.DirectorySeparatorChar);

      List<FileInfo> marketFiles = dataDirectory.EnumerateFiles("MarketData*")
                         .Select(x => { x.Refresh(); return x; })
                         .Where(x => x.LastWriteTimeUtc <= DateTime.UtcNow.AddHours(-24))
                         .ToArray().OrderByDescending(f => f.LastWriteTimeUtc).ToList();

      bool build24hMarketDataFile = false;
      FileInfo marketFile = null;
      if (marketFiles.Count > 0)
      {
        marketFile = marketFiles.First();
        if (marketFile.LastWriteTimeUtc <= DateTime.UtcNow.AddHours(-24).AddSeconds(-systemConfiguration.AnalyzerSettings.MarketAnalyzer.IntervalSeconds).AddSeconds(-10))
        {
          log.DoLogDebug("CoinMarketCap - 24h market data file too old (" + marketFile.LastWriteTimeUtc.ToString() + "). Rebuilding data...");
          build24hMarketDataFile = true;
        }
      }
      else
      {
        marketFiles = dataDirectory.EnumerateFiles("MarketData*")
                         .Select(x => { x.Refresh(); return x; })
                         .Where(x => x.LastWriteTimeUtc >= DateTime.UtcNow.AddHours(-24))
                         .ToArray().OrderBy(f => f.LastWriteTimeUtc).ToList();

        if (marketFiles.Count > 0)
        {
          marketFile = marketFiles.First();
          if (marketFile.LastWriteTimeUtc >= DateTime.UtcNow.AddHours(-24).AddSeconds(systemConfiguration.AnalyzerSettings.MarketAnalyzer.IntervalSeconds).AddSeconds(10))
          {
            log.DoLogDebug("CoinMarketCap - 24h market data file too young (" + marketFile.LastWriteTimeUtc.ToString() + "). Rebuilding data...");
            build24hMarketDataFile = true;
          }
        }
        else
        {
          log.DoLogDebug("CoinMarketCap - 24h market data not found. Rebuilding data...");
          build24hMarketDataFile = true;
        }
      }

      if (build24hMarketDataFile)
      {
        Dictionary<string, Market> markets24h = new Dictionary<string, Market>();
        foreach (string key in markets.Keys)
        {
          Market market24h = new Market();
          market24h.Position = markets.Count + 1;
          market24h.Name = markets[key].Name;
          market24h.Symbol = markets[key].Symbol;
          market24h.Price = markets[key].Price / (1 + (markets[key].TrendChange24h / 100));
          market24h.Volume24h = markets[key].Volume24h;

          markets24h.Add(markets[key].Name, market24h);
        }

        // Save the 24hr market data
        DateTime fileDateTime = DateTime.UtcNow.AddHours(-24);
        FileHelper.WriteTextToFile(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + Constants.PTMagicPathCoinMarketCap + Path.DirectorySeparatorChar, "MarketData_" + fileDateTime.ToString("yyyy-MM-dd_HH.mm") + ".json", JsonConvert.SerializeObject(markets24h), fileDateTime, fileDateTime);

        log.DoLogInfo("CoinMarketCap - 24h market data rebuilt.");
      }
    }
  }
}
