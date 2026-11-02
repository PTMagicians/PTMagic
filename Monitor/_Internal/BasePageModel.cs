using System;
using System.IO;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Core.Main;
using Core.Helper;
using Core.Main.DataObjects.PTMagicData;
using Core.MarketAnalyzer;
using System.Diagnostics;
using Core.Main.DataObjects;

namespace Monitor._Internal
{

    public class BasePageModel : PageModel
    {
        public string PTMagicBasePath = "";
        public string PTMagicMonitorBasePath = "";
        public PTMagicConfiguration PTMagicConfiguration = null;
        public Summary Summary { get; set; } = new Summary();
        public LogHelper Log = null;
        public string LatestVersion = "";
        public string CurrentBotVersion = "";
        public string NotifyHeadline = "";
        public string NotifyMessage = "";
        public string NotifyType = "";

        public string MainFiatCurrencySymbol = "$";
        private static readonly object _ptDataLock = new object();
        private static ProfitTrailerData _ptData = null;
        private static DateTime _ptDataGeneralSettingsLastWriteUtc = DateTime.MinValue;

        private static readonly object _monitorInfrastructureLock = new object();
        private static string _cachedMonitorBasePath = null;
        private static string _cachedPTMagicBasePath = null;
        private static DateTime _cachedAppSettingsLastWriteUtc = DateTime.MinValue;
        private static DateTime _cachedGeneralSettingsLastWriteUtc = DateTime.MinValue;
        private static DateTime _cachedAnalyzerSettingsLastWriteUtc = DateTime.MinValue;
        private static DateTime _cachedSecureSettingsLastWriteUtc = DateTime.MinValue;
        private static DateTime _cachedNLogConfigLastWriteUtc = DateTime.MinValue;
        private static PTMagicConfiguration _cachedPTMagicConfiguration = null;
        private static LogHelper _cachedLog = null;

        private static readonly object _runtimeSummaryLock = new object();
        private static Summary _cachedRuntimeSummary = null;
        private static string _cachedRuntimeSummaryPath = null;
        private static DateTime _cachedRuntimeSummaryLastWriteUtc = DateTime.MinValue;

        // Profit Trailer data accessor object
        public ProfitTrailerData PtDataObject
        {
            get
            {
                string generalSettingsPath = PTMagicBasePath + "settings.general.json";
                DateTime generalSettingsLastWriteUtc = System.IO.File.GetLastWriteTimeUtc(generalSettingsPath);

                if (_ptData == null || generalSettingsLastWriteUtc != _ptDataGeneralSettingsLastWriteUtc)
                {
                    lock (_ptDataLock)
                    {
                        generalSettingsLastWriteUtc = System.IO.File.GetLastWriteTimeUtc(generalSettingsPath);

                        if (_ptData == null || generalSettingsLastWriteUtc != _ptDataGeneralSettingsLastWriteUtc)
                        {
                            _ptData = new ProfitTrailerData(PTMagicConfiguration);
                            _ptDataGeneralSettingsLastWriteUtc = generalSettingsLastWriteUtc;
                        }
                    }
                }

                return _ptData;
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

        private void LoadSharedMonitorInfrastructure()
        {
            lock (_monitorInfrastructureLock)
            {
                if (String.IsNullOrEmpty(_cachedMonitorBasePath))
                {
                    string monitorBasePath = Directory.GetCurrentDirectory();
                    if (!System.IO.File.Exists(monitorBasePath + Path.DirectorySeparatorChar + "appsettings.json"))
                    {
                        monitorBasePath += Path.DirectorySeparatorChar + "Monitor";
                    }

                    _cachedMonitorBasePath = EnsureTrailingDirectorySeparator(monitorBasePath);
                }

                string appSettingsPath = _cachedMonitorBasePath + "appsettings.json";
                DateTime appSettingsLastWriteUtc = System.IO.File.GetLastWriteTimeUtc(appSettingsPath);
                bool basePathChanged = false;

                if (String.IsNullOrEmpty(_cachedPTMagicBasePath) || appSettingsLastWriteUtc != _cachedAppSettingsLastWriteUtc)
                {
                    IConfiguration config = new ConfigurationBuilder()
                        .SetBasePath(_cachedMonitorBasePath)
                        .AddJsonFile("appsettings.json", false)
                        .Build();

                    string configuredPTMagicBasePath = EnsureTrailingDirectorySeparator(config.GetValue<string>("PTMagicBasePath"));
                    basePathChanged = !String.Equals(_cachedPTMagicBasePath, configuredPTMagicBasePath, StringComparison.Ordinal);
                    _cachedPTMagicBasePath = configuredPTMagicBasePath;
                    _cachedAppSettingsLastWriteUtc = appSettingsLastWriteUtc;
                }

                string generalSettingsPath = _cachedPTMagicBasePath + "settings.general.json";
                string analyzerSettingsPath = _cachedPTMagicBasePath + "settings.analyzer.json";
                string secureSettingsPath = _cachedPTMagicBasePath + "settings.secure.json";
                string nlogConfigPath = _cachedPTMagicBasePath + "nlog.config";

                DateTime generalSettingsLastWriteUtc = System.IO.File.GetLastWriteTimeUtc(generalSettingsPath);
                DateTime analyzerSettingsLastWriteUtc = System.IO.File.GetLastWriteTimeUtc(analyzerSettingsPath);
                DateTime secureSettingsLastWriteUtc = System.IO.File.Exists(secureSettingsPath)
                    ? System.IO.File.GetLastWriteTimeUtc(secureSettingsPath)
                    : DateTime.MinValue;

                if (basePathChanged
                    || _cachedPTMagicConfiguration == null
                    || generalSettingsLastWriteUtc != _cachedGeneralSettingsLastWriteUtc
                    || analyzerSettingsLastWriteUtc != _cachedAnalyzerSettingsLastWriteUtc
                    || secureSettingsLastWriteUtc != _cachedSecureSettingsLastWriteUtc)
                {
                    _cachedPTMagicConfiguration = new PTMagicConfiguration(_cachedPTMagicBasePath);
                    _cachedGeneralSettingsLastWriteUtc = generalSettingsLastWriteUtc;
                    _cachedAnalyzerSettingsLastWriteUtc = analyzerSettingsLastWriteUtc;
                    _cachedSecureSettingsLastWriteUtc = secureSettingsLastWriteUtc;
                }

                DateTime nlogConfigLastWriteUtc = System.IO.File.GetLastWriteTimeUtc(nlogConfigPath);
                if (basePathChanged || _cachedLog == null || nlogConfigLastWriteUtc != _cachedNLogConfigLastWriteUtc)
                {
                    IServiceProvider logProvider = ServiceHelper.BuildLoggerService(_cachedPTMagicBasePath);
                    _cachedLog = logProvider.GetRequiredService<LogHelper>();
                    _cachedNLogConfigLastWriteUtc = nlogConfigLastWriteUtc;
                }

                PTMagicMonitorBasePath = _cachedMonitorBasePath;
                PTMagicBasePath = _cachedPTMagicBasePath;
                PTMagicConfiguration = _cachedPTMagicConfiguration;
                Log = _cachedLog;
            }
        }

        private Summary LoadRuntimeSummary()
        {
            string summaryPath = PTMagicBasePath + Constants.PTMagicPathData + Path.DirectorySeparatorChar + "LastRuntimeSummary.json";

            lock (_runtimeSummaryLock)
            {
                while (true)
                {
                    DateTime lastWriteBeforeReadUtc = System.IO.File.GetLastWriteTimeUtc(summaryPath);

                    if (_cachedRuntimeSummary != null
                        && String.Equals(_cachedRuntimeSummaryPath, summaryPath, StringComparison.Ordinal)
                        && lastWriteBeforeReadUtc == _cachedRuntimeSummaryLastWriteUtc)
                    {
                        return _cachedRuntimeSummary;
                    }

                    try
                    {
                        string summaryJson = System.IO.File.ReadAllText(summaryPath);
                        DateTime lastWriteAfterReadUtc = System.IO.File.GetLastWriteTimeUtc(summaryPath);

                        if (lastWriteBeforeReadUtc != lastWriteAfterReadUtc)
                        {
                            Thread.Sleep(250);
                            continue;
                        }

                        Summary runtimeSummary = JsonConvert.DeserializeObject<Summary>(summaryJson);
                        _cachedRuntimeSummary = runtimeSummary;
                        _cachedRuntimeSummaryPath = summaryPath;
                        _cachedRuntimeSummaryLastWriteUtc = lastWriteAfterReadUtc;
                        return runtimeSummary;
                    }
                    catch (IOException)
                    {
                        // Preserve the existing retry behavior when PTMagic is writing the summary file.
                        Thread.Sleep(250);
                    }
                }
            }
        }

        public void PreInit()
        {
            LoadSharedMonitorInfrastructure();
            Summary = LoadRuntimeSummary();

            if (Summary.CurrentGlobalSetting == null) Summary.CurrentGlobalSetting = PTMagicConfiguration.AnalyzerSettings.GlobalSettings.Find(s => s.SettingName.IndexOf("default", StringComparison.InvariantCultureIgnoreCase) > -1);

            MainFiatCurrencySymbol = SystemHelper.GetCurrencySymbol(Summary.MainFiatCurrency);

            try
            {
                // Get latest release from GitHub
                if (!String.IsNullOrEmpty(HttpContext.Session.GetString("LatestVersion")))
                {
                    LatestVersion = HttpContext.Session.GetString("LatestVersion");
                }
                else
                {
                    LatestVersion = BaseAnalyzer.GetLatestGitHubRelease(Log, Summary.Version);
                    HttpContext.Session.SetString("LatestVersion", LatestVersion);
                }
            }
            catch { }

            try
            {
                // Get current bot version
                if (!String.IsNullOrEmpty(HttpContext.Session.GetString("CurrentBotVersion")))
                {
                    CurrentBotVersion = HttpContext.Session.GetString("CurrentBotVersion");
                }
                else
                {
                    string ptMagicBotDllPath = PTMagicBasePath + "PTMagic.dll";
                    if (System.IO.File.Exists(ptMagicBotDllPath))
                    {
                        FileVersionInfo ptMagicDllInfo = FileVersionInfo.GetVersionInfo(ptMagicBotDllPath);

                        CurrentBotVersion = ptMagicDllInfo.ProductVersion.Substring(0, ptMagicDllInfo.ProductVersion.LastIndexOf("."));
                        HttpContext.Session.SetString("CurrentBotVersion", CurrentBotVersion);
                    }
                    else
                    {
                        CurrentBotVersion = Summary.Version;
                    }
                }

            }
            catch
            {
                CurrentBotVersion = Summary.Version;
            }
        }

        protected string GetStringParameter(string paramName, string defaultValue)
        {
            string result = defaultValue;

            if (HttpContext.Request.Query.ContainsKey(paramName))
            {
                result = HttpContext.Request.Query[paramName];
            }
            else if (HttpContext.Request.Method.Equals("POST") && HttpContext.Request.Form.ContainsKey(paramName))
            {
                result = HttpContext.Request.Form[paramName];
            }

            return result;
        }

        /// <summary>
        /// Holt einen Url-Parameter als Integer, wenn vorhanden.
        /// </summary>
        /// <param name="paramName">Name des Parameters</param>
        /// <param name="defaultValue">Defaultvalue, wenn Parameter nicht vorhanden ist.</param>
        /// <returns>Der Wert des Parameters als Integer.</returns>
        protected int GetIntParameter(string paramName, int defaultValue)
        {
            int result = defaultValue;

            if (HttpContext.Request.Query.ContainsKey(paramName))
            {
                try
                {
                    result = Int32.Parse(HttpContext.Request.Query[paramName]);
                }
                catch
                {
                    result = defaultValue;
                }
            }
            else if (HttpContext.Request.Method.Equals("POST") && HttpContext.Request.Form.ContainsKey(paramName))
            {
                try
                {
                    result = Int32.Parse(HttpContext.Request.Form[paramName]);
                }
                catch
                {
                    result = defaultValue;
                }
            }

            return result;
        }
    }
}
