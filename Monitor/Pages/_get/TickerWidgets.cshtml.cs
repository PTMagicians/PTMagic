using System;
using System.Collections.Generic;
using System.Linq;
using Core.Main.DataObjects;
using Core.Main.DataObjects.PTMagicData;
using System.IO; 
using Microsoft.AspNetCore.Hosting; 
using System.Threading;

namespace Monitor.Pages {
  public class TickerWidgetsModel : _Internal.BasePageModelSecureAJAX {
    public ProfitTrailerData PTData = null;
    public List<string> MarketsWithSingleSettings = new List<string>();
    private readonly IWebHostEnvironment _hostingEnvironment; 
    private Mutex mutex = new Mutex(false, "analyzerStateMutex");

    public TickerWidgetsModel(IWebHostEnvironment hostingEnvironment) // Add this constructor
    {
        _hostingEnvironment = hostingEnvironment;
    }

    public void OnGet() {
      // Initialize Config
      base.Init();
      
      BindData();
    }
    public bool IsAnalyzerRunning()
    {
        bool ownsMutex = false;
        try
        {
            // Try to acquire the mutex.
            ownsMutex = mutex.WaitOne(0);

            string webRootParent = Directory.GetParent(_hostingEnvironment.WebRootPath).FullName;
            string ptMagicRoot = Directory.GetParent(webRootParent).FullName;
            string analyzerStatePath = Path.Combine(ptMagicRoot, "_data", "AnalyzerState");
            if (System.IO.File.Exists(analyzerStatePath))
            {
                string state = System.IO.File.ReadAllText(analyzerStatePath);
                return state == "1";
            }
            return false;
        }
        finally
        {
            // Only release the mutex if this thread owns it.
            if (ownsMutex)
            {
                mutex.ReleaseMutex();
            }
        }
    }

    private void BindData() {
      PTData = this.PtDataObject;
      // Get markets with active single settings
      var MarketsWithSingleSettingsData = from x in Summary.MarketSummary
                                          where x.Value.ActiveSingleSettings != null
                                          && x.Value.ActiveSingleSettings.Count > 0
                                          orderby x.Key ascending
                                          select x;

      foreach (var market in MarketsWithSingleSettingsData) {
        // Get the name of all active single market settings
        string activeSettings = string.Empty;
        foreach (var singleSetting in market.Value.ActiveSingleSettings)
        {
            activeSettings += (", " + singleSetting.SettingName);
        }
        activeSettings = activeSettings.Substring(2); // Chop the unrequired comma

        MarketsWithSingleSettings.Add(String.Format("{0} : {1}", market.Key, activeSettings));        
      }
    }
  }
}
