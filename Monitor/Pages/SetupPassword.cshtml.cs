using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Core.Main;
using Core.Helper;

namespace Monitor.Pages
{
  public class SetupPasswordModel : _Internal.BasePageModel
  {
    public string ValidationMessage = "";

    public void OnGet()
    {
      base.PreInit();
    }

    public void OnPost(string OldPassword, string Password, string PasswordConfirm)
    {
      base.PreInit();

      string encryptedOldPassword = null;

      if (OldPassword != null)
      {
          encryptedOldPassword = EncryptionHelper.Encrypt(OldPassword);

          if (!Password.Equals(PasswordConfirm) || !encryptedOldPassword.Equals(PTMagicConfiguration.SecureSettings.MonitorPassword) && System.IO.File.Exists(System.IO.Directory.GetCurrentDirectory().Split("Monitor")[0] + "settings.secure.json"))
          {
            Response.Redirect(PTMagicConfiguration.GeneralSettings.Monitor.RootUrl + "SetupPassword");
          }
          else if (ModelState.IsValid)
          {
            PTMagicConfiguration.WriteSecureSettings(Password);
            Response.Redirect(PTMagicConfiguration.GeneralSettings.Monitor.RootUrl + "Login");
          }
      }
      else
      {
          if (!Password.Equals(PasswordConfirm) && !System.IO.File.Exists(System.IO.Directory.GetCurrentDirectory().Split("Monitor")[0] + "settings.secure.json"))
          {
            Response.Redirect(PTMagicConfiguration.GeneralSettings.Monitor.RootUrl + "SetupPassword");
          }
          else if (ModelState.IsValid)
          {
            PTMagicConfiguration.WriteSecureSettings(Password);
            Response.Redirect(PTMagicConfiguration.GeneralSettings.Monitor.RootUrl + "Login");
          }
      }
    }

  }
}
