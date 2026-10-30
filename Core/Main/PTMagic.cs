using System;
using System.Data;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using Core.Helper;
using Core.Main.DataObjects.PTMagicData;
using Core.MarketAnalyzer;
using Core.ProfitTrailer;
using Newtonsoft.Json;

namespace Core.Main
{
  public class PTMagic
  {
    public PTMagic(LogHelper log)
    {
      this.Log = log;
    }
    
    #region Properties
    private LogHelper _log;
    private PTMagicConfiguration _systemConfiguration;
    private System.Timers.Timer _timer;
    private Summary _lastRuntimeSummary = null;
    private int _state = 0;
    private int _runCount = 0;
    private int _totalElapsedSeconds = 0;
    private bool _globalSettingWritten = false;
    private bool _singleMarketSettingChanged = false;
    private List<KeyValuePair<string, string>> _lastActiveSingleMarketSettings = null;
    private bool _enforceSettingsReapply = false;
    private DateTime _lastRuntime = Constants.confMinDate;
    private DateTime _lastSettingsChange = Constants.confMinDate;
    private DateTime _lastSettingFileCheck = Constants.confMinDate;
    private DateTime _lastVersionCheck = Constants.confMinDate;
    private DateTime _lastFiatCurrencyCheck = Constants.confMinDate;
    private string _activeSettingName = "";
    private GlobalSetting _activeSetting = null;
    private string _defaultSettingName = "";
    private string _pairsFileName = "PAIRS.PROPERTIES";
    private string _dcaFileName = "DCA.PROPERTIES";
    private string _indicatorsFileName = "INDICATORS.PROPERTIES";
    private Version _currentVersion = null;
    private string _latestVersion = "";
    private string _lastMainFiatCurrency = "USD";
    private double _lastMainFiatCurrencyExchangeRate = 1;
    private List<SingleMarketSettingSummary> _singleMarketSettingSummaries = new List<SingleMarketSettingSummary>();
    private List<string> _pairsLines = null;
    private List<string> _dcaLines = null;
    private List<string> _indicatorsLines = null;
    private List<string> _exchangeMarketList = null;
    private List<string> _marketList = new List<string>();
    private ConcurrentDictionary<string, MarketInfo> _marketInfos = new ConcurrentDictionary<string, MarketInfo>();
    private Dictionary<string, double> _averageMarketTrendChanges = new Dictionary<string, double>();
    private Dictionary<string, List<MarketTrendChange>> _singleMarketTrendChanges = new Dictionary<string, List<MarketTrendChange>>();
    private Dictionary<string, List<MarketTrendChange>> _globalMarketTrendChanges = new Dictionary<string, List<MarketTrendChange>>();
    private Dictionary<string, int> _singleMarketSettingsCount = new Dictionary<string, int>();
    Dictionary<string, List<SingleMarketSetting>> _triggeredSingleMarketSettings = new Dictionary<string, List<SingleMarketSetting>>();
    private readonly SemaphoreSlim _raidSemaphore = new SemaphoreSlim(1, 1);
    private Mutex mutex = new Mutex(false, "analyzerStateMutex");

    public LogHelper Log
    {
      get
      {
        return _log;
      }
      set
      {
        _log = value;
      }
    }

    public PTMagicConfiguration PTMagicConfiguration
    {
      get
      {
        return _systemConfiguration;
      }
      set
      {
        _systemConfiguration = value;
      }
    }
    public System.Timers.Timer Timer
    {
      get
      {
        return _timer;
      }
      set
      {
        _timer = value;
      }
    }

    public Summary LastRuntimeSummary
    {
      get
      {
        return _lastRuntimeSummary;
      }
      set
      {
        _lastRuntimeSummary = value;
      }
    }

    public int State
    {
      get
      {
        return _state;
      }
      set
      {
        _state = value;
      }
    }
   public void WriteStateToFile()
{
    try
    {
        mutex.WaitOne(); // Acquire the mutex

        string dirPath = "_data";
        string filePath = Path.Combine(dirPath, "AnalyzerState");

        // Ensure the directory exists
        Directory.CreateDirectory(dirPath);

        File.WriteAllText(filePath, this.State.ToString());

    }
    finally
    {
        mutex.ReleaseMutex(); // Release the mutex even if exceptions occur
    }
}


    public int RunCount
    {
      get
      {
        return _runCount;
      }
      set
      {
        _runCount = value;
      }
    }

    public int TotalElapsedSeconds
    {
      get
      {
        return _totalElapsedSeconds;
      }
      set
      {
        _totalElapsedSeconds = value;
      }
    }

    public bool GlobalSettingWritten
    {
      get
      {
        return _globalSettingWritten;
      }
      set
      {
        _globalSettingWritten = value;
      }
    }

    public bool SingleMarketSettingChanged
    {
      get
      {
        return _singleMarketSettingChanged;
      }
      set
      {
        _singleMarketSettingChanged = value;
      }
    }

    public bool EnforceSettingsReapply
    {
      get
      {
        return _enforceSettingsReapply;
      }
      set
      {
        _enforceSettingsReapply = value;
      }
    }

    public DateTime LastSettingsChange
    {
      get
      {
        return _lastSettingsChange;
      }
      set
      {
        _lastSettingsChange = value;
      }
    }

    public DateTime LastVersionCheck
    {
      get
      {
        return _lastVersionCheck;
      }
      set
      {
        _lastVersionCheck = value;
      }
    }

    public DateTime LastFiatCurrencyCheck
    {
      get
      {
        return _lastFiatCurrencyCheck;
      }
      set
      {
        _lastFiatCurrencyCheck = value;
      }
    }

    public DateTime LastSettingFileCheck
    {
      get
      {
        return _lastSettingFileCheck;
      }
      set
      {
        _lastSettingFileCheck = value;
      }
    }

    public DateTime LastRuntime
    {
      get
      {
        return _lastRuntime;
      }
      set
      {
        _lastRuntime = value;
      }
    }

    public string DefaultSettingName
    {
      get
      {
        return _defaultSettingName;
      }
      set
      {
        _defaultSettingName = value;
      }
    }

    public GlobalSetting ActiveSetting
    {
      get
      {
        return _activeSetting;
      }
      set
      {
        _activeSetting = value;
      }
    }

    public string ActiveSettingName
    {
      get
      {
        return _activeSettingName;
      }
      set
      {
        _activeSettingName = value;
      }
    }

    public string PairsFileName
    {
      get
      {
        return _pairsFileName;
      }
      set
      {
        _pairsFileName = value;
      }
    }

    public string DCAFileName
    {
      get
      {
        return _dcaFileName;
      }
      set
      {
        _dcaFileName = value;
      }
    }

    public string IndicatorsFileName
    {
      get
      {
        return _indicatorsFileName;
      }
      set
      {
        _indicatorsFileName = value;
      }
    }

    public Version CurrentVersion
    {
      get
      {
        return _currentVersion;
      }
      set
      {
        _currentVersion = value;
      }
    }

    public string LatestVersion
    {
      get
      {
        return _latestVersion;
      }
      set
      {
        _latestVersion = value;
      }
    }

    public string LastMainFiatCurrency
    {
      get
      {
        return _lastMainFiatCurrency;
      }
      set
      {
        _lastMainFiatCurrency = value;
      }
    }

    public double LastMainFiatCurrencyExchangeRate
    {
      get
      {
        return _lastMainFiatCurrencyExchangeRate;
      }
      set
      {
        _lastMainFiatCurrencyExchangeRate = value;
      }
    }

    public List<SingleMarketSettingSummary> SingleMarketSettingSummaries
    {
      get
      {
        return _singleMarketSettingSummaries;
      }
      set
      {
        _singleMarketSettingSummaries = value;
      }
    }

    public List<string> PairsLines
    {
      get
      {
        return _pairsLines;
      }
      set
      {
        _pairsLines = value;
      }
    }

    public List<string> DCALines
    {
      get
      {
        return _dcaLines;
      }
      set
      {
        _dcaLines = value;
      }
    }

    public List<string> IndicatorsLines
    {
      get
      {
        return _indicatorsLines;
      }
      set
      {
        _indicatorsLines = value;
      }
    }

    public List<string> ExchangeMarketList
    {
      get
      {
        return _exchangeMarketList;
      }
      set
      {
        _exchangeMarketList = value;
      }
    }

    public List<string> MarketList
    {
      get
      {
        return _marketList;
      }
      set
      {
        _marketList = value;
      }
    }

    public ConcurrentDictionary<string, MarketInfo> MarketInfos
    {
      get
      {
        return _marketInfos;
      }
      set
      {
        _marketInfos = value;
      }
    }

    public Dictionary<string, List<MarketTrendChange>> SingleMarketTrendChanges
    {
      get
      {
        return _singleMarketTrendChanges;
      }
      set
      {
        _singleMarketTrendChanges = value;
      }
    }

    public Dictionary<string, List<MarketTrendChange>> GlobalMarketTrendChanges
    {
      get
      {
        return _globalMarketTrendChanges;
      }
      set
      {
        _globalMarketTrendChanges = value;
      }
    }

    public Dictionary<string, double> AverageMarketTrendChanges
    {
      get
      {
        return _averageMarketTrendChanges;
      }
      set
      {
        _averageMarketTrendChanges = value;
      }
    }

    public Dictionary<string, int> SingleMarketSettingsCount
    {
      get
      {
        return _singleMarketSettingsCount;
      }
      set
      {
        _singleMarketSettingsCount = value;
      }
    }

    public Dictionary<string, List<SingleMarketSetting>> TriggeredSingleMarketSettings
    {
      get
      {
        return _triggeredSingleMarketSettings;
      }
      set
      {
        _triggeredSingleMarketSettings = value;
      }
    }
    #endregion

    #region PTMagic Startup  Methods

    private static int ExponentialDelay(int failedAttempts, int maxDelayInSeconds = 900)
    {
      var delayInSeconds = ((1d / 2d) * (Math.Pow(2d, failedAttempts) - 1d));

      return maxDelayInSeconds < delayInSeconds
          ? maxDelayInSeconds
          : (int)delayInSeconds;
    }

    public bool StartProcess()
    {
      bool result = true;

      this.Log.DoLogInfo("");
      this.Log.DoLogInfo("  ██████╗ ████████╗    ███╗   ███╗ █████╗  ██████╗ ██╗ ██████╗");
      this.Log.DoLogInfo("  ██╔══██╗╚══██╔══╝    ████╗ ████║██╔══██╗██╔════╝ ██║██╔════╝");
      this.Log.DoLogInfo("  ██████╔╝   ██║       ██╔████╔██║███████║██║  ███╗██║██║     ");
      this.Log.DoLogInfo("  ██╔═══╝    ██║       ██║╚██╔╝██║██╔══██║██║   ██║██║██║     ");
      this.Log.DoLogInfo("  ██║        ██║       ██║ ╚═╝ ██║██║  ██║╚██████╔╝██║╚██████╗");
      this.Log.DoLogInfo("  ╚═╝        ╚═╝       ╚═╝     ╚═╝╚═╝  ╚═╝ ╚═════╝ ╚═╝ ╚═════╝");
      this.Log.DoLogInfo("                         Version " + this.CurrentVersion.Major + "." + this.CurrentVersion.Minor + "." + this.CurrentVersion.Build);
      this.Log.DoLogInfo("");
      this.Log.DoLogInfo("Starting PTMagic in " + Directory.GetCurrentDirectory());
      this.Log.DoLogInfo("with .NET Core: " + Path.GetDirectoryName(typeof(object).Assembly.Location));

      if (!this.RunStartupChecks())
      {
        return false;
      }
      if (!this.InitializeConfiguration())
      {
        return false;
      }

      bool configCheckResult = this.RunConfigurationChecks();

      if (!configCheckResult)
      {
        int configRetryCount = 1;
        int delaySeconds;

        while (!configCheckResult)
        {
          delaySeconds = ExponentialDelay(configRetryCount);
          this.Log.DoLogError("Configuration check retry " + configRetryCount + " failed, starting next retry in " + delaySeconds + " seconds...");
          Thread.Sleep(delaySeconds * 1000);

          this.InitializeConfiguration();
          configCheckResult = this.RunConfigurationChecks();
          configRetryCount++;
        }
      }

      this.LastSettingFileCheck = DateTime.UtcNow;
      SettingsFiles.CheckPresets(this.PTMagicConfiguration, this.Log, true);

      SettingsFiles.PresetFileWatcher.Changed += PresetFileWatcher_OnChanged;
      SettingsFiles.PresetFileWatcher.EnableRaisingEvents = true;

      EnforceSettingsReapply = true;
      this.ActiveSetting = this.PTMagicConfiguration.AnalyzerSettings.GlobalSettings.Find(s => s.SettingName.Equals(this.DefaultSettingName, StringComparison.InvariantCultureIgnoreCase));
      this.ActiveSettingName = ActiveSetting.SettingName;
      this.LastSettingsChange = DateTime.UtcNow;

      this.StartPTMagicIntervalTimer();

      return result;
    }

    private void PresetFileWatcher_OnChanged(object source, FileSystemEventArgs e)
    {
      SettingsFiles.PresetFileWatcher.EnableRaisingEvents = false;
      this.Log.DoLogInfo("Detected a '" + e.ChangeType.ToString() + "' change in the following preset file: " + e.FullPath);
      this.EnforceSettingsReapply = true;
      PTMagicIntervalTimer_Elapsed(new object(), null);
      SettingsFiles.PresetFileWatcher.EnableRaisingEvents = true;
    }

    public bool RunStartupChecks()
    {
      bool result = true;

      if (!File.Exists(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "settings.general.json"))
      {
        this.Log.DoLogError("File 'settings.general.json' not found! Please review the setup steps on the wiki and double check every step that involves copying files!");
        result = false;
      }

      if (!File.Exists(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "settings.analyzer.json"))
      {
        this.Log.DoLogError("File 'settings.analyzer.json' not found! Please review the setup steps on the wiki and double check every step that involves copying files!");
        result = false;
      }

      return result;
    }

    public bool InitializeConfiguration()
    {
      bool result = true;

      try
      {
        this.PTMagicConfiguration = new PTMagicConfiguration();

        this.Log.DoLogInfo("Configuration loaded. Found " +
                            this.PTMagicConfiguration.AnalyzerSettings.MarketAnalyzer.MarketTrends != null ? this.PTMagicConfiguration.AnalyzerSettings.MarketAnalyzer.MarketTrends.Count.ToString() : "0" +
                            " Market Trends, " +
                            this.PTMagicConfiguration.AnalyzerSettings.GlobalSettings != null ? this.PTMagicConfiguration.AnalyzerSettings.GlobalSettings.Count.ToString() : "0" +
                            " Global Settings and " +
                            this.PTMagicConfiguration.AnalyzerSettings.SingleMarketSettings != null ? this.PTMagicConfiguration.AnalyzerSettings.SingleMarketSettings.Count.ToString() : "0" +
                            " Single Market Settings.");
      }
      catch (Exception ex)
      {
        result = false;
        this.Log.DoLogCritical("Error loading configuration!", ex);
        throw;
      }

      return result;
    }

    public bool RunConfigurationChecks()
    {
      bool result = true;

      GlobalSetting defaultSetting = this.PTMagicConfiguration.AnalyzerSettings.GlobalSettings.Find(s => s.SettingName.Equals("default", StringComparison.InvariantCultureIgnoreCase));
      if (defaultSetting == null)
      {
        defaultSetting = this.PTMagicConfiguration.AnalyzerSettings.GlobalSettings.Find(s => s.SettingName.IndexOf("default", StringComparison.InvariantCultureIgnoreCase) > -1);
        if (defaultSetting != null)
        {
          this.Log.DoLogDebug("No setting named 'default' found, taking '" + defaultSetting.SettingName + "' as default.");
          this.DefaultSettingName = defaultSetting.SettingName;
        }
        else
        {
          this.Log.DoLogError("No 'default' setting found! Terminating process...");
          result = false;
        }
      }
      else
      {
        this.DefaultSettingName = defaultSetting.SettingName;
      }

      if (!this.PTMagicConfiguration.GeneralSettings.Application.Exchange.Equals("Binance", StringComparison.InvariantCultureIgnoreCase)
        && !this.PTMagicConfiguration.GeneralSettings.Application.Exchange.Equals("Bittrex", StringComparison.InvariantCultureIgnoreCase)
        && !this.PTMagicConfiguration.GeneralSettings.Application.Exchange.Equals("BinanceUS", StringComparison.InvariantCultureIgnoreCase)
        && !this.PTMagicConfiguration.GeneralSettings.Application.Exchange.Equals("BinanceFutures", StringComparison.InvariantCultureIgnoreCase)
        && !this.PTMagicConfiguration.GeneralSettings.Application.Exchange.Equals("Poloniex", StringComparison.InvariantCultureIgnoreCase))
      {
        this.Log.DoLogError("Exchange '" + this.PTMagicConfiguration.GeneralSettings.Application.Exchange + "' specified in settings.general.json is invalid! Terminating process...");
        result = false;
      }

      if (this.PTMagicConfiguration.GeneralSettings.Application.IsEnabled)
      {
        result = RunProfitTrailerSettingsAPIChecks();
        try
        {
          if (this.PTMagicConfiguration.GeneralSettings.Application.TestMode) 
          {
            this.Log.DoLogWarn("TESTMODE ENABLED - No PT settings will be changed!");
          }

          if (!String.IsNullOrEmpty(this.PTMagicConfiguration.GeneralSettings.Application.CoinMarketCapAPIKey))
          {
            this.Log.DoLogInfo("CoinMarketCap API KEY found");
          }
          else
          {
            this.Log.DoLogInfo("No CoinMarketCap API KEY specified! That's ok, but you can't use CoinMarketCap in your settings.analyzer.json");
          }
        }
        catch (System.NullReferenceException)
        {
          this.Log.DoLogError("PTM failed to read the General Settings file. That means something in the file is either missing or incorrect. If this happend after an update please take a look at the release notes at: https://github.com/PTMagicians/PTMagic/releases");
          Console.WriteLine("Press enter to close the Application...");
          Console.ReadLine();
          Environment.Exit(0);
        }
      }
      else
      {
        this.Log.DoLogWarn("PTMagic is disabled.  The scheduled raid was skipped.");
        result = false;
      }

      return result;
    }

    private bool RunProfitTrailerSettingsAPIChecks()
    {
      bool result = true;

      this.Log.DoLogInfo("========== STARTING CHECKS FOR Profit Trailer ==========");

      if (!String.IsNullOrEmpty(this.PTMagicConfiguration.GeneralSettings.Application.ProfitTrailerLicense))
      {
        this.Log.DoLogInfo("Profit Trailer check: Profit Trailer license found");
      }
      else
      {
        this.Log.DoLogError("Profit Trailer check: No Profit Trailer license key specified! The license key is necessary to adjust your Profit Trailer settings");
        result = false;
      }

      if (!String.IsNullOrEmpty(this.PTMagicConfiguration.GeneralSettings.Application.ProfitTrailerServerAPIToken))
      {
        this.Log.DoLogInfo("Profit Trailer check: Profit Trailer Server API Token Specified");
      }
      else
      {
        this.Log.DoLogError("Profit Trailer check: No Server API Token specified. Please configure ProfitTrailerServerAPIToken in settings.general.json , ensuring it has to be the same Token as in the Profit Trailer Config File!");
        result = false;
      }

      if (!String.IsNullOrEmpty(this.PTMagicConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName))
      {
        this.Log.DoLogInfo("Profit Trailer check: Profit Trailer default setting name specified");
      }
      else
      {
        this.Log.DoLogError("Profit Trailer check: No Profit Trailer default setting name specified! The default setting name is necessary to adjust your Profit Trailer settings since 2.0");
        result = false;
      }

      if (!String.IsNullOrEmpty(this.PTMagicConfiguration.GeneralSettings.Application.ProfitTrailerMonitorURL))
      {
        this.Log.DoLogInfo("Profit Trailer check: Profit Trailer monitor URL found");
      }
      else
      {
        this.Log.DoLogError("Profit Trailer check: No Profit Trailer monitor URL specified! The monitor URL is necessary to adjust your Profit Trailer settings since 2.0");
        result = false;
      }

      if (SystemHelper.UrlIsReachable(this.PTMagicConfiguration.GeneralSettings.Application.ProfitTrailerMonitorURL))
      {
        this.Log.DoLogInfo("Profit Trailer check: Profit Trailer monitor connection test succeeded");
      }
      else
      {
        this.Log.DoLogError("Profit Trailer check: Your Profit Trailer monitor (" + this.PTMagicConfiguration.GeneralSettings.Application.ProfitTrailerMonitorURL + ") is not available! Make sure your Profit Trailer bot is up and running and your monitor is accessible.");
        result = false;
      }

      if (result)
      {
        this.Log.DoLogInfo("========== CHECKS FOR Profit Trailer COMPLETED! ==========");
      }
      else
      {
        this.Log.DoLogInfo("========== CHECKS FOR Profit Trailer FAILED! ==========");
      }

      return result;
    }

    public void StartPTMagicIntervalTimer()
    {
      this.Timer = new System.Timers.Timer(this.PTMagicConfiguration.AnalyzerSettings.MarketAnalyzer.IntervalMinutes * 60 * 1000);
      this.Timer.Enabled = true;
      this.Timer.Elapsed += new System.Timers.ElapsedEventHandler(this.PTMagicIntervalTimer_Elapsed);

      this.Log.DoLogInfo("Checking market trends every " + this.PTMagicConfiguration.AnalyzerSettings.MarketAnalyzer.IntervalMinutes.ToString() + " minutes...");
      this.PTMagicIntervalTimer_Elapsed(null, null);
    }
    #endregion

    #region PTMagic Interval Methods

    public async void PTMagicIntervalTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
    {
      if (this.State == Constants.PTMagicBotState_Idle)
      {
        if (!await _raidSemaphore.WaitAsync(0).ConfigureAwait(false))
        {
          return;
        }

        try
        {
          try
          {
            this.State = Constants.PTMagicBotState_Running;
            this.WriteStateToFile();
            this.RunCount++;
            this.LastRuntime = DateTime.UtcNow;

            this.EnforceSettingsReapply = this.HaveSettingsChanged() || this.EnforceSettingsReapply;

            if (PTMagicConfiguration.GeneralSettings.Application.IsEnabled)
            {
              this.ValidateSettings();

              this.Log.DoLogInfo("");
              this.Log.DoLogInfo("##########################################################");
              this.Log.DoLogInfo("#********************************************************#");
              this.Log.DoLogInfo("Starting market trend check with Version " + this.CurrentVersion.Major + "." + this.CurrentVersion.Minor + "." + this.CurrentVersion.Build);

              this.LastRuntimeSummary = new Summary();
              this.LastRuntimeSummary.LastRuntime = this.LastRuntime;
              this.LastRuntimeSummary.Version = this.CurrentVersion.Major.ToString() + "." + this.CurrentVersion.Minor.ToString() + "." + this.CurrentVersion.Build.ToString();

              await this.CheckLatestGitHubVersionAsync(this.LastRuntimeSummary.Version).ConfigureAwait(false);

              this.LoadCurrentProfitTrailerProperties();
              this.LoadSMSSummaries();
              this.MarketInfos = BaseAnalyzer.GetMarketInfosFromFile(this.PTMagicConfiguration, this.Log);

              await this.BuildMarketDataAsync().ConfigureAwait(false);

              this.BuildMarketList();
              this.ValidateMarketList();
              this.BuildGlobalMarketTrends();

              GlobalSetting triggeredSetting = this.PTMagicConfiguration.AnalyzerSettings.GlobalSettings.Find(s => s.SettingName.Equals(this.DefaultSettingName, StringComparison.InvariantCultureIgnoreCase));
              List<string> matchedTriggers = new List<string>();
              this.CheckGlobalSettingsTriggers(ref triggeredSetting, ref matchedTriggers);
              this.ActivateSetting(ref triggeredSetting, ref matchedTriggers);
              this.ApplySingleMarketSettings();

              if (this.PTMagicConfiguration.GeneralSettings.Application.Exchange.Equals("BinanceFutures", StringComparison.InvariantCultureIgnoreCase))
              {
                var results = this.MarketList.FindAll(m => m.Contains("_", StringComparison.InvariantCultureIgnoreCase));

                if (results.Count > 0)
                {
                  this.PairsLines.AddRange(new string[] {
                    "",
                    "# BinanceFutures Quarterly Contracts - Ignore list:",
                    "###################################################"
                  });

                  foreach (var marketPair in results)
                  {
                    this.PairsLines.Add(String.Format("{0}_trading_enabled = false", marketPair));
                  }
                }
              }

              this.SaveProfitTrailerProperties();
              this.SaveSingleMarketSettingsSummary();

              DateTime endTime = DateTime.UtcNow;
              int elapsedSeconds = (int)Math.Round(endTime.Subtract(this.LastRuntime).TotalSeconds, 0);
              this.TotalElapsedSeconds += elapsedSeconds;

              this.SaveRuntimeSummary(elapsedSeconds);

              this.Log.DoLogInfo("##########################################################");
              this.Log.DoLogInfo("#******************* RAID SUMMARY ********************#");
              this.Log.DoLogInfo("+ PT Magic Version: " + this.LastRuntimeSummary.Version);
              if (!SystemHelper.IsRecentVersion(this.LastRuntimeSummary.Version, this.LatestVersion))
              {
                this.Log.DoLogWarn("+ Your version is out of date! The most recent version is " + this.LatestVersion);
              }
              this.Log.DoLogInfo("+ Instance name: " + PTMagicConfiguration.GeneralSettings.Application.InstanceName);
              this.Log.DoLogInfo("+ Time spent: " + SystemHelper.GetProperDurationTime(elapsedSeconds));
              this.Log.DoLogInfo("+ Active setting: " + this.LastRuntimeSummary.CurrentGlobalSetting.SettingName);
              this.Log.DoLogInfo("+ Global setting changed: " + ((this.LastRuntimeSummary.LastGlobalSettingSwitch == this.LastRuntimeSummary.LastRuntime) ? "Yes" : "No") + " " + ((this.LastRuntimeSummary.FloodProtectedSetting != null) ? "(Flood protection!)" : ""));
              this.Log.DoLogInfo("+ Single Market Settings changed: " + (this.SingleMarketSettingChanged ? "Yes" : "No"));
              this.Log.DoLogInfo("+ PT Config updated: " + (((this.GlobalSettingWritten || this.SingleMarketSettingChanged) && !this.PTMagicConfiguration.GeneralSettings.Application.TestMode) ? "Yes" : "No") + ((this.PTMagicConfiguration.GeneralSettings.Application.TestMode) ? " - TESTMODE active" : ""));
              this.Log.DoLogInfo("+ Markets with active single market settings: " + this.TriggeredSingleMarketSettings.Count.ToString());
              foreach (string activeSMS in this.SingleMarketSettingsCount.Keys)
              {
                this.Log.DoLogInfo("+   " + activeSMS + ": " + this.SingleMarketSettingsCount[activeSMS].ToString());
              }
              this.Log.DoLogInfo("+ " + this.TotalElapsedSeconds.ToString() + " Magicbots killed in " + this.RunCount.ToString() + " raids on Cryptodragon's Lair " + this.PTMagicConfiguration.AnalyzerSettings.MarketAnalyzer.IntervalMinutes.ToString() + ".");
              this.Log.DoLogInfo("");
              this.Log.DoLogInfo("DO NOT CLOSE THIS WINDOW! THIS IS THE BOT THAT ANALYZES TRENDS AND CHANGES SETTINGS!");
              this.Log.DoLogInfo("");
              this.Log.DoLogInfo("#********************************************************#");
              this.Log.DoLogInfo("##########################################################");
              this.Log.DoLogInfo("");
            }
            else
            {
              this.State = Constants.PTMagicBotState_Idle;
              Log.DoLogWarn("PTMagic is disabled.  The scheduled raid was skipped.");
            }
          }
          catch (Exception ex)
          {
            this.Log.DoLogCritical("A error occurred during the raid, the raid did not complete, but will try again next interval!", ex);
          }
          finally
          {
            this.Cleanup();
            this.State = Constants.PTMagicBotState_Idle;
            this.WriteStateToFile();
          }
        }
        finally
        {
          _raidSemaphore.Release();
        }
      }
      else
      {
        if (this.RunCount > 1)
        {
          Log.DoLogWarn("PTMagic has been raiding since " + this.LastRuntime.ToLocalTime().ToString() + ".  Checking things...");

          if (File.Exists(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + "LastRuntimeSummary.json"))
          {
            FileInfo fiLastSummary = new FileInfo(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + "LastRuntimeSummary.json");
            if (fiLastSummary.LastWriteTimeUtc < DateTime.UtcNow.AddMinutes(-(this.PTMagicConfiguration.AnalyzerSettings.MarketAnalyzer.IntervalMinutes * 2)))
            {
              Log.DoLogWarn("PTMagic raid " + this.RunCount.ToString() + " is taking longer than expected.  Consider increasing your IntervalMinues setting, reducing other processes on your PC, or raising PTMagic's priority.");
              this.State = Constants.PTMagicBotState_Idle;
              this.WriteStateToFile();
              Log.DoLogInfo("PTMagic status reset, waiting for the next raid to be good to go again.");
            }
          }
          else
          {
            Log.DoLogWarn("No LastRuntimeSummary.json found after raid " + this.RunCount.ToString() + ", trying to reset PT Magic status...");
            this.State = Constants.PTMagicBotState_Idle;
            this.WriteStateToFile();
            Log.DoLogInfo("PTMagic status reset, waiting for the next raid to be good to go again.");
          }
        }
      }
    }

    private bool HaveSettingsChanged()
    {
      bool result = false;

      FileInfo generalSettingsFile = new FileInfo(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "settings.general.json");
      FileInfo analyzerSettingsFile = new FileInfo(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + "settings.analyzer.json");
      if (generalSettingsFile.LastWriteTimeUtc > this.LastSettingFileCheck || analyzerSettingsFile.LastWriteTimeUtc > this.LastSettingFileCheck)
      {
        Log.DoLogInfo("Detected configuration changes. Reloading settings...");

        try
        {
          PTMagicConfiguration = new PTMagicConfiguration();

          GlobalSetting defaultSetting = this.PTMagicConfiguration.AnalyzerSettings.GlobalSettings.Find(s => s.SettingName.Equals("default", StringComparison.InvariantCultureIgnoreCase));
          if (defaultSetting == null)
          {
            defaultSetting = this.PTMagicConfiguration.AnalyzerSettings.GlobalSettings.Find(s => s.SettingName.IndexOf("default", StringComparison.InvariantCultureIgnoreCase) > -1);
            if (defaultSetting != null)
            {
              Log.DoLogDebug("No setting named 'default' found, taking '" + defaultSetting.SettingName + "' as default.");
              this.DefaultSettingName = defaultSetting.SettingName;
            }
            else
            {
              Log.DoLogError("No 'default' setting found! Terminating process...");
              this.Timer.Stop();
              Exception ex = new Exception("No 'default' setting found!Terminating process...");
              throw ex;
            }
          }
          else
          {
            this.DefaultSettingName = defaultSetting.SettingName;
          }

          Log.DoLogInfo("New configuration reloaded.");
          this.LastSettingFileCheck = DateTime.UtcNow;
          result = true;

          if (this.Timer.Interval != this.PTMagicConfiguration.AnalyzerSettings.MarketAnalyzer.IntervalMinutes * 60 * 1000)
          {
            Log.DoLogInfo("Setting for 'IntervalMinutes' changed in MarketAnalyzer, setting new timer...");
            this.Timer.Stop();
            this.Timer.Interval = this.PTMagicConfiguration.AnalyzerSettings.MarketAnalyzer.IntervalMinutes * 60 * 1000;
            this.Timer.Start();
            Log.DoLogInfo("New timer set to " + this.PTMagicConfiguration.AnalyzerSettings.MarketAnalyzer.IntervalMinutes.ToString() + " minutes.");
          }

          SettingsFiles.CheckPresets(this.PTMagicConfiguration, this.Log, true);
        }
        catch (Exception ex)
        {
          Log.DoLogCritical("Error loading new configuration!", ex);
        }
      }
      else
      {
        result = SettingsFiles.CheckPresets(this.PTMagicConfiguration, this.Log, false);
      }

      return result;
    }

    private void ValidateSettings()
    {
      if (this.PTMagicConfiguration.GeneralSettings.Application.Exchange == null)
      {
        Log.DoLogError("Your setting for Application.Exchange in settings.general.json is invalid (null)! Terminating process.");
        this.Timer.Stop();
        Exception ex = new Exception("Your setting for Application.Exchange in settings.general.json is invalid (null)! Terminating process.");
        throw ex;
      }
      else if (String.IsNullOrEmpty(this.PTMagicConfiguration.GeneralSettings.Application.Exchange))
      {
        Log.DoLogError("Your setting for Application.Exchange in settings.general.json is invalid (empty)! Terminating process.");
        this.Timer.Stop();
        Exception ex = new Exception("Your setting for Application.Exchange in settings.general.json is invalid (empty)! Terminating process.");
        throw ex;
      }
      else if (!this.PTMagicConfiguration.GeneralSettings.Application.Exchange.Equals("Binance", StringComparison.InvariantCultureIgnoreCase) && !this.PTMagicConfiguration.GeneralSettings.Application.Exchange.Equals("BinanceUS", StringComparison.InvariantCultureIgnoreCase) && !this.PTMagicConfiguration.GeneralSettings.Application.Exchange.Equals("BinanceFutures", StringComparison.InvariantCultureIgnoreCase) && !this.PTMagicConfiguration.GeneralSettings.Application.Exchange.Equals("Bittrex", StringComparison.InvariantCultureIgnoreCase) && !this.PTMagicConfiguration.GeneralSettings.Application.Exchange.Equals("Poloniex", StringComparison.InvariantCultureIgnoreCase))
      {
        Log.DoLogError("Your setting for Application.Exchange in settings.general.json is invalid (" + this.PTMagicConfiguration.GeneralSettings.Application.Exchange + ")! Terminating process.");
        this.Timer.Stop();
        Exception ex = new Exception("Your setting for Application.Exchange in settings.general.json is invalid (" + this.PTMagicConfiguration.GeneralSettings.Application.Exchange + ")! Terminating process.");
        throw ex;
      }
    }

    private async Task CheckLatestGitHubVersionAsync(string currentVersion, CancellationToken cancellationToken = default)
    {
      if (this.LastVersionCheck < DateTime.UtcNow.AddMinutes(-30))
      {
        this.LatestVersion = await BaseAnalyzer.GetLatestGitHubReleaseAsync(this.Log, currentVersion, cancellationToken).ConfigureAwait(false);
        this.LastVersionCheck = DateTime.UtcNow;
        if (!SystemHelper.IsRecentVersion(currentVersion, this.LatestVersion))
        {
          this.Log.DoLogWarn("Your bot is out of date! The most recent version of PTMagic is " + this.LatestVersion);
        }
      }
    }

    private void LoadCurrentProfitTrailerProperties()
    {
      this.Log.DoLogInfo("Loading current Profit Trailer properties from preset files...");
      SettingsHandler.CompileProperties(this, this.ActiveSetting, this.LastSettingsChange.ToLocalTime());

      if (this.PairsLines != null && this.DCALines != null && this.IndicatorsLines != null)
      {
        this.Log.DoLogInfo("Properties loaded - P (" + this.PairsLines.Count.ToString() + " lines) - D (" + this.DCALines.Count.ToString() + " lines) - I (" + this.IndicatorsLines.Count.ToString() + " lines).");
      }
      else
      {
        this.Log.DoLogError("Unable to load all Profit Trailer properties! Waiting for the next interval to retry...");
        Exception ex = new Exception("Unable to load all Profit Trailer properties! Waiting for the next interval to retry...");
        this.State = 0;
        throw ex;
      }

      this.LastRuntimeSummary.MainMarket = SettingsHandler.GetMainMarket(this.PTMagicConfiguration, this.PairsLines, this.Log);
    }

    private void LoadSMSSummaries()
    {
      this.Log.DoLogInfo("Loading Single Market Setting Summaries...");
      this.SingleMarketSettingSummaries = new List<SingleMarketSettingSummary>();
      if (File.Exists(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + "SingleMarketSettingSummary.json"))
      {
        try
        {
          Dictionary<string, bool> smsVerificationResult = new Dictionary<string, bool>();
          foreach (SingleMarketSettingSummary smsSummary in JsonConvert.DeserializeObject<List<SingleMarketSettingSummary>>(System.IO.File.ReadAllText(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + "SingleMarketSettingSummary.json")))
          {
            string smsName = smsSummary.SingleMarketSetting.SettingName;
            bool smsIsValid = false;
            if (smsVerificationResult.ContainsKey(smsName))
            {
              smsIsValid = smsVerificationResult[smsName];
            }
            else
            {
              SingleMarketSetting sms = this.PTMagicConfiguration.AnalyzerSettings.SingleMarketSettings.Find(s => s.SettingName.Equals(smsName));
              if (sms != null)
              {
                smsIsValid = true;
                smsVerificationResult.Add(smsName, true);
              }
              else
              {
                smsVerificationResult.Add(smsName, false);
              }
            }

            if (smsIsValid)
            {
              this.SingleMarketSettingSummaries.Add(smsSummary);
            }
          }

          this.Log.DoLogInfo("Single Market Setting Summaries loaded.");
        }
        catch { }
      }
    }

    private async Task BuildMarketDataAsync(CancellationToken cancellationToken = default)
    {
      if (!String.IsNullOrEmpty(this.PTMagicConfiguration.GeneralSettings.Application.CoinMarketCapAPIKey))
      {
        await CoinMarketCap.GetMarketDataAsync(this.PTMagicConfiguration, this.Log, cancellationToken).ConfigureAwait(false);
      }
      else
      {
        this.Log.DoLogInfo("No CMC API-Key specified. That's OK, but no CMC Data can be pulled.");
      }

      if (this.PTMagicConfiguration.GeneralSettings.Application.Exchange.Equals("Bittrex", StringComparison.InvariantCultureIgnoreCase))
      {
        this.ExchangeMarketList = Bittrex.GetMarketData(this.LastRuntimeSummary.MainMarket, this.MarketInfos, this.PTMagicConfiguration, this.Log);
      }
      else if (this.PTMagicConfiguration.GeneralSettings.Application.Exchange.Equals("Binance", StringComparison.InvariantCultureIgnoreCase))
      {
        this.ExchangeMarketList = await Binance.GetMarketDataAsync(this.LastRuntimeSummary.MainMarket, this.MarketInfos, this.PTMagicConfiguration, this.Log, cancellationToken).ConfigureAwait(false);
      }
      else if (this.PTMagicConfiguration.GeneralSettings.Application.Exchange.Equals("BinanceUS", StringComparison.InvariantCultureIgnoreCase))
      {
        this.ExchangeMarketList = await BinanceUS.GetMarketDataAsync(this.LastRuntimeSummary.MainMarket, this.MarketInfos, this.PTMagicConfiguration, this.Log, cancellationToken).ConfigureAwait(false);
      }
      else if (this.PTMagicConfiguration.GeneralSettings.Application.Exchange.Equals("BinanceFutures", StringComparison.InvariantCultureIgnoreCase))
      {
        this.ExchangeMarketList = await BinanceFutures.GetMarketDataAsync(this.LastRuntimeSummary.MainMarket, this.MarketInfos, this.PTMagicConfiguration, this.Log, cancellationToken).ConfigureAwait(false);
      }
      else if (this.PTMagicConfiguration.GeneralSettings.Application.Exchange.Equals("Poloniex", StringComparison.InvariantCultureIgnoreCase))
      {
        this.ExchangeMarketList = Poloniex.GetMarketData(this.LastRuntimeSummary.MainMarket, this.MarketInfos, this.PTMagicConfiguration, this.Log);
      }

      if (this.ExchangeMarketList == null)
      {
        Exception ex = new Exception("Unable to contact " + this.PTMagicConfiguration.GeneralSettings.Application.Exchange + " for fresh market data. Trying again in " + this.PTMagicConfiguration.AnalyzerSettings.MarketAnalyzer.IntervalMinutes + " minute(s).");
        Log.DoLogError(ex.Message);
        this.State = Constants.PTMagicBotState_Idle;
        throw ex;
      }
    }

    private void BuildMarketList()
    {
      string marketPairs = SettingsHandler.GetMarketPairs(this.PTMagicConfiguration, this.PairsLines, this.Log);
      if (marketPairs.ToLower().Equals("all") || marketPairs.ToLower().Equals("false") || marketPairs.ToLower().Equals("true") || String.IsNullOrEmpty(marketPairs))
      {
        this.MarketList = this.ExchangeMarketList;
      }
      else
      {
        List<string> originalMarketList = SystemHelper.ConvertTokenStringToList(marketPairs, ",");
        foreach (string market in originalMarketList)
        {
          this.MarketList.Add(SystemHelper.GetFullMarketName(this.LastRuntimeSummary.MainMarket, market, this.PTMagicConfiguration.GeneralSettings.Application.Exchange));
        }
      }
    }

    private void ValidateMarketList()
    {
      List<string> validMarkets = this.MarketList.FindAll(m => m.IndexOf(this.LastRuntimeSummary.MainMarket, StringComparison.InvariantCultureIgnoreCase) > -1);
      if (validMarkets.Count == 0)
      {
        Exception ex = new Exception("No valid pairs found for main market '" + this.LastRuntimeSummary.MainMarket + "' in configured pars list (" + SystemHelper.ConvertListToTokenString(this.MarketList, ",", true) + ")! Terminating process...");
        Log.DoLogError(ex.Message);
        this.State = Constants.PTMagicBotState_Idle;
        this.Timer.Stop();
        throw ex;
      }
    }

    private void BuildGlobalMarketTrends()
    {
      this.Log.DoLogInfo("Build global market trends...");
      this.SingleMarketTrendChanges = BaseAnalyzer.BuildMarketTrends("Exchange", this.LastRuntimeSummary.MainMarket, this.MarketList, "Volume", false, new Dictionary<string, List<MarketTrendChange>>(), this.PTMagicConfiguration, this.Log);
      this.GlobalMarketTrendChanges = new Dictionary<string, List<MarketTrendChange>>();
      this.GlobalMarketTrendChanges = BaseAnalyzer.BuildMarketTrends("CoinMarketCap", this.LastRuntimeSummary.MainMarket, new List<string>(), "", true, this.GlobalMarketTrendChanges, this.PTMagicConfiguration, this.Log);

      foreach (MarketTrend marketTrend in this.PTMagicConfiguration.AnalyzerSettings.MarketAnalyzer.MarketTrends.FindAll(mt => mt.Platform.Equals("Exchange", StringComparison.InvariantCultureIgnoreCase)))
      {
        if (this.SingleMarketTrendChanges.ContainsKey(marketTrend.Name))
        {
          int maxMarkets = this.SingleMarketTrendChanges[marketTrend.Name].Count;
          if (marketTrend.MaxMarkets > 0 && marketTrend.MaxMarkets <= this.SingleMarketTrendChanges[marketTrend.Name].Count)
          {
            maxMarkets = marketTrend.MaxMarkets;
          }

          this.GlobalMarketTrendChanges.Add(marketTrend.Name, this.SingleMarketTrendChanges[marketTrend.Name].Take(maxMarkets).ToList());
        }
      }

      this.AverageMarketTrendChanges = BaseAnalyzer.BuildGlobalMarketTrends(this.GlobalMarketTrendChanges, this.PTMagicConfiguration, this.Log);
      this.Log.DoLogInfo("Global market trends built.");
    }

    private void CheckGlobalSettingsTriggers(ref GlobalSetting triggeredSetting, ref List<string> matchedTriggers)
    {
        this.Log.DoLogInfo("Checking global settings triggers...");

        foreach (GlobalSetting globalSetting in this.PTMagicConfiguration.AnalyzerSettings.GlobalSettings)
        {
            matchedTriggers = new List<string>();

            if (globalSetting.Triggers.Count > 0)
            {
                this.Log.DoLogInfo("Checking triggers for '" + globalSetting.SettingName + "'...");
                Dictionary<string, bool> triggerResults = new Dictionary<string, bool>();
                foreach (Trigger trigger in globalSetting.Triggers)
                {
                    MarketTrend marketTrend = this.PTMagicConfiguration.AnalyzerSettings.MarketAnalyzer.MarketTrends.Find(mt => mt.Name == trigger.MarketTrendName);
                    if (marketTrend != null)
                    {
                        if (this.AverageMarketTrendChanges.ContainsKey(marketTrend.Name))
                        {
                            double averageMarketTrendChange = this.AverageMarketTrendChanges[marketTrend.Name];
                            bool isTriggered = averageMarketTrendChange >= trigger.MinChange && averageMarketTrendChange < trigger.MaxChange;
                            triggerResults[trigger.Tag] = isTriggered;

                            if (isTriggered)
                            {
                                this.Log.DoLogInfo("Trigger '" + trigger.MarketTrendName + "' triggered! TrendChange = " + averageMarketTrendChange.ToString("#,#0.00", new System.Globalization.CultureInfo("en-US")) + "%");

                                string triggerContent = trigger.MarketTrendName + " - ";
                                if (trigger.MinChange != Constants.MinTrendChange)
                                {
                                  triggerContent += " - Min: " + trigger.MinChange.ToString("#,#0.00", new System.Globalization.CultureInfo("en-US")) + "%";
                                }

                                if (trigger.MaxChange != Constants.MaxTrendChange)
                                {
                                  triggerContent += " - Max: " + trigger.MaxChange.ToString("#,#0.00", new System.Globalization.CultureInfo("en-US")) + "%";
                                }

                                matchedTriggers.Add(triggerContent);
                            }
                            else
                            {
                                this.Log.DoLogDebug("Trigger '" + trigger.MarketTrendName + "' not triggered. TrendChange = " + averageMarketTrendChange.ToString("#,#0.00", new System.Globalization.CultureInfo("en-US")) + "%");
                            }
                        }
                        else
                        {
                            this.Log.DoLogError("Trigger '" + trigger.MarketTrendName + "' not found in this.AverageMarketTrendChanges[] (" + SystemHelper.ConvertListToTokenString(this.AverageMarketTrendChanges.Keys.ToList(), ",", true) + "). Unable to load recent trends?");
                        }
                    }
                    else
                    {
                        this.Log.DoLogWarn("Market Trend '" + trigger.MarketTrendName + "' not found! Trigger ignored!");
                    }
                }

                if (!string.IsNullOrEmpty(globalSetting.TriggerConnection))
                {
                    if (globalSetting.TriggerConnection.ToLower() == "and" || globalSetting.TriggerConnection.ToLower() == "or")
                    {
                        bool settingTriggered = false;
                        switch (globalSetting.TriggerConnection.ToLower())
                        {
                            case "and":
                                settingTriggered = triggerResults.Values.All(tr => tr);
                                break;
                            case "or":
                                settingTriggered = triggerResults.Values.Any(tr => tr);
                                break;
                        }

                        if (settingTriggered)
                        {
                            triggeredSetting = globalSetting;
                            break;
                        }
                    }
                    else
                    {
                        string triggerConnection = globalSetting.TriggerConnection;
                        foreach (var triggerResult in triggerResults)
                        {
                          if (!string.IsNullOrEmpty(triggerResult.Key))
                          {
                              triggerConnection = triggerConnection.Replace(triggerResult.Key, triggerResult.Value.ToString().ToLower());
                          }
                          else
                          {
                              this.Log.DoLogError($"ERROR: A required trigger Tag is missing for global setting {globalSetting.SettingName}. Program halted.");
                              Environment.Exit(1);
                          }
                        }

                        try
                        {
                            bool settingTriggered = (bool)System.Linq.Dynamic.Core.DynamicExpressionParser.ParseLambda(System.Linq.Dynamic.Core.ParsingConfig.Default, new ParameterExpression[0], typeof(bool), triggerConnection).Compile().DynamicInvoke();
                            if (settingTriggered)
                            {
                                triggeredSetting = globalSetting;
                                break;
                            }
                        }
                        catch (Exception)
                        {
                            this.Log.DoLogError($"ERROR: Trigger Connection for global setting {globalSetting.SettingName} is invalid or missing. Program halted.");
                            Environment.Exit(1);
                        }
                    }
                }
                else
                {
                    this.Log.DoLogError($"ERROR: Trigger Connection for global setting {globalSetting.SettingName} is missing. Program halted.");
                    Environment.Exit(1);
                }
            }
        }
    }

    private void ActivateSetting(ref GlobalSetting triggeredSetting, ref List<string> matchedTriggers)
    {
      if (this.EnforceSettingsReapply || !this.ActiveSettingName.Equals(triggeredSetting.SettingName, StringComparison.InvariantCultureIgnoreCase))
      {
        this.Log.DoLogInfo("Setting '" + this.ActiveSettingName + "' currently active. Checking for flood protection...");
        if (triggeredSetting.SettingName.Equals(this.DefaultSettingName, StringComparison.InvariantCultureIgnoreCase))
        {
          matchedTriggers = new List<string>();
        }

        if (this.EnforceSettingsReapply || this.LastSettingsChange <= DateTime.UtcNow.AddMinutes(-PTMagicConfiguration.GeneralSettings.Application.FloodProtectionMinutes))
        {
          if (!this.ActiveSettingName.Equals(triggeredSetting.SettingName, StringComparison.InvariantCultureIgnoreCase))
          {
            this.Log.DoLogInfo("Switching global settings to '" + triggeredSetting.SettingName + "'...");
          }
          else
          {
            this.Log.DoLogInfo("Applying '" + triggeredSetting.SettingName + "' as the settings.analyzer.json or a preset file got changed.");
          }

          SettingsHandler.CompileProperties(this, triggeredSetting, DateTime.Now);
          this.GlobalSettingWritten = true;
          this.LastRuntimeSummary.LastGlobalSettingSwitch = this.LastRuntimeSummary.LastRuntime;
          this.LastRuntimeSummary.CurrentGlobalSetting = triggeredSetting;
          this.LastSettingsChange = DateTime.UtcNow;

          try
          {
            string telegramMessage;
            telegramMessage = this.PTMagicConfiguration.GeneralSettings.Application.InstanceName + ": Setting switched to '*" + SystemHelper.SplitCamelCase(triggeredSetting.SettingName) + "*'.";

            if (matchedTriggers.Count > 0)
            {
              telegramMessage += "\n\n*Matching Triggers:*";
              foreach (string triggerResult in matchedTriggers)
              {
                telegramMessage += "\n" + triggerResult;
              }
            }

            if (this.AverageMarketTrendChanges.Keys.Count > 0)
            {
              telegramMessage += "\n\n*Market Trends:*";
              foreach (string key in this.AverageMarketTrendChanges.Keys)
              {
                telegramMessage += "\n" + key + ": " + this.AverageMarketTrendChanges[key].ToString("#,#0.00", new System.Globalization.CultureInfo("en-US")) + "%";
              }
            }

            if (this.PTMagicConfiguration.GeneralSettings.Telegram.IsEnabled)
            {
              TelegramHelper.SendMessage(this.PTMagicConfiguration.GeneralSettings.Telegram.BotToken, this.PTMagicConfiguration.GeneralSettings.Telegram.ChatId, telegramMessage, this.PTMagicConfiguration.GeneralSettings.Telegram.SilentMode, this.Log);
            }
          }
          catch (Exception ex)
          {
            this.Log.DoLogCritical("Failed to send Telegram message", ex);
          }
        }
        else
        {
          this.Log.DoLogInfo("Flood protection active until " + this.LastSettingsChange.AddMinutes(PTMagicConfiguration.GeneralSettings.Application.FloodProtectionMinutes).ToString() + " (UTC). Not switching settings to '" + triggeredSetting.SettingName + "'!");
          this.LastRuntimeSummary.FloodProtectedSetting = triggeredSetting;
          this.LastRuntimeSummary.CurrentGlobalSetting = this.ActiveSetting;
        }
      }
      else
      {
        matchedTriggers = new List<string>();
        this.Log.DoLogInfo("Setting '" + triggeredSetting.SettingName + "' already active. No action taken.");
        this.LastRuntimeSummary.CurrentGlobalSetting = triggeredSetting;
      }

      this.ActiveSetting = this.LastRuntimeSummary.CurrentGlobalSetting;
      this.ActiveSettingName = this.ActiveSetting.SettingName;
    }

    private void ApplySingleMarketSettings()
    {
      if (this.PTMagicConfiguration.AnalyzerSettings.SingleMarketSettings.Count > 0)
      {
        this.Log.DoLogInfo("Checking single market settings triggers for " + this.MarketList.Count.ToString() + " markets...");

        int marketPairProcess = 1;
        Dictionary<string, List<string>> matchedMarketTriggers = new Dictionary<string, List<string>>();
        string mainMarket = this.LastRuntimeSummary.MainMarket;

        foreach (string marketPair in this.MarketList)
        {
          this.Log.DoLogDebug("'" + marketPair + "' - Checking triggers (" + marketPairProcess.ToString() + "/" + this.MarketList.Count.ToString() + ")...");
          string market = marketPair.Replace(mainMarket, "");
          switch (this.PTMagicConfiguration.GeneralSettings.Application.Exchange.ToLower())
          {
            case "bittrex":
              market = market.Replace("-", "");
              break;
            case "poloniex":
              market = market.Replace("_", "");
              break;
          }
          bool stopTriggers = false;

          foreach (SingleMarketSetting marketSetting in this.PTMagicConfiguration.AnalyzerSettings.SingleMarketSettings)
          {
            List<string> matchedSingleMarketTriggers = new List<string>();

            string ignored = marketSetting.IgnoredMarkets.ToUpper();
            ignored = ignored.Replace(mainMarket, "");
            switch (this.PTMagicConfiguration.GeneralSettings.Application.Exchange.ToLower())
            {
              case "bittrex":
                ignored = ignored.Replace("-", "");
                break;
              case "poloniex":
                ignored = ignored.Replace("_", "");
                break;
            }
            List<string> ignoredMarkets = SystemHelper.ConvertTokenStringToList(ignored, ",");
            if (ignoredMarkets.Contains(market))
            {
              this.Log.DoLogDebug("'" + marketPair + "' - Is ignored in '" + marketSetting.SettingName + "'.");
              continue;
            }

            string allowed = marketSetting.AllowedMarkets.ToUpper();
            allowed = allowed.Replace(mainMarket, "");
            switch (this.PTMagicConfiguration.GeneralSettings.Application.Exchange.ToLower())
            {
              case "bittrex":
                allowed = allowed.Replace("-", "");
                break;
              case "poloniex":
                allowed = allowed.Replace("_", "");
                break;
            }
            List<string> allowedMarkets = SystemHelper.ConvertTokenStringToList(allowed, ",");
            if (allowedMarkets.Count > 0 && !allowedMarkets.Contains(market))
            {
              this.Log.DoLogDebug("'" + marketPair + "' - Is not allowed in '" + marketSetting.SettingName + "'.");
              continue;
            }

            List<string> ignoredGlobalSettings = SystemHelper.ConvertTokenStringToList(marketSetting.IgnoredGlobalSettings, ",");
            if (ignoredGlobalSettings.Contains(this.ActiveSettingName))
            {
              this.Log.DoLogDebug("'" + marketPair + "' - '" + this.ActiveSettingName + "' - Is ignored in '" + marketSetting.SettingName + "'.");
              continue;
            }

            List<string> allowedGlobalSettings = SystemHelper.ConvertTokenStringToList(marketSetting.AllowedGlobalSettings, ",");
            if (allowedGlobalSettings.Count > 0 && !allowedGlobalSettings.Contains(this.ActiveSettingName))
            {
              this.Log.DoLogDebug("'" + marketPair + "' - '" + this.ActiveSettingName + "' - Is not allowed in '" + marketSetting.SettingName + "'.");
              continue;
            }

            SingleMarketSettingSummary smss = this.SingleMarketSettingSummaries.Find(s => s.Market.Equals(marketPair, StringComparison.InvariantCultureIgnoreCase) && s.SingleMarketSetting.SettingName.Equals(marketSetting.SettingName, StringComparison.InvariantCultureIgnoreCase));
            if (smss != null)
            {
              if (marketSetting.OffTriggers != null)
              {
                if (marketSetting.OffTriggers.Count > 0)
                {
                  this.Log.DoLogDebug("'" + marketPair + "' - Checking off triggers '" + marketSetting.SettingName + "'...");
                  List<bool> offTriggerResults = new List<bool>();
                  foreach (OffTrigger offTrigger in marketSetting.OffTriggers)
                  {
                    if (offTrigger.HoursSinceTriggered > 0)
                    {
                      int smsActiveHours = (int)Math.Floor(DateTime.UtcNow.Subtract(smss.ActivationDateTimeUTC).TotalHours);
                      if (smsActiveHours >= offTrigger.HoursSinceTriggered)
                      {
                        this.Log.DoLogDebug("'" + marketPair + "' - SMS already active for  " + smsActiveHours.ToString() + " hours. Trigger matched!");
                        offTriggerResults.Add(true);
                      }
                      else
                      {
                        this.Log.DoLogDebug("'" + marketPair + "' - SMS only active for  " + smsActiveHours.ToString() + " hours. Trigger not matched!");
                        offTriggerResults.Add(false);
                      }
                    }
                    else if (offTrigger.Min24hVolume > 0 || offTrigger.Max24hVolume < Constants.Max24hVolume)
                    {
                      List<MarketTrendChange> marketTrendChanges = this.SingleMarketTrendChanges[this.SingleMarketTrendChanges.Keys.Last()];
                      if (marketTrendChanges.Count > 0)
                      {
                        MarketTrendChange mtc = marketTrendChanges.Find(m => m.Market.Equals(marketPair, StringComparison.InvariantCultureIgnoreCase));
                        if (mtc != null)
                        {
                          if (mtc.Volume24h >= offTrigger.Min24hVolume && mtc.Volume24h <= offTrigger.Max24hVolume)
                          {
                            this.Log.DoLogDebug("'" + marketPair + "' - 24h volume off trigger matched! 24h volume = " + mtc.Volume24h.ToString(new System.Globalization.CultureInfo("en-US")) + " " + this.LastRuntimeSummary.MainMarket);
                            offTriggerResults.Add(true);
                          }
                          else
                          {
                            this.Log.DoLogDebug("'" + marketPair + "' - 24h volume off trigger not matched! 24h volume = " + mtc.Volume24h.ToString(new System.Globalization.CultureInfo("en-US")) + " " + this.LastRuntimeSummary.MainMarket);
                            offTriggerResults.Add(false);
                          }
                        }
                      }
                    }
                    else
                    {
                      if (this.SingleMarketTrendChanges.ContainsKey(offTrigger.MarketTrendName))
                      {
                        List<MarketTrendChange> marketTrendChanges = this.SingleMarketTrendChanges[offTrigger.MarketTrendName];
                        if (marketTrendChanges.Count > 0)
                        {
                          double averageMarketTrendChange = 0;
                          var trendThreshold = (from mt in this.PTMagicConfiguration.AnalyzerSettings.MarketAnalyzer.MarketTrends
                                                where mt.Name == offTrigger.MarketTrendName
                                                select new { mt.TrendThreshold }).Single();

                          if (trendThreshold.TrendThreshold != 0)
                          {
                            var includedMarkets = from m in marketTrendChanges
                                                  where m.TrendChange <= trendThreshold.TrendThreshold && m.TrendChange >= (trendThreshold.TrendThreshold * -1.0)
                                                  orderby m.Market
                                                  select m;
                            averageMarketTrendChange = includedMarkets.Average(m => m.TrendChange);
                          }
                          else
                          {
                            averageMarketTrendChange = marketTrendChanges.Average(m => m.TrendChange);
                          }

                          MarketTrendChange mtc = marketTrendChanges.Find(m => m.Market.Equals(marketPair, StringComparison.InvariantCultureIgnoreCase));
                          if (mtc != null)
                          {
                            double trendChange = mtc.TrendChange;
                            if (offTrigger.MarketTrendRelation.Equals(Constants.MarketTrendRelationRelative))
                            {
                              trendChange = trendChange - averageMarketTrendChange;
                            }
                            else if (offTrigger.MarketTrendRelation.Equals(Constants.MarketTrendRelationRelativeTrigger))
                            {
                              double currentPrice = mtc.LastPrice;
                              double triggerPrice = smss.TriggerSnapshot.LastPrice;
                              trendChange = (currentPrice - triggerPrice) / triggerPrice * 100;
                            }

                            if (trendChange >= offTrigger.MinChange && trendChange < offTrigger.MaxChange)
                            {
                              this.Log.DoLogDebug("'" + marketPair + "' - Off Trigger '" + offTrigger.MarketTrendName + "' triggered! TrendChange (" + offTrigger.MarketTrendRelation + ") = " + trendChange.ToString("#,#0.00", new System.Globalization.CultureInfo("en-US")) + "%");
                              offTriggerResults.Add(true);
                            }
                            else
                            {
                              this.Log.DoLogDebug("'" + marketPair + "' - Off Trigger '" + offTrigger.MarketTrendName + "' not triggered. TrendChange (" + offTrigger.MarketTrendRelation + ") = " + trendChange.ToString("#,#0.00", new System.Globalization.CultureInfo("en-US")) + "%");
                              offTriggerResults.Add(false);
                            }
                          }
                          else
                          {
                            offTriggerResults.Add(false);
                          }
                        }
                        else
                        {
                          offTriggerResults.Add(false);
                        }
                      }
                    }
                  }

                  bool settingOffTriggered = false;
                  switch (marketSetting.OffTriggerConnection.ToLower())
                  {
                    case "and":
                      settingOffTriggered = offTriggerResults.FindAll(tr => tr == false).Count == 0;
                      break;
                    case "or":
                      settingOffTriggered = offTriggerResults.FindAll(tr => tr == true).Count > 0;
                      break;
                  }

                  if (settingOffTriggered)
                  {
                    this.Log.DoLogDebug("'" + marketPair + "' - '" + marketSetting.SettingName + "' off triggered!");
                    this.SingleMarketSettingSummaries.Remove(smss);
                    smss = null;
                  }
                  else
                  {
                    this.Log.DoLogDebug("'" + marketPair + "' - '" + marketSetting.SettingName + "' not off triggered!");
                  }
                }
                else
                {
                  this.Log.DoLogDebug("'" + marketPair + "' - '" + marketSetting.SettingName + "' has no off triggers -> triggering off!");
                  this.SingleMarketSettingSummaries.Remove(smss);
                  smss = null;
                }
              }
            }

            if (marketSetting.Triggers.Count > 0 && !stopTriggers)
            {
              this.Log.DoLogDebug("'" + marketPair + "' - Checking triggers for '" + marketSetting.SettingName + "'...");

              List<bool> triggerResults = new List<bool>();
              Dictionary<int, double> relevantTriggers = new Dictionary<int, double>();
              int triggerIndex = 0;
              Dictionary<string, bool> triggerTagsResults = new Dictionary<string, bool>();
              foreach (Trigger trigger in marketSetting.Triggers)
              {
                  triggerTagsResults[trigger.Tag] = false;
              }

              foreach (Trigger trigger in marketSetting.Triggers)
              {
                if (trigger.Min24hVolume > 0 || trigger.Max24hVolume < Constants.Max24hVolume)
                {
                  List<MarketTrendChange> marketTrendChanges = this.SingleMarketTrendChanges[this.SingleMarketTrendChanges.Keys.Last()];
                  if (marketTrendChanges.Count > 0)
                  {
                    MarketTrendChange mtc = marketTrendChanges.Find(m => m.Market.Equals(marketPair, StringComparison.InvariantCultureIgnoreCase));
                    if (mtc != null)
                    {
                      if (mtc.Volume24h >= trigger.Min24hVolume && mtc.Volume24h <= trigger.Max24hVolume)
                      {
                        this.Log.DoLogDebug("'" + marketPair + "' - 24h volume trigger matched! 24h volume = " + mtc.Volume24h.ToString(new System.Globalization.CultureInfo("en-US")) + " " + this.LastRuntimeSummary.MainMarket);
                        relevantTriggers.Add(triggerIndex, mtc.Volume24h);

                        string triggerContent = "24h Volume";
                        if (trigger.Min24hVolume > 0)
                        {
                          triggerContent += " - Min: " + trigger.Min24hVolume.ToString(new System.Globalization.CultureInfo("en-US")) + " " + this.LastRuntimeSummary.MainMarket;
                        }
                        if (trigger.Max24hVolume < Constants.Max24hVolume)
                        {
                          triggerContent += " - Max: " + trigger.Max24hVolume.ToString(new System.Globalization.CultureInfo("en-US")) + " " + this.LastRuntimeSummary.MainMarket;
                        }

                        matchedSingleMarketTriggers.Add(marketSetting.SettingName + ": " + triggerContent + " - 24h volume = " + mtc.Volume24h.ToString(new System.Globalization.CultureInfo("en-US")) + " " + this.LastRuntimeSummary.MainMarket);
                        triggerResults.Add(true);
                        triggerTagsResults[trigger.Tag] = true;
                      }
                      else
                      {
                        this.Log.DoLogDebug("'" + marketPair + "' - 24h volume trigger not matched. 24h volume = " + mtc.Volume24h.ToString(new System.Globalization.CultureInfo("en-US")) + " " + this.LastRuntimeSummary.MainMarket);
                        triggerResults.Add(false);
                      }
                    }
                  }
                }
                else if (trigger.AgeDaysLowerThan > 0)
                {
                  MarketInfo marketInfo = null;
                  if (this.MarketInfos.ContainsKey(marketPair))
                  {
                    marketInfo = this.MarketInfos[marketPair];
                  }

                  if (marketInfo != null)
                  {
                    int marketAge = (int)Math.Floor(DateTime.UtcNow.Subtract(marketInfo.FirstSeen).TotalDays);
                    if (marketAge < trigger.AgeDaysLowerThan)
                    {
                      matchedSingleMarketTriggers.Add(marketSetting.SettingName + ": '" + marketPair + "' is only " + marketAge.ToString() + " days old on this exchange. Trigger matched!");
                      this.Log.DoLogDebug("'" + marketPair + "' - Is only " + marketAge.ToString() + " days old on this exchange. Trigger matched!");
                      relevantTriggers.Add(triggerIndex, marketAge);
                      triggerResults.Add(true);
                      triggerTagsResults[trigger.Tag] = true;
                    }
                    else
                    {
                      this.Log.DoLogDebug("'" + marketPair + "' - Age Trigger not triggered. Is already " + marketAge.ToString() + " days old on this exchange.");
                      triggerResults.Add(false);
                    }
                  }
                  else
                  {
                    matchedSingleMarketTriggers.Add("Age for '" + marketPair + "' not found, trigger matched just to be safe!");
                    this.Log.DoLogDebug("'" + marketPair + "' - Age not found, trigger matched just to be safe!");
                    triggerResults.Add(true);
                  }
                }
                else
                {
                  if (this.SingleMarketTrendChanges.ContainsKey(trigger.MarketTrendName))
                  {
                    List<MarketTrendChange> marketTrendChanges = this.SingleMarketTrendChanges[trigger.MarketTrendName];
                    if (marketTrendChanges.Count > 0)
                    {
                      double averageMarketTrendChange = 0;
                      var trendThreshold = (from mt in this.PTMagicConfiguration.AnalyzerSettings.MarketAnalyzer.MarketTrends
                                            where mt.Name == trigger.MarketTrendName
                                            select new { mt.TrendThreshold }).Single();

                      if (trendThreshold.TrendThreshold != 0)
                      {
                        var includedMarkets = from m in marketTrendChanges
                                              where m.TrendChange <= trendThreshold.TrendThreshold && m.TrendChange >= (trendThreshold.TrendThreshold * -1.0)
                                              orderby m.Market
                                              select m;
                        averageMarketTrendChange = includedMarkets.Average(m => m.TrendChange);
                      }
                      else
                      {
                        averageMarketTrendChange = marketTrendChanges.Average(m => m.TrendChange);
                      }

                      MarketTrendChange mtc = marketTrendChanges.Find(m => m.Market.Equals(marketPair, StringComparison.InvariantCultureIgnoreCase));
                      if (mtc != null)
                      {
                        double trendChange = mtc.TrendChange;
                        if (trigger.MarketTrendRelation.Equals(Constants.MarketTrendRelationRelative))
                        {
                          trendChange = trendChange - averageMarketTrendChange;
                        }

                        if (trendChange >= trigger.MinChange && trendChange < trigger.MaxChange)
                        {
                          this.Log.DoLogDebug("'" + marketPair + "' - Trigger '" + trigger.MarketTrendName + "' triggered! TrendChange (" + trigger.MarketTrendRelation + ") = " + trendChange.ToString("#,#0.00", new System.Globalization.CultureInfo("en-US")) + "%");
                          relevantTriggers.Add(triggerIndex, trendChange);

                          string triggerContent = trigger.MarketTrendName + " - ";
                          if (trigger.MinChange != Constants.MinTrendChange)
                          {
                            triggerContent += " - Min: " + trigger.MinChange.ToString("#,#0.00", new System.Globalization.CultureInfo("en-US")) + "%";
                          }
                          if (trigger.MaxChange != Constants.MaxTrendChange)
                          {
                            triggerContent += " - Max: " + trigger.MaxChange.ToString("#,#0.00", new System.Globalization.CultureInfo("en-US")) + "%";
                          }

                          matchedSingleMarketTriggers.Add(marketSetting.SettingName + ": " + triggerContent + " - TrendChange (" + trigger.MarketTrendRelation + ") = " + trendChange.ToString("#,#0.00", new System.Globalization.CultureInfo("en-US")) + "%");
                          triggerResults.Add(true);
                          triggerTagsResults[trigger.Tag] = true;
                        }
                        else
                        {
                          this.Log.DoLogDebug("'" + marketPair + "' - Trigger '" + trigger.MarketTrendName + "' not triggered. TrendChange (" + trigger.MarketTrendRelation + ") = " + trendChange.ToString("#,#0.00", new System.Globalization.CultureInfo("en-US")) + "%");
                          triggerResults.Add(false);
                        }
                      }
                      else
                      {
                        this.Log.DoLogDebug("'" + marketPair + "' - No market trend change found for '" + trigger.MarketTrendName + "'! Coin just got released? Trigger ignored!");
                        triggerResults.Add(false);
                      }
                    }
                    else
                    {
                      this.Log.DoLogWarn("'" + marketPair + "' - No market trend changes found for '" + trigger.MarketTrendName + "'! Trigger ignored!");
                      triggerResults.Add(false);
                    }
                  }
                  else
                  {
                    MarketTrend marketTrend = this.PTMagicConfiguration.AnalyzerSettings.MarketAnalyzer.MarketTrends.Find(mt => mt.Name.Equals(trigger.MarketTrendName, StringComparison.InvariantCultureIgnoreCase));
                    if (marketTrend != null)
                    {
                      if (!marketTrend.Platform.Equals("Exchange", StringComparison.InvariantCultureIgnoreCase))
                      {
                        this.Log.DoLogWarn("Market Trend '" + trigger.MarketTrendName + "' is invalid for single market settings! Only trends using the platform 'Exchange' are valid for single market settings.");
                        triggerResults.Add(false);
                      }
                    }
                    else
                    {
                      this.Log.DoLogWarn("Market Trend '" + trigger.MarketTrendName + "' not found! Trigger ignored!");
                    }
                  }
                }
                triggerIndex++;
              }

              bool settingTriggered = false;
              if (marketSetting.TriggerConnection.ToLower() == "and" || marketSetting.TriggerConnection.ToLower() == "or")
              {
                switch (marketSetting.TriggerConnection.ToLower())
                {
                  case "and":
                    settingTriggered = triggerResults.FindAll(tr => tr == false).Count == 0;
                    break;
                  case "or":
                    settingTriggered = triggerResults.FindAll(tr => tr == true).Count > 0;
                    break;
                }
              }
              else
              {
                string triggerConnection = marketSetting.TriggerConnection;
                foreach (var triggerResult in triggerTagsResults)
                {
                  if (!string.IsNullOrEmpty(triggerResult.Key))
                  {
                    triggerConnection = triggerConnection.Replace(triggerResult.Key, triggerResult.Value.ToString().ToLower());
                  }
                  else
                  {
                    this.Log.DoLogError($"ERROR: A required trigger Tag is missing for global setting {marketSetting.SettingName}. Program halted.");
                    Environment.Exit(1);
                  }
                }
                try
                {
                  settingTriggered = (bool)DynamicExpressionParser.ParseLambda(ParsingConfig.Default, new ParameterExpression[0], typeof(bool), triggerConnection).Compile().DynamicInvoke();
                }
                catch (Exception)
                {
                  this.Log.DoLogError($"ERROR: Trigger Connection for global setting {marketSetting.SettingName} is invalid or missing. Program halted.");
                  Environment.Exit(1);
                }
              }

              bool isFreshTrigger = true;
              if (!settingTriggered)
              {
                this.Log.DoLogDebug("'" + marketPair + "' - SMS '" + marketSetting.SettingName + "' not triggered, checking for long term activation.");
                if (smss != null && marketSetting.OffTriggers != null && marketSetting.OffTriggers.Count > 0)
                {
                  this.Log.DoLogDebug("'" + marketPair + "' - SMS '" + marketSetting.SettingName + "' has off triggers, starting special trigger...");
                  settingTriggered = true;
                  isFreshTrigger = false;

                  matchedSingleMarketTriggers = new List<string>();
                  foreach (string matchedTriggerContent in smss.TriggerSnapshot.MatchedTriggersContent)
                  {
                    if (matchedTriggerContent.StartsWith(marketSetting.SettingName + ":"))
                    {
                      matchedSingleMarketTriggers.Add(matchedTriggerContent);
                    }
                  }

                  int removalLength = matchedSingleMarketTriggers.Count - marketSetting.Triggers.Count;
                  if (removalLength > 0)
                  {
                    matchedSingleMarketTriggers.RemoveRange(0, removalLength);
                  }

                  this.Log.DoLogDebug("'" + marketPair + "' - Activating SMS '" + marketSetting.SettingName + "' as off triggers are not met.");
                }
              }

              if (settingTriggered)
              {
                this.Log.DoLogDebug("'" + marketPair + "' - '" + marketSetting.SettingName + "' triggered!");

                if (!matchedMarketTriggers.ContainsKey(marketPair))
                {
                  matchedMarketTriggers.Add(marketPair, matchedSingleMarketTriggers);
                }
                else
                {
                  matchedMarketTriggers[marketPair].AddRange(matchedSingleMarketTriggers);
                }

                if (!this.TriggeredSingleMarketSettings.ContainsKey(marketPair))
                {
                  List<SingleMarketSetting> smsList = new List<SingleMarketSetting>();
                  smsList.Add(marketSetting);
                  this.TriggeredSingleMarketSettings.Add(marketPair, smsList);
                }
                else
                {
                  this.TriggeredSingleMarketSettings[marketPair].Add(marketSetting);
                }

                if (!this.SingleMarketSettingsCount.ContainsKey(marketSetting.SettingName))
                {
                  this.SingleMarketSettingsCount.Add(marketSetting.SettingName, 1);
                }
                else
                {
                  this.SingleMarketSettingsCount[marketSetting.SettingName]++;
                }

                if (isFreshTrigger)
                {
                  this.Log.DoLogDebug("'" + marketPair + "' - SMS '" + marketSetting.SettingName + "' saving summary data...");

                  if (smss == null || marketSetting.RefreshOffTriggers)
                  {
                    if (smss == null)
                    {
                      smss = new SingleMarketSettingSummary();
                    }
                    else
                    {
                      this.SingleMarketSettingSummaries.Remove(smss);
                    }

                    smss.ActivationDateTimeUTC = DateTime.UtcNow;
                    smss.Market = marketPair;
                    smss.SingleMarketSetting = marketSetting;
                    smss.TriggerSnapshot = new TriggerSnapshot();
                    smss.TriggerSnapshot.Last24hVolume = 0;
                    smss.TriggerSnapshot.LastPrice = 0;
                    smss.TriggerSnapshot.RelevantTriggers = relevantTriggers;
                    smss.TriggerSnapshot.MatchedTriggersContent = matchedSingleMarketTriggers;

                    List<MarketTrendChange> marketTrendChanges = this.SingleMarketTrendChanges[this.SingleMarketTrendChanges.Keys.Last()];
                    if (marketTrendChanges.Count > 0)
                    {
                      MarketTrendChange mtc = marketTrendChanges.Find(m => m.Market.Equals(marketPair, StringComparison.InvariantCultureIgnoreCase));
                      if (mtc != null)
                      {
                        smss.TriggerSnapshot.Last24hVolume = mtc.Volume24h;
                        smss.TriggerSnapshot.LastPrice = mtc.LastPrice;
                      }
                    }

                    this.SingleMarketSettingSummaries.Add(smss);
                    this.Log.DoLogDebug("'" + marketPair + "' - SMS '" + marketSetting.SettingName + "' summary data saved.");
                  }
                  else
                  {
                    this.Log.DoLogDebug("'" + marketPair + "' - SMS '" + marketSetting.SettingName + "' already active for this market and no refresh allowed.");
                  }
                }

                if (marketSetting.StopProcessWhenTriggered)
                {
                  stopTriggers = true;
                }
              }
              else
              {
                this.Log.DoLogDebug("'" + marketPair + "' - '" + marketSetting.SettingName + "' not triggered!");
              }
            }
          }

          if ((marketPairProcess % 10) == 0)
          {
            this.Log.DoLogInfo("What are you looking at? " + marketPairProcess + "/" + this.MarketList.Count + " markets done...");
          }

          marketPairProcess++;
        }

        if (this.TriggeredSingleMarketSettings.Count > 0)
        {
          this.Log.DoLogInfo("Building single market settings for '" + this.TriggeredSingleMarketSettings.Count.ToString() + "' markets...");
          var newSingleMarketSettings = SettingsHandler.CompileSingleMarketProperties(this, matchedMarketTriggers);

          if (_lastActiveSingleMarketSettings == null || haveSingleMarketSettingsHaveChanged(_lastActiveSingleMarketSettings, newSingleMarketSettings))
          {
            this.SingleMarketSettingChanged = true;
          }

          _lastActiveSingleMarketSettings = newSingleMarketSettings;
          this.Log.DoLogInfo("Building single market settings completed.");
        }
        else
        {
          this.Log.DoLogInfo("No settings triggered for single markets.");
          this.SingleMarketSettingChanged = SettingsHandler.RemoveSingleMarketSettings(this);
        }
      }
      else
      {
        this.Log.DoLogInfo("No single market settings found.");
      }
    }

    private bool haveSingleMarketSettingsHaveChanged(List<KeyValuePair<string, string>> oldMarketTriggers, List<KeyValuePair<string, string>> newMarketTriggers)
    {
      string oldSms = "", newSms = "";

      foreach (var entry in oldMarketTriggers)
      {
        oldSms += string.Format("{0}: {1}|", entry.Key, entry.Value);
      }

      foreach (var entry in newMarketTriggers)
      {
        newSms += string.Format("{0}: {1}|", entry.Key, entry.Value);
      }

      return !string.Equals(oldSms, newSms, StringComparison.OrdinalIgnoreCase);
    }

    private void SaveProfitTrailerProperties()
    {
      if (this.GlobalSettingWritten || this.SingleMarketSettingChanged)
      {
        this.Log.DoLogInfo("Saving properties using API...");

        if (!this.PTMagicConfiguration.GeneralSettings.Application.TestMode)
        {
          SettingsAPI.SendPropertyLinesToAPI(this.PairsLines, this.DCALines, this.IndicatorsLines, this.PTMagicConfiguration, this.Log);
          this.Log.DoLogInfo("Settings updates sent to PT!");
        }
        else
        {
          this.Log.DoLogWarn("TESTMODE enabled -- no updates sent to PT!");
        }
      }
      else
      {
        this.Log.DoLogInfo("Nothing changed, no config written!");
      }
    }

    private void SaveSingleMarketSettingsSummary()
    {
      JsonSerializerSettings smsSummaryJsonSettings = new JsonSerializerSettings();
      smsSummaryJsonSettings.NullValueHandling = NullValueHandling.Ignore;
      smsSummaryJsonSettings.DefaultValueHandling = DefaultValueHandling.Ignore;

      FileHelper.WriteTextToFile(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar, "SingleMarketSettingSummary.json", JsonConvert.SerializeObject(this.SingleMarketSettingSummaries, Formatting.None, smsSummaryJsonSettings));
      this.Log.DoLogInfo("Single Market Settings Summary saved.");
    }

    private void SaveRuntimeSummary(int elapsedSeconds)
    {
      this.Log.DoLogInfo("Building LastRuntimeSummary.json for your monitor...");

      this.LastRuntimeSummary.LastRuntimeSeconds = elapsedSeconds;

      if (File.Exists(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + "LastRuntimeSummary.json"))
      {
        try
        {
          Summary summary = JsonConvert.DeserializeObject<Summary>(System.IO.File.ReadAllText(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar + "LastRuntimeSummary.json"));
          if (summary != null)
          {
            if (this.LastRuntimeSummary.LastGlobalSettingSwitch == Constants.confMinDate)
            {
              this.LastRuntimeSummary.LastGlobalSettingSwitch = summary.LastGlobalSettingSwitch;
            }

            foreach (string key in summary.MarketTrendChanges.Keys)
            {
              this.LastRuntimeSummary.MarketTrendChanges.Add(key, summary.MarketTrendChanges[key].FindAll(mtc => mtc.TrendDateTime >= DateTime.UtcNow.AddHours(-PTMagicConfiguration.AnalyzerSettings.MarketAnalyzer.StoreDataMaxHours)));
            }

            this.LastRuntimeSummary.GlobalSettingSummary.AddRange(summary.GlobalSettingSummary.FindAll(gss => gss.SwitchDateTime >= DateTime.UtcNow.AddHours(-96)));
            this.Log.DoLogInfo("Summary: Loaded old LastRuntimeSummary.json to keep data.");
          }
        }
        catch (Exception ex)
        {
          this.Log.DoLogCritical("Summary: Error loading old summary (" + ex.Message + "). Creating new one.", ex);
        }
      }

      this.Log.DoLogInfo("Summary: Building global settings summary...");

      GlobalSettingSummary lastSettingSummary = null;
      if (this.LastRuntimeSummary.LastGlobalSettingSwitch == this.LastRuntimeSummary.LastRuntime || this.LastRuntimeSummary.GlobalSettingSummary.Count == 0)
      {
        GlobalSettingSummary gss = new GlobalSettingSummary();
        gss.SettingName = this.LastRuntimeSummary.CurrentGlobalSetting.SettingName;
        gss.SwitchDateTime = this.LastRuntimeSummary.LastRuntime.ToUniversalTime();

        if (this.LastRuntimeSummary.GlobalSettingSummary.Count > 0)
        {
          lastSettingSummary = this.LastRuntimeSummary.GlobalSettingSummary.OrderByDescending(lss => lss.SwitchDateTime).First();
          lastSettingSummary.ActiveSeconds = (int)Math.Ceiling(DateTime.UtcNow.Subtract(lastSettingSummary.SwitchDateTime).TotalSeconds);
        }

        this.LastRuntimeSummary.GlobalSettingSummary.Add(gss);
        lastSettingSummary = this.LastRuntimeSummary.GlobalSettingSummary.OrderByDescending(lss => lss.SwitchDateTime).First();
      }
      else if (this.LastRuntimeSummary.GlobalSettingSummary.Count > 0)
      {
        lastSettingSummary = this.LastRuntimeSummary.GlobalSettingSummary.OrderByDescending(lss => lss.SwitchDateTime).First();
        lastSettingSummary.ActiveSeconds = (int)Math.Ceiling(DateTime.UtcNow.Subtract(lastSettingSummary.SwitchDateTime).TotalSeconds);
      }

      this.Log.DoLogInfo("Summary: Built global settings summary.");
      this.Log.DoLogInfo("Summary: Save market trend changes for summary.");

      foreach (string key in this.AverageMarketTrendChanges.Keys)
      {
        List<MarketTrendChange> mtChanges = new List<MarketTrendChange>();
        if (this.LastRuntimeSummary.MarketTrendChanges.ContainsKey(key))
        {
          mtChanges = this.LastRuntimeSummary.MarketTrendChanges[key];
        }

        MarketTrendChange newChange = new MarketTrendChange();
        newChange.MarketTrendName = key;
        newChange.TrendChange = this.AverageMarketTrendChanges[key];
        newChange.TrendDateTime = this.LastRuntimeSummary.LastRuntime;

        mtChanges.Add(newChange);
        if (lastSettingSummary != null && !lastSettingSummary.MarketTrendChanges.ContainsKey(key))
        {
          GlobalSetting gs = this.PTMagicConfiguration.AnalyzerSettings.GlobalSettings.Find(g => g.SettingName.Equals(lastSettingSummary.SettingName));
          if (gs != null && (gs.SettingName.Equals("Default", StringComparison.InvariantCultureIgnoreCase) || gs.Triggers.Find(t => t.MarketTrendName.Equals(key)) != null))
          {
            lastSettingSummary.MarketTrendChanges.Add(key, newChange);
          }
        }

        this.LastRuntimeSummary.MarketTrendChanges[key] = mtChanges;
      }
      this.Log.DoLogInfo("Summary: Market trends saved.");
      this.Log.DoLogInfo("Summary: Getting current global properties...");

      Dictionary<string, string> pairsProperties = SettingsHandler.GetPropertiesAsDictionary(this.PairsLines);
      Dictionary<string, string> dcaProperties = SettingsHandler.GetPropertiesAsDictionary(this.DCALines);

      double defaultBuyValue = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(pairsProperties, "ALL_buy_value", "DEFAULT_A_buy_value"), 0, "en-US");
      double defaultTrailingBuy = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(pairsProperties, "ALL_trailing_buy", "DEFAULT_trailing_buy"), 0, "en-US");
      double defaultSellValue = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(pairsProperties, "ALL_sell_value", "DEFAULT_A_sell_value"), 0, "en-US");
      double defaultTrailingProfit = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(pairsProperties, "ALL_trailing_profit", "DEFAULT_trailing_profit"), 0, "en-US");
      double defaultMaxTradingPairs = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(pairsProperties, "ALL_max_trading_pairs", "max_trading_pairs"), 0, "en-US");
      double defaultMaxCost = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(pairsProperties, "ALL_max_cost", "DEFAULT_initial_cost"), 0, "en-US");
      double defaultMaxCostPercentage = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(pairsProperties, "ALL_max_cost_percentage", "DEFAULT_initial_cost_percentage"), 0, "en-US");
      double defaultMinBuyVolume = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(pairsProperties, "ALL_min_buy_volume", "DEFAULT_min_buy_volume"), 0, "en-US");
      double defaultDCALevel = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(dcaProperties, "max_buy_times", "DEFAULT_DCA_max_buy_times"), 0, "en-US");
      string defaultBuyStrategy = SettingsHandler.GetCurrentPropertyValue(pairsProperties, "ALL_buy_strategy", "DEFAULT_A_buy_strategy");
      string defaultSellStrategy = SettingsHandler.GetCurrentPropertyValue(pairsProperties, "ALL_sell_strategy", "DEFAULT_A_sell_strategy");
      bool defaultDCAEnabled = !SettingsHandler.GetCurrentPropertyValue(pairsProperties, "ALL_DCA_enabled", "DEFAULT_DCA_enabled").Equals("false", StringComparison.InvariantCultureIgnoreCase);
      bool defaultSOMActive = !SettingsHandler.GetCurrentPropertyValue(pairsProperties, "ALL_sell_only_mode", "DEFAULT_sell_only_mode_enabled").Equals("false", StringComparison.InvariantCultureIgnoreCase);
      this.LastRuntimeSummary.IsSOMActive = defaultSOMActive;
      this.LastRuntimeSummary.DCABuyStrategy = SettingsHandler.GetCurrentPropertyValue(dcaProperties, "buy_strategy", "DEFAULT_DCA_A_buy_strategy");

      this.LastRuntimeSummary.BuyValue = defaultBuyValue;
      this.LastRuntimeSummary.TrailingBuy = defaultTrailingBuy;
      this.LastRuntimeSummary.SellValue = defaultSellValue;
      this.LastRuntimeSummary.TrailingProfit = defaultTrailingProfit;
      this.LastRuntimeSummary.MaxTradingPairs = defaultMaxTradingPairs;
      this.LastRuntimeSummary.MaxCost = defaultMaxCost;
      this.LastRuntimeSummary.MaxCostPercentage = defaultMaxCostPercentage;
      this.LastRuntimeSummary.MinBuyVolume = defaultMinBuyVolume;
      this.LastRuntimeSummary.BuyStrategy = defaultBuyStrategy;
      this.LastRuntimeSummary.SellStrategy = defaultSellStrategy;
      this.LastRuntimeSummary.DCALevels = defaultDCALevel;

      double maxDCALevel = this.LastRuntimeSummary.DCALevels;
      if (maxDCALevel == 0) maxDCALevel = 1000;

      this.LastRuntimeSummary.DCATrigger = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(dcaProperties, "buy_trigger", "DEFAULT_DCA_buy_trigger"), 0, "en-US");

      string ProfitPercentageLabel = "";
      for (char c = 'A'; c <= 'Z'; c++)
      {
        string buyStrategyName = SettingsHandler.GetCurrentPropertyValue(dcaProperties, "DEFAULT_DCA_" + c + "_buy_strategy", "");
        if (buyStrategyName.Contains("PROFITPERCENTAGE"))
        {
          ProfitPercentageLabel = "" + c;
        }
      }

      for (int dca = 1; dca <= maxDCALevel; dca++)
      {
        string dcaTriggerString = SettingsHandler.GetCurrentPropertyValue(dcaProperties, ProfitPercentageLabel + "buy_value_" + dca.ToString(), "DEFAULT_DCA_" + ProfitPercentageLabel + "_buy_value_" + dca.ToString());
        if (!String.IsNullOrEmpty(dcaTriggerString))
        {
          this.LastRuntimeSummary.DCATriggers.Add(dca, SystemHelper.TextToDouble(dcaTriggerString, 0, "en-US"));
        }
        else
        {
          if (this.LastRuntimeSummary.DCALevels == 0) this.LastRuntimeSummary.DCALevels = dca - 1;
          break;
        }
      }

      this.LastRuntimeSummary.DCAPercentage = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(dcaProperties, "DEFAULT_DCA_buy_percentage", ""), 0, "en-US");
      for (int dca = 1; dca <= maxDCALevel; dca++)
      {
        string dcaPercentageString = SettingsHandler.GetCurrentPropertyValue(dcaProperties, "DEFAULT_DCA_buy_percentage_" + dca.ToString(), "");
        if (!String.IsNullOrEmpty(dcaPercentageString))
        {
          this.LastRuntimeSummary.DCAPercentages.Add(dca, SystemHelper.TextToDouble(dcaPercentageString, 0, "en-US"));
        }
        else
        {
          if (this.LastRuntimeSummary.DCALevels == 0) this.LastRuntimeSummary.DCALevels = dca - 1;
          break;
        }
      }

      for (char c = 'A'; c <= 'Z'; c++)
      {
        string buyStrategyName = SettingsHandler.GetCurrentPropertyValue(pairsProperties, "DEFAULT_" + c + "_buy_strategy", "");
        if (String.IsNullOrEmpty(buyStrategyName)) break;
        StrategySummary buyStrategy = new StrategySummary();
        buyStrategy.Name = buyStrategyName;
        buyStrategy.Value = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(pairsProperties, "DEFAULT_" + c + "_buy_value", ""), 0, "en-US");
        this.LastRuntimeSummary.BuyStrategies.Add(buyStrategy);
      }

      for (char c = 'A'; c <= 'Z'; c++)
      {
        string sellStrategyName = SettingsHandler.GetCurrentPropertyValue(pairsProperties, "DEFAULT_" + c + "_sell_strategy", "");
        if (String.IsNullOrEmpty(sellStrategyName)) break;
        StrategySummary sellStrategy = new StrategySummary();
        sellStrategy.Name = sellStrategyName;
        sellStrategy.Value = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(pairsProperties, "DEFAULT_" + c + "_sell_value", ""), 0, "en-US");
        this.LastRuntimeSummary.SellStrategies.Add(sellStrategy);
      }

      for (char c = 'A'; c <= 'Z'; c++)
      {
        string buyStrategyName = SettingsHandler.GetCurrentPropertyValue(dcaProperties, "DEFAULT_DCA_" + c + "_buy_strategy", "");
        if (String.IsNullOrEmpty(buyStrategyName)) break;
        StrategySummary buyStrategy = new StrategySummary();
        buyStrategy.Name = buyStrategyName;
        buyStrategy.Value = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(dcaProperties, "DEFAULT_DCA_" + c + "_buy_value", ""), 0, "en-US");
        this.LastRuntimeSummary.DCABuyStrategies.Add(buyStrategy);
      }

      for (char c = 'A'; c <= 'Z'; c++)
      {
        string sellStrategyName = SettingsHandler.GetCurrentPropertyValue(dcaProperties, "DEFAULT_DCA_" + c + "_sell_strategy", "");
        if (String.IsNullOrEmpty(sellStrategyName)) break;
        StrategySummary sellStrategy = new StrategySummary();
        sellStrategy.Name = sellStrategyName;
        sellStrategy.Value = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(dcaProperties, "DEFAULT_DCA_" + c + "_sell_value", ""), 0, "en-US");
        this.LastRuntimeSummary.DCASellStrategies.Add(sellStrategy);
      }

      Dictionary<string, Market> recentMarkets = BaseAnalyzer.GetMarketDataFromFile(this.PTMagicConfiguration, this.Log, "Exchange", DateTime.UtcNow, "Recent");
      if (recentMarkets.Keys.Count > 0)
      {
        this.LastRuntimeSummary.MainMarketPrice = recentMarkets.First().Value.MainCurrencyPriceUSD;
        if (!this.LastRuntimeSummary.MainFiatCurrency.Equals("USD", StringComparison.InvariantCultureIgnoreCase))
        {
          this.LastRuntimeSummary.MainMarketPrice = this.LastRuntimeSummary.MainMarketPrice * this.LastRuntimeSummary.MainFiatCurrencyExchangeRate;
        }
      }

      this.Log.DoLogInfo("Summary: Current global properties saved.");
      this.Log.DoLogInfo("Summary: Getting current single market properties...");

      foreach (string marketPair in this.MarketList)
      {
        MarketPairSummary mpSummary = new MarketPairSummary();
        mpSummary.CurrentBuyValue = defaultBuyValue;
        mpSummary.CurrentTrailingBuy = defaultTrailingBuy;
        mpSummary.CurrentSellValue = defaultSellValue;
        mpSummary.CurrentTrailingProfit = defaultTrailingProfit;
        mpSummary.IsDCAEnabled = defaultDCAEnabled;
        mpSummary.IsSOMActive = defaultSOMActive;
        mpSummary.ActiveSingleSettings = new List<SingleMarketSetting>();

        if (this.MarketList.Contains(marketPair))
        {
          mpSummary.IsTradingEnabled = true;
          if (this.TriggeredSingleMarketSettings.Count > 0 && this.TriggeredSingleMarketSettings.ContainsKey(marketPair))
          {
            mpSummary.ActiveSingleSettings = this.TriggeredSingleMarketSettings[marketPair];
          }

          string marketPairSimple = marketPair.Replace(this.LastRuntimeSummary.MainMarket, "").Replace("_", "").Replace("-", "");

          for (char c = 'A'; c <= 'Z'; c++)
          {
            string buyStrategyName = SettingsHandler.GetCurrentPropertyValue(pairsProperties, marketPairSimple + "_" + c + "_buy_strategy", "");
            if (String.IsNullOrEmpty(buyStrategyName)) break;
            StrategySummary buyStrategy = new StrategySummary();
            buyStrategy.Name = buyStrategyName;
            buyStrategy.Value = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(pairsProperties, marketPair + "_" + c + "_buy_value", ""), 0, "en-US");
            mpSummary.BuyStrategies.Add(buyStrategy);
          }
          if (mpSummary.BuyStrategies.Count == 0) mpSummary.BuyStrategies = this.LastRuntimeSummary.BuyStrategies;

          for (char c = 'A'; c <= 'Z'; c++)
          {
            string sellStrategyName = SettingsHandler.GetCurrentPropertyValue(pairsProperties, marketPairSimple + "_" + c + "_sell_strategy", "");
            if (String.IsNullOrEmpty(sellStrategyName)) break;
            StrategySummary sellStrategy = new StrategySummary();
            sellStrategy.Name = sellStrategyName;
            sellStrategy.Value = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(pairsProperties, marketPairSimple + "_" + c + "_sell_value", ""), 0, "en-US");
            mpSummary.SellStrategies.Add(sellStrategy);
          }
          if (mpSummary.SellStrategies.Count == 0) mpSummary.SellStrategies = this.LastRuntimeSummary.SellStrategies;

          double pairBuyValue = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(pairsProperties, marketPairSimple + "_buy_value", marketPairSimple + "_A_buy_value"), 100, "en-US");
          if (pairBuyValue < 100) mpSummary.CurrentBuyValue = pairBuyValue;
          double pairTrailingBuy = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(pairsProperties, marketPair + "_trailing_buy", marketPairSimple + "_trailing_buy"), -1, "en-US");
          if (pairTrailingBuy > -1) mpSummary.CurrentTrailingBuy = pairTrailingBuy;
          double pairSellValue = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(pairsProperties, marketPair + "_sell_value", marketPairSimple + "_A_sell_value"), 0, "en-US");
          if (pairSellValue > -1) mpSummary.CurrentSellValue = pairSellValue;
          double pairTrailingProfit = SystemHelper.TextToDouble(SettingsHandler.GetCurrentPropertyValue(pairsProperties, marketPair + "_trailing_profit", marketPairSimple + "_trailing_profit"), 0, "en-US");
          if (pairTrailingProfit > -1) mpSummary.CurrentTrailingProfit = pairTrailingProfit;

          mpSummary.IsTradingEnabled = !SettingsHandler.GetCurrentPropertyValue(pairsProperties, marketPair + "_trading_enabled", marketPairSimple + "_trading_enabled").Equals("false", StringComparison.InvariantCultureIgnoreCase);
          mpSummary.IsDCAEnabled = !SettingsHandler.GetCurrentPropertyValue(pairsProperties, marketPair + "_DCA_enabled", marketPairSimple + "_DCA_enabled").Equals("false", StringComparison.InvariantCultureIgnoreCase) && defaultDCAEnabled;
          mpSummary.IsSOMActive = SettingsHandler.GetCurrentPropertyValue(pairsProperties, marketPair + "_sell_only_mode", marketPairSimple + "_sell_only_mode_enabled").Equals("true", StringComparison.InvariantCultureIgnoreCase);
        }

        foreach (string marketTrendName in this.SingleMarketTrendChanges.Keys)
        {
          MarketTrendChange mtc = this.SingleMarketTrendChanges[marketTrendName].Find(m => m.Market == marketPair);
          if (mtc != null)
          {
            mpSummary.MarketTrendChanges.Add(marketTrendName, mtc.TrendChange);
            mpSummary.LatestPrice = mtc.LastPrice;
            mpSummary.Latest24hVolume = mtc.Volume24h;
          }
        }

        this.LastRuntimeSummary.MarketSummary.TryAdd(marketPair, mpSummary);
      }

      this.Log.DoLogInfo("Summary: Current single market properties saved.");

      string serialziedJson = JsonConvert.SerializeObject(this.LastRuntimeSummary);
      try
      {
        FileHelper.WriteTextToFile(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar, "LastRuntimeSummary.json", serialziedJson);
        this.Log.DoLogInfo("Summary: LastRuntimeSummary.json saved.");
      }
      catch (Exception ex)
      {
        this.Log.DoLogCritical("Exception while writing LastRuntimeSummary.json", ex);
        try
        {
          this.Log.DoLogInfo("Summary: Retrying one more time to save LastRuntimeSummary.json.");
          FileHelper.WriteTextToFile(Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathData + Path.DirectorySeparatorChar, "LastRuntimeSummary.json", serialziedJson);
          this.Log.DoLogInfo("Summary: LastRuntimeSummary.json saved.");
        }
        catch (Exception ex2)
        {
          this.Log.DoLogCritical("Nope, another Exception while writing LastRuntimeSummary.json", ex2);
        }
      }
    }

    private void Cleanup()
    {
      this.GlobalSettingWritten = false;
      this.SingleMarketSettingChanged = false;
      this.EnforceSettingsReapply = false;

      this.PairsLines = null;
      this.DCALines = null;
      this.IndicatorsLines = null;

      this.SingleMarketTrendChanges = new Dictionary<string, List<MarketTrendChange>>();
      this.GlobalMarketTrendChanges = new Dictionary<string, List<MarketTrendChange>>();
      this.AverageMarketTrendChanges = new Dictionary<string, double>();
      this.SingleMarketSettingsCount = new Dictionary<string, int>();
      this.TriggeredSingleMarketSettings = new Dictionary<string, List<SingleMarketSetting>>();
      this.ExchangeMarketList = null;
      this.MarketList = new List<string>();
      this.LastRuntimeSummary = null;

      string logsPath = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar + Constants.PTMagicPathLogs + Path.DirectorySeparatorChar;
      if (Directory.Exists(logsPath))
      {
        FileHelper.CleanupFiles(logsPath, 24 * 3);
        this.Log.DoLogInfo("Cleaned up logfiles.");
      }
    }
    #endregion
  }
}
