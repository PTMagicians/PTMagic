using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Core.Main;
using Core.Main.DataObjects;
using Core.Main.DataObjects.PTMagicData;

namespace Monitor._Internal
{
  public class LiveTcvService : BackgroundService
  {
    private readonly string _ptMagicBasePath;
    private readonly ILogger<LiveTcvService> _logger;
    private readonly object _samplesLock = new object();
    private readonly List<(DateTime Timestamp, double TotalCurrentValue)> _samples = new List<(DateTime Timestamp, double TotalCurrentValue)>();

    private PTMagicConfiguration _configuration;
    private ProfitTrailerData _ptData;
    private Summary _summary;
    private DateTime _generalSettingsLastWriteUtc = DateTime.MinValue;
    private DateTime _runtimeSummaryLastWriteUtc = DateTime.MinValue;

    public LiveTcvService(string ptMagicBasePath, ILogger<LiveTcvService> logger)
    {
      _ptMagicBasePath = EnsureTrailingDirectorySeparator(ptMagicBasePath);
      _logger = logger;
    }

    public IReadOnlyList<(DateTime Timestamp, double TotalCurrentValue)> GetSamplesSnapshot()
    {
      lock (_samplesLock)
      {
        return _samples.ToList();
      }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        DateTime cycleStartedUtc = DateTime.UtcNow;
        int refreshSeconds = 30;

        try
        {
          RefreshConfiguration();
          RefreshRuntimeSummary();

          if (_configuration != null)
          {
            refreshSeconds = Math.Max(1, _configuration.GeneralSettings.Monitor.DashboardChartsRefreshSeconds);
          }

          if (_ptData != null && _summary != null)
          {
            SampleTotalCurrentValue();
          }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
          break;
        }
        catch (Exception ex)
        {
          _logger.LogWarning(ex, "Live TCV sampling failed. Sampling will retry on the next interval.");
        }

        TimeSpan elapsed = DateTime.UtcNow - cycleStartedUtc;
        TimeSpan delay = TimeSpan.FromSeconds(refreshSeconds) - elapsed;
        if (delay < TimeSpan.Zero)
        {
          delay = TimeSpan.Zero;
        }

        try
        {
          await Task.Delay(delay, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
          break;
        }
      }
    }

    private void RefreshConfiguration()
    {
      string generalSettingsPath = _ptMagicBasePath + "settings.general.json";
      DateTime generalSettingsLastWriteUtc = File.GetLastWriteTimeUtc(generalSettingsPath);

      if (_configuration == null || generalSettingsLastWriteUtc != _generalSettingsLastWriteUtc)
      {
        _configuration = new PTMagicConfiguration(_ptMagicBasePath);
        _ptData = new ProfitTrailerData(_configuration);
        _generalSettingsLastWriteUtc = generalSettingsLastWriteUtc;
      }
    }

    private void RefreshRuntimeSummary()
    {
      string summaryPath = _ptMagicBasePath + Constants.PTMagicPathData + Path.DirectorySeparatorChar + "LastRuntimeSummary.json";
      DateTime summaryLastWriteUtc = File.GetLastWriteTimeUtc(summaryPath);

      if (_summary != null && summaryLastWriteUtc == _runtimeSummaryLastWriteUtc)
      {
        return;
      }

      for (int attempt = 0; attempt < 3; attempt++)
      {
        DateTime lastWriteBeforeReadUtc = File.GetLastWriteTimeUtc(summaryPath);

        try
        {
          string summaryJson = File.ReadAllText(summaryPath);
          DateTime lastWriteAfterReadUtc = File.GetLastWriteTimeUtc(summaryPath);

          if (lastWriteBeforeReadUtc != lastWriteAfterReadUtc)
          {
            Thread.Sleep(50);
            continue;
          }

          Summary runtimeSummary = JsonConvert.DeserializeObject<Summary>(summaryJson);
          if (runtimeSummary != null)
          {
            if (runtimeSummary.CurrentGlobalSetting == null && _configuration != null)
            {
              runtimeSummary.CurrentGlobalSetting = _configuration.AnalyzerSettings.GlobalSettings.Find(s => s.SettingName.IndexOf("default", StringComparison.InvariantCultureIgnoreCase) > -1);
            }

            _summary = runtimeSummary;
            _runtimeSummaryLastWriteUtc = lastWriteAfterReadUtc;
          }

          return;
        }
        catch (IOException)
        {
          Thread.Sleep(50);
        }
      }
    }

    private void SampleTotalCurrentValue()
    {
      double pairsBalance = 0.0;
      double dcaBalance = 0.0;
      double pendingBalance = 0.0;
      double availableBalance = _ptData.GetCurrentBalance();
      bool isSellStrategyTrue = false;
      bool isTrailingSellActive = false;

      foreach (DCALogData dcaLogEntry in _ptData.DCALog)
      {
        string sellStrategyText = Core.ProfitTrailer.StrategyHelper.GetStrategyText(_summary, dcaLogEntry.SellStrategies, dcaLogEntry.SellStrategy, isSellStrategyTrue, isTrailingSellActive);
        double leverage = dcaLogEntry.Leverage == 0 ? 1 : dcaLogEntry.Leverage;
        double positionValue = dcaLogEntry.Amount * dcaLogEntry.CurrentPrice / leverage;

        if (sellStrategyText.Contains("PENDING"))
        {
          pendingBalance += positionValue;
        }
        else if (dcaLogEntry.BuyStrategies.Count > 0)
        {
          dcaBalance += positionValue;
        }
        else
        {
          pairsBalance += positionValue;
        }
      }

      double totalCurrentValue = pendingBalance + dcaBalance + pairsBalance + availableBalance;
      DateTime now = DateTime.UtcNow;
      DateTime threshold = now.AddMinutes(-_configuration.GeneralSettings.Monitor.LiveTCVTimeframeMinutes);

      lock (_samplesLock)
      {
        _samples.Add((now, totalCurrentValue));

        while (_samples.Count > 0 && _samples[0].Timestamp < threshold)
        {
          _samples.RemoveAt(0);
        }
      }
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
      if (!path.EndsWith(Path.DirectorySeparatorChar))
      {
        path += Path.DirectorySeparatorChar;
      }

      return path;
    }
  }
}
