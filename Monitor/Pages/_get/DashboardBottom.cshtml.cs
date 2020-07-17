using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Core.Main;
using Core.Helper;
using Core.Main.DataObjects;
using Core.Main.DataObjects.PTMagicData;
using Core.MarketAnalyzer;

namespace Monitor.Pages {
  public class DashboardBottomModel : _Internal.BasePageModelSecureAJAX {
    public ProfitTrailerData PTData = null;
    public List<MarketTrend> MarketTrends { get; set; } = new List<MarketTrend>();
    public string TrendChartDataJSON = "";
    public string ProfitChartDataJSON = "";
    public string LastGlobalSetting = "Default";
    public DateTimeOffset DateTimeNow = Constants.confMinDate;
    public string AssetDistributionData = "";
    public double currentBalance = 0;
    public string currentBalanceString = "";
    public double TotalBagCost = 0;
    public double TotalBagValue = 0;
    public double totalCurrentValue = 0;
    public void OnGet() {
      // Initialize Config
      base.Init();

      BindData();
      BuildAssetDistributionData();
    }

    private void BindData() {
      PTData = this.PtDataObject;

      // Cleanup temp files
      FileHelper.CleanupFilesMinutes(PTMagicMonitorBasePath + "wwwroot" + System.IO.Path.DirectorySeparatorChar + "assets" + System.IO.Path.DirectorySeparatorChar + "tmp" + System.IO.Path.DirectorySeparatorChar, 5);

      // Convert local offset time to UTC
      TimeSpan offsetTimeSpan = TimeSpan.Parse(PTMagicConfiguration.GeneralSettings.Application.TimezoneOffset.Replace("+", ""));
      DateTimeNow = DateTimeOffset.UtcNow.ToOffset(offsetTimeSpan);

      // Get last and current active setting
      if (!String.IsNullOrEmpty(HttpContext.Session.GetString("LastGlobalSetting"))) {
        LastGlobalSetting = HttpContext.Session.GetString("LastGlobalSetting");
      }
      HttpContext.Session.SetString("LastGlobalSetting", Summary.CurrentGlobalSetting.SettingName);

      // Get market trends
      MarketTrends = PTMagicConfiguration.AnalyzerSettings.MarketAnalyzer.MarketTrends.OrderBy(mt => mt.TrendMinutes).ThenByDescending(mt => mt.Platform).ToList();

      BuildMarketTrendChartData();
      BuildProfitChartData();
    }
    private void BuildMarketTrendChartData() {
      if (MarketTrends.Count > 0) {
        TrendChartDataJSON = "[";
        int mtIndex = 0;
        foreach (MarketTrend mt in MarketTrends) {
          if (mt.DisplayGraph) {
            string lineColor = "";
            if (mtIndex < Constants.ChartLineColors.Length) {
              lineColor = Constants.ChartLineColors[mtIndex];
            } else {
              lineColor = Constants.ChartLineColors[mtIndex - 20];
            }

            if (Summary.MarketTrendChanges.ContainsKey(mt.Name)) {
              List<MarketTrendChange> marketTrendChangeSummaries = Summary.MarketTrendChanges[mt.Name];

              if (marketTrendChangeSummaries.Count > 0) {
                if (!TrendChartDataJSON.Equals("[")) TrendChartDataJSON += ",";

                TrendChartDataJSON += "{";
                TrendChartDataJSON += "key: '" + SystemHelper.SplitCamelCase(mt.Name) + "',";
                TrendChartDataJSON += "color: '" + lineColor + "',";
                TrendChartDataJSON += "values: [";

                // Get trend ticks for chart
                DateTime currentDateTime = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, 0, 0);
                DateTime startDateTime = currentDateTime.AddHours(-PTMagicConfiguration.GeneralSettings.Monitor.GraphMaxTimeframeHours);
                DateTime endDateTime = currentDateTime;
                int trendChartTicks = 0;
                for (DateTime tickTime = startDateTime; tickTime <= endDateTime; tickTime = tickTime.AddMinutes(PTMagicConfiguration.GeneralSettings.Monitor.GraphIntervalMinutes)) {
                  List<MarketTrendChange> tickRange = marketTrendChangeSummaries.FindAll(m => m.TrendDateTime >= tickTime).OrderBy(m => m.TrendDateTime).ToList();
                  if (tickRange.Count > 0) {
                    MarketTrendChange mtc = tickRange.First();
                    if (tickTime != startDateTime) TrendChartDataJSON += ",\n";
                    if (Double.IsInfinity(mtc.TrendChange)) mtc.TrendChange = 0;

                    TrendChartDataJSON += "{ x: new Date('" + tickTime.ToString("yyyy-MM-ddTHH:mm:ss").Replace(".", ":") + "'), y: " + mtc.TrendChange.ToString("0.00", new System.Globalization.CultureInfo("en-US")) + "}";
                    trendChartTicks++;
                  }
                }
                // Add most recent tick
                List<MarketTrendChange> latestTickRange = marketTrendChangeSummaries.OrderByDescending(m => m.TrendDateTime).ToList();
                if (latestTickRange.Count > 0) {
                  MarketTrendChange mtc = latestTickRange.First();
                  if (trendChartTicks > 0) TrendChartDataJSON += ",\n";
                  if (Double.IsInfinity(mtc.TrendChange)) mtc.TrendChange = 0;
                  TrendChartDataJSON += "{ x: new Date('" + mtc.TrendDateTime.ToString("yyyy-MM-ddTHH:mm:ss").Replace(".", ":") + "'), y: " + mtc.TrendChange.ToString("0.00", new System.Globalization.CultureInfo("en-US")) + "}";
                }
                TrendChartDataJSON += "]";
                TrendChartDataJSON += "}";
                mtIndex++;
              }
            }
          }
        }
        TrendChartDataJSON += "]";
      }
    }

    private void BuildProfitChartData() {
      int tradeDayIndex = 0;
      string profitPerDayJSON = "";
      if (PTData.SellLog.Count > 0) {
        DateTime minSellLogDate = PTData.SellLog.OrderBy(sl => sl.SoldDate).First().SoldDate.Date;
        DateTime graphStartDate = DateTime.UtcNow.Date.AddDays(-30);
        if (minSellLogDate > graphStartDate) graphStartDate = minSellLogDate;
        for (DateTime salesDate = graphStartDate; salesDate <= DateTime.UtcNow.Date; salesDate = salesDate.AddDays(1)) {
          if (tradeDayIndex > 0) {
            profitPerDayJSON += ",\n";
          }
          int trades = PTData.SellLog.FindAll(t => t.SoldDate.Date == salesDate).Count;
          double profit = PTData.SellLog.FindAll(t => t.SoldDate.Date == salesDate).Sum(t => t.Profit);
          double profitFiat = Math.Round(profit * Summary.MainMarketPrice, 2);
          profitPerDayJSON += "{x: new Date('" + salesDate.ToString("yyyy-MM-dd") + "'), y: " + profitFiat.ToString("0.00", new System.Globalization.CultureInfo("en-US")) + "}";
          tradeDayIndex++;
        }
        ProfitChartDataJSON = "[";
        ProfitChartDataJSON += "{";
        ProfitChartDataJSON += "key: 'Profit in " + Summary.MainFiatCurrency + "',";
        ProfitChartDataJSON += "color: '" + Constants.ChartLineColors[1] + "',";
        ProfitChartDataJSON += "values: [" + profitPerDayJSON + "]";
        ProfitChartDataJSON += "}";
        ProfitChartDataJSON += "]";
      }
    }
    private void BuildAssetDistributionData()
    {
      // the per PT-Eelroy, the PT API doesn't provide these values when using leverage, so they are calculated here to cover either case.
      double PairsBalance = 0.0;
      double DCABalance = 0.0;
      double PendingBalance = 0.0;
      double AvailableBalance = PTData.GetCurrentBalance();
      bool isSellStrategyTrue =false;
      bool isTrailingSellActive =false;
        
      foreach (Core.Main.DataObjects.PTMagicData.DCALogData dcaLogEntry in PTData.DCALog) 
      {
        Core.Main.DataObjects.PTMagicData.MarketPairSummary mps = null;
        string sellStrategyText = Core.ProfitTrailer.StrategyHelper.GetStrategyText(Summary, dcaLogEntry.SellStrategies, dcaLogEntry.SellStrategy, isSellStrategyTrue, isTrailingSellActive);

        // Aggregate totals
        if (dcaLogEntry.Leverage == 0)
        {
          if (sellStrategyText.Contains("PENDING"))
          {
          PendingBalance = PendingBalance + (dcaLogEntry.Amount * dcaLogEntry.CurrentPrice); 
          }
          else if (dcaLogEntry.BuyStrategies.Count > 0) 
          {
          DCABalance = DCABalance + (dcaLogEntry.Amount * dcaLogEntry.CurrentPrice);          
          }
          else
          {
          PairsBalance = PairsBalance + (dcaLogEntry.Amount * dcaLogEntry.CurrentPrice);
          }
        }
        else
        {
          if (sellStrategyText.Contains("PENDING"))
          {
          PendingBalance = PendingBalance + ((dcaLogEntry.Amount * dcaLogEntry.CurrentPrice) / dcaLogEntry.Leverage); 
          }
          else if (dcaLogEntry.BuyStrategies.Count > 0) 
          {
          DCABalance = DCABalance + ((dcaLogEntry.Amount * dcaLogEntry.CurrentPrice) / dcaLogEntry.Leverage);          
          }
          else
          {
          PairsBalance = PairsBalance + ((dcaLogEntry.Amount * dcaLogEntry.CurrentPrice) / dcaLogEntry.Leverage);
          }
        }
      }
      totalCurrentValue = PendingBalance + DCABalance + PairsBalance + AvailableBalance;
      AssetDistributionData = "[";
      AssetDistributionData += "{label: 'Pairs',color: '#82E0AA',value: '" + PairsBalance.ToString("0.00", new System.Globalization.CultureInfo("en-US")) + "'},";
      AssetDistributionData += "{label: 'DCA',color: '#D98880',value: '" + DCABalance.ToString("0.00", new System.Globalization.CultureInfo("en-US")) + "'},";
      AssetDistributionData += "{label: 'Pending',color: '#F5B041',value: '" + PendingBalance.ToString("0.00", new System.Globalization.CultureInfo("en-US")) + "'},";
      AssetDistributionData += "{label: 'Balance',color: '#85C1E9',value: '" + AvailableBalance.ToString("0.00", new System.Globalization.CultureInfo("en-US")) + "'}]";
    }
  }
}
