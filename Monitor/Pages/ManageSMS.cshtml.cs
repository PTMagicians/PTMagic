using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Core.Main;
using Core.Main.DataObjects.PTMagicData;
using Newtonsoft.Json;

namespace Monitor.Pages
{
  public class ManageSMSModel : _Internal.BasePageModelSecure
  {
    public List<SingleMarketSettingSummary> SingleMarketSettingSummaries = new List<SingleMarketSettingSummary>();
    private readonly IWebHostEnvironment _hostingEnvironment;

    public ManageSMSModel(IWebHostEnvironment hostingEnvironment)
    {
        _hostingEnvironment = hostingEnvironment;
    }
    public void OnGet()
    {
      base.Init();

      BindData();
    }

    public List<string> smsList = new List<string>();

    public void CreateSmsList ()
    {
      
      foreach (Core.Main.DataObjects.PTMagicData.SingleMarketSettingSummary smsSummary in SingleMarketSettingSummaries) 
      {
        if (!smsList.Contains(smsSummary.SingleMarketSetting.SettingName))
        {
          smsList.Add(smsSummary.SingleMarketSetting.SettingName);
        }
      }
    }
    
    public IActionResult OnPostDeleteFile(string filename)
    {
        string webRootParent = Directory.GetParent(_hostingEnvironment.WebRootPath).FullName;
        string ptMagicRoot = Directory.GetParent(webRootParent).FullName;
        string analyzerStatePath = Path.Combine(ptMagicRoot, "_data", "AnalyzerState");

        // Read the AnalyzerState file
        if (System.IO.File.Exists(analyzerStatePath))
        {
            string state = System.IO.File.ReadAllText(analyzerStatePath);
            if (state != "0")
            {
                return new JsonResult(new { success = false, message = "Tha Analyzer is in the middle of a run.  Try again in a moment." });
            }
        }

        // If state is "0", proceed to delete the file
        try
        {
            string path = Path.Combine(ptMagicRoot, "_data", filename);
            if (System.IO.File.Exists(path))
            {
                System.IO.File.Delete(path);
            }
            return new JsonResult(new { success = true, message = "All SMS settings reset!" });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, message = ex.Message });
        }
    }

    private void BindData()
    {
      if (System.IO.File.Exists(PTMagicBasePath + Constants.PTMagicPathData + Path.DirectorySeparatorChar + "SingleMarketSettingSummary.json"))
      {
        try
        {
          SingleMarketSettingSummaries = JsonConvert.DeserializeObject<List<SingleMarketSettingSummary>>(System.IO.File.ReadAllText(PTMagicBasePath + Constants.PTMagicPathData + Path.DirectorySeparatorChar + "SingleMarketSettingSummary.json"));
        }
        catch { }
      }

      string notification = GetStringParameter("n", "");
      if (notification.Equals("SettingReset"))
      {
        NotifyHeadline = "Setting Reset!";
        NotifyMessage = "The setting will get reset on the next interval!";
        NotifyType = "success";
      }
    }

    public double GetTrendChange(string marketTrend, MarketPairSummary mps, TriggerSnapshot ts, string marketTrendRelation)
    {
      double result = 0;

      if (mps.MarketTrendChanges.ContainsKey(marketTrend))
      {
        result = mps.MarketTrendChanges[marketTrend];
        double averageMarketTrendChange = Summary.MarketTrendChanges[marketTrend].OrderByDescending(mtc => mtc.TrendDateTime).First().TrendChange;
        if (marketTrendRelation.Equals(Constants.MarketTrendRelationAbsolute, StringComparison.InvariantCulture))
        {
          result = result - averageMarketTrendChange;
        }
        else if (marketTrendRelation.Equals(Constants.MarketTrendRelationRelativeTrigger, StringComparison.InvariantCulture))
        {
          double currentPrice = mps.LatestPrice;
          double triggerPrice = ts.LastPrice;
          double triggerTrend = (currentPrice - triggerPrice) / triggerPrice * 100;
          result = triggerTrend;
        }
      }

      return result;
    }
  }
}
