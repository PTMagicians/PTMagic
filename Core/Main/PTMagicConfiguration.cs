using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Core.Helper;
using Core.Main.DataObjects.PTMagicData;

namespace Core.Main
{

  public class PTMagicConfiguration
  {
    private GeneralSettings _generalSettings = null;
    private AnalyzerSettings _analyzerSettings = null;
    private SecureSettings _secureSettings = null;
    private string _basePath;

    public PTMagicConfiguration()
    {
      _basePath = Directory.GetCurrentDirectory();
      LoadSettings(_basePath);
    }

    public PTMagicConfiguration(string basePath)
    {
      _basePath = basePath;
      LoadSettings(_basePath);
    }

    public void RefreshSettings()
    {
      LoadSettings(_basePath);
    }

    private void LoadSettings(string basePath)
    {
      if (!basePath.EndsWith(Path.DirectorySeparatorChar))
      {
        basePath += Path.DirectorySeparatorChar;
      }

      GeneralSettingsWrapper gsw = JsonConvert.DeserializeObject<GeneralSettingsWrapper>(File.ReadAllText(basePath + "settings.general.json"));
      _generalSettings = gsw.GeneralSettings;

      AnalyzerSettingsWrapper asw = JsonConvert.DeserializeObject<AnalyzerSettingsWrapper>(File.ReadAllText(basePath + "settings.analyzer.json"));
      _analyzerSettings = asw.AnalyzerSettings;

      if (!_generalSettings.Application.ProfitTrailerMonitorURL.EndsWith("/"))
      {
        _generalSettings.Application.ProfitTrailerMonitorURL += "/";
      }

      if (File.Exists(basePath + "settings.secure.json"))
      {
        SecureSettingsWrapper ssw = JsonConvert.DeserializeObject<SecureSettingsWrapper>(File.ReadAllText(basePath + "settings.secure.json"));
        _secureSettings = ssw.SecureSettings;
      }
    }

    public string GetProfitTrailerLicenseKeyMasked()
    {
      string result = "";

      if (!this.GeneralSettings.Application.ProfitTrailerLicense.Equals(""))
      {
        result = this.GeneralSettings.Application.ProfitTrailerLicense.Substring(0, 4);

        for (int i = 1; i < this.GeneralSettings.Application.ProfitTrailerLicense.Length - 8; i++)
        {
          result += "*";
        }

        result += this.GeneralSettings.Application.ProfitTrailerLicense.Substring(this.GeneralSettings.Application.ProfitTrailerLicense.Length - 4);
      }

      return result;
    }

    public string GetProfitTrailerServerAPITokenMasked()
    {
      string result = "";

      if (!this.GeneralSettings.Application.ProfitTrailerServerAPIToken.Equals(""))
      {
        result = this.GeneralSettings.Application.ProfitTrailerServerAPIToken.Substring(0, 4);

        for (int i = 1; i < this.GeneralSettings.Application.ProfitTrailerServerAPIToken.Length - 8; i++)
        {
          result += "*";
        }

        result += this.GeneralSettings.Application.ProfitTrailerServerAPIToken.Substring(this.GeneralSettings.Application.ProfitTrailerServerAPIToken.Length - 4);
      }

      return result;
    }

    public GeneralSettings GeneralSettings
    {
      get
      {
        return _generalSettings;
      }
    }

    public AnalyzerSettings AnalyzerSettings
    {
      get
      {
        return _analyzerSettings;
      }
    }

    public SecureSettings SecureSettings
    {
      get
      {
        if (_secureSettings == null) _secureSettings = new SecureSettings();
        return _secureSettings;
      }
    }

    public void WriteGeneralSettings()
    {
      GeneralSettingsWrapper gsWrapper = new GeneralSettingsWrapper();
      gsWrapper.GeneralSettings = this.GeneralSettings;

      FileHelper.CreateBackup(_basePath + "settings.general.json", _basePath, "settings.general.json.backup");

      FileHelper.WriteTextToFile(_basePath, "settings.general.json", JsonConvert.SerializeObject(gsWrapper, Formatting.Indented));
    }

    public void WriteAnalyzerSettings()
    {
      AnalyzerSettingsWrapper asWrapper = new AnalyzerSettingsWrapper();
      asWrapper.AnalyzerSettings = this.AnalyzerSettings;

      JsonSerializerSettings settings = new JsonSerializerSettings();
      settings.NullValueHandling = NullValueHandling.Ignore;
      settings.DefaultValueHandling = DefaultValueHandling.Ignore;

      FileHelper.CreateBackup(_basePath + "settings.analyzer.json", _basePath, "settings.analyzer.json.backup");

      FileHelper.WriteTextToFile(_basePath, "settings.analyzer.json", JsonConvert.SerializeObject(asWrapper, Formatting.Indented, settings));
    }

    public void WriteSecureSettings(string password)
    {
      string passwordEncrypted = EncryptionHelper.Encrypt(password);

      this.SecureSettings.MonitorPassword = passwordEncrypted;

      SecureSettingsWrapper ssWrapper = new SecureSettingsWrapper();
      ssWrapper.SecureSettings = this.SecureSettings;

      FileHelper.WriteTextToFile(_basePath, "settings.secure.json", JsonConvert.SerializeObject(ssWrapper, Formatting.Indented));
    }
  }
}
