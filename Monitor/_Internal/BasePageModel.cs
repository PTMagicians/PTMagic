﻿using System;
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
        private volatile object _ptDataLock = new object();
        private static ProfitTrailerData _ptData = null;

        // Profit Trailer data accessor object
        public ProfitTrailerData PtDataObject
        {
            get
            {
                if (_ptData == null)
                {
                    lock (_ptDataLock)
                    {
                        _ptData = new ProfitTrailerData(PTMagicConfiguration);
                    }
                }

                return _ptData;
            }
        }

        public void PreInit()
        {
            PTMagicMonitorBasePath = Directory.GetCurrentDirectory();
            if (!System.IO.File.Exists(PTMagicMonitorBasePath + Path.DirectorySeparatorChar + "appsettings.json"))
            {
                PTMagicMonitorBasePath += Path.DirectorySeparatorChar + "Monitor";
            }

            if (!PTMagicMonitorBasePath.EndsWith(Path.DirectorySeparatorChar))
            {
                PTMagicMonitorBasePath += Path.DirectorySeparatorChar;
            }

            IConfiguration config = new ConfigurationBuilder()
                      .SetBasePath(PTMagicMonitorBasePath)
                      .AddJsonFile("appsettings.json", false)
                      .Build();

            PTMagicBasePath = config.GetValue<string>("PTMagicBasePath");

            if (!PTMagicBasePath.EndsWith(Path.DirectorySeparatorChar))
            {
                PTMagicBasePath += Path.DirectorySeparatorChar;
            }

            PTMagicConfiguration = new PTMagicConfiguration(PTMagicBasePath);

            IServiceProvider logProvider = ServiceHelper.BuildLoggerService(PTMagicBasePath);
            Log = logProvider.GetRequiredService<LogHelper>();

            bool exitLoop = false;

            while (!exitLoop)
            {
                try
                {
                    // Try to read the current runtime summary, but may be being written, so retry if necessary.
                    Summary = JsonConvert.DeserializeObject<Summary>(System.IO.File.ReadAllText(PTMagicBasePath + Constants.PTMagicPathData + Path.DirectorySeparatorChar + "LastRuntimeSummary.json"));
                    exitLoop = true;
                }
                catch (IOException e)
                {
                    // Squash exception and try again, as file was locked.
                    Thread.Sleep(250);
                }
            }

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
