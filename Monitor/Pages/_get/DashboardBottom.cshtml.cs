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
    public MiscData MiscData { get; set; }
    public List<MarketTrend> MarketTrends { get; set; } = new List<MarketTrend>();
    public double DataHours { get; set; }
    public int ProfitDays { get; set; }
    public string TrendChartDataJSON = "";
    public string ProfitChartDataJSON = "";
    public string LastGlobalSetting = "Default";
    public DateTimeOffset DateTimeNow = Constants.confMinDate;
    public string AssetDistributionData = "";
    public double totalCurrentValue = 0;
    public string TotalCurrentValueLiveChartDataJSON { get; set; }
    public void OnGet()
    {
      // Initialize Config
      base.Init();

      BindData();
   
    }
    
    private void BindData()
    {
      PTData = this.PtDataObject;
      StatsData = this.PTData.Stats;
      PropertiesData = this.PTData.Properties;
      MiscData = this.PTData.Misc;
      List<MonthlyStatsData> monthlyStatsData = this.PTData.MonthlyStats;
      List<DailyPNLData> dailyPNLData = this.PTData.DailyPNL;
      

      // Cleanup temp files
      FileHelper.CleanupFilesMinutes(PTMagicMonitorBasePath + "wwwroot" + System.IO.Path.DirectorySeparatorChar + "assets" + System.IO.Path.DirectorySeparatorChar + "tmp" + System.IO.Path.DirectorySeparatorChar, 5);

      // Convert local offset time to UTC
      TimeSpan offsetTimeSpan = TimeSpan.Parse(MiscData.TimeZoneOffset.Replace("+", ""));
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
      BuildAssetDistributionData();
      BuildProfitChartData();
      StartUpdatingTotalCurrentValueLive();
      UpdateTotalCurrentValueLive();
      BuildTotalCurrentValueLiveChartData();
    }
    private static System.Timers.Timer timer;
    private static List<(DateTime Timestamp, double TotalCurrentValue)> totalCurrentValueLiveList;

public void StartUpdatingTotalCurrentValueLive()
{
    int liveTCVInterval = PTMagicConfiguration.GeneralSettings.Monitor.RefreshSeconds;
    if (timer != null)
    {
        // Timer is already running
        return;
    }

    totalCurrentValueLiveList = new List<(DateTime Timestamp, double TotalCurrentValueLive)>();

    timer = new System.Timers.Timer(liveTCVInterval * 1000); // Set interval to liveTCVTimer seconds
    timer.Elapsed += (sender, e) => UpdateTotalCurrentValueLive();
    timer.Start();
}

private void UpdateTotalCurrentValueLive()
{
    double PairsBalance = 0.0;
    double DCABalance = 0.0;
    double PendingBalance = 0.0;
    double AvailableBalance = PTData.GetCurrentBalance();
    bool isSellStrategyTrue = false;
    bool isTrailingSellActive = false;

    foreach (DCALogData dcaLogEntry in PTData.DCALog)
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
            PendingBalance = PendingBalance + (dcaLogEntry.Amount * dcaLogEntry.CurrentPrice / leverage);
        }
        else if (dcaLogEntry.BuyStrategies.Count > 0)
        {
            DCABalance = DCABalance + (dcaLogEntry.Amount * dcaLogEntry.CurrentPrice / leverage);
        }
        else
        {
            PairsBalance = PairsBalance + (dcaLogEntry.Amount * dcaLogEntry.CurrentPrice / leverage);
        }
    }
    double totalCurrentValueLive = PendingBalance + DCABalance + PairsBalance + AvailableBalance;

    // Get the current time
    DateTime now = DateTime.UtcNow;
    totalCurrentValueLiveList.Add((now, totalCurrentValueLive));
    
    // Get liveTCVTimeframe from PTMagicConfiguration.GeneralSettings.Monitor
    int liveTCVTimeframe = PTMagicConfiguration.GeneralSettings.Monitor.LiveTCVTimeframeMinutes;

    // Calculate the timestamp that is liveTCVTimeframe minutes ago
    DateTime threshold = now.AddMinutes(-liveTCVTimeframe);

    // Remove all data points that are older than the threshold
    while (totalCurrentValueLiveList.Count > 0 && totalCurrentValueLiveList[0].Item1 < threshold)
    {
        totalCurrentValueLiveList.RemoveAt(0);
    }
}

private void BuildTotalCurrentValueLiveChartData()
{
    List<object> TotalCurrentValueLivePerIntervalList = new List<object>();

    if (totalCurrentValueLiveList.Count > 0)
    {
        foreach (var dataPoint in totalCurrentValueLiveList)
        {
            DateTime timestamp = dataPoint.Timestamp;
            double totalCurrentValueLive = dataPoint.TotalCurrentValue;

            // Convert the timestamp to a Unix timestamp
            long unixTimestamp = new DateTimeOffset(timestamp).ToUnixTimeMilliseconds();

            // Add the data point to the list
            TotalCurrentValueLivePerIntervalList.Add(new { x = unixTimestamp, y = totalCurrentValueLive });
        }

        // Convert the list to a JSON string using Newtonsoft.Json
        TotalCurrentValueLiveChartDataJSON = Newtonsoft.Json.JsonConvert.SerializeObject(new[] {
            new {
                key = "Total Current Value",
                color = Constants.ChartLineColors[1],
                values = TotalCurrentValueLivePerIntervalList
            }
        });
    }
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
                            TimeSpan offset;
                            bool isNegative = MiscData.TimeZoneOffset.StartsWith("-");
                            string offsetWithoutSign = MiscData.TimeZoneOffset.TrimStart('+', '-');

                            if (!TimeSpan.TryParse(offsetWithoutSign, out offset))
                            {
                                offset = TimeSpan.Zero; // If offset is invalid, set it to zero
                            }

                            DateTime currentDateTime = DateTime.UtcNow;
                            DateTime startDateTime = currentDateTime.AddHours(-PTMagicConfiguration.GeneralSettings.Monitor.GraphMaxTimeframeHours);
                            DateTime endDateTime = currentDateTime;

                            // Ensure startDateTime doesn't exceed the available data
                            DateTime earliestTrendDateTime = marketTrendChangeSummaries.Min(mtc => mtc.TrendDateTime);
                            startDateTime = startDateTime > earliestTrendDateTime ? startDateTime : earliestTrendDateTime;
                            DataHours = (currentDateTime - earliestTrendDateTime).TotalHours;

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

                                    // Adjust tickTime to the desired timezone before converting to string
                                    DateTime adjustedTickTime = tickTime.Add(isNegative ? -offset : offset);
                                    trendValues.Add("{ x: new Date('" + adjustedTickTime.ToString("yyyy-MM-ddTHH:mm:ss").Replace(".", ":") + "'), y: " + mtc.TrendChange.ToString("0.00", CultureInfo.InvariantCulture) + "}");
                                }
                            }

                            // Add most recent tick
                            MarketTrendChange latestMtc = marketTrendChangeSummaries.Last();
                            if (Double.IsInfinity(latestMtc.TrendChange)) latestMtc.TrendChange = 0;

                            // Adjust latestMtc.TrendDateTime to the desired timezone before converting to string
                            DateTime adjustedLatestTrendDateTime = latestMtc.TrendDateTime.Add(isNegative ? -offset : offset);
                            trendValues.Add("{ x: new Date('" + adjustedLatestTrendDateTime.ToString("yyyy-MM-ddTHH:mm:ss").Replace(".", ":") + "'), y: " + latestMtc.TrendChange.ToString("0.00", CultureInfo.InvariantCulture) + "}");

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
        List<object> profitPerDayList = new List<object>();

        if (PTData.DailyPNL.Count > 0)
        {
            // Get timezone offset
            TimeSpan offset;
            bool isNegative = MiscData.TimeZoneOffset.StartsWith("-");
            string offsetWithoutSign = MiscData.TimeZoneOffset.TrimStart('+', '-');

            if (!TimeSpan.TryParse(offsetWithoutSign, out offset))
            {
                offset = TimeSpan.Zero; // If offset is invalid, set it to zero
            }

            DateTime endDate = DateTime.UtcNow.Add(isNegative ? -offset : offset).Date;

            // Parse dates once and adjust them to the local timezone
            Dictionary<DateTime, DailyPNLData> dailyPNLByDate = PTData.DailyPNL
                .Select(data => {
                    DateTime dateUtc = DateTime.ParseExact(data.Date, "d-M-yyyy", CultureInfo.InvariantCulture);
                    DateTime dateLocal = dateUtc.Add(isNegative ? -offset : offset);
                    return new { Date = dateLocal.Date, Data = data };
                })
                .ToDictionary(
                    item => item.Date,
                    item => item.Data
                );

            DateTime earliestDataDate = dailyPNLByDate.Keys.Min();
            DateTime startDate = endDate.AddDays(-PTMagicConfiguration.GeneralSettings.Monitor.ProfitsMaxTimeframeDays - 1); // Fetch data for timeframe + 1 days
            if (startDate < earliestDataDate)
            {
                startDate = earliestDataDate;
            }

            // Calculate the total days of data available
            ProfitDays = (endDate - startDate).Days;

            double previousDayCumulativeProfit = 0;
            bool isFirstDay = true;
            for (DateTime date = startDate; date <= endDate; date = date.AddDays(1))
            {
                // Use the dictionary to find the DailyPNLData for the date
                if (dailyPNLByDate.TryGetValue(date, out DailyPNLData dailyPNL))
                {
                    if (isFirstDay)
                    {
                        isFirstDay = false;
                    }
                    else
                    {
                        // Calculate the profit for the current day
                        double profitFiat = Math.Round(dailyPNL.CumulativeProfitCurrency - previousDayCumulativeProfit, 2);

                        // Add the data point to the list
                        profitPerDayList.Add(new { x = new DateTimeOffset(date).ToUnixTimeMilliseconds(), y = profitFiat });
                    }
                    previousDayCumulativeProfit = dailyPNL.CumulativeProfitCurrency;
                }
            }
            // Convert the list to a JSON string using Newtonsoft.Json
            ProfitChartDataJSON = Newtonsoft.Json.JsonConvert.SerializeObject(new[] {
                new {
                    key = "Profit in " + PTData.Misc.Market,
                    color = Constants.ChartLineColors[1],
                    values = profitPerDayList
                }
            });
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
