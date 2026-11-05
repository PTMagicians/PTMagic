using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Core.Helper;

namespace Monitor._Internal
{
  public class TempFileCleanupService : BackgroundService
  {
    private const int MaxFileAgeMinutes = 5;
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromMinutes(1);

    private readonly string _tempFolderPath;
    private readonly ILogger<TempFileCleanupService> _logger;

    public TempFileCleanupService(string monitorBasePath, ILogger<TempFileCleanupService> logger)
    {
      _tempFolderPath = Path.Combine(monitorBasePath, "wwwroot", "assets", "tmp") + Path.DirectorySeparatorChar;
      _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
      while (!stoppingToken.IsCancellationRequested)
      {
        try
        {
          FileHelper.CleanupFilesMinutes(_tempFolderPath, MaxFileAgeMinutes);
        }
        catch (Exception ex)
        {
          _logger.LogWarning(ex, "Monitor temp-file cleanup failed. Cleanup will retry on the next interval.");
        }

        try
        {
          await Task.Delay(CleanupInterval, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
          break;
        }
      }
    }
  }
}
