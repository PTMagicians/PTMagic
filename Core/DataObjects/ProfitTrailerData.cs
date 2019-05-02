using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Core.Main.DataObjects.PTMagicData;

namespace Core.Main.DataObjects
{

  public class ProfitTrailerData
  {
    private SummaryData _summary = null;
    private List<SellLogData> _sellLog = new List<SellLogData>();
    private List<DCALogData> _dcaLog = new List<DCALogData>();
    private List<BuyLogData> _buyLog = new List<BuyLogData>();
    private string _ptmBasePath = "";
    private PTMagicConfiguration _systemConfiguration = null;
    private TransactionData _transactionData = null;
    private DateTimeOffset _dateTimeNow = Constants.confMinDate;

    public ProfitTrailerData(PTMagicConfiguration systemConfiguration)
    {
      _systemConfiguration = systemConfiguration;

      string html = "";
      string url = systemConfiguration.GeneralSettings.Application.ProfitTrailerMonitorURL + "api/data?token=" + systemConfiguration.GeneralSettings.Application.ProfitTrailerServerAPIToken;

      // Get the data from PT
      HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
      request.AutomaticDecompression = DecompressionMethods.GZip;
      request.KeepAlive = true;

      WebResponse response = request.GetResponse();
      Stream dataStream = response.GetResponseStream();
      StreamReader reader = new StreamReader(dataStream);
      html = reader.ReadToEnd();
      reader.Close();
      response.Close();

      // Parse the JSON and build the data sets
      dynamic rawPTData = JObject.Parse(html);

      Parallel.Invoke(() =>
      {
        _summary = BuildSummaryData(rawPTData);
      },
      () =>
      {
        if (rawPTData.sellLogData != null)
        {
          this.BuildSellLogData(rawPTData.sellLogData, _systemConfiguration);
        }
      },
      () =>
      {
        if (rawPTData.bbBuyLogData != null)
        {
          this.BuildBuyLogData(rawPTData.bbBuyLogData);
        }
      },
      () =>
      {
        if (rawPTData.dcaLogData != null)
        {
          this.BuildDCALogData(rawPTData.dcaLogData, rawPTData.gainLogData, _systemConfiguration);
        }
      });

      // Convert local offset time to UTC
      TimeSpan offsetTimeSpan = TimeSpan.Parse(systemConfiguration.GeneralSettings.Application.TimezoneOffset.Replace("+", ""));
      _dateTimeNow = DateTimeOffset.UtcNow.ToOffset(offsetTimeSpan);
    }

    public SummaryData Summary
    {
      get
      {
        return _summary;
      }
    }
    public List<SellLogData> SellLog
    {
      get
      {
        return _sellLog;
      }
    }

    public List<SellLogData> SellLogToday
    {
      get
      {
        return _sellLog.FindAll(sl => sl.SoldDate.Date == _dateTimeNow.DateTime.Date);
      }
    }

    public List<SellLogData> SellLogYesterday
    {
      get
      {
        return _sellLog.FindAll(sl => sl.SoldDate.Date == _dateTimeNow.DateTime.AddDays(-1).Date);
      }
    }

    public List<SellLogData> SellLogLast7Days
    {
      get
      {
        return _sellLog.FindAll(sl => sl.SoldDate.Date >= _dateTimeNow.DateTime.AddDays(-7).Date);
      }
    }

    public List<SellLogData> SellLogLast30Days
    {
      get
      {
        return _sellLog.FindAll(sl => sl.SoldDate.Date >= _dateTimeNow.DateTime.AddDays(-30).Date);
      }
    }

    public List<DCALogData> DCALog
    {
      get
      {
        return _dcaLog;
      }
    }

    public List<BuyLogData> BuyLog
    {
      get
      {
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
      (this.Summary.Balance +
      this.Summary.PairsValue +
      this.Summary.DCAValue +
      this.Summary.PendingValue +
      this.Summary.DustValue);
    }
     public double GetPairsBalance()
    {
      return 
      (this.Summary.PairsValue);
    }
     public double GetDCABalance()
    {
      return 
      (this.Summary.DCAValue);
    }
     public double GetPendingBalance()
    {
      return 
      (this.Summary.PendingValue);
    }
    public double GetDustBalance()
    {
      return 
      (this.Summary.DustValue);
    }



    public double GetSnapshotBalance(DateTime snapshotDateTime)
    {
      double result = _systemConfiguration.GeneralSettings.Application.StartBalance;

      result += this.SellLog.FindAll(sl => sl.SoldDate.Date < snapshotDateTime.Date).Sum(sl => sl.Profit);
      result += this.TransactionData.Transactions.FindAll(t => t.UTCDateTime < snapshotDateTime).Sum(t => t.Amount);

      // Calculate holdings for snapshot date
      result += this.DCALog.FindAll(pairs => pairs.FirstBoughtDate <= snapshotDateTime).Sum(pairs => pairs.CurrentValue);

      return result;
    }

    private SummaryData BuildSummaryData(dynamic PTData)
    {
      return new SummaryData()
      {
        Market = PTData.market,
        Balance = PTData.realBalance,
        PairsValue = PTData.totalPairsCurrentValue,
        DCAValue = PTData.totalDCACurrentValue,
        PendingValue = PTData.totalPendingCurrentValue,
        DustValue = PTData.totalDustCurrentValue
      };
    }

    private void BuildSellLogData(dynamic rawSellLogData, PTMagicConfiguration systemConfiguration)
    {
      foreach (var rsld in rawSellLogData)
      {
        SellLogData sellLogData = new SellLogData();
        sellLogData.SoldAmount = rsld.soldAmount;
        sellLogData.BoughtTimes = rsld.boughtTimes;
        sellLogData.Market = rsld.market;
        sellLogData.ProfitPercent = rsld.profit;
        sellLogData.SoldPrice = rsld.currentPrice;
        sellLogData.AverageBuyPrice = rsld.avgPrice;
        sellLogData.TotalCost = sellLogData.SoldAmount * sellLogData.AverageBuyPrice;

        double soldValueRaw = (sellLogData.SoldAmount * sellLogData.SoldPrice);
        double soldValueAfterFees = soldValueRaw - (soldValueRaw * ((double)rsld.fee / 100));
        sellLogData.SoldValue = soldValueAfterFees;
        sellLogData.Profit = Math.Round(sellLogData.SoldValue - sellLogData.TotalCost, 8);

        //Convert Unix Timestamp to Datetime
        System.DateTime dtDateTime = new DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
        dtDateTime = dtDateTime.AddSeconds((double)rsld.soldDate).ToUniversalTime();

        // Profit Trailer sales are saved in UTC
        DateTimeOffset ptSoldDate = DateTimeOffset.Parse(dtDateTime.Year.ToString() + "-" + dtDateTime.Month.ToString("00") + "-" + dtDateTime.Day.ToString("00") + "T" + dtDateTime.Hour.ToString("00") + ":" + dtDateTime.Minute.ToString("00") + ":" + dtDateTime.Second.ToString("00"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

        // Convert UTC sales time to local offset time
        TimeSpan offsetTimeSpan = TimeSpan.Parse(systemConfiguration.GeneralSettings.Application.TimezoneOffset.Replace("+", ""));
        ptSoldDate = ptSoldDate.ToOffset(offsetTimeSpan);

        sellLogData.SoldDate = ptSoldDate.DateTime;

        _sellLog.Add(sellLogData);
      }
    }

    private void BuildDCALogData(dynamic rawDCALogData, dynamic rawPairsLogData, PTMagicConfiguration systemConfiguration)
    {
      foreach (var rdld in rawDCALogData)
      {
        DCALogData dcaLogData = new DCALogData();
        dcaLogData.Amount = rdld.totalAmount;
        dcaLogData.BoughtTimes = rdld.boughtTimes;
        dcaLogData.Market = rdld.market;
        dcaLogData.ProfitPercent = rdld.profit;
        dcaLogData.AverageBuyPrice = rdld.avgPrice;
        dcaLogData.TotalCost = rdld.totalCost;
        dcaLogData.BuyTriggerPercent = rdld.buyProfit;
        dcaLogData.CurrentLowBBValue = rdld.bbLow == null ? 0 : rdld.bbLow;
        dcaLogData.CurrentHighBBValue = rdld.highBb == null ? 0 : rdld.highBb;
        dcaLogData.BBTrigger = rdld.bbTrigger == null ? 0 : rdld.bbTrigger;
        dcaLogData.CurrentPrice = rdld.currentPrice;
        dcaLogData.SellTrigger = rdld.triggerValue == null ? 0 : rdld.triggerValue;
        dcaLogData.PercChange = rdld.percChange;
        dcaLogData.BuyStrategy = rdld.buyStrategy == null ? "" : rdld.buyStrategy;
        dcaLogData.SellStrategy = rdld.sellStrategy == null ? "" : rdld.sellStrategy;

        if (rdld.positive != null)
        {
          dcaLogData.IsTrailing = ((string)rdld.positive).IndexOf("trailing", StringComparison.InvariantCultureIgnoreCase) > -1;
          dcaLogData.IsTrue = ((string)rdld.positive).IndexOf("true", StringComparison.InvariantCultureIgnoreCase) > -1;
        }
        else
        {
          if (rdld.buyStrategies != null)
          {
            foreach (var bs in rdld.buyStrategies)
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
              buyStrategy.IsTrailing = ((string)bs.positive).IndexOf("trailing", StringComparison.InvariantCultureIgnoreCase) > -1;
              buyStrategy.IsTrue = ((string)bs.positive).IndexOf("true", StringComparison.InvariantCultureIgnoreCase) > -1;

              dcaLogData.BuyStrategies.Add(buyStrategy);
            }
          }

          if (rdld.sellStrategies != null)
          {
            foreach (var ss in rdld.sellStrategies)
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
              sellStrategy.IsTrailing = ((string)ss.positive).IndexOf("trailing", StringComparison.InvariantCultureIgnoreCase) > -1;
              sellStrategy.IsTrue = ((string)ss.positive).IndexOf("true", StringComparison.InvariantCultureIgnoreCase) > -1;

              dcaLogData.SellStrategies.Add(sellStrategy);
            }
          }
        }

        // Calculate current value
        dcaLogData.CurrentValue = dcaLogData.CurrentPrice * dcaLogData.Amount;

        //Convert Unix Timestamp to Datetime
        System.DateTime rdldDateTime = new DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
        rdldDateTime = rdldDateTime.AddSeconds((double)rdld.firstBoughtDate).ToUniversalTime();

        // Profit Trailer bought times are saved in UTC
        if (rdld.firstBoughtDate > 0)
        {
          DateTimeOffset ptFirstBoughtDate = DateTimeOffset.Parse(rdldDateTime.Year.ToString() + "-" + rdldDateTime.Month.ToString("00") + "-" + rdldDateTime.Day.ToString("00") + "T" + rdldDateTime.Hour.ToString("00") + ":" + rdldDateTime.Minute.ToString("00") + ":" + rdldDateTime.Second.ToString("00"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

          // Convert UTC bought time to local offset time
          TimeSpan offsetTimeSpan = TimeSpan.Parse(systemConfiguration.GeneralSettings.Application.TimezoneOffset.Replace("+", ""));
          ptFirstBoughtDate = ptFirstBoughtDate.ToOffset(offsetTimeSpan);

          dcaLogData.FirstBoughtDate = ptFirstBoughtDate.DateTime;
        }
        else
        {
          dcaLogData.FirstBoughtDate = Constants.confMinDate;
        }

        _dcaLog.Add(dcaLogData);
      }

      foreach (var rpld in rawPairsLogData)
      {
        DCALogData dcaLogData = new DCALogData();
        dcaLogData.Amount = rpld.totalAmount;
        dcaLogData.BoughtTimes = 0;
        dcaLogData.Market = rpld.market;
        dcaLogData.ProfitPercent = rpld.profit;
        dcaLogData.AverageBuyPrice = rpld.avgPrice;
        dcaLogData.TotalCost = rpld.totalCost;
        dcaLogData.BuyTriggerPercent = rpld.buyProfit;
        dcaLogData.CurrentPrice = rpld.currentPrice;
        dcaLogData.SellTrigger = rpld.triggerValue == null ? 0 : rpld.triggerValue;
        dcaLogData.PercChange = rpld.percChange;
        dcaLogData.BuyStrategy = rpld.buyStrategy == null ? "" : rpld.buyStrategy;
        dcaLogData.SellStrategy = rpld.sellStrategy == null ? "" : rpld.sellStrategy;
        dcaLogData.IsTrailing = false;

        if (rpld.sellStrategies != null)
        {
          foreach (var ss in rpld.sellStrategies)
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
            sellStrategy.IsTrailing = ((string)ss.positive).IndexOf("trailing", StringComparison.InvariantCultureIgnoreCase) > -1;
            sellStrategy.IsTrue = ((string)ss.positive).IndexOf("true", StringComparison.InvariantCultureIgnoreCase) > -1;

            dcaLogData.SellStrategies.Add(sellStrategy);
          }
        }

        //Convert Unix Timestamp to Datetime
        System.DateTime rpldDateTime = new DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
        rpldDateTime = rpldDateTime.AddSeconds((double)rpld.firstBoughtDate).ToUniversalTime();

        // Profit Trailer bought times are saved in UTC
        if (rpld.firstBoughtDate > 0)
        {
          DateTimeOffset ptFirstBoughtDate = DateTimeOffset.Parse(rpldDateTime.Year.ToString() + "-" + rpldDateTime.Month.ToString("00") + "-" + rpldDateTime.Day.ToString("00") + "T" + rpldDateTime.Hour.ToString("00") + ":" + rpldDateTime.Minute.ToString("00") + ":" + rpldDateTime.Second.ToString("00"), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

          // Convert UTC bought time to local offset time
          TimeSpan offsetTimeSpan = TimeSpan.Parse(systemConfiguration.GeneralSettings.Application.TimezoneOffset.Replace("+", ""));
          ptFirstBoughtDate = ptFirstBoughtDate.ToOffset(offsetTimeSpan);

          dcaLogData.FirstBoughtDate = ptFirstBoughtDate.DateTime;
        }
        else
        {
          dcaLogData.FirstBoughtDate = Constants.confMinDate;
        }

        _dcaLog.Add(dcaLogData);
      }
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

        if (rbld.positive != null)
        {
          buyLogData.IsTrailing = ((string)(rbld.positive)).IndexOf("trailing", StringComparison.InvariantCultureIgnoreCase) > -1;
          buyLogData.IsTrue = ((string)(rbld.positive)).IndexOf("true", StringComparison.InvariantCultureIgnoreCase) > -1;
        }
        else
        {
          if (rbld.buyStrategies != null)
          {
            foreach (var bs in rbld.buyStrategies)
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
              buyStrategy.IsTrailing = ((string)(bs.positive)).IndexOf("trailing", StringComparison.InvariantCultureIgnoreCase) > -1;
              buyStrategy.IsTrue = ((string)(bs.positive)).IndexOf("true", StringComparison.InvariantCultureIgnoreCase) > -1;

              // Is SOM?
              buyLogData.IsSom = buyLogData.IsSom || buyStrategy.Name.Equals("som enabled", StringComparison.OrdinalIgnoreCase);

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
