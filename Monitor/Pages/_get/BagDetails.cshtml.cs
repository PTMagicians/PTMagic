﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Core.Main;
using Core.Main.DataObjects;
using Core.Main.DataObjects.PTMagicData;
using Core.MarketAnalyzer;

namespace Monitor.Pages {
  public class BagDetailsModel : _Internal.BasePageModelSecure {
    public ProfitTrailerData PTData = null;
    public MiscData MiscData = null;
    public string DCAMarket = "";
    public DCALogData DCALogData = null;
    public DateTimeOffset DateTimeNow = Constants.confMinDate;

    public void OnGet() {
      // Initialize Config
      base.Init();
      
      BindData();
    }

    private void BindData() {
      DCAMarket = GetStringParameter("m", "");
      PTData = this.PtDataObject;
      MiscData = this.PTData.Misc;
      DCALogData = PTData.DCALog.Find(d => d.Market == DCAMarket);

      // Convert local offset time to UTC
      TimeSpan offsetTimeSpan = TimeSpan.Parse(MiscData.TimeZoneOffset.Replace("+", ""));
      DateTimeNow = DateTimeOffset.UtcNow.ToOffset(offsetTimeSpan);
    }
  }
}
