using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Core.Main;
using Core.Helper;
using Core.Main.DataObjects.PTMagicData;
using Newtonsoft.Json;

namespace Core.ProfitTrailer
{
  public static class SettingsAPI
  {
    public static void GetInitialProfitTrailerSettings(PTMagicConfiguration systemConfiguration)
    {
      string html = "";
      string url = systemConfiguration.GeneralSettings.Application.ProfitTrailerMonitorURL + "api/data?token=" + systemConfiguration.GeneralSettings.Application.ProfitTrailerServerAPIToken;

      try
      {
        HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
        request.AutomaticDecompression = DecompressionMethods.GZip;

        WebResponse response = request.GetResponse();
        Stream dataStream = response.GetResponseStream();
        StreamReader reader = new StreamReader(dataStream);
        html = reader.ReadToEnd();
        reader.Close();
        response.Close();

      }
      catch (System.Exception)
      {
        throw;
      }

      dynamic json = JsonConvert.DeserializeObject(html);

      systemConfiguration.GeneralSettings.Application.Exchange = json.exchange;
      systemConfiguration.GeneralSettings.Application.TimezoneOffset = json.timeZoneOffset;
      systemConfiguration.GeneralSettings.Application.StartBalance = json.startBalance;
      systemConfiguration.GeneralSettings.Application.MainFiatCurrency = json.settings.currency;
    }
    public static List<string> GetPropertyLinesFromAPI(string ptFileName, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      List<string> result = null;

      try
      {
        ServicePointManager.Expect100Continue = true;
        ServicePointManager.DefaultConnectionLimit = 9999;
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
        ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(CertificateHelper.AllwaysGoodCertificate);

        HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(systemConfiguration.GeneralSettings.Application.ProfitTrailerMonitorURL + "settingsapi/settings/load");
        httpWebRequest.ContentType = "application/x-www-form-urlencoded";
        httpWebRequest.Method = "POST";

        // PT is using ordinary POST data, not JSON
        string query = "fileName=" + ptFileName + "&configName=" + systemConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName + "&license=" + systemConfiguration.GeneralSettings.Application.ProfitTrailerLicense;
        byte[] formData = Encoding.ASCII.GetBytes(query);
        httpWebRequest.ContentLength = formData.Length;

        using (Stream stream = httpWebRequest.GetRequestStream())
        {
          stream.Write(formData, 0, formData.Length);
        }

        //using (StreamWriter streamWriter = new StreamWriter(httpWebRequest.GetRequestStream())) {
        //  string json = JsonConvert.SerializeObject(new {
        //    fileName = ptFileName,
        //    configName = systemConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName,
        //    license = systemConfiguration.GeneralSettings.Application.ProfitTrailerLicense
        //  });

        //  streamWriter.Write(json);
        //}

        HttpWebResponse httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
        using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
        {
          string jsonResult = streamReader.ReadToEnd();
          result = JsonConvert.DeserializeObject<List<string>>(jsonResult);
        }
      }
      catch (WebException ex)
      {
        // Manual error handling as PT doesn't seem to provide a proper error response...
        if (ex.Message.IndexOf("401") > -1)
        {
          log.DoLogError("Loading " + ptFileName + ".properties failed for setting '" + systemConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName + "': Unauthorized! The specified Profit Trailer license key '" + systemConfiguration.GetProfitTrailerLicenseKeyMasked() + "' is invalid!");
        }
        else
        {
          log.DoLogCritical("Loading " + ptFileName + ".properties failed for setting '" + systemConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName + "': " + ex.Message, ex);
        }

      }
      catch (Exception ex)
      {
        log.DoLogCritical("Loading " + ptFileName + ".properties failed for setting '" + systemConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName + "': " + ex.Message, ex);
      }

      return result;
    }

    public static void SendPropertyLinesToAPI(List<string> pairsLines, List<string> dcaLines, List<string> indicatorsLines, PTMagicConfiguration systemConfiguration, LogHelper log)
    {
      int retryCount = 0;
      int maxRetries = 3;
      bool transferCompleted = false;
      bool transferCanceled = false;

      while (!transferCompleted && !transferCanceled)
      {
        try
        {
          ServicePointManager.Expect100Continue = true;
          ServicePointManager.DefaultConnectionLimit = 9999;
          ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
          ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(CertificateHelper.AllwaysGoodCertificate);

          HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(systemConfiguration.GeneralSettings.Application.ProfitTrailerMonitorURL + "settingsapi/settings/saveAll");
          httpWebRequest.ContentType = "application/x-www-form-urlencoded";
          httpWebRequest.Method = "POST";
          httpWebRequest.Proxy = null;
          httpWebRequest.Timeout = 30000;

          // PT is using ordinary POST data, not JSON
          string query = "configName=" + systemConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName + "&license=" + systemConfiguration.GeneralSettings.Application.ProfitTrailerLicense;
          string pairsPropertiesString = SystemHelper.ConvertListToTokenString(pairsLines, Environment.NewLine, false);
          string dcaPropertiesString = SystemHelper.ConvertListToTokenString(dcaLines, Environment.NewLine, false);
          string indicatorsPropertiesString = SystemHelper.ConvertListToTokenString(indicatorsLines, Environment.NewLine, false);
          query += "&pairsData=" + WebUtility.UrlEncode(pairsPropertiesString) + "&dcaData=" + WebUtility.UrlEncode(dcaPropertiesString) + "&indicatorsData=" + WebUtility.UrlEncode(indicatorsPropertiesString);

          byte[] formData = Encoding.ASCII.GetBytes(query);
          httpWebRequest.ContentLength = formData.Length;

          using (Stream stream = httpWebRequest.GetRequestStream())
          {
            stream.Write(formData, 0, formData.Length);
          }
          log.DoLogDebug("Built POST request for Properties");

          //using (StreamWriter streamWriter = new StreamWriter(httpWebRequest.GetRequestStream())) {
          //  string json = JsonConvert.SerializeObject(new {
          //    fileName = ptFileName,
          //    configName = systemConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName,
          //    license = systemConfiguration.GeneralSettings.Application.ProfitTrailerLicense,
          //    saveData = propertiesString
          //  });

          //  streamWriter.Write(json);
          //}

          log.DoLogInfo("Sending Properties...");
          HttpWebResponse httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
          log.DoLogInfo("Properties sent!");
          httpResponse.Close();
          log.DoLogDebug("Properties response object closed.");
          transferCompleted = true;

        }
        catch (WebException ex)
        {
          // Manual error handling as PT doesn't seem to provide a proper error response...
          if (ex.Message.IndexOf("401") > -1)
          {
            log.DoLogError("Saving Properties failed for setting '" + systemConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName + "': Unauthorized! The specified Profit Trailer license key '" + systemConfiguration.GetProfitTrailerLicenseKeyMasked() + "' is invalid!");
            transferCanceled = true;
          }
          else if (ex.Message.IndexOf("timed out") > -1)
          {
            // Handle timeout seperately
            retryCount++;
            if (retryCount <= maxRetries)
            {
              log.DoLogError("Saving Properties failed for setting '" + systemConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName + "': Timeout! Starting retry number " + retryCount + "/" + maxRetries.ToString() + "!");
            }
            else
            {
              transferCanceled = true;
              log.DoLogError("Saving Properties failed for setting '" + systemConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName + "': Timeout! Canceling transfer after " + maxRetries.ToString() + " failed retries.");
            }
          }
          else
          {
            log.DoLogCritical("Saving Properties failed for setting '" + systemConfiguration.GeneralSettings.Application.ProfitTrailerDefaultSettingName + "': " + ex.Message, ex);
            transferCanceled = true;
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