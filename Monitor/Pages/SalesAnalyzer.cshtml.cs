using System;
using System.Collections.Generic;
using System.Linq;
using Core.Main;
using System.Globalization;
using Core.Main.DataObjects;
using Core.Main.DataObjects.PTMagicData;

namespace Monitor.Pages
{
  public class SalesAnalyzer : _Internal.BasePageModelSecure
  {
    public ProfitTrailerData PTData = null;
    public MiscData MiscData { get; set; }
    public PropertiesData PropertiesData { get; set; }
    public StatsData StatsData { get; set; }
    public List<DailyPNLData> DailyPNL { get; set; }
    public List<DailyTCVData> DailyTCV { get; set; }
    public List<ProfitablePairsData> ProfitablePairs { get; set; }
    public int ProfitDays { get; set; }
    public int TCVDays { get; set; }
    public List<MonthlyStatsData> MonthlyStats { get; set; }

    public string TradesChartDataJSON = "";
    public string CumulativeProfitChartDataJSON = "";
    public string TCVChartDataJSON = "";
    public string ProfitChartDataJSON = "";
    public string BalanceChartDataJSON = "";
    public IEnumerable<KeyValuePair<string, double>> TopMarkets = null;
    public DateTime MinSellLogDate = Constants.confMinDate;
    public Dictionary<DateTime, double> DailyGains = new Dictionary<DateTime, double>();
    public Dictionary<DateTime, double> MonthlyGains = new Dictionary<DateTime, double>();
    public DateTimeOffset DateTimeNow = Constants.confMinDate;
    public double totalCurrentValue = 0;
    
    public void OnGet()
    {
      base.Init();

      BindData();
    }
    
    private void BindData()
    {
      PTData = this.PtDataObject;
      MiscData = this.PTData.Misc;
      PropertiesData = this.PTData.Properties;
      StatsData = this.PTData.Stats;
      MonthlyStats = this.PTData.MonthlyStats;
      DailyPNL = this.PTData.DailyPNL;
      DailyTCV = this.PTData.DailyTCV;
      ProfitablePairs = this.PTData.ProfitablePairs;
      
      // Convert local offset time to UTC
      TimeSpan offsetTimeSpan = TimeSpan.Parse(PTMagicConfiguration.GeneralSettings.Application.TimezoneOffset.Replace("+", ""));
      DateTimeNow = DateTimeOffset.UtcNow.ToOffset(offsetTimeSpan);

      BuildSalesChartData();
      BuildProfitChartData();
      BuildCumulativeProfitChartData();
      BuildTCVChartData();
    }
    private void BuildTCVChartData()
    {
        List<object> TCVPerDayList = new List<object>();

        if (PTData.DailyTCV.Count > 0)
        {
            // Get timezone offset
            TimeSpan offset;
            bool isNegative = PTMagicConfiguration.GeneralSettings.Application.TimezoneOffset.StartsWith("-");
            string offsetWithoutSign = PTMagicConfiguration.GeneralSettings.Application.TimezoneOffset.TrimStart('+', '-');

            if (!TimeSpan.TryParse(offsetWithoutSign, out offset))
            {
                offset = TimeSpan.Zero; // If offset is invalid, set it to zero
            }

            DateTime endDate = DateTime.UtcNow.Add(isNegative ? -offset : offset).Date;

            // Parse dates once and adjust them to the local timezone
            Dictionary<DateTime, DailyTCVData> dailyTCVByDate = PTData.DailyTCV
                .Select(data => {
                    DateTime dateUtc = DateTime.ParseExact(data.Date, "d-M-yyyy", CultureInfo.InvariantCulture);
                    DateTime dateLocal = dateUtc.Add(isNegative ? -offset : offset);
                    return new { Date = dateLocal.Date, Data = data };
                })
                .ToDictionary(
                    item => item.Date,
                    item => item.Data
                );

            DateTime earliestDataDate = dailyTCVByDate.Keys.Min();
            DateTime startDate = earliestDataDate;

            // Calculate the total days of data available
            TCVDays = (endDate - startDate).Days;

            for (DateTime date = startDate; date <= endDate; date = date.AddDays(1))
            {
                // Use the dictionary to find the Data for the date
                if (dailyTCVByDate.TryGetValue(date, out DailyTCVData dailyTCV))
                {
                    double TCV = dailyTCV.TCV;

                    // Add the data point to the list
                    TCVPerDayList.Add(new { x = new DateTimeOffset(date).ToUnixTimeMilliseconds(), y = TCV });
                }
            }
            // Convert the list to a JSON string using Newtonsoft.Json
            TCVChartDataJSON = Newtonsoft.Json.JsonConvert.SerializeObject(new[] {
                new {
                    key = "TCV in " + PTData.Misc.Market,
                    color = Constants.ChartLineColors[1],
                    values = TCVPerDayList
                }
            });
        }
    }

    private void BuildCumulativeProfitChartData()
    {
        List<object> profitPerDayList = new List<object>();

        if (PTData.DailyPNL.Count > 0)
        {
            // Get timezone offset
            TimeSpan offset;
            bool isNegative = PTMagicConfiguration.GeneralSettings.Application.TimezoneOffset.StartsWith("-");
            string offsetWithoutSign = PTMagicConfiguration.GeneralSettings.Application.TimezoneOffset.TrimStart('+', '-');

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
            DateTime startDate = earliestDataDate;

            // Calculate the total days of data available
            ProfitDays = (endDate - startDate).Days;

            double previousDayCumulativeProfit = 0;
            for (DateTime date = startDate; date <= endDate; date = date.AddDays(1))
            {
                // Use the dictionary to find the DailyPNLData for the date
                if (dailyPNLByDate.TryGetValue(date, out DailyPNLData dailyPNL))
                {
                    // Use the CumulativeProfitCurrency directly
                    double profitFiat = dailyPNL.CumulativeProfitCurrency;

                    // Add the data point to the list
                    profitPerDayList.Add(new { x = new DateTimeOffset(date).ToUnixTimeMilliseconds(), y = profitFiat });

                    previousDayCumulativeProfit = dailyPNL.CumulativeProfitCurrency;
                }
            }
            // Convert the list to a JSON string using Newtonsoft.Json
            CumulativeProfitChartDataJSON = Newtonsoft.Json.JsonConvert.SerializeObject(new[] {
                new {
                    key = "Profit in " + PTData.Misc.Market,
                    color = Constants.ChartLineColors[1],
                    values = profitPerDayList
                }
            });
        }
    }
    private void BuildProfitChartData()
    {
        List<object> profitPerDayList = new List<object>();

        if (PTData.DailyPNL.Count > 0)
        {
            // Get timezone offset
            TimeSpan offset;
            bool isNegative = PTMagicConfiguration.GeneralSettings.Application.TimezoneOffset.StartsWith("-");
            string offsetWithoutSign = PTMagicConfiguration.GeneralSettings.Application.TimezoneOffset.TrimStart('+', '-');

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
            DateTime startDate = earliestDataDate;

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
    
    public (double totalMonths, DateTime startDate, DateTime endDate) MonthlyAverages(List<MonthlyStatsData> monthlyStats, List<DailyPNLData> dailyPNL)
    {
        double totalMonths = 0;
        // Get the exact start and end dates of sales data
        DateTime startDate = dailyPNL.Min(d => DateTime.ParseExact(d.Date, "d-M-yyyy", CultureInfo.InvariantCulture));
        DateTime endDate = dailyPNL.Max(d => DateTime.ParseExact(d.Date, "d-M-yyyy", CultureInfo.InvariantCulture));

        int daysInFirstMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month) - startDate.Day + 1;
        int daysInLastMonth = endDate.Day;
        //Console.WriteLine("Start Date: {0}, End Date: {1}, Days in first month: {2}, Days in last month: {3}", startDate, endDate, daysInFirstMonth, daysInLastMonth);

        for (int i = 0; i < monthlyStats.Count; i++)
        {
            var monthStat = monthlyStats[i];
            double weight;

            // Parse the Month property into a DateTime object
            DateTime monthDate = DateTime.ParseExact(monthStat.Month, "M-yyyy", CultureInfo.InvariantCulture);

            // If it's the first or last month in the dataset, calculate the weight based on the number of days
            if (i == 0)
              {
                  // Calculate weight based on the number of days in the dataset for the first month
                  weight = daysInFirstMonth / 30.0;
              }
              else if (i == monthlyStats.Count - 1)
              {
                  // Calculate weight based on the number of days in the dataset for the last month
                  weight = (daysInLastMonth / 30.0);
              }
              else
              {
                  // Otherwise, assume it's a full month
                  weight = 1;
              }
            totalMonths += weight;
            //Console.WriteLine("Month: {0}, Weight: {1}", monthStat.Month, weight);
        }
        return (totalMonths, startDate, endDate);
    }
    private void BuildTopMarkets()
    {
      var markets = PTData.SellLog.GroupBy(m => m.Market);
      Dictionary<string, double> topMarketsDic = new Dictionary<string, double>();
      foreach (var market in markets)
      {
        double totalProfit = 0;
        totalProfit = PTData.SellLog.FindAll(m => m.Market == market.Key).Sum(m => m.Profit);
        topMarketsDic.Add(market.Key, totalProfit);
      }
      TopMarkets = new SortedDictionary<string, double>(topMarketsDic).OrderByDescending(m => m.Value).Take(PTMagicConfiguration.GeneralSettings.Monitor.MaxTopMarkets);
    }
    private void BuildSalesChartData()
    {
      if (PTData.SellLog.Count > 0)
      {
        MinSellLogDate = PTData.SellLog.OrderBy(sl => sl.SoldDate).First().SoldDate.Date;
        DateTime graphStartDate = DateTimeNow.DateTime.Date.AddDays(-1850);
        if (MinSellLogDate > graphStartDate) graphStartDate = MinSellLogDate;

        int tradeDayIndex = 0;
        string tradesPerDayJSON = "";
        for (DateTime salesDate = graphStartDate; salesDate <= DateTimeNow.DateTime.Date; salesDate = salesDate.AddDays(1))
        {
          if (tradeDayIndex > 0)
          {
            tradesPerDayJSON += ",\n";
          }
          int trades = PTData.SellLog.FindAll(t => t.SoldDate.Date == salesDate.Date).Count;
          tradesPerDayJSON += "{x: new Date('" + salesDate.Date.ToString("yyyy-MM-dd") + "'), y: " + trades + "}";
          tradeDayIndex++;
        }
        TradesChartDataJSON = "[";
        TradesChartDataJSON += "{";
        TradesChartDataJSON += "key: 'Sales',";
        TradesChartDataJSON += "color: '" + Constants.ChartLineColors[0] + "',";
        TradesChartDataJSON += "values: [" + tradesPerDayJSON + "]";
        TradesChartDataJSON += "}";
        TradesChartDataJSON += "]";

        
      }
    }
  }
}
