using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Core.Main;
using Core.Helper;
using Core.Main.DataObjects;
using Core.Main.DataObjects.PTMagicData;
using System.Globalization;
using System.Text;

namespace Monitor.Pages
{
  public class DashboardBottomModel : _Internal.BasePageModelSecureAJAX
  {
    public ProfitTrailerData PTData = null;
    public StatsData StatsData { get; set; }
    public PropertiesData PropertiesData { get; set; }
    public SummaryData SummaryData { get; set; }

    public List<MarketTrend> MarketTrends { get; set; } = new List<MarketTrend>();
    public string TrendChartDataJSON = "";
    public string ProfitChartDataJSON = "";
    public string LastGlobalSetting = "Default";
    public DateTimeOffset DateTimeNow = Constants.confMinDate;
    public string AssetDistributionData = "";
    public double totalCurrentValue = 0;
    public void OnGet()
    {
      // Initialize Config
      base.Init();

      BindData();
      
      BuildAssetDistributionData();
    }

    private void BindData()
    {
      PTData = this.PtDataObject;
      StatsData = this.PTData.Stats;
      PropertiesData = this.PTData.Properties;
      SummaryData = this.PTData.Summary;

      List<DailyPNLData> dailyPNLData = this.PTData.DailyPNL;

      // Cleanup temp files
      FileHelper.CleanupFilesMinutes(PTMagicMonitorBasePath + "wwwroot" + System.IO.Path.DirectorySeparatorChar + "assets" + System.IO.Path.DirectorySeparatorChar + "tmp" + System.IO.Path.DirectorySeparatorChar, 5);

      // Convert local offset time to UTC
      TimeSpan offsetTimeSpan = TimeSpan.Parse(PTMagicConfiguration.GeneralSettings.Application.TimezoneOffset.Replace("+", ""));
      DateTimeNow = DateTimeOffset.UtcNow.ToOffset(offsetTimeSpan);

      // Get last and current active setting
      if (!String.IsNullOrEmpty(HttpContext.Session.GetString("LastGlobalSetting")))
      {
        LastGlobalSetting = HttpContext.Session.GetString("LastGlobalSetting");
      }
      HttpContext.Session.SetString("LastGlobalSetting", Summary.CurrentGlobalSetting.SettingName);

      // Get market trends
      MarketTrends = PTMagicConfiguration.AnalyzerSettings.MarketAnalyzer.MarketTrends.OrderBy(mt => mt.TrendMinutes).ThenByDescending(mt => mt.Platform).ToList();

      BuildMarketTrendChartData();
      BuildProfitChartData();
    }
    private void BuildMarketTrendChartData()
    {
        List<string> trendChartData = new List<string>();
        if (MarketTrends.Count > 0)
        {
          
            int mtIndex = 0;
            foreach (MarketTrend mt in MarketTrends)
            {
                if (mt.DisplayGraph)
                {
                    string lineColor = mtIndex < Constants.ChartLineColors.Length
                        ? Constants.ChartLineColors[mtIndex]
                        : Constants.ChartLineColors[mtIndex - 20];

                    if (Summary.MarketTrendChanges.ContainsKey(mt.Name))
                    {
                        List<MarketTrendChange> marketTrendChangeSummaries = Summary.MarketTrendChanges[mt.Name];

                        if (marketTrendChangeSummaries.Count > 0)
                        {
                            List<string> trendValues = new List<string>();

                            // Sort marketTrendChangeSummaries by TrendDateTime
                            marketTrendChangeSummaries = marketTrendChangeSummaries.OrderBy(m => m.TrendDateTime).ToList();

                            // Get trend ticks for chart
                            DateTime currentDateTime = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, DateTime.UtcNow.Day, DateTime.UtcNow.Hour, 0, 0);
                            DateTime startDateTime = currentDateTime.AddHours(-PTMagicConfiguration.GeneralSettings.Monitor.GraphMaxTimeframeHours);
                            DateTime endDateTime = currentDateTime;
                            
                            // Cache the result of SplitCamelCase(mt.Name)
                            string splitCamelCaseName = SystemHelper.SplitCamelCase(mt.Name);

                            for (DateTime tickTime = startDateTime; tickTime <= endDateTime; tickTime = tickTime.AddMinutes(PTMagicConfiguration.GeneralSettings.Monitor.GraphIntervalMinutes))
                            {
                                // Use binary search to find the range of items that match the condition
                                int index = marketTrendChangeSummaries.BinarySearch(new MarketTrendChange { TrendDateTime = tickTime }, Comparer<MarketTrendChange>.Create((x, y) => x.TrendDateTime.CompareTo(y.TrendDateTime)));
                                if (index < 0) index = ~index;
                                if (index < marketTrendChangeSummaries.Count)
                                {
                                    MarketTrendChange mtc = marketTrendChangeSummaries[index];
                                    if (Double.IsInfinity(mtc.TrendChange)) mtc.TrendChange = 0;

                                    trendValues.Add("{ x: new Date('" + tickTime.ToString("yyyy-MM-ddTHH:mm:ss").Replace(".", ":") + "'), y: " + mtc.TrendChange.ToString("0.00", CultureInfo.InvariantCulture) + "}");
                                }
                            }

                            // Add most recent tick
                            MarketTrendChange latestMtc = marketTrendChangeSummaries.Last();
                            if (Double.IsInfinity(latestMtc.TrendChange)) latestMtc.TrendChange = 0;
                            trendValues.Add("{ x: new Date('" + latestMtc.TrendDateTime.ToString("yyyy-MM-ddTHH:mm:ss").Replace(".", ":") + "'), y: " + latestMtc.TrendChange.ToString("0.00", CultureInfo.InvariantCulture) + "}");

                            // Use cached splitCamelCaseName
                            trendChartData.Add("{ key: '" + splitCamelCaseName + "', color: '" + lineColor + "', values: [" + string.Join(",\n", trendValues) + "] }");
                            mtIndex++;
                        }
                    }
                }
            }
            
        }
        TrendChartDataJSON = "[" + string.Join(",", trendChartData) + "]";
    }

    private void BuildProfitChartData()
    {
        StringBuilder profitPerDayJSON = new StringBuilder();

        if (PTData.DailyPNL.Count > 0)
        {
            DateTime endDate = DateTime.UtcNow.Date;
            DateTime startDate = endDate.AddDays(-PTMagicConfiguration.GeneralSettings.Monitor.ProfitsMaxTimeframeDays - 1); // Fetch data for timeframe + 1 days
            double previousDayCumulativeProfit = 0;
            bool isFirstDay = true;
            for (DateTime date = startDate; date <= endDate; date = date.AddDays(1))
            {
                DailyPNLData dailyPNL = PTData.DailyPNL.Find(ds => DateTime.ParseExact(ds.Date, "d-M-yyyy", CultureInfo.InvariantCulture) == date);
                if (dailyPNL != null)
                {
                    if (isFirstDay)
                    {
                        isFirstDay = false;
                    }
                    else
                    {
                        // Calculate the profit for the current day
                        double profitFiat = Math.Round(dailyPNL.CumulativeProfitCurrency - previousDayCumulativeProfit, 2);

                        // Add the data point to the JSON string
                        if (profitPerDayJSON.Length > 0)
                        {
                            profitPerDayJSON.Append(",\n");
                        }
                        profitPerDayJSON.Append("{x: new Date('" + date.ToString("yyyy-MM-dd") + "'), y: " + profitFiat.ToString("0.00", new System.Globalization.CultureInfo("en-US")) + "}");
                    }
                    previousDayCumulativeProfit = dailyPNL.CumulativeProfitCurrency;
                }
            }
            ProfitChartDataJSON = "[{key: 'Profit in " + PTData.Properties.Currency + "',color: '" + Constants.ChartLineColors[1] + "',values: [" + profitPerDayJSON.ToString() + "]}]";
        }
    }

    private void BuildAssetDistributionData()
    {
      // the per PT-Eelroy, the PT API doesn't provide these values when using leverage, so they are calculated here to cover either case.
      double PairsBalance = 0.0;
      double DCABalance = 0.0;
      double PendingBalance = 0.0;
      double AvailableBalance = PTData.GetCurrentBalance();
      bool isSellStrategyTrue = false;
      bool isTrailingSellActive = false;

      foreach (Core.Main.DataObjects.PTMagicData.DCALogData dcaLogEntry in PTData.DCALog)
      {
            string sellStrategyText = Core.ProfitTrailer.StrategyHelper.GetStrategyText(Summary, dcaLogEntry.SellStrategies, dcaLogEntry.SellStrategy, isSellStrategyTrue, isTrailingSellActive);

        // Aggregate totals
        double leverage = dcaLogEntry.Leverage;
        if (leverage == 0)
        {
          leverage = 1;
        }
        if (sellStrategyText.Contains("PENDING"))
        {
          PendingBalance = PendingBalance + ((dcaLogEntry.Amount * dcaLogEntry.CurrentPrice) / leverage);
        }
        else if (dcaLogEntry.BuyStrategies.Count > 0)
        {
          DCABalance = DCABalance + ((dcaLogEntry.Amount * dcaLogEntry.CurrentPrice) / leverage);
        }
        else
        {
          PairsBalance = PairsBalance + ((dcaLogEntry.Amount * dcaLogEntry.CurrentPrice) / leverage);
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
