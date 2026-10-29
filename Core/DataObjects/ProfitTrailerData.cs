using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Core.Helper;
using Core.Main;
using Core.Main.DataObjects;
using Core.MarketAnalyzer;
using Core.ProfitTrailer;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Core.Main.DataObjects
{
  public class ProfitTrailerData
  {
    private static readonly object _lock = new object();
    private static ProfitTrailerData _instance;

    private readonly PTMagic _ptMagic;
    private readonly LogHelper _log;
    private readonly DateTime _lastChanged = DateTime.MinValue;
    private DateTime _lastDcaLogLoad = DateTime.MinValue;
    private DateTime _lastPairsLogLoad = DateTime.MinValue;
    private DateTime _lastPendingLogLoad = DateTime.MinValue;
    private DateTime _lastBuyLogLoad = DateTime.MinValue;
    private DateTime _lastPossibleBuyLogLoad = DateTime.MinValue;
    private DateTime _lastSalesLogLoad = DateTime.MinValue;
    private DateTime _lastDCALogLoad = DateTime.MinValue;
    private List<PairLogData> _pairsLog = new List<PairLogData>();
    private List<PendingLogData> _pendingLog = new List<PendingLogData>();
    private List<BuyLogData> _buyLog = new List<BuyLogData>();
    private List<BuyLogData> _possibleBuyLog = new List<BuyLogData>();
    private List<SalesLogData> _salesLog = new List<SalesLogData>();
    private List<DCALogData> _dcaLog = new List<DCALogData>();
    private GlobalSettingsData _globalSettings = new GlobalSettingsData();
    private int _marketCount = 0;
    private int _marketCountBuy = 0;
    private decimal _totalCurrentValue = 0;
    private decimal _totalBoughtCost = 0;
    private decimal _totalProfit = 0;
    private decimal _totalProfitPercent = 0;
    private decimal _totalProfitToday = 0;
    private decimal _totalProfitYesterday = 0;
    private decimal _totalProfit7Days = 0;
    private decimal _totalProfit30Days = 0;
    private decimal _totalProfit365Days = 0;
    private decimal _totalProfitAll = 0;
    private decimal _totalProfitAllPercentage = 0;
    private decimal _totalProfitYesterdayPercentage = 0;
    private decimal _totalProfit7DaysPercentage = 0;
    private decimal _totalProfit30DaysPercentage = 0;
    private decimal _totalProfit365DaysPercentage = 0;
    private decimal _totalProfitTodayPercentage = 0;
    private decimal _totalProfitTodayBoughtCost = 0;
    private decimal _totalProfitYesterdayBoughtCost = 0;
    private decimal _totalProfit7DaysBoughtCost = 0;
    private decimal _totalProfit30DaysBoughtCost = 0;
    private decimal _totalProfit365DaysBoughtCost = 0;
    private decimal _totalProfitAllBoughtCost = 0;
    private decimal _totalCurrentValueBTC = 0;
    private decimal _totalBoughtCostBTC = 0;
    private decimal _totalProfitBTC = 0;
    private decimal _totalProfitTodayBTC = 0;
    private decimal _totalProfitYesterdayBTC = 0;
    private decimal _totalProfit7DaysBTC = 0;
    private decimal _totalProfit30DaysBTC = 0;
    private decimal _totalProfit365DaysBTC = 0;
    private decimal _totalProfitAllBTC = 0;
    private decimal _totalProfitTodayBoughtCostBTC = 0;
    private decimal _totalProfitYesterdayBoughtCostBTC = 0;
    private decimal _totalProfit7DaysBoughtCostBTC = 0;
    private decimal _totalProfit30DaysBoughtCostBTC = 0;
    private decimal _totalProfit365DaysBoughtCostBTC = 0;
    private decimal _totalProfitAllBoughtCostBTC = 0;
    private decimal _totalCurrentValueUSD = 0;
    private decimal _totalBoughtCostUSD = 0;
    private decimal _totalProfitUSD = 0;
    private decimal _totalProfitTodayUSD = 0;
    private decimal _totalProfitYesterdayUSD = 0;
    private decimal _totalProfit7DaysUSD = 0;
    private decimal _totalProfit30DaysUSD = 0;
    private decimal _totalProfit365DaysUSD = 0;
    private decimal _totalProfitAllUSD = 0;
    private decimal _totalProfitTodayBoughtCostUSD = 0;
    private decimal _totalProfitYesterdayBoughtCostUSD = 0;
    private decimal _totalProfit7DaysBoughtCostUSD = 0;
    private decimal _totalProfit30DaysBoughtCostUSD = 0;
    private decimal _totalProfit365DaysBoughtCostUSD = 0;
    private decimal _totalProfitAllBoughtCostUSD = 0;
    private decimal _totalCurrentValueUSDT = 0;
    private decimal _totalBoughtCostUSDT = 0;
    private decimal _totalProfitUSDT = 0;
    private decimal _totalProfitTodayUSDT = 0;
    private decimal _totalProfitYesterdayUSDT = 0;
    private decimal _totalProfit7DaysUSDT = 0;
    private decimal _totalProfit30DaysUSDT = 0;
    private decimal _totalProfit365DaysUSDT = 0;
    private decimal _totalProfitAllUSDT = 0;
    private decimal _totalProfitTodayBoughtCostUSDT = 0;
    private decimal _totalProfitYesterdayBoughtCostUSDT = 0;
    private decimal _totalProfit7DaysBoughtCostUSDT = 0;
    private decimal _totalProfit30DaysBoughtCostUSDT = 0;
    private decimal _totalProfit365DaysBoughtCostUSDT = 0;
    private decimal _totalProfitAllBoughtCostUSDT = 0;
    private decimal _totalCurrentValueUSDC = 0;
    private decimal _totalBoughtCostUSDC = 0;
    private decimal _totalProfitUSDC = 0;
    private decimal _totalProfitTodayUSDC = 0;
    private decimal _totalProfitYesterdayUSDC = 0;
    private decimal _totalProfit7DaysUSDC = 0;
    private decimal _totalProfit30DaysUSDC = 0;
    private decimal _totalProfit365DaysUSDC = 0;
    private decimal _totalProfitAllUSDC = 0;
    private decimal _totalProfitTodayBoughtCostUSDC = 0;
    private decimal _totalProfitYesterdayBoughtCostUSDC = 0;
    private decimal _totalProfit7DaysBoughtCostUSDC = 0;
    private decimal _totalProfit30DaysBoughtCostUSDC = 0;
    private decimal _totalProfit365DaysBoughtCostUSDC = 0;
    private decimal _totalProfitAllBoughtCostUSDC = 0;
    private decimal _totalCurrentValueBNB = 0;
    private decimal _totalBoughtCostBNB = 0;
    private decimal _totalProfitBNB = 0;
    private decimal _totalProfitTodayBNB = 0;
    private decimal _totalProfitYesterdayBNB = 0;
    private decimal _totalProfit7DaysBNB = 0;
    private decimal _totalProfit30DaysBNB = 0;
    private decimal _totalProfit365DaysBNB = 0;
    private decimal _totalProfitAllBNB = 0;
    private decimal _totalProfitTodayBoughtCostBNB = 0;
    private decimal _totalProfitYesterdayBoughtCostBNB = 0;
    private decimal _totalProfit7DaysBoughtCostBNB = 0;
    private decimal _totalProfit30DaysBoughtCostBNB = 0;
    private decimal _totalProfit365DaysBoughtCostBNB = 0;
    private decimal _totalProfitAllBoughtCostBNB = 0;
    private decimal _totalCurrentValueETH = 0;
    private decimal _totalBoughtCostETH = 0;
    private decimal _totalProfitETH = 0;
    private decimal _totalProfitTodayETH = 0;
    private decimal _totalProfitYesterdayETH = 0;
    private decimal _totalProfit7DaysETH = 0;
    private decimal _totalProfit30DaysETH = 0;
    private decimal _totalProfit365DaysETH = 0;
    private decimal _totalProfitAllETH = 0;
    private decimal _totalProfitTodayBoughtCostETH = 0;
    private decimal _totalProfitYesterdayBoughtCostETH = 0;
    private decimal _totalProfit7DaysBoughtCostETH = 0;
    private decimal _totalProfit30DaysBoughtCostETH = 0;
    private decimal _totalProfit365DaysBoughtCostETH = 0;
    private decimal _totalProfitAllBoughtCostETH = 0;
    private decimal _balance = 0;
    private decimal _balanceUSD = 0;
    private decimal _balanceUSDT = 0;
    private decimal _balanceUSDC = 0;
    private decimal _balanceBTC = 0;
    private decimal _balanceBNB = 0;
    private decimal _balanceETH = 0;
    private decimal _availableBalance = 0;
    private decimal _availableBalanceUSD = 0;
    private decimal _availableBalanceUSDT = 0;
    private decimal _availableBalanceUSDC = 0;
    private decimal _availableBalanceBTC = 0;
    private decimal _availableBalanceBNB = 0;
    private decimal _availableBalanceETH = 0;
    private decimal _pendingOrderCost = 0;
    private decimal _pendingOrderCostUSD = 0;
    private decimal _pendingOrderCostUSDT = 0;
    private decimal _pendingOrderCostUSDC = 0;
    private decimal _pendingOrderCostBTC = 0;
    private decimal _pendingOrderCostBNB = 0;
    private decimal _pendingOrderCostETH = 0;
    private decimal _exchangeFee = 0;
    private decimal _exchangeFeeUSD = 0;
    private decimal _exchangeFeeUSDT = 0;
    private decimal _exchangeFeeUSDC = 0;
    private decimal _exchangeFeeBTC = 0;
    private decimal _exchangeFeeBNB = 0;
    private decimal _exchangeFeeETH = 0;
    private decimal _marketValue = 0;
    private decimal _marketValueUSD = 0;
    private decimal _marketValueUSDT = 0;
    private decimal _marketValueUSDC = 0;
    private decimal _marketValueBTC = 0;
    private decimal _marketValueBNB = 0;
    private decimal _marketValueETH = 0;
    private TimeSpan _offsetTimeSpan = new TimeSpan();

    private ProfitTrailerData(PTMagic ptMagic)
    {
      _ptMagic = ptMagic;
      _log = _ptMagic.Log;
    }

    public static ProfitTrailerData Instance(PTMagic ptMagic)
    {
      lock (_lock)
      {
        if (_instance == null)
        {
          _instance = new ProfitTrailerData(ptMagic);
        }
      }
      return _instance;
    }

    public TimeSpan OffsetTimeSpan
    {
      get { return _offsetTimeSpan; }
      set { _offsetTimeSpan = value; }
    }

    public List<PairLogData> PairsLog
    {
      get
      {
        if ((DateTime.Now - _lastPairsLogLoad).TotalSeconds > 5)
        {
          LoadPairsLog();
        }
        return _pairsLog;
      }
    }

    public List<PendingLogData> PendingLog
    {
      get
      {
        if ((DateTime.Now - _lastPendingLogLoad).TotalSeconds > 5)
        {
          LoadPendingLog();
        }
        return _pendingLog;
      }
    }

    public List<BuyLogData> BuyLog
    {
      get
      {
        if ((DateTime.Now - _lastBuyLogLoad).TotalSeconds > 5)
        {
          LoadBuyLog();
        }
        return _buyLog;
      }
    }

    public List<BuyLogData> PossibleBuyLog
    {
      get
      {
        if ((DateTime.Now - _lastPossibleBuyLogLoad).TotalSeconds > 5)
        {
          LoadPossibleBuyLog();
        }
        return _possibleBuyLog;
      }
    }

    public List<SalesLogData> SalesLog
    {
      get
      {
        if ((DateTime.Now - _lastSalesLogLoad).TotalSeconds > 5)
        {
          LoadSalesLog();
        }
        return _salesLog;
      }
    }

    public List<DCALogData> DCALog
    {
      get
      {
        if ((DateTime.Now - _lastDCALogLoad).TotalSeconds > 5)
        {
          LoadDCALog();
        }
        return _dcaLog;
      }
    }

    public GlobalSettingsData GlobalSettings
    {
      get
      {
        return _globalSettings;
      }
    }

    public int MarketCount
    {
      get { return _marketCount; }
      set { _marketCount = value; }
    }

    public int MarketCountBuy
    {
      get { return _marketCountBuy; }
      set { _marketCountBuy = value; }
    }

    public decimal TotalCurrentValue
    {
      get { return _totalCurrentValue; }
      set { _totalCurrentValue = value; }
    }

    public decimal TotalBoughtCost
    {
      get { return _totalBoughtCost; }
      set { _totalBoughtCost = value; }
    }

    public decimal TotalProfit
    {
      get { return _totalProfit; }
      set { _totalProfit = value; }
    }

    public decimal TotalProfitPercent
    {
      get { return _totalProfitPercent; }
      set { _totalProfitPercent = value; }
    }

    public decimal TotalProfitToday
    {
      get { return _totalProfitToday; }
      set { _totalProfitToday = value; }
    }

    public decimal TotalProfitYesterday
    {
      get { return _totalProfitYesterday; }
      set { _totalProfitYesterday = value; }
    }

    public decimal TotalProfit7Days
    {
      get { return _totalProfit7Days; }
      set { _totalProfit7Days = value; }
    }

    public decimal TotalProfit30Days
    {
      get { return _totalProfit30Days; }
      set { _totalProfit30Days = value; }
    }

    public decimal TotalProfit365Days
    {
      get { return _totalProfit365Days; }
      set { _totalProfit365Days = value; }
    }

    public decimal TotalProfitAll
    {
      get { return _totalProfitAll; }
      set { _totalProfitAll = value; }
    }

    public decimal TotalProfitAllPercentage
    {
      get { return _totalProfitAllPercentage; }
      set { _totalProfitAllPercentage = value; }
    }

    public decimal TotalProfitYesterdayPercentage
    {
      get { return _totalProfitYesterdayPercentage; }
      set { _totalProfitYesterdayPercentage = value; }
    }

    public decimal TotalProfit7DaysPercentage
    {
      get { return _totalProfit7DaysPercentage; }
      set { _totalProfit7DaysPercentage = value; }
    }

    public decimal TotalProfit30DaysPercentage
    {
      get { return _totalProfit30DaysPercentage; }
      set { _totalProfit30DaysPercentage = value; }
    }

    public decimal TotalProfit365DaysPercentage
    {
      get { return _totalProfit365DaysPercentage; }
      set { _totalProfit365DaysPercentage = value; }
    }

    public decimal TotalProfitTodayPercentage
    {
      get { return _totalProfitTodayPercentage; }
      set { _totalProfitTodayPercentage = value; }
    }

    public decimal TotalProfitTodayBoughtCost
    {
      get { return _totalProfitTodayBoughtCost; }
      set { _totalProfitTodayBoughtCost = value; }
    }

    public decimal TotalProfitYesterdayBoughtCost
    {
      get { return _totalProfitYesterdayBoughtCost; }
      set { _totalProfitYesterdayBoughtCost = value; }
    }

    public decimal TotalProfit7DaysBoughtCost
    {
      get { return _totalProfit7DaysBoughtCost; }
      set { _totalProfit7DaysBoughtCost = value; }
    }

    public decimal TotalProfit30DaysBoughtCost
    {
      get { return _totalProfit30DaysBoughtCost; }
      set { _totalProfit30DaysBoughtCost = value; }
    }

    public decimal TotalProfit365DaysBoughtCost
    {
      get { return _totalProfit365DaysBoughtCost; }
      set { _totalProfit365DaysBoughtCost = value; }
    }

    public decimal TotalProfitAllBoughtCost
    {
      get { return _totalProfitAllBoughtCost; }
      set { _totalProfitAllBoughtCost = value; }
    }

    public decimal TotalCurrentValueBTC
    {
      get { return _totalCurrentValueBTC; }
      set { _totalCurrentValueBTC = value; }
    }

    public decimal TotalBoughtCostBTC
    {
      get { return _totalBoughtCostBTC; }
      set { _totalBoughtCostBTC = value; }
    }

    public decimal TotalProfitBTC
    {
      get { return _totalProfitBTC; }
      set { _totalProfitBTC = value; }
    }

    public decimal TotalProfitTodayBTC
    {
      get { return _totalProfitTodayBTC; }
      set { _totalProfitTodayBTC = value; }
    }

    public decimal TotalProfitYesterdayBTC
    {
      get { return _totalProfitYesterdayBTC; }
      set { _totalProfitYesterdayBTC = value; }
    }

    public decimal TotalProfit7DaysBTC
    {
      get { return _totalProfit7DaysBTC; }
      set { _totalProfit7DaysBTC = value; }
    }

    public decimal TotalProfit30DaysBTC
    {
      get { return _totalProfit30DaysBTC; }
      set { _totalProfit30DaysBTC = value; }
    }

    public decimal TotalProfit365DaysBTC
    {
      get { return _totalProfit365DaysBTC; }
      set { _totalProfit365DaysBTC = value; }
    }

    public decimal TotalProfitAllBTC
    {
      get { return _totalProfitAllBTC; }
      set { _totalProfitAllBTC = value; }
    }

    public decimal TotalProfitTodayBoughtCostBTC
    {
      get { return _totalProfitTodayBoughtCostBTC; }
      set { _totalProfitTodayBoughtCostBTC = value; }
    }

    public decimal TotalProfitYesterdayBoughtCostBTC
    {
      get { return _totalProfitYesterdayBoughtCostBTC; }
      set { _totalProfitYesterdayBoughtCostBTC = value; }
    }

    public decimal TotalProfit7DaysBoughtCostBTC
    {
      get { return _totalProfit7DaysBoughtCostBTC; }
      set { _totalProfit7DaysBoughtCostBTC = value; }
    }

    public decimal TotalProfit30DaysBoughtCostBTC
    {
      get { return _totalProfit30DaysBoughtCostBTC; }
      set { _totalProfit30DaysBoughtCostBTC = value; }
    }

    public decimal TotalProfit365DaysBoughtCostBTC
    {
      get { return _totalProfit365DaysBoughtCostBTC; }
      set { _totalProfit365DaysBoughtCostBTC = value; }
    }

    public decimal TotalProfitAllBoughtCostBTC
    {
      get { return _totalProfitAllBoughtCostBTC; }
      set { _totalProfitAllBoughtCostBTC = value; }
    }

    public decimal TotalCurrentValueUSD
    {
      get { return _totalCurrentValueUSD; }
      set { _totalCurrentValueUSD = value; }
    }

    public decimal TotalBoughtCostUSD
    {
      get { return _totalBoughtCostUSD; }
      set { _totalBoughtCostUSD = value; }
    }

    public decimal TotalProfitUSD
    {
      get { return _totalProfitUSD; }
      set { _totalProfitUSD = value; }
    }

    public decimal TotalProfitTodayUSD
    {
      get { return _totalProfitTodayUSD; }
      set { _totalProfitTodayUSD = value; }
    }

    public decimal TotalProfitYesterdayUSD
    {
      get { return _totalProfitYesterdayUSD; }
      set { _totalProfitYesterdayUSD = value; }
    }

    public decimal TotalProfit7DaysUSD
    {
      get { return _totalProfit7DaysUSD; }
      set { _totalProfit7DaysUSD = value; }
    }

    public decimal TotalProfit30DaysUSD
    {
      get { return _totalProfit30DaysUSD; }
      set { _totalProfit30DaysUSD = value; }
    }

    public decimal TotalProfit365DaysUSD
    {
      get { return _totalProfit365DaysUSD; }
      set { _totalProfit365DaysUSD = value; }
    }

    public decimal TotalProfitAllUSD
    {
      get { return _totalProfitAllUSD; }
      set { _totalProfitAllUSD = value; }
    }

    public decimal TotalProfitTodayBoughtCostUSD
    {
      get { return _totalProfitTodayBoughtCostUSD; }
      set { _totalProfitTodayBoughtCostUSD = value; }
    }

    public decimal TotalProfitYesterdayBoughtCostUSD
    {
      get { return _totalProfitYesterdayBoughtCostUSD; }
      set { _totalProfitYesterdayBoughtCostUSD = value; }
    }

    public decimal TotalProfit7DaysBoughtCostUSD
    {
      get { return _totalProfit7DaysBoughtCostUSD; }
      set { _totalProfit7DaysBoughtCostUSD = value; }
    }

    public decimal TotalProfit30DaysBoughtCostUSD
    {
      get { return _totalProfit30DaysBoughtCostUSD; }
      set { _totalProfit30DaysBoughtCostUSD = value; }
    }

    public decimal TotalProfit365DaysBoughtCostUSD
    {
      get { return _totalProfit365DaysBoughtCostUSD; }
      set { _totalProfit365DaysBoughtCostUSD = value; }
    }

    public decimal TotalProfitAllBoughtCostUSD
    {
      get { return _totalProfitAllBoughtCostUSD; }
      set { _totalProfitAllBoughtCostUSD = value; }
    }

    public decimal TotalCurrentValueUSDT
    {
      get { return _totalCurrentValueUSDT; }
      set { _totalCurrentValueUSDT = value; }
    }

    public decimal TotalBoughtCostUSDT
    {
      get { return _totalBoughtCostUSDT; }
      set { _totalBoughtCostUSDT = value; }
    }

    public decimal TotalProfitUSDT
    {
      get { return _totalProfitUSDT; }
      set { _totalProfitUSDT = value; }
    }

    public decimal TotalProfitTodayUSDT
    {
      get { return _totalProfitTodayUSDT; }
      set { _totalProfitTodayUSDT = value; }
    }

    public decimal TotalProfitYesterdayUSDT
    {
      get { return _totalProfitYesterdayUSDT; }
      set { _totalProfitYesterdayUSDT = value; }
    }

    public decimal TotalProfit7DaysUSDT
    {
      get { return _totalProfit7DaysUSDT; }
      set { _totalProfit7DaysUSDT = value; }
    }

    public decimal TotalProfit30DaysUSDT
    {
      get { return _totalProfit30DaysUSDT; }
      set { _totalProfit30DaysUSDT = value; }
    }

    public decimal TotalProfit365DaysUSDT
    {
      get { return _totalProfit365DaysUSDT; }
      set { _totalProfit365DaysUSDT = value; }
    }

    public decimal TotalProfitAllUSDT
    {
      get { return _totalProfitAllUSDT; }
      set { _totalProfitAllUSDT = value; }
    }

    public decimal TotalProfitTodayBoughtCostUSDT
    {
      get { return _totalProfitTodayBoughtCostUSDT; }
      set { _totalProfitTodayBoughtCostUSDT = value; }
    }

    public decimal TotalProfitYesterdayBoughtCostUSDT
    {
      get { return _totalProfitYesterdayBoughtCostUSDT; }
      set { _totalProfitYesterdayBoughtCostUSDT = value; }
    }

    public decimal TotalProfit7DaysBoughtCostUSDT
    {
      get { return _totalProfit7DaysBoughtCostUSDT; }
      set { _totalProfit7DaysBoughtCostUSDT = value; }
    }

    public decimal TotalProfit30DaysBoughtCostUSDT
    {
      get { return _totalProfit30DaysBoughtCostUSDT; }
      set { _totalProfit30DaysBoughtCostUSDT = value; }
    }

    public decimal TotalProfit365DaysBoughtCostUSDT
    {
      get { return _totalProfit365DaysBoughtCostUSDT; }
      set { _totalProfit365DaysBoughtCostUSDT = value; }
    }

    public decimal TotalProfitAllBoughtCostUSDT
    {
      get { return _totalProfitAllBoughtCostUSDT; }
      set { _totalProfitAllBoughtCostUSDT = value; }
    }

    public decimal TotalCurrentValueUSDC
    {
      get { return _totalCurrentValueUSDC; }
      set { _totalCurrentValueUSDC = value; }
    }

    public decimal TotalBoughtCostUSDC
    {
      get { return _totalBoughtCostUSDC; }
      set { _totalBoughtCostUSDC = value; }
    }

    public decimal TotalProfitUSDC
    {
      get { return _totalProfitUSDC; }
      set { _totalProfitUSDC = value; }
    }

    public decimal TotalProfitTodayUSDC
    {
      get { return _totalProfitTodayUSDC; }
      set { _totalProfitTodayUSDC = value; }
    }

    public decimal TotalProfitYesterdayUSDC
    {
      get { return _totalProfitYesterdayUSDC; }
      set { _totalProfitYesterdayUSDC = value; }
    }

    public decimal TotalProfit7DaysUSDC
    {
      get { return _totalProfit7DaysUSDC; }
      set { _totalProfit7DaysUSDC = value; }
    }

    public decimal TotalProfit30DaysUSDC
    {
      get { return _totalProfit30DaysUSDC; }
      set { _totalProfit30DaysUSDC = value; }
    }

    public decimal TotalProfit365DaysUSDC
    {
      get { return _totalProfit365DaysUSDC; }
      set { _totalProfit365DaysUSDC = value; }
    }

    public decimal TotalProfitAllUSDC
    {
      get { return _totalProfitAllUSDC; }
      set { _totalProfitAllUSDC = value; }
    }

    public decimal TotalProfitTodayBoughtCostUSDC
    {
      get { return _totalProfitTodayBoughtCostUSDC; }
      set { _totalProfitTodayBoughtCostUSDC = value; }
    }

    public decimal TotalProfitYesterdayBoughtCostUSDC
    {
      get { return _totalProfitYesterdayBoughtCostUSDC; }
      set { _totalProfitYesterdayBoughtCostUSDC = value; }
    }

    public decimal TotalProfit7DaysBoughtCostUSDC
    {
      get { return _totalProfit7DaysBoughtCostUSDC; }
      set { _totalProfit7DaysBoughtCostUSDC = value; }
    }

    public decimal TotalProfit30DaysBoughtCostUSDC
    {
      get { return _totalProfit30DaysBoughtCostUSDC; }
      set { _totalProfit30DaysBoughtCostUSDC = value; }
    }

    public decimal TotalProfit365DaysBoughtCostUSDC
    {
      get { return _totalProfit365DaysBoughtCostUSDC; }
      set { _totalProfit365DaysBoughtCostUSDC = value; }
    }

    public decimal TotalProfitAllBoughtCostUSDC
    {
      get { return _totalProfitAllBoughtCostUSDC; }
      set { _totalProfitAllBoughtCostUSDC = value; }
    }

    public decimal TotalCurrentValueBNB
    {
      get { return _totalCurrentValueBNB; }
      set { _totalCurrentValueBNB = value; }
    }

    public decimal TotalBoughtCostBNB
    {
      get { return _totalBoughtCostBNB; }
      set { _totalBoughtCostBNB = value; }
    }

    public decimal TotalProfitBNB
    {
      get { return _totalProfitBNB; }
      set { _totalProfitBNB = value; }
    }

    public decimal TotalProfitTodayBNB
    {
      get { return _totalProfitTodayBNB; }
      set { _totalProfitTodayBNB = value; }
    }

    public decimal TotalProfitYesterdayBNB
    {
      get { return _totalProfitYesterdayBNB; }
      set { _totalProfitYesterdayBNB = value; }
    }

    public decimal TotalProfit7DaysBNB
    {
      get { return _totalProfit7DaysBNB; }
      set { _totalProfit7DaysBNB = value; }
    }

    public decimal TotalProfit30DaysBNB
    {
      get { return _totalProfit30DaysBNB; }
      set { _totalProfit30DaysBNB = value; }
    }

    public decimal TotalProfit365DaysBNB
    {
      get { return _totalProfit365DaysBNB; }
      set { _totalProfit365DaysBNB = value; }
    }

    public decimal TotalProfitAllBNB
    {
      get { return _totalProfitAllBNB; }
      set { _totalProfitAllBNB = value; }
    }

    public decimal TotalProfitTodayBoughtCostBNB
    {
      get { return _totalProfitTodayBoughtCostBNB; }
      set { _totalProfitTodayBoughtCostBNB = value; }
    }

    public decimal TotalProfitYesterdayBoughtCostBNB
    {
      get { return _totalProfitYesterdayBoughtCostBNB; }
      set { _totalProfitYesterdayBoughtCostBNB = value; }
    }

    public decimal TotalProfit7DaysBoughtCostBNB
    {
      get { return _totalProfit7DaysBoughtCostBNB; }
      set { _totalProfit7DaysBoughtCostBNB = value; }
    }

    public decimal TotalProfit30DaysBoughtCostBNB
    {
      get { return _totalProfit30DaysBoughtCostBNB; }
      set { _totalProfit30DaysBoughtCostBNB = value; }
    }

    public decimal TotalProfit365DaysBoughtCostBNB
    {
      get { return _totalProfit365DaysBoughtCostBNB; }
      set { _totalProfit365DaysBoughtCostBNB = value; }
    }

    public decimal TotalProfitAllBoughtCostBNB
    {
      get { return _totalProfitAllBoughtCostBNB; }
      set { _totalProfitAllBoughtCostBNB = value; }
    }

    public decimal TotalCurrentValueETH
    {
      get { return _totalCurrentValueETH; }
      set { _totalCurrentValueETH = value; }
    }

    public decimal TotalBoughtCostETH
    {
      get { return _totalBoughtCostETH; }
      set { _totalBoughtCostETH = value; }
    }

    public decimal TotalProfitETH
    {
      get { return _totalProfitETH; }
      set { _totalProfitETH = value; }
    }

    public decimal TotalProfitTodayETH
    {
      get { return _totalProfitTodayETH; }
      set { _totalProfitTodayETH = value; }
    }

    public decimal TotalProfitYesterdayETH
    {
      get { return _totalProfitYesterdayETH; }
      set { _totalProfitYesterdayETH = value; }
    }

    public decimal TotalProfit7DaysETH
    {
      get { return _totalProfit7DaysETH; }
      set { _totalProfit7DaysETH = value; }
    }

    public decimal TotalProfit30DaysETH
    {
      get { return _totalProfit30DaysETH; }
      set { _totalProfit30DaysETH = value; }
    }

    public decimal TotalProfit365DaysETH
    {
      get { return _totalProfit365DaysETH; }
      set { _totalProfit365DaysETH = value; }
    }

    public decimal TotalProfitAllETH
    {
      get { return _totalProfitAllETH; }
      set { _totalProfitAllETH = value; }
    }

    public decimal TotalProfitTodayBoughtCostETH
    {
      get { return _totalProfitTodayBoughtCostETH; }
      set { _totalProfitTodayBoughtCostETH = value; }
    }

    public decimal TotalProfitYesterdayBoughtCostETH
    {
      get { return _totalProfitYesterdayBoughtCostETH; }
      set { _totalProfitYesterdayBoughtCostETH = value; }
    }

    public decimal TotalProfit7DaysBoughtCostETH
    {
      get { return _totalProfit7DaysBoughtCostETH; }
      set { _totalProfit7DaysBoughtCostETH = value; }
    }

    public decimal TotalProfit30DaysBoughtCostETH
    {
      get { return _totalProfit30DaysBoughtCostETH; }
      set { _totalProfit30DaysBoughtCostETH = value; }
    }

    public decimal TotalProfit365DaysBoughtCostETH
    {
      get { return _totalProfit365DaysBoughtCostETH; }
      set { _totalProfit365DaysBoughtCostETH = value; }
    }

    public decimal TotalProfitAllBoughtCostETH
    {
      get { return _totalProfitAllBoughtCostETH; }
      set { _totalProfitAllBoughtCostETH = value; }
    }

    public decimal Balance
    {
      get { return _balance; }
      set { _balance = value; }
    }

    public decimal BalanceUSD
    {
      get { return _balanceUSD; }
      set { _balanceUSD = value; }
    }

    public decimal BalanceUSDT
    {
      get { return _balanceUSDT; }
      set { _balanceUSDT = value; }
    }

    public decimal BalanceUSDC
    {
      get { return _balanceUSDC; }
      set { _balanceUSDC = value; }
    }

    public decimal BalanceBTC
    {
      get { return _balanceBTC; }
      set { _balanceBTC = value; }
    }

    public decimal BalanceBNB
    {
      get { return _balanceBNB; }
      set { _balanceBNB = value; }
    }

    public decimal BalanceETH
    {
      get { return _balanceETH; }
      set { _balanceETH = value; }
    }

    public decimal AvailableBalance
    {
      get { return _availableBalance; }
      set { _availableBalance = value; }
    }

    public decimal AvailableBalanceUSD
    {
      get { return _availableBalanceUSD; }
      set { _availableBalanceUSD = value; }
    }

    public decimal AvailableBalanceUSDT
    {
      get { return _availableBalanceUSDT; }
      set { _availableBalanceUSDT = value; }
    }

    public decimal AvailableBalanceUSDC
    {
      get { return _availableBalanceUSDC; }
      set { _availableBalanceUSDC = value; }
    }

    public decimal AvailableBalanceBTC
    {
      get { return _availableBalanceBTC; }
      set { _availableBalanceBTC = value; }
    }

    public decimal AvailableBalanceBNB
    {
      get { return _availableBalanceBNB; }
      set { _availableBalanceBNB = value; }
    }

    public decimal AvailableBalanceETH
    {
      get { return _availableBalanceETH; }
      set { _availableBalanceETH = value; }
    }

    public decimal PendingOrderCost
    {
      get { return _pendingOrderCost; }
      set { _pendingOrderCost = value; }
    }

    public decimal PendingOrderCostUSD
    {
      get { return _pendingOrderCostUSD; }
      set { _pendingOrderCostUSD = value; }
    }

    public decimal PendingOrderCostUSDT
    {
      get { return _pendingOrderCostUSDT; }
      set { _pendingOrderCostUSDT = value; }
    }

    public decimal PendingOrderCostUSDC
    {
      get { return _pendingOrderCostUSDC; }
      set { _pendingOrderCostUSDC = value; }
    }

    public decimal PendingOrderCostBTC
    {
      get { return _pendingOrderCostBTC; }
      set { _pendingOrderCostBTC = value; }
    }

    public decimal PendingOrderCostBNB
    {
      get { return _pendingOrderCostBNB; }
      set { _pendingOrderCostBNB = value; }
    }

    public decimal PendingOrderCostETH
    {
      get { return _pendingOrderCostETH; }
      set { _pendingOrderCostETH = value; }
    }

    public decimal ExchangeFee
    {
      get { return _exchangeFee; }
      set { _exchangeFee = value; }
    }

    public decimal ExchangeFeeUSD
    {
      get { return _exchangeFeeUSD; }
      set { _exchangeFeeUSD = value; }
    }

    public decimal ExchangeFeeUSDT
    {
      get { return _exchangeFeeUSDT; }
      set { _exchangeFeeUSDT = value; }
    }

    public decimal ExchangeFeeUSDC
    {
      get { return _exchangeFeeUSDC; }
      set { _exchangeFeeUSDC = value; }
    }

    public decimal ExchangeFeeBTC
    {
      get { return _exchangeFeeBTC; }
      set { _exchangeFeeBTC = value; }
    }

    public decimal ExchangeFeeBNB
    {
      get { return _exchangeFeeBNB; }
      set { _exchangeFeeBNB = value; }
    }

    public decimal ExchangeFeeETH
    {
      get { return _exchangeFeeETH; }
      set { _exchangeFeeETH = value; }
    }

    public decimal MarketValue
    {
      get { return _marketValue; }
      set { _marketValue = value; }
    }

    public decimal MarketValueUSD
    {
      get { return _marketValueUSD; }
      set { _marketValueUSD = value; }
    }

    public decimal MarketValueUSDT
    {
      get { return _marketValueUSDT; }
      set { _marketValueUSDT = value; }
    }

    public decimal MarketValueUSDC
    {
      get { return _marketValueUSDC; }
      set { _marketValueUSDC = value; }
    }

    public decimal MarketValueBTC
    {
      get { return _marketValueBTC; }
      set { _marketValueBTC = value; }
    }

    public decimal MarketValueBNB
    {
      get { return _marketValueBNB; }
      set { _marketValueBNB = value; }
    }

    public decimal MarketValueETH
    {
      get { return _marketValueETH; }
      set { _marketValueETH = value; }
    }

    public void Reset()
    {
      _lastDcaLogLoad = DateTime.MinValue;
      _lastPairsLogLoad = DateTime.MinValue;
      _lastPendingLogLoad = DateTime.MinValue;
      _lastBuyLogLoad = DateTime.MinValue;
      _lastPossibleBuyLogLoad = DateTime.MinValue;
      _lastSalesLogLoad = DateTime.MinValue;
      _lastDCALogLoad = DateTime.MinValue;
    }

    private void LoadBuyLog()
    {
      try
      {
        dynamic rawBuyLogData = GetProfitTrailerData("api/data/buylog");
        if (rawBuyLogData != null)
        {
          _buyLog.Clear();
          BuildBuyLogData(rawBuyLogData);
          _lastBuyLogLoad = DateTime.Now;
        }
      }
      catch (Exception ex)
      {
        _log.DoLogError("Error loading BuyLog", ex);
      }
    }

    private void LoadPossibleBuyLog()
    {
      try
      {
        dynamic rawBuyLogData = GetProfitTrailerData("api/data/possiblebuylog");
        if (rawBuyLogData != null)
        {
          _possibleBuyLog.Clear();
          BuildBuyLogData(rawBuyLogData);
          _lastPossibleBuyLogLoad = DateTime.Now;
        }
      }
      catch (Exception ex)
      {
        _log.DoLogError("Error loading PossibleBuyLog", ex);
      }
    }

    private void LoadPairsLog()
    {
      try
      {
        dynamic rawPairsLogData = GetProfitTrailerData("api/data/pairslog");
        if (rawPairsLogData != null)
        {
          _pairsLog.Clear();
          BuildPairsLogData(rawPairsLogData);
          _lastPairsLogLoad = DateTime.Now;
        }
      }
      catch (Exception ex)
      {
        _log.DoLogError("Error loading PairsLog", ex);
      }
    }

    private void LoadPendingLog()
    {
      try
      {
        dynamic rawPendingLogData = GetProfitTrailerData("api/data/pendinglog");
        if (rawPendingLogData != null)
        {
          _pendingLog.Clear();
          BuildPendingLogData(rawPendingLogData);
          _lastPendingLogLoad = DateTime.Now;
        }
      }
      catch (Exception ex)
      {
        _log.DoLogError("Error loading PendingLog", ex);
      }
    }

    private void LoadSalesLog()
    {
      try
      {
        dynamic rawSalesLogData = GetProfitTrailerData("api/data/saleslog");
        if (rawSalesLogData != null)
        {
          _salesLog.Clear();
          BuildSalesLogData(rawSalesLogData);
          _lastSalesLogLoad = DateTime.Now;
        }
      }
      catch (Exception ex)
      {
        _log.DoLogError("Error loading SalesLog", ex);
      }
    }

    private void LoadDCALog()
    {
      try
      {
        dynamic rawDCALogData = GetProfitTrailerData("api/data/dcalog");
        if (rawDCALogData != null)
        {
          _dcaLog.Clear();
          BuildDCALogData(rawDCALogData);
          _lastDCALogLoad = DateTime.Now;
        }
      }
      catch (Exception ex)
      {
        _log.DoLogError("Error loading DCALog", ex);
      }
    }

    private dynamic GetProfitTrailerData(string apiPath)
    {
      try
      {
        string apiUrl = _ptMagic.PTConnectionUrl + "/" + apiPath;
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(apiUrl);
        request.Method = "GET";
        request.Timeout = 10000;
        request.ReadWriteTimeout = 10000;
        request.ContentType = "application/json";

        using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
        using (Stream responseStream = response.GetResponseStream())
        using (StreamReader reader = new StreamReader(responseStream))
        {
          string responseText = reader.ReadToEnd();
          if (!string.IsNullOrWhiteSpace(responseText))
          {
            return JsonConvert.DeserializeObject(responseText);
          }
        }
      }
      catch (Exception ex)
      {
        _log.DoLogError("Error loading ProfitTrailer data from " + apiPath, ex);
      }

      return null;
    }

    private void BuildPairsLogData(dynamic rawPairsLogData)
    {
      List<PairLogData> pairs = new List<PairLogData>();

      foreach (var pair in rawPairsLogData)
      {
        PairLogData pairLogData = new PairLogData();
        pairLogData.Market = pair.market;
        pairLogData.AverageBoughtPrice = pair.avgBoughtPrice;
        pairLogData.CurrentPrice = pair.currentPrice;
        pairLogData.ProfitPercent = pair.profit;
        pairLogData.PercChange = pair.percChange;
        pairLogData.Volume24h = pair.volume;
        pairLogData.TotalCost = pair.totalCost;
        pairLogData.DCACount = pair.dcaCount;
        pairLogData.FirstBoughtDate = pair.firstBoughtDate;

        pairs.Add(pairLogData);
      }

      _pairsLog = pairs;
    }

    private void BuildPendingLogData(dynamic rawPendingLogData)
    {
      List<PendingLogData> pending = new List<PendingLogData>();

      foreach (var pair in rawPendingLogData)
      {
        PendingLogData pendingLogData = new PendingLogData();
        pendingLogData.Market = pair.market;
        pendingLogData.AverageBoughtPrice = pair.avgBoughtPrice;
        pendingLogData.CurrentPrice = pair.currentPrice;
        pendingLogData.ProfitPercent = pair.profit;
        pendingLogData.PercChange = pair.percChange;
        pendingLogData.Volume24h = pair.volume;
        pendingLogData.TotalCost = pair.totalCost;
        pendingLogData.DCACount = pair.dcaCount;

        pending.Add(pendingLogData);
      }

      _pendingLog = pending;
    }

    private void BuildSalesLogData(dynamic rawSalesLogData)
    {
      List<SalesLogData> sales = new List<SalesLogData>();

      foreach (var pair in rawSalesLogData)
      {
        SalesLogData salesLogData = new SalesLogData();
        salesLogData.Market = pair.market;
        salesLogData.SoldPrice = pair.soldPrice;
        salesLogData.BoughtPrice = pair.boughtPrice;
        salesLogData.ProfitPercent = pair.profit;
        salesLogData.SoldDate = pair.soldDate;
        salesLogData.BoughtDate = pair.boughtDate;
        salesLogData.BuyStrategy = pair.buyStrategy;
        salesLogData.SellStrategy = pair.sellStrategy;
        salesLogData.TriggerValue = pair.triggerValue;
        salesLogData.CurrentValue = pair.currentValue;

        sales.Add(salesLogData);
      }

      _salesLog = sales;
    }

    private void BuildDCALogData(dynamic rawDCALogData)
    {
      List<DCALogData> pairs = new List<DCALogData>();

      foreach (var pair in rawDCALogData)
      {
        DCALogData dcaLogData = new DCALogData();
        dcaLogData.Market = pair.market;
        dcaLogData.AverageBoughtPrice = pair.avgBoughtPrice;
        dcaLogData.CurrentPrice = pair.currentPrice;
        dcaLogData.ProfitPercent = pair.profit;
        dcaLogData.PercChange = pair.percChange;
        dcaLogData.Volume24h = pair.volume;
        dcaLogData.TotalCost = pair.totalCost;
        dcaLogData.DCACount = pair.dcaCount;

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
              buyStrategy.Name = bs.letter != null ? bs.letter : "";
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
