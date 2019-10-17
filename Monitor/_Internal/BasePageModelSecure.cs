using System;
using Microsoft.AspNetCore.Http;
using Core.Main;
using Core.Helper;

namespace Monitor._Internal
{
  public class BasePageModelSecure : BasePageModel
  {
    public void Init()
    {
      base.PreInit();

      if (String.IsNullOrEmpty(HttpContext.Session.GetString("LoggedIn" + PTMagicConfiguration.GeneralSettings.Monitor.Port.ToString())) && PTMagicConfiguration.GeneralSettings.Monitor.IsPasswordProtected)
      {
        bool redirectToLogin = true;
        if (Request.Cookies.ContainsKey("PTMRememberMeKey"))
        {
          string rememberMeKey = Request.Cookies["PTMRememberMeKey"];
          if (!rememberMeKey.Equals(""))
          {
            string encryptedPassword = EncryptionHelper.Decrypt(Request.Cookies["PTMRememberMeKey"]);
            if (encryptedPassword.Equals(PTMagicConfiguration.SecureSettings.MonitorPassword))
            {
              HttpContext.Session.SetString("LoggedIn" + PTMagicConfiguration.GeneralSettings.Monitor.Port.ToString(), DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'"));
              redirectToLogin = false;
            }
          }
        }

        if (redirectToLogin)
        {
          HttpContext.Response.Redirect(PTMagicConfiguration.GeneralSettings.Monitor.RootUrl + "Login");
        }
      }
    }
  }
}
