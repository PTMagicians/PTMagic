using System.Net;
using System;
using Microsoft.AspNetCore.Http;
using Core.Main;
using Core.Helper;

namespace Monitor._Internal
{
  public class BasePageModelSecure : BasePageModel
  {
    // The string to redirect to if it fails security
    protected string _redirectUrl;

    public BasePageModelSecure(string redirect = null)
    {
      // Configure redirect URL
      _redirectUrl = !String.IsNullOrEmpty(redirect) ? redirect : "Login";
    }

    /// <summary>
    /// Must be called from inheritting pages to check security
    /// </summary>
    public void Init()
    {
      // Initialise base class
      base.PreInit();

      // Security check
      if (!IsLoggedIn(this.HttpContext))
      {
        this.HttpContext.Response.Clear();
        this.HttpContext.Response.Redirect(PTMagicConfiguration.GeneralSettings.Monitor.RootUrl + _redirectUrl);        
      }
    }

    /// <summary>
    /// Check to see a user if logged in interactively
    /// </summary>
    /// <returns>Boolean - User logged in or not</returns>
    protected Boolean IsLoggedIn(HttpContext context)
    {
      bool isLoggedIn = false;

      if (PTMagicConfiguration.GeneralSettings.Monitor.IsPasswordProtected)
      {
        // Do we have a session active?
        if (!String.IsNullOrEmpty(context.Session.GetString("LoggedIn" + PTMagicConfiguration.GeneralSettings.Monitor.Port.ToString())))
        {
          isLoggedIn = true;
        }
        else
        {
          // Do we have a auto login cookie?
          if (Request.Cookies.ContainsKey("PTMRememberMeKey"))
          {
            string rememberMeKey = Request.Cookies["PTMRememberMeKey"];
            if (!rememberMeKey.Equals(""))
            {
              string encryptedPassword = EncryptionHelper.Decrypt(Request.Cookies["PTMRememberMeKey"]);
              if (encryptedPassword.Equals(PTMagicConfiguration.SecureSettings.MonitorPassword))
              {
                context.Session.SetString("LoggedIn" + PTMagicConfiguration.GeneralSettings.Monitor.Port.ToString(), DateTime.UtcNow.ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'"));
                isLoggedIn = true;
              }
            }
          }
        }
      }
      else
      {
        // No password required
        isLoggedIn = true;
      }

      return isLoggedIn;
    }

  }

}
