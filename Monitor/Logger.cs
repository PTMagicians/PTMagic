using System;
using Core.Helper;
using Microsoft.Extensions.DependencyInjection;

namespace Monitor
{
  public static class Logger
  {
    // Create a logger
    private static LogHelper _log = ServiceHelper.BuildLoggerService().GetRequiredService<LogHelper>();

    // Log writer functions
    public static void WriteLine(string line)
    {
      // Write to console and log
      Console.WriteLine(line);
      _log.DoLogInfo(line);
    }

    public static void WriteException(Exception ex, string description = null)
    {
      string output;

      output = string.Format("An exception has occurred {0}: {1}", description != null ? description : "", ex.ToString());
      
      // Write to console and log
      Console.WriteLine(output);
      _log.DoLogInfo(output);
    }
  }
}