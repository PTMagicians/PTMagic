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
      ValidationMessage = "Test";
      string encryptedOldPassword = null;

      if (OldPassword != null)
      {
          encryptedOldPassword = EncryptionHelper.Encrypt(OldPassword);

          if (!Password.Equals(PasswordConfirm) || !encryptedOldPassword.Equals(PTMagicConfiguration.SecureSettings.MonitorPassword) && System.IO.File.Exists(System.IO.Directory.GetCurrentDirectory().Split("Monitor")[0] + "/settings.secure.json"))
          {
            ValidationMessage = "Old Password wrong or new Password does not match with confirmation";
          }
          else if (ModelState.IsValid)
          {
            PTMagicConfiguration.WriteSecureSettings(Password);
            ValidationMessage = "";
            Response.Redirect(PTMagicConfiguration.GeneralSettings.Monitor.RootUrl + "Login");
          }
      }
      else
      {
          if (!Password.Equals(PasswordConfirm) && !System.IO.File.Exists(System.IO.Directory.GetCurrentDirectory().Split("Monitor")[0] + "/settings.secure.json"))
          {
            ValidationMessage = "New Password does not match with confirmation";
          }
          else if (ModelState.IsValid)
          {
            PTMagicConfiguration.WriteSecureSettings(Password);
            ValidationMessage = "";
            Response.Redirect(PTMagicConfiguration.GeneralSettings.Monitor.RootUrl + "Login");
          }
      }
    }

  }
}
