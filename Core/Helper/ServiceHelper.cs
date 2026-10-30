using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Extensions.Logging;

namespace Core.Helper
{
  public static class ServiceHelper
  {

    public static IServiceProvider BuildLoggerService()
    {
      return ServiceHelper.BuildLoggerService("");
    }

    public static IServiceProvider BuildLoggerService(string basePath)
    {
      ServiceCollection services = new ServiceCollection();

      services.AddTransient<LogHelper>();

      NLog.LogManager.LoadConfiguration(basePath + "nlog.config");
      services.AddLogging(builder =>
      {
        builder.SetMinimumLevel(LogLevel.Trace);
        builder.AddNLog(new NLogProviderOptions { CaptureMessageTemplates = true, CaptureMessageProperties = true });
      });

      return services.BuildServiceProvider();
    }
  }
}
