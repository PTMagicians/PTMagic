using System.Collections;
using Core.Main;
using Core.Helper;

namespace Monitor.Pages {
  public class DownloadFileModel : _Internal.BasePageModelSecure {

    public void OnGet() {
      // Initialize Config
      base.Init();
      
      // Check we have a log in
      if (base.IsLoggedIn(this.HttpContext))
      {
        InitializeDownload();
      }
    }

    private void InitializeDownload() {
      string fileName = GetStringParameter("f", "");
      if (System.IO.File.Exists(PTMagicBasePath + fileName)) {
        if (!System.IO.Directory.Exists(PTMagicMonitorBasePath + "wwwroot" + System.IO.Path.DirectorySeparatorChar + "assets" + System.IO.Path.DirectorySeparatorChar + "tmp" + System.IO.Path.DirectorySeparatorChar)) {
          System.IO.Directory.CreateDirectory(PTMagicMonitorBasePath + "wwwroot" + System.IO.Path.DirectorySeparatorChar + "assets" + System.IO.Path.DirectorySeparatorChar + "tmp" + System.IO.Path.DirectorySeparatorChar);
        }

        string sourcefilePath = PTMagicBasePath + fileName;
        string destinationFilePath = PTMagicMonitorBasePath + "wwwroot" + System.IO.Path.DirectorySeparatorChar + "assets" + System.IO.Path.DirectorySeparatorChar + "tmp" + System.IO.Path.DirectorySeparatorChar + fileName + ".zip";

        ZIPHelper.CreateZipFile(new ArrayList() { sourcefilePath }, destinationFilePath);

        Response.Redirect(PTMagicConfiguration.GeneralSettings.Monitor.RootUrl + "assets/tmp/" + fileName + ".zip");
      }
    }
  }
}
