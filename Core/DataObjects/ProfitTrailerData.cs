using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Diagnostics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Core.Main.DataObjects.PTMagicData;

namespace Core.Main.DataObjects
{

  public class ProfitTrailerData
  {
    private MiscData _misc = null;
    private PropertiesData _properties = null;
    private StatsData _stats = null;
    private List<DailyPNLData> _dailyPNL = new List<DailyPNLData>();
    private List<DailyTCVData> _dailyTCV = new List<DailyTCVData>();
    private List<MonthlyStatsData> _monthlyStats = new List<MonthlyStatsData>();
    private List<ProfitablePairsData> _profitablePairs = new List<ProfitablePairsData>();
    private List<DailyStatsData> _dailyStats = new List<DailyStatsData>();
    private decimal? _totalProfit = null;
    public decimal? TotalProfit
    {
        get { return _totalProfit; }
        set { _totalProfit = value; }
    }
    private decimal? _totalSales = null;
    public decimal? TotalSales
    {
        get { return _totalSales; }
        set { _totalSales = value; }
    }
    //private List<SellLogData> _sellLog = new List<SellLogData>();
    private List<DCALogData> _dcaLog = new List<DCALogData>();
    private List<BuyLogData> _buyLog = new List<BuyLogData>();
    private string _ptmBasePath = "";
    private PTMagicConfiguration _systemConfiguration = null;
    private TransactionData _transactionData = null;
    private DateTime _dailyPNLRefresh = DateTime.UtcNow;
    private DateTime _dailyTCVRefresh = DateTime.UtcNow;
    private DateTime _monthlyStatsRefresh = DateTime.UtcNow;
    private DateTime _statsRefresh = DateTime.UtcNow;
    private DateTime _buyLogRefresh = DateTime.UtcNow;
    //private DateTime _sellLogRefresh = DateTime.UtcNow;
    private DateTime _dcaLogRefresh = DateTime.UtcNow;
    private DateTime _miscRefresh = DateTime.UtcNow;
    private DateTime _propertiesRefresh = DateTime.UtcNow;  
    private DateTime _profitablePairsRefresh = DateTime.UtcNow; 
    private DateTime _dailyStatsRefresh = DateTime.UtcNow; 
    private volatile object _dailyPNLLock = new object();    
    private volatile object _dailyTCVLock = new object();       
    private volatile object _monthlyStatsLock = new object();    
    private volatile object _statsLock = new object();
    private volatile object _buyLock = new object();
    private volatile object _sellLock = new object();
    private volatile object _dcaLock = new object();
    private volatile object _miscLock = new object();
    private volatile object _propertiesLock = new object();  
    private volatile object _profitablePairsLock = new object(); 
    private volatile object _dailyStatsLock = new object();    
    private TimeSpan? _offsetTimeSpan = null;
    public void DoLog(string message)
    {
        // Implement your logging logic here
        Console.WriteLine(message);
    }
    // Constructor
    public ProfitTrailerData(PTMagicConfiguration systemConfiguration)
    {
      _systemConfiguration = systemConfiguration;
    }

    // Get a time span for the UTC offset from the settings
    private TimeSpan OffsetTimeSpan
    {
      get
      {
        if (!_offsetTimeSpan.HasValue)
        {
          // Ensure Misc is populated
          var misc = this.Misc;

          // Get offset from Misc
          _offsetTimeSpan = TimeSpan.Parse(misc.TimeZoneOffset);
        }

        return _offsetTimeSpan.Value;
      }
    }

    // Get the time with the settings UTC offset applied
    private DateTimeOffset LocalizedTime
    {
      get
      {
        return DateTimeOffset.UtcNow.ToOffset(OffsetTimeSpan);
      }
    }

    public MiscData Misc
    {
      get
      {
        if (_misc == null || (DateTime.UtcNow > _miscRefresh))
        {
          lock (_miscLock)
          {
            // Thread double locking
            if (_misc == null || (DateTime.UtcNow > _miscRefresh))
            {
              _misc = BuildMiscData(GetDataFromProfitTrailer("api/v2/data/misc"));
              _miscRefresh = DateTime.UtcNow.AddSeconds(_systemConfiguration.GeneralSettings.Monitor.DashboardChartsRefreshSeconds - 1);
            }
          }
        }
        
        return _misc;
      }
    }
    private MiscData BuildMiscData(dynamic PTData)
    {
      return new MiscData()
      {
        Market = PTData.market,
        FiatConversionRate = PTData.priceDataFiatConversionRate,
        Balance = PTData.realBalance,
        PairsValue = PTData.totalPairsCurrentValue,
        DCAValue = PTData.totalDCACurrentValue,
        PendingValue = PTData.totalPendingCurrentValue,
        DustValue = PTData.totalDustCurrentValue,
        StartBalance = PTData.startBalance,
        TotalCurrentValue = PTData.totalCurrentValue,
        TimeZoneOffset = PTData.timeZoneOffset,
        ExchangeURL = PTData.exchangeUrl,
        PTVersion = PTData.version,
      };
    }
    public List<DailyStatsData> DailyStats
    {
      get
      {
          if (_dailyStats == null || DateTime.UtcNow > _dailyStatsRefresh)
          {
              lock (_dailyStatsLock)
              {
                  if (_dailyStats == null || DateTime.UtcNow > _dailyStatsRefresh)
                  {
                      using (var stream = GetDataFromProfitTrailerAsStream("/api/v2/data/stats"))
                      using (var reader = new StreamReader(stream))
                      using (var jsonReader = new JsonTextReader(reader))
                      {
                          JObject basicSection = null;
                          JObject extraSection = null;

                          while (jsonReader.Read())
                          {
                              if (jsonReader.TokenType == JsonToken.PropertyName)
                              {
                                  if ((string)jsonReader.Value == "basic")
                                  {
                                      jsonReader.Read(); // Move to the value of the "basic" property
                                      basicSection = JObject.Load(jsonReader);
                                  }
                                  else if ((string)jsonReader.Value == "extra")
                                  {
                                      jsonReader.Read(); // Move to the value of the "extra" property
                                      extraSection = JObject.Load(jsonReader);
                                  }
                              }

                              if (basicSection != null && extraSection != null)
                              {
                                  break;
                              }
                          }

                          if (basicSection != null)
                          {
                             if (extraSection != null)
                              {
                                  JArray dailyStatsSection = (JArray)extraSection["dailyStats"];
                                  _dailyStats = dailyStatsSection.Select(j => BuildDailyStatsData(j as JObject)).ToList();
                                  _dailyStatsRefresh = DateTime.UtcNow.AddSeconds(_systemConfiguration.GeneralSettings.Monitor.DashboardChartsRefreshSeconds - 1);
                              }
                          }
                      }
                  }
              }
          }
          return _dailyStats;
        }
    }
    private DailyStatsData BuildDailyStatsData(dynamic dailyStatsDataJson)
    {
        return new DailyStatsData()
        {
            Date = dailyStatsDataJson["date"],
            TotalSales = dailyStatsDataJson["totalSales"],
            TotalBuys = dailyStatsDataJson["totalBuys"],
            TotalProfit = dailyStatsDataJson["totalProfitCurrency"],
            AvgProfit = dailyStatsDataJson["avgProfit"], 
            AvgGrowth = dailyStatsDataJson["avgGrowth"], 
        };
    }

    public PropertiesData Properties
    {
      get
      {
        if (_properties == null || (DateTime.UtcNow > _propertiesRefresh))
        {
          lock (_propertiesLock)
          {
            // Thread double locking
            if (_properties == null || (DateTime.UtcNow > _propertiesRefresh))
            {
              _properties = BuildProptertiesData(GetDataFromProfitTrailer("api/v2/data/properties"));
              _propertiesRefresh = DateTime.UtcNow.AddSeconds(_systemConfiguration.GeneralSettings.Monitor.DashboardChartsRefreshSeconds - 1);
            }
          }
        }

        return _properties;
      }
    }
    private PropertiesData BuildProptertiesData(dynamic PTProperties)
    {
      return new PropertiesData()
      {
        Currency = PTProperties.currency,
        Shorting = PTProperties.shorting,
        Margin = PTProperties.margin,
        UpTime = PTProperties.upTime,
        Port = PTProperties.port,
        IsLeverageExchange = PTProperties.isLeverageExchange,
        BaseUrl = PTProperties.baseUrl
      };
    }

    public StatsData Stats
    {
        get
        {
            if (_stats == null || DateTime.UtcNow > _statsRefresh)
            {
                lock (_statsLock)
                {
                    if (_stats == null || DateTime.UtcNow > _statsRefresh)
                    {
                        using (var stream = GetDataFromProfitTrailerAsStream("/api/v2/data/stats"))
                        using (var reader = new StreamReader(stream))
                        using (var jsonReader = new JsonTextReader(reader))
                        {
                            while (jsonReader.Read())
                            {
                                if (jsonReader.TokenType == JsonToken.PropertyName && (string)jsonReader.Value == "basic")
                                {
                                    jsonReader.Read(); // Move to the value of the "basic" property
                                    JObject basicSection = JObject.Load(jsonReader);
                                    _stats = BuildStatsData(basicSection);
                                    _statsRefresh = DateTime.UtcNow.AddSeconds(_systemConfiguration.GeneralSettings.Monitor.DashboardChartsRefreshSeconds - 1);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            return _stats;
        }
    }
    private StatsData BuildStatsData(dynamic statsDataJson)
    {
        return new StatsData()
        {
            SalesToday = statsDataJson["totalSalesToday"],
            ProfitToday = statsDataJson["totalProfitToday"],
            ProfitPercToday = statsDataJson["totalProfitPercToday"],
            SalesYesterday = statsDataJson["totalSalesYesterday"],
            ProfitYesterday = statsDataJson["totalProfitYesterday"],
            ProfitPercYesterday = statsDataJson["totalProfitPercYesterday"],
            SalesWeek = statsDataJson["totalSalesWeek"],
            ProfitWeek = statsDataJson["totalProfitWeek"],
            ProfitPercWeek = statsDataJson["totalProfitPercWeek"],
            SalesThisMonth = statsDataJson["totalSalesThisMonth"],
            ProfitThisMonth = statsDataJson["totalProfitThisMonth"],
            ProfitPercThisMonth = statsDataJson["totalProfitPercThisMonth"],
            SalesLastMonth = statsDataJson["totalSalesLastMonth"],
            ProfitLastMonth = statsDataJson["totalProfitLastMonth"],
            ProfitPercLastMonth = statsDataJson["totalProfitPercLastMonth"],
            TotalProfit = statsDataJson["totalProfit"],
            TotalSales = statsDataJson["totalSales"],
            TotalProfitPerc = statsDataJson["totalProfitPerc"],
            FundingToday = statsDataJson["totalFundingToday"],
            FundingYesterday = statsDataJson["totalFundingYesterday"],
            FundingWeek = statsDataJson["totalFundingWeek"],
            FundingThisMonth = statsDataJson["totalFundingThisMonth"],
            FundingLastMonth = statsDataJson["totalFundingLastMonth"],
            FundingTotal = statsDataJson["totalFunding"],
            TotalFundingPerc = statsDataJson["totalFundingPerc"],
            TotalFundingPercYesterday = statsDataJson["totalFundingPercYesterday"],
            TotalFundingPercWeek = statsDataJson["totalFundingPercWeekPerc"],
            TotalFundingPercToday = statsDataJson["totalFundingPercTodayPerc"]
        };
    }
    public List<DailyPNLData> DailyPNL
    {
      get
      {
          if (_dailyPNL == null || DateTime.UtcNow > _dailyPNLRefresh)
          {
              lock (_dailyPNLLock)
              {
                  if (_dailyPNL == null || DateTime.UtcNow > _dailyPNLRefresh)
                  {
                      using (var stream = GetDataFromProfitTrailerAsStream("/api/v2/data/stats"))
                      using (var reader = new StreamReader(stream))
                      using (var jsonReader = new JsonTextReader(reader))
                      {
                          JObject basicSection = null;
                          JObject extraSection = null;

                          while (jsonReader.Read())
                          {
                              if (jsonReader.TokenType == JsonToken.PropertyName)
                              {
                                  if ((string)jsonReader.Value == "basic")
                                  {
                                      jsonReader.Read(); // Move to the value of the "basic" property
                                      basicSection = JObject.Load(jsonReader);
                                  }
                                  else if ((string)jsonReader.Value == "extra")
                                  {
                                      jsonReader.Read(); // Move to the value of the "extra" property
                                      extraSection = JObject.Load(jsonReader);
                                  }
                              }

                              if (basicSection != null && extraSection != null)
                              {
                                  break;
                              }
                          }

                          if (basicSection != null && 
                              ((_totalProfit == null || 
                              !Decimal.Equals(_totalProfit.Value, basicSection["totalProfit"].Value<decimal>())) ||
                              (_totalSales == null || 
                              !Decimal.Equals(_totalSales.Value, basicSection["totalSales"].Value<decimal>()))))
                          {
                              _totalProfit = basicSection["totalProfit"].Value<decimal>();
                              _totalSales = basicSection["totalSales"].Value<decimal>();

                              if (extraSection != null)
                              {
                                  JArray dailyPNLSection = (JArray)extraSection["dailyPNLStats"];
                                  _dailyPNL = dailyPNLSection.Select(j => BuildDailyPNLData(j as JObject)).ToList();
                                  _dailyPNLRefresh = DateTime.UtcNow.AddSeconds(_systemConfiguration.GeneralSettings.Monitor.DashboardChartsRefreshSeconds - 1);
                              }
                          }
                      }
                  }
              }
          }
          return _dailyPNL;
        }
    }
    private DailyPNLData BuildDailyPNLData(dynamic dailyPNLDataJson)
    {
        return new DailyPNLData()
        {
            Date = dailyPNLDataJson["date"],
            CumulativeProfitCurrency = dailyPNLDataJson["cumulativeProfitCurrency"],
            Order = dailyPNLDataJson["order"],
        };
    }
    public List<ProfitablePairsData> ProfitablePairs
    {
        get
        {
            if (_profitablePairs == null || DateTime.UtcNow > _profitablePairsRefresh)
            {
                lock (_profitablePairsLock)
                {
                    if (_profitablePairs == null || DateTime.UtcNow > _profitablePairsRefresh)
                    {
                        using (var stream = GetDataFromProfitTrailerAsStream("/api/v2/data/stats"))
                        using (var reader = new StreamReader(stream))
                        using (var jsonReader = new JsonTextReader(reader))
                        {
                            JObject basicSection = null;
                            JObject extraSection = null;
                            while (jsonReader.Read())
                            {
                                if (jsonReader.TokenType == JsonToken.PropertyName)
                                {
                                    if ((string)jsonReader.Value == "basic")
                                    {
                                        jsonReader.Read(); // Move to the value of the "basic" property
                                        basicSection = JObject.Load(jsonReader);
                                    }
                                    else if ((string)jsonReader.Value == "extra")
                                    {
                                        jsonReader.Read(); // Move to the value of the "extra" property
                                        extraSection = JObject.Load(jsonReader);
                                    }
                                }

                                if (basicSection != null && extraSection != null)
                                {
                                    break;
                                }
                            }
                            if (basicSection != null) 
                              {
                                if (extraSection != null)
                                  {
                                      JObject profitablePairsSection = (JObject)extraSection["profitablePairs"];
                                      _profitablePairs = new List<ProfitablePairsData>();
                                      int counter = 0;
                                      foreach (var j in profitablePairsSection)
                                      {
                                          if (counter >= _systemConfiguration.GeneralSettings.Monitor.MaxTopMarkets)
                                          {
                                              break;
                                          }
                                          // Process each JObject in the dictionary
                                          JObject profitablePair = (JObject)j.Value;
                                          _profitablePairs.Add(BuildProfitablePairs(profitablePair));
                                          counter++;
                                      }
                                      _profitablePairsRefresh = DateTime.UtcNow.AddSeconds(_systemConfiguration.GeneralSettings.Monitor.DashboardChartsRefreshSeconds - 1);
                                  }
                            }
                        }
                    }
                }
            }
            return _profitablePairs;
        }
    }
    private ProfitablePairsData BuildProfitablePairs(JObject profitablePairsJson)
    {
        return new ProfitablePairsData()
        {
            Coin = profitablePairsJson["coin"].Value<string>(),
            ProfitCurrency = profitablePairsJson["profitCurrency"].Value<double>(),
            SoldTimes = profitablePairsJson["soldTimes"].Value<int>(),
            Avg = profitablePairsJson["avg"].Value<double>(),
        };
    }

    public List<DailyTCVData> DailyTCV
    {
      get
      {
          if (_dailyTCV == null || DateTime.UtcNow > _dailyTCVRefresh)
          {
              lock (_dailyTCVLock)
              {
                  if (_dailyTCV == null || DateTime.UtcNow > _dailyTCVRefresh)
                  {
                      using (var stream = GetDataFromProfitTrailerAsStream("/api/v2/data/stats"))
                      using (var reader = new StreamReader(stream))
                      using (var jsonReader = new JsonTextReader(reader))
                      {
                        JObject basicSection = null;
                        JObject extraSection = null;

                          while (jsonReader.Read())
                          {
                            if (jsonReader.TokenType == JsonToken.PropertyName)
                            {
                                if ((string)jsonReader.Value == "basic")
                                {
                                    jsonReader.Read(); // Move to the value of the "basic" property
                                    basicSection = JObject.Load(jsonReader);
                                }
                                else if ((string)jsonReader.Value == "extra")
                                {
                                    jsonReader.Read(); // Move to the value of the "extra" property
                                    extraSection = JObject.Load(jsonReader);
                                }
                            }

                            if (basicSection != null && extraSection != null)
                            {
                                break;
                            }
                          }

                          if (basicSection != null)
                            {
                              if (extraSection != null)
                              {
                                  JArray dailyTCVSection = (JArray)extraSection["dailyTCVStats"];
                                  _dailyTCV = dailyTCVSection.Select(j => BuildDailyTCVData(j as JObject)).ToList();
                                  _dailyTCVRefresh = DateTime.UtcNow.AddSeconds(_systemConfiguration.GeneralSettings.Monitor.DashboardChartsRefreshSeconds - 1);
                              }
                            }
                      }
                  }
              }
          }
          return _dailyTCV;
        }
    }
    public int GetTotalTCVDays()
    {
        return DailyTCV?.Count ?? 0;
    }
    private DailyTCVData BuildDailyTCVData(dynamic dailyTCVDataJson)
    {
        return new DailyTCVData()
        {
            Date = dailyTCVDataJson["date"],
            TCV = dailyTCVDataJson["TCV"],
            Order = dailyTCVDataJson["order"],
        };
    }
    public List<MonthlyStatsData> MonthlyStats
    {
        get
        {
            if (_monthlyStats == null || DateTime.UtcNow > _monthlyStatsRefresh)
            {
                lock (_monthlyStatsLock)
                {
                    if (_monthlyStats == null || DateTime.UtcNow > _monthlyStatsRefresh)
                    {
                        using (var stream = GetDataFromProfitTrailerAsStream("/api/v2/data/stats"))
                        using (var reader = new StreamReader(stream))
                        using (var jsonReader = new JsonTextReader(reader))
                        {
                            JObject basicSection = null;
                            JObject extraSection = null;

                            while (jsonReader.Read())
                            {
                                if (jsonReader.TokenType == JsonToken.PropertyName)
                                {
                                    if ((string)jsonReader.Value == "basic")
                                    {
                                        jsonReader.Read(); // Move to the value of the "basic" property
                                        basicSection = JObject.Load(jsonReader);
                                    }
                                    else if ((string)jsonReader.Value == "extra")
                                    {
                                        jsonReader.Read(); // Move to the value of the "extra" property
                                        extraSection = JObject.Load(jsonReader);
                                    }
                                }

                                if (basicSection != null && extraSection != null)
                                {
                                    break;
                                }
                            }

                            if (basicSection != null)// && 
                              {
                                if (extraSection != null)
                                {
                                    JArray monthlyStatsSection = (JArray)extraSection["monthlyStats"];
                                    _monthlyStats = monthlyStatsSection.Select(j => BuildMonthlyStatsData(j as JObject)).ToList();
                                    _monthlyStatsRefresh = DateTime.UtcNow.AddSeconds(_systemConfiguration.GeneralSettings.Monitor.DashboardChartsRefreshSeconds - 1);
                                }
                            }
                        }
                    }
                }
            }
            return _monthlyStats;
        }
    }

    private MonthlyStatsData BuildMonthlyStatsData(dynamic monthlyStatsDataJson)
    {
        return new MonthlyStatsData()
        {
            Month = monthlyStatsDataJson["month"],
            TotalSales = monthlyStatsDataJson["totalSales"],
            TotalProfitCurrency = monthlyStatsDataJson["totalProfitCurrency"],
            AvgGrowth = monthlyStatsDataJson["avgGrowth"],
            Order = monthlyStatsDataJson["order"],
        };
    }

    public List<DCALogData> DCALog
    {
      get
      {
        if (_dcaLog == null || (DateTime.UtcNow > _dcaLogRefresh))
        {
          lock (_dcaLock)
          {
            // Thread double locking
            if (_dcaLog == null || (DateTime.UtcNow > _dcaLogRefresh))
            {
              dynamic dcaData = null, pairsData = null, pendingData = null, watchData = null;
              _dcaLog.Clear();

              Parallel.Invoke(() =>
              {
                dcaData = GetDataFromProfitTrailer("/api/v2/data/dca", true);
              },
              () =>
              {
                pairsData = GetDataFromProfitTrailer("/api/v2/data/pairs", true);
              },
              () =>
              {
                pendingData = GetDataFromProfitTrailer("/api/v2/data/pending", true);
              },
              () =>
              {
                watchData = GetDataFromProfitTrailer("/api/v2/data/watchmode", true);
              });

              this.BuildDCALogData(dcaData, pairsData, pendingData, watchData);
              _dcaLogRefresh = DateTime.UtcNow.AddSeconds(_systemConfiguration.GeneralSettings.Monitor.BagAnalyzerRefreshSeconds - 1);
            }
          }
        }

        return _dcaLog;
      }
    }
    
    public List<BuyLogData> BuyLog
    {
      get
      {
          if (_systemConfiguration.GeneralSettings.Monitor.MaxDashboardBuyEntries == 0)
          {
              return _buyLog;
          }

          if (_buyLog == null || (DateTime.UtcNow > _buyLogRefresh))
          {
              lock (_buyLock)
              {
                  if (_buyLog == null || (DateTime.UtcNow > _buyLogRefresh))
                  {
                      _buyLog.Clear();
                      this.BuildBuyLogData(GetDataFromProfitTrailer("/api/v2/data/pbl", true));
                      _buyLogRefresh = DateTime.UtcNow.AddSeconds(_systemConfiguration.GeneralSettings.Monitor.BuyAnalyzerRefreshSeconds - 1);
                  }
              }
          }

          return _buyLog;
      }
    }

    public TransactionData TransactionData
    {
      get
      {
        if (_transactionData == null) _transactionData = new TransactionData(_ptmBasePath);
        return _transactionData;
      }
    }

    public double GetCurrentBalance()
    {
      return
      (this.Misc.Balance);
    }
    public double GetPairsBalance()
    {
      return
      (this.Misc.PairsValue);
    }
    public double GetDCABalance()
    {
      return
      (this.Misc.DCAValue);
    }
    public double GetPendingBalance()
    {
      return
      (this.Misc.PendingValue);
    }
    public double GetDustBalance()
    {
      return
      (this.Misc.DustValue);
    }
    
    
    // public double GetSnapshotBalance(DateTime snapshotDateTime)
    // {
    //   double result = _misc.StartBalance;
      
    //   result += this.SellLog.FindAll(sl => sl.SoldDate.Date < snapshotDateTime.Date).Sum(sl => sl.Profit);
    //   result += this.TransactionData.Transactions.FindAll(t => t.UTCDateTime < snapshotDateTime).Sum(t => t.Amount);
      
    //   // Calculate holdings for snapshot date
    //   result += this.DCALog.FindAll(pairs => pairs.FirstBoughtDate <= snapshotDateTime).Sum(pairs => pairs.CurrentValue);
     
    //   return result;
    // }
    
    private dynamic GetDataFromProfitTrailer(string callPath, bool arrayReturned = false)
    {
      string rawBody = "";
      string url = string.Format("{0}{1}{2}token={3}", _systemConfiguration.GeneralSettings.Application.ProfitTrailerMonitorURL, 
      callPath,
      callPath.Contains("?") ? "&" : "?", 
      _systemConfiguration.GeneralSettings.Application.ProfitTrailerServerAPIToken);
      
      // Get the data from PT
      Debug.WriteLine(String.Format("{0} - Calling '{1}'", DateTime.UtcNow, url));
      HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
      request.AutomaticDecompression = DecompressionMethods.GZip;
      request.KeepAlive = true;
      
      WebResponse response = request.GetResponse();
      
      using (Stream dataStream = response.GetResponseStream())
      {
        StreamReader reader = new StreamReader(dataStream);
        rawBody = reader.ReadToEnd();
        reader.Close();
      }

      response.Close();
      
      
      if (!arrayReturned)
        {
            return JObject.Parse(rawBody);
        }
        else
        {
            return JArray.Parse(rawBody);
        }
    }
    private Stream GetDataFromProfitTrailerAsStream(string callPath)
    {
        string url = string.Format("{0}{1}{2}token={3}", _systemConfiguration.GeneralSettings.Application.ProfitTrailerMonitorURL, 
        callPath,
        callPath.Contains("?") ? "&" : "?", 
        _systemConfiguration.GeneralSettings.Application.ProfitTrailerServerAPIToken);
        
        // Get the data from PT
        Debug.WriteLine(String.Format("{0} - Calling '{1}'", DateTime.UtcNow, url));
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        request.AutomaticDecompression = DecompressionMethods.GZip;
        request.KeepAlive = true;
        
        WebResponse response = request.GetResponse();
        
        return response.GetResponseStream();
    }

    
    // private void BuildSellLogData(dynamic rawSellLogData)
    // {
    //   foreach (var rsld in rawSellLogData.data)
    //   {
    //     SellLogData sellLogData = new SellLogData();
    //     sellLogData.SoldAmount = rsld.soldAmount;
    //     sellLogData.BoughtTimes = rsld.boughtTimes;
    //     sellLogData.Market = rsld.market;
    //     sellLogData.ProfitPercent = rsld.profit;
    //     sellLogData.SoldPrice = rsld.currentPrice;
    //     sellLogData.AverageBuyPrice = rsld.avgPrice;
    //     sellLogData.TotalCost = rsld.totalCost;
    //     sellLogData.Profit = rsld.profitCurrency;
        
        
    //     //Convert Unix Timestamp to Datetime
    //     System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
    //     dtDateTime = dtDateTime.AddSeconds((double)rsld.soldDate).ToUniversalTime();
        
        
    //     // Profit Trailer sales are saved in UTC
    //     DateTimeOffset ptSoldDate = DateTimeOffset.Parse(dtDateTime.Year.ToString() + "-" + dtDateTime.Month.ToString("00") + "-" + dtDateTime.Day.ToString("00") + "T" + dtDateTime.Hour.ToString("00") + ":" + dtDateTime.Minute.ToString("00") + ":" + dtDateTime.Second.ToString("00"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
        
        
    //     // Convert UTC sales time to local offset time
       
    //     ptSoldDate = ptSoldDate.ToOffset(OffsetTimeSpan);
        
    //     sellLogData.SoldDate = ptSoldDate.DateTime;
       
    //     _sellLog.Add(sellLogData);
    //   }
    // }

    private void BuildDCALogData(dynamic rawDCALogData, dynamic rawPairsLogData, dynamic rawPendingLogData, dynamic rawWatchModeLogData)
    {
      // Parse DCA data
      _dcaLog.AddRange(ParsePairsData(rawDCALogData, true));
      
      // Parse Pairs data
      _dcaLog.AddRange(ParsePairsData(rawPairsLogData, false));
      
      // Parse pending pairs data
      _dcaLog.AddRange(ParsePairsData(rawPendingLogData, false));
      
      // Parse watch only pairs data
      _dcaLog.AddRange(ParsePairsData(rawWatchModeLogData, false));
    }

    // Parse the pairs data from PT to our own common data structure.
    private List<DCALogData> ParsePairsData(dynamic pairsData, bool processBuyStrategies)
    {
      List<DCALogData> pairs = new List<DCALogData>();
      
      foreach (var pair in pairsData)
      {
        DCALogData dcaLogData = new DCALogData();
        dcaLogData.Amount = pair.totalAmount;
        dcaLogData.BoughtTimes = pair.boughtTimes;
        dcaLogData.Market = pair.market;
        dcaLogData.ProfitPercent = pair.profit;
        dcaLogData.AverageBuyPrice = pair.avgPrice;
        dcaLogData.TotalCost = pair.totalCost;
        dcaLogData.BuyTriggerPercent = pair.buyProfit;
        dcaLogData.CurrentLowBBValue = pair.bbLow == null ? 0 : pair.bbLow;
        dcaLogData.CurrentHighBBValue = pair.highBb == null ? 0 : pair.highBb;
        dcaLogData.BBTrigger = pair.bbTrigger == null ? 0 : pair.bbTrigger;
        dcaLogData.CurrentPrice = pair.currentPrice;
        dcaLogData.SellTrigger = pair.triggerValue == null ? 0 : pair.triggerValue;
        dcaLogData.PercChange = pair.percChange;
        dcaLogData.Leverage = pair.leverage == null ? 0 : pair.leverage;
        dcaLogData.BuyStrategy = pair.buyStrategy == null ? "" : pair.buyStrategy;
        dcaLogData.SellStrategy = pair.sellStrategy == null ? "" : pair.sellStrategy;
        dcaLogData.IsTrailing = false;
        
        // See if they are using PT 2.5 (buyStrategiesData) or 2.4 (buyStrategies)
        var buyStrats = pair.buyStrategies != null ? pair.buyStrategies : pair.buyStrategiesData.data;
        if (buyStrats != null && processBuyStrategies)
        {
          foreach (var bs in buyStrats)
          {
            Strategy buyStrategy = new Strategy();
            buyStrategy.Type = bs.type;
            buyStrategy.Name = bs.name;
            buyStrategy.EntryValue = bs.entryValue;
            buyStrategy.EntryValueLimit = bs.entryValueLimit;
            buyStrategy.TriggerValue = bs.triggerValue;
            buyStrategy.CurrentValue = bs.currentValue;
            buyStrategy.CurrentValuePercentage = bs.currentValuePercentage;
            buyStrategy.Decimals = bs.decimals;
            buyStrategy.IsTrailing = bs.trailing;
            buyStrategy.IsTrue = bs.strategyResult;
           
            dcaLogData.BuyStrategies.Add(buyStrategy);
          }
        }

        // See if they are using PT 2.5 (sellStrategiesData) or 2.4 (sellStrategies)
        var sellStrats = pair.sellStrategies != null ? pair.sellStrategies : pair.sellStrategiesData.data;
        if (sellStrats != null)
        {
          foreach (var ss in sellStrats)
          {
            Strategy sellStrategy = new Strategy();
            sellStrategy.Type = ss.type;
            sellStrategy.Name = ss.name;
            sellStrategy.EntryValue = ss.entryValue;
            sellStrategy.EntryValueLimit = ss.entryValueLimit;
            sellStrategy.TriggerValue = ss.triggerValue;
            sellStrategy.CurrentValue = ss.currentValue;
            sellStrategy.CurrentValuePercentage = ss.currentValuePercentage;
            sellStrategy.Decimals = ss.decimals;
            sellStrategy.IsTrailing = ss.trailing;
            sellStrategy.IsTrue = ss.strategyResult;
            
            dcaLogData.SellStrategies.Add(sellStrategy);
            
            // Find the target percentage gain to sell.
            if (sellStrategy.Name.Contains("GAIN", StringComparison.InvariantCultureIgnoreCase))
            {
              if (!dcaLogData.TargetGainValue.HasValue || dcaLogData.TargetGainValue.Value > sellStrategy.EntryValue)
              {
                // Set the target sell percentage
                dcaLogData.TargetGainValue = sellStrategy.EntryValue;
              }
            }
          }
        }
        
        // Calculate current value
        dcaLogData.CurrentValue = dcaLogData.CurrentPrice * dcaLogData.Amount;
        
        // Convert Unix Timestamp to Datetime
        System.DateTime rdldDateTime = new DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
        rdldDateTime = rdldDateTime.AddSeconds((double)pair.firstBoughtDate).ToUniversalTime();
        
        // Profit Trailer bought times are saved in UTC
        if (pair.firstBoughtDate > 0)
        {
          DateTimeOffset ptFirstBoughtDate = DateTimeOffset.Parse(rdldDateTime.Year.ToString() + "-" + rdldDateTime.Month.ToString("00") + "-" + rdldDateTime.Day.ToString("00") + "T" + rdldDateTime.Hour.ToString("00") + ":" + rdldDateTime.Minute.ToString("00") + ":" + rdldDateTime.Second.ToString("00"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

          // Convert UTC bought time to local offset time
          ptFirstBoughtDate = ptFirstBoughtDate.ToOffset(OffsetTimeSpan);

          dcaLogData.FirstBoughtDate = ptFirstBoughtDate.DateTime;
        }
        else
        {
          dcaLogData.FirstBoughtDate = Constants.confMinDate;
        }
        
        _dcaLog.Add(dcaLogData);
      }

      return pairs;
    }

    private void BuildBuyLogData(dynamic rawBuyLogData)
    {
      foreach (var rbld in rawBuyLogData)
      {
        BuyLogData buyLogData = new BuyLogData() { IsTrailing = false, IsTrue = false, IsSom = false, TrueStrategyCount = 0 };
        buyLogData.Market = rbld.market;
        buyLogData.ProfitPercent = rbld.profit;
        buyLogData.CurrentPrice = rbld.currentPrice;
        buyLogData.PercChange = rbld.percChange;
        buyLogData.Volume24h = rbld.volume;
        
        if (rbld.positive != null)
        {
          buyLogData.IsTrailing = ((string)(rbld.positive)).IndexOf("trailing", StringComparison.InvariantCultureIgnoreCase) > -1;
          buyLogData.IsTrue = ((string)(rbld.positive)).IndexOf("true", StringComparison.InvariantCultureIgnoreCase) > -1;
        }
        else
        {
          // Parse buy strategies
          
          // See if they are using PT 2.5 (buyStrategiesData) or 2.4 (buyStrategies)
          var buyStrats = rbld.buyStrategies != null ? rbld.buyStrategies : rbld.buyStrategiesData.data;
          
          if (buyStrats != null)
          {
            foreach (var bs in buyStrats)
            {
              Strategy buyStrategy = new Strategy();
              buyStrategy.Type = bs.type;
              buyStrategy.Name = bs.name;
              buyStrategy.EntryValue = bs.entryValue;
              buyStrategy.EntryValueLimit = bs.entryValueLimit;
              buyStrategy.TriggerValue = bs.triggerValue;
              buyStrategy.CurrentValue = bs.currentValue;
              buyStrategy.CurrentValuePercentage = bs.currentValuePercentage;
              buyStrategy.Decimals = bs.decimals;
              buyStrategy.IsTrailing = bs.trailing;
              buyStrategy.IsTrue = bs.strategyResult;
              
              // Is SOM?
              buyLogData.IsSom = buyLogData.IsSom || buyStrategy.Name.Contains("som enabled", StringComparison.OrdinalIgnoreCase);
              
              // Is the pair trailing?
              buyLogData.IsTrailing = buyLogData.IsTrailing || buyStrategy.IsTrailing;
              buyLogData.IsTrue = buyLogData.IsTrue || buyStrategy.IsTrue;
              
              // True status strategy count total
              buyLogData.TrueStrategyCount += buyStrategy.IsTrue ? 1 : 0;
              
              // Add
              buyLogData.BuyStrategies.Add(buyStrategy);
            }
          }
        }

        _buyLog.Add(buyLogData);
      }
    }
  }
}
