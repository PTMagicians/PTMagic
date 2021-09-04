using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Core.Main;
using Core.Main.DataObjects;
using Core.Main.DataObjects.PTMagicData;
using Core.MarketAnalyzer;

namespace Monitor.Pages {
  public class TickerWidgetsModel : _Internal.BasePageModelSecureAJAX {
    public ProfitTrailerData PTData = null;
    public List<string> MarketsWithSingleSettings = new List<string>();

    public void OnGet() {
      // Initialize Config
      base.Init();
      
      BindData();
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
