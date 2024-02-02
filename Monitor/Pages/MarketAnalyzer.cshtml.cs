using System;
using System.Collections.Generic;
using System.Linq;
using Core.Main;
using Core.Helper;
using Core.Main.DataObjects;
using Core.Main.DataObjects.PTMagicData;
using System.Globalization;

namespace Monitor.Pages
{
  public class MarketAnalyzerModel : _Internal.BasePageModelSecure
  {
    public List<MarketTrend> MarketTrends { get; set; } = new List<MarketTrend>();
    public ProfitTrailerData PTData = null;
    public MiscData MiscData { get; set; }
    public string TrendChartDataJSON = "";
    public double DataHours { get; set; }

    public void OnGet()
    {
      base.Init();
      BindData();
    }

    private void BindData()
    {
      PTData = this.PtDataObject;
      MiscData = this.PTData.Misc;  
      MarketTrends = PTMagicConfiguration.AnalyzerSettings.MarketAnalyzer.MarketTrends.OrderBy(mt => mt.TrendMinutes).ThenByDescending(mt => mt.Platform).ToList();

      BuildMarketTrendChartData();
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
  }
}
