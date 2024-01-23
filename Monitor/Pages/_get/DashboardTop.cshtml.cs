using System;
using Core.Main;
using Core.Main.DataObjects;
using Core.Main.DataObjects.PTMagicData;

namespace Monitor.Pages {
  public class DashboardTopModel : _Internal.BasePageModelSecureAJAX {
    public ProfitTrailerData PTData = null;
    public MiscData MiscData = null;
    public DateTimeOffset DateTimeNow = Constants.confMinDate;
    public void OnGet() {
      // Initialize Config
      base.Init();
      BindData();
    }
    public double TotalBagCost = 0;
    public double TotalBagValue = 0;
    public double TotalBagGain = 0;
    private void BindData() {
      PTData = this.PtDataObject;
      MiscData = this.PTData.Misc;
      // Convert local offset time to UTC
      TimeSpan offsetTimeSpan = TimeSpan.Parse(MiscData.TimeZoneOffset.Replace("+", ""));
      DateTimeNow = DateTimeOffset.UtcNow.ToOffset(offsetTimeSpan);
    }
  }
}
