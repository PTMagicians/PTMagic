using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Core.Main;

namespace Monitor.Pages
{
  public class SetupPasswordModel : _Internal.BasePageModel
  {
    public string ValidationMessage = "";

    public void OnGet()
    {
      base.PreInit();
    }

    public void OnPost(string password, string passwordConfirm)
    {
      if (!password.Equals(passwordConfirm))
      {
        base.PreInit();
        Response.Redirect(PTMagicConfiguration.GeneralSettings.Monitor.RootUrl + "SetupPassword");
      }
      else if (ModelState.IsValid)
      {
        base.PreInit();
        PTMagicConfiguration.WriteSecureSettings(password);

        Response.Redirect(PTMagicConfiguration.GeneralSettings.Monitor.RootUrl + "Login");
      }
    }

  }
}
