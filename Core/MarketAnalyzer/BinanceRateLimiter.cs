using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Core.Helper;

namespace Core.MarketAnalyzer
{
  internal static class BinanceRateLimiter
  {
    private const double SoftLimitRatio = 0.80;
    private static readonly ConcurrentDictionary<string, HostState> HostStates = new ConcurrentDictionary<string, HostState>(StringComparer.OrdinalIgnoreCase);

    private sealed class HostState
    {
      public readonly object SyncRoot = new object();
      public int UsedWeight;
      public DateTimeOffset WindowResetUtc = GetNextMinuteBoundary();
      public DateTimeOffset CooldownUntilUtc = DateTimeOffset.MinValue;
    }

    public static void WaitIfNeeded(Uri requestUri, LogHelper log)
    {
      if (!TryGetLimit(requestUri, out int requestWeightLimit))
      {
        return;
      }

      HostState state = HostStates.GetOrAdd(requestUri.Host, _ => new HostState());
      while (true)
      {
        TimeSpan delay = GetRequiredDelay(state, requestWeightLimit);
        if (delay <= TimeSpan.Zero)
        {
          return;
        }

        log.DoLogWarn($"Binance rate limiter pausing requests to {requestUri.Host} for {delay.TotalSeconds:0.###} seconds.");
        Thread.Sleep(delay);
      }
    }

    public static async Task WaitIfNeededAsync(Uri requestUri, LogHelper log, CancellationToken cancellationToken)
    {
      if (!TryGetLimit(requestUri, out int requestWeightLimit))
      {
        return;
      }

      HostState state = HostStates.GetOrAdd(requestUri.Host, _ => new HostState());
      while (true)
      {
        TimeSpan delay = GetRequiredDelay(state, requestWeightLimit);
        if (delay <= TimeSpan.Zero)
        {
          return;
        }

        log.DoLogWarn($"Binance rate limiter pausing requests to {requestUri.Host} for {delay.TotalSeconds:0.###} seconds.");
        await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
      }
    }

    public static void ObserveResponse(Uri requestUri, HttpResponseMessage response)
    {
      if (!TryGetLimit(requestUri, out _))
      {
        return;
      }

      HostState state = HostStates.GetOrAdd(requestUri.Host, _ => new HostState());
      lock (state.SyncRoot)
      {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ResetWindowIfNeeded(state, now);

        if (TryGetUsedWeight(response, out int usedWeight))
        {
          state.UsedWeight = usedWeight;
        }

        if ((int)response.StatusCode == 429 || (int)response.StatusCode == 418)
        {
          TimeSpan delay = GetRetryAfter(response);
          DateTimeOffset cooldownUntil = now + delay;
          if (cooldownUntil > state.CooldownUntilUtc)
          {
            state.CooldownUntilUtc = cooldownUntil;
          }
        }
      }
    }

    private static TimeSpan GetRequiredDelay(HostState state, int requestWeightLimit)
    {
      lock (state.SyncRoot)
      {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        ResetWindowIfNeeded(state, now);

        DateTimeOffset waitUntil = state.CooldownUntilUtc;
        int softLimit = (int)Math.Floor(requestWeightLimit * SoftLimitRatio);
        if (state.UsedWeight >= softLimit && state.WindowResetUtc > waitUntil)
        {
          waitUntil = state.WindowResetUtc;
        }

        return waitUntil > now ? waitUntil - now : TimeSpan.Zero;
      }
    }

    private static void ResetWindowIfNeeded(HostState state, DateTimeOffset now)
    {
      if (now >= state.WindowResetUtc)
      {
        state.UsedWeight = 0;
        state.WindowResetUtc = GetNextMinuteBoundary(now);
      }

      if (now >= state.CooldownUntilUtc)
      {
        state.CooldownUntilUtc = DateTimeOffset.MinValue;
      }
    }

    private static DateTimeOffset GetNextMinuteBoundary()
    {
      return GetNextMinuteBoundary(DateTimeOffset.UtcNow);
    }

    private static DateTimeOffset GetNextMinuteBoundary(DateTimeOffset now)
    {
      DateTimeOffset minuteStart = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, TimeSpan.Zero);
      return minuteStart.AddMinutes(1).AddSeconds(1);
    }

    private static TimeSpan GetRetryAfter(HttpResponseMessage response)
    {
      if (response.Headers.RetryAfter?.Delta != null)
      {
        TimeSpan delay = response.Headers.RetryAfter.Delta.Value;
        return delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1);
      }

      if (response.Headers.RetryAfter?.Date != null)
      {
        TimeSpan delay = response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
        return delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1);
      }

      return TimeSpan.FromSeconds(60);
    }

    private static bool TryGetUsedWeight(HttpResponseMessage response, out int usedWeight)
    {
      usedWeight = 0;
      if (!response.Headers.TryGetValues("X-MBX-USED-WEIGHT-1M", out var values))
      {
        return false;
      }

      string value = values.FirstOrDefault();
      return Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out usedWeight);
    }

    private static bool TryGetLimit(Uri requestUri, out int requestWeightLimit)
    {
      requestWeightLimit = 0;
      if (requestUri == null)
      {
        return false;
      }

      if (requestUri.Host.Equals("fapi.binance.com", StringComparison.OrdinalIgnoreCase))
      {
        requestWeightLimit = 2400;
        return true;
      }

      if (requestUri.Host.Equals("api.binance.com", StringComparison.OrdinalIgnoreCase) || requestUri.Host.Equals("api.binance.us", StringComparison.OrdinalIgnoreCase))
      {
        requestWeightLimit = 6000;
        return true;
      }

      return false;
    }
  }
}
