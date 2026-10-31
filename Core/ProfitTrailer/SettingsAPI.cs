using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Core.Main;
using Core.Helper;

namespace Core.ProfitTrailer
{
  public static class SettingsAPI
  {
    // Save config back to Profit Trailer
    public static void SendPropertyLinesToAPI(List<string> pairsLines, List<string> dcaLines, List<string> indicatorsLines, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      int retryCount = 0;
      int maxRetries = 3;
      bool transferCompleted = false;
      bool transferCanceled = false;

      // get PT license list
      string licenses = systemConfiguration.GeneralSettings.Application.ProfitTrailerLicense;
      if (systemConfiguration.GeneralSettings.Application.ProfitTrailerLicenseXtra != "")
      {
        licenses = licenses + ", " + systemConfiguration.GeneralSettings.Application.ProfitTrailerLicenseXtra;
      }
      List<string> licenseList = SystemHelper.ConvertTokenStringToList(licenses, ",");
      int licenseCount = licenseList.Count;

      // get URL list
      string urls = systemConfiguration.GeneralSettings.Application.ProfitTrailerMonitorURL;
      if (systemConfiguration.GeneralSettings.Application.ProfitTrailerMonitorURLXtra != "")
      {
        urls = urls + ", " + systemConfiguration.GeneralSettings.Application.ProfitTrailerMonitorURLXtra;
      }
      List<string> urlList = SystemHelper.ConvertTokenStringToList(urls, ",");
      int urlCount = urlList.Count;

      log.DoLogInfo("Found " + licenseCount + " licenses and " + urlCount + " URLs");
      if (urlCount != licenseCount)
      {
        log.DoLogWarn("ERROR - urlCount must match licenseCount");
      }
      for (int i = 0; i < licenseCount; i++)
      {
        transferCompleted = false;
        while (!transferCompleted && !transferCanceled)
        {
          try
          {
            using (HttpClientHandler handler = new HttpClientHandler())
            {
              handler.UseProxy = false;
              handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

              using (HttpClient httpClient = new HttpClient(handler))
              {
                httpClient.Timeout = TimeSpan.FromSeconds(30);

                // PT is using ordinary POST data, not JSON
                string query = "configName=" + systemConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName;
                query = query + "&license=" + licenseList[i];
                string pairsPropertiesString = SystemHelper.ConvertListToTokenString(pairsLines, Environment.NewLine, false);
                string dcaPropertiesString = SystemHelper.ConvertListToTokenString(dcaLines, Environment.NewLine, false);
                string indicatorsPropertiesString = SystemHelper.ConvertListToTokenString(indicatorsLines, Environment.NewLine, false);
                query += "&pairsData=" + WebUtility.UrlEncode(pairsPropertiesString) + "&dcaData=" + WebUtility.UrlEncode(dcaPropertiesString) + "&indicatorsData=" + WebUtility.UrlEncode(indicatorsPropertiesString);

                byte[] formData = Encoding.ASCII.GetBytes(query);
                using (ByteArrayContent content = new ByteArrayContent(formData))
                {
                  content.Headers.ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded");

                  log.DoLogDebug("Built POST request for Properties");
                  int adjustedCount = i + 1;
                  log.DoLogInfo("Sending Properties to license " + adjustedCount + " at " + urlList[i]);

                  using (HttpResponseMessage response = httpClient.PostAsync(urlList[i] + "settingsapi/settings/saveAll", content).GetAwaiter().GetResult())
                  {
                    if (response.StatusCode == HttpStatusCode.NotModified)
                    {
                      log.DoLogInfo("Properties already up to date in ProfitTrailer.");
                    }
                    else
                    {
                      response.EnsureSuccessStatusCode();
                      log.DoLogInfo("Properties sent!");
                    }
                    log.DoLogDebug("Properties response object closed.");
                  }
                }
              }
            }

            transferCompleted = true;
          }
          catch (HttpRequestException ex)
          {
            // Manual error handling as PT doesn't seem to provide a proper error response...
            if (ex.StatusCode == HttpStatusCode.Unauthorized || ex.Message.IndexOf("401") > -1)
            {
              log.DoLogError("Saving Properties failed for setting '" + systemConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName + "': Unauthorized! The specified Profit Trailer license key '" + systemConfiguration.GetProfitTrailerLicenseKeyMasked() + "' is invalid!");
              transferCanceled = true;
            }
            else
            {
              log.DoLogCritical("Saving Properties failed for setting '" + systemConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName + "': " + ex.Message, ex);
              transferCanceled = true;
            }
          }
          catch (TaskCanceledException ex)
          {
            retryCount++;
            if (retryCount <= maxRetries)
            {
              log.DoLogError("Saving Properties failed for setting '" + systemConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName + "': Timeout (" + ex.Message + ")! Starting retry number " + retryCount + "/" + maxRetries.ToString() + "!");
            }
            else
            {
              transferCanceled = true;
              log.DoLogError("Saving Properties failed for setting '" + systemConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName + "': Timeout (" + ex.Message + ")! Canceling transfer after " + maxRetries.ToString() + " failed retries.");
            }
          }
          catch (TimeoutException ex)
          {
            retryCount++;
            if (retryCount <= maxRetries)
            {
              log.DoLogError("Saving Properties failed for setting '" + systemConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName + "': Timeout (" + ex.Message + ")! Starting retry number " + retryCount + "/" + maxRetries.ToString() + "!");
            }
            else
            {
              transferCanceled = true;
              log.DoLogError("Saving Properties failed for setting '" + systemConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName + "': Timeout (" + ex.Message + ")! Canceling transfer after " + maxRetries.ToString() + " failed retries.");
            }
          }
          catch (Exception ex)
          {
            log.DoLogCritical("Saving Properties failed for setting '" + systemConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName + "': " + ex.Message, ex);
            transferCanceled = true;
          }
        }
      }
    }
  }
}