using System;
using System.Collections;
using System.IO;
using Microsoft.Net.Http.Headers;
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

    private async void InitializeDownload() {
      // Zip the file in an non web accessible folder
      string fileName = GetStringParameter("f", "");
      string tempFolder = PTMagicMonitorBasePath + System.IO.Path.DirectorySeparatorChar + "tmp" + System.IO.Path.DirectorySeparatorChar;

      if (System.IO.File.Exists(PTMagicBasePath + fileName)) {
        if (!System.IO.Directory.Exists(tempFolder)) {
          System.IO.Directory.CreateDirectory(tempFolder);
        }

        string sourcefilePath = PTMagicBasePath + fileName;
        string destinationFilePath = tempFolder + fileName + ".zip";

        ZIPHelper.CreateZipFile(new ArrayList() { sourcefilePath }, destinationFilePath);

        // Write out the file
        var data = System.IO.File.ReadAllBytes(destinationFilePath);

        Response.ContentType = "application/zip";
        Response.Headers[HeaderNames.CacheControl] = "no-cache";
        Response.Headers[HeaderNames.ContentDisposition] = String.Format("attachment; filename={0}", fileName);
        await Response.BodyWriter.WriteAsync(new Memory<byte>(data));
        Response.BodyWriter.Complete();
      }
    }
  }
}
