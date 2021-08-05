using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Core.Main;
using Core.Helper;
using Core.Main.DataObjects.PTMagicData;
using System.Text.RegularExpressions;

namespace Core.ProfitTrailer
{
  public static class StrategyHelper
  {
    public static string GetStrategyShortcut(string strategyName, bool onlyValidStrategies)
    {
      string result = strategyName;
      string time = "";
      string leverage = "";
      string strategyLetter = "";
      string strategyNameOnly = strategyName;

      // PT allows for "advanced_stats" to show details of the trailing logic and dynamic formulas.
      if (result.Contains("STATS") || result.Contains("DYN"))
      {
        if (result.Contains("STATS-TSL") && (!result.Contains("TSL-STRAT")))
        {
          result = "TSL";
        }
        else
        {
          result = "";
        }  
      }
      // strategy labels with variable values
      if (result.Contains("BUY TIMEOUT AS"))
      {
        time = strategyName.Remove(0, 15);
        result = "REBUY " + time;
      }
      if (result.Contains("BUY TIMEOUT"))
      {
        time = strategyName.Remove(0, 12);
        result = "REBUY " + time;
      }
      if (result.Contains("CHANGE PERC"))
      {
        result = "CHANGE";
      }
      if (result.Contains("CROSSED"))
      {
        leverage = strategyName.Remove(0, 9);
        leverage = leverage.Remove(leverage.Length - 1, 1);
        result = "CROSS " + leverage + "X";
      }
      if (result.Contains("ISOLATED"))
      {
        leverage = strategyName.Remove(0, 10);
        leverage = leverage.Remove(leverage.Length - 1, 1);
        result = "ISOL " + leverage + "X";
      }
      // remove the letter and colon, change to shortcut, then reapply the letter and colon
      if (strategyName.Contains(":"))
      {
        int strategyLength = strategyName.Length - 3;
        strategyLetter = strategyName.Remove(3, strategyLength);
        strategyNameOnly = strategyName.Remove(0, 3);
      }
      switch (strategyNameOnly.ToLower())
      {
        case "lowbb":
          result = String.Concat(strategyLetter, "LBB");
          break;
        case "highbb":
          result = String.Concat(strategyLetter, "HBB");
          break;
        case "gain":
          result = String.Concat(strategyLetter, "GAIN");
          break;
        case "loss":
          result = String.Concat(strategyLetter, "LOSS");
          break;
        case "smagain":
          result = String.Concat(strategyLetter, "SMA-G");
          break;
        case "emagain":
          result = String.Concat(strategyLetter, "EMA-G");
          break;
        case "hmagain":
          result = String.Concat(strategyLetter, "HMA-G");
          break;
        case "dmagain":
          result = String.Concat(strategyLetter, "DMA-G");
          break;
        case "smaspread":
          result = String.Concat(strategyLetter, "SMA-S");
          break;
        case "emaspread":
          result = String.Concat(strategyLetter, "EMA-S");
          break;
        case "hmaspread":
          result = String.Concat(strategyLetter, "HMA-S");
          break;
        case "dmaspread":
          result = String.Concat(strategyLetter, "DMA-S");
          break;
        case "smacross":
          result = String.Concat(strategyLetter, "SMA-C");
          break;
        case "emacross":
          result = String.Concat(strategyLetter, "EMA-C");
          break;
        case "hmacross":
          result = String.Concat(strategyLetter, "HMA-C");
          break;
        case "dmacross":
          result = String.Concat(strategyLetter, "DMA-C");
          break;
        case "rsi":
          result = String.Concat(strategyLetter, "RSI");
          break;
        case "stoch":
          result = String.Concat(strategyLetter, "STOCH");
          break;
        case "stochrsi":
          result = String.Concat(strategyLetter, "SRSI");
          break;
        case "stochrsik":
          result = String.Concat(strategyLetter, "SRSI-K");
          break;
        case "stochrsid":
          result = String.Concat(strategyLetter, "SRSI-D");
          break;
        case "stochrsicross":
          result = String.Concat(strategyLetter, "SRSI-C");
          break;
        case "macd":
          result = String.Concat(strategyLetter, "MACD");
          break;
        case "obv":
          result = String.Concat(strategyLetter, "OBV");
          break;
        case "bbwidth":
          result = String.Concat(strategyLetter, "BBW");
          break;
        case "pdhigh":
          result = String.Concat(strategyLetter, "PDH");
          break;
        case "anderson":
          result = String.Concat(strategyLetter, "AND");
          break;
        case "pdlow":
          result = String.Concat(strategyLetter, "PDL");
          break;
        case "pdclose":
          result = String.Concat(strategyLetter, "PDC");
          break;
        case "signal":
          result = String.Concat(strategyLetter, "SIG");
          break;
        case "changepercentage":
          result = String.Concat(strategyLetter, "CHGPCT");
          break;
        case "profitpercentage":
          result = String.Concat(strategyLetter, "PPCT");
          break;
        case "lastdcabuy":
          result = String.Concat(strategyLetter, "LASTDCA");
          break;
        case "fixedprice":
          result = String.Concat(strategyLetter, "FIXED");
          break;
        case "lowatrband":
          result = String.Concat(strategyLetter, "L-ATR");
          break;
        case "highatrband":
          result = String.Concat(strategyLetter, "H-ATR");
          break;
        case "atrpercentage":
          result = String.Concat(strategyLetter, "ATR-PCT");
          break;
        case "vwappercentage":
          result = String.Concat(strategyLetter, "VWAP");
          break;
        case "mvwappercentage":
          result = String.Concat(strategyLetter, "M-VWAP");
          break;
        case "btcdominance":
          result = String.Concat(strategyLetter, "BTCDOM");
          break;
        case "config som enabled":
          result = String.Concat(strategyLetter, "SOM");
          break;
        case "max buy times":
          result = String.Concat(strategyLetter, "DCAMAX");
          break;
        case "max pairs":
          result = String.Concat(strategyLetter, "PAIRS");
          break;
        case "max spread":
          result = String.Concat(strategyLetter, "SPREAD");
          break;
        case "price increase":
          result = String.Concat(strategyLetter, "INC");
          break;
        case "min buy volume":
          result = String.Concat(strategyLetter, "VOL");
          break;
        case "min buy balance":
          result = String.Concat(strategyLetter, "MIN");
          break;
        case "coin age":
          result = String.Concat(strategyLetter, "AGE");
          break;
        case "too new":
          result = String.Concat(strategyLetter, "NEW");
          break;
        case "blacklisted":
          result = String.Concat(strategyLetter, "BLACK");
          break;
        case "insufficient balance":
          result = String.Concat(strategyLetter, "BAL");
          break;
        case "max cost reached":
          result = String.Concat(strategyLetter, "COST");
          break;
        case "buy value below dust":
          result = String.Concat(strategyLetter, "DUST");
          break;
        case "sell wall detected":
          result = String.Concat(strategyLetter, "WALL");
          break;
        case "ignoring buy trigger":
          result = String.Concat(strategyLetter, "TRIG");
          break;
        case "no dca buy logic":
          result = String.Concat(strategyLetter, "NODCA");
          break;
        case "combimagain":
          result = String.Concat(strategyLetter, "COMBI-G");
          break;
        case "combimaspread":
          result = String.Concat(strategyLetter, "COMBI-S");
          break;
        case "combimacross":
          result = String.Concat(strategyLetter, "COMBI-C");
          break;
        case "macdpercentage":
          result = String.Concat(strategyLetter, "MACDPERC");
          break;
        default:
          break;
      }
      if (onlyValidStrategies)
      {
        if (strategyName.IndexOf("SOM") > -1 || strategyName.IndexOf("MAX") > -1 || strategyName.IndexOf("MIN") > -1 || strategyName.IndexOf("PRICE") > -1 || strategyName.IndexOf("BLACK") > -1 || strategyName.IndexOf("INSUFFICIENT") > -1 || strategyName.IndexOf("COST") > -1)
        {
          result = "";
        }
      }
      return result;
    }

    public static bool IsValidStrategy(string strategyName)
    {
      return StrategyHelper.IsValidStrategy(strategyName, false);
    }

    public static bool IsValidStrategy(string strategyName, bool checkForAnyInvalid)
    {
      bool result = false;

      if (!string.IsNullOrEmpty(strategyName))
      {
        // buy/sell strategies beginning with PT 2.3.3 contain the letter followed by a colon and space.
        if (strategyName.Contains(":") && !strategyName.Contains("FORMULA"))
        {
          result = true;
        }
        if (!checkForAnyInvalid)
        {
          switch (strategyName.ToLower())
          {
            case "lowbb":
            case "highbb":
            case "gain":
            case "loss":
            case "smagain":
            case "emagain":
            case "hmagain":
            case "dmagain":
            case "smaspread":
            case "emaspread":
            case "hmaspread":
            case "dmaspread":
            case "smacross":
            case "emacross":
            case "hmacross":
            case "dmacross":
            case "rsi":
            case "stoch":
            case "stochrsi":
            case "stochrsik":
            case "stochrsid":
            case "stochrsicross":
            case "macd":
            case "obv":
            case "bbwidth":
            case "anderson":
            case "dema":
            case "hma":
            case "pdhigh":
            case "pdlow":
            case "pdclose":
            case "signal":
            case "changepercentage":
            case "profitpercentage":
            case "lastdcabuy":
            case "fixedprice":
            case "lowatrband":
            case "highatrband":
            case "atrpercentage":
            case "vwappercentage":
            case "mvwappercentage":
            case "btcdominance":
            case "combimagain":
            case "combimaspread":
            case "combimacross":
            case "macdpercentage":
              result = true;
              break;
            default:
              break;
          }
        }
        else
        {
          if (strategyName.IndexOf("max", StringComparison.InvariantCultureIgnoreCase) == -1
            && strategyName.IndexOf("min", StringComparison.InvariantCultureIgnoreCase) == -1
            && strategyName.IndexOf("som", StringComparison.InvariantCultureIgnoreCase) == -1
            && strategyName.IndexOf("price", StringComparison.InvariantCultureIgnoreCase) == -1
            && strategyName.IndexOf("black", StringComparison.InvariantCultureIgnoreCase) == -1
            && strategyName.IndexOf("new", StringComparison.InvariantCultureIgnoreCase) == -1
            && strategyName.IndexOf("insufficient", StringComparison.InvariantCultureIgnoreCase) == -1
            && strategyName.IndexOf("timeout", StringComparison.InvariantCultureIgnoreCase) == -1
            && strategyName.IndexOf("spread", StringComparison.InvariantCultureIgnoreCase) == -1
            && strategyName.IndexOf("pairs", StringComparison.InvariantCultureIgnoreCase) == -1)
          {
            result = true;
          }
        }
      }

      return result;
    }


    public static int GetStrategyValueDecimals(string strategyName)
    {
      int result = 0;
      switch (strategyName.ToLower())
      {
        case "lowbb":
        case "highbb":
          result = 8;
          break;
        case "gain":
        case "loss":
        case "smagain":
        case "emagain":
        case "hmagain":
        case "dmagain":
        case "smaspread":
        case "emaspread":
        case "hmaspread":
        case "dmaspread":
        case "smacross":
        case "emacross":
        case "hmacross":
        case "dmacross":
        case "anderson":
        case "pdhigh":
          result = 2;
          break;
        case "rsi":
        case "stochrsi":
        case "stochrsik":
        case "stochrsid":
        case "stochrsicross":
        case "stoch":
        case "macd":
        case "obv":
        case "bbwidth":
          result = 0;
          break;
        default:
          break;
      }
      return result;
    }
    public static string GetStrategyText(Summary summary, List<Strategy> strategies, string strategyText, bool isTrue, bool isTrailingBuyActive)
    {
      bool isValidStrategy = false;
      Regex regx = new Regex(@"[ABCDEFGHIJKLMNOPQRSTUVWXYZ]", RegexOptions.Compiled);

      if (strategies.Count > 0)
      {
        foreach (Strategy strategy in strategies)
        {
          string textClass = (strategy.IsTrue) ? "label-success" : "label-danger";
          isValidStrategy = StrategyHelper.IsValidStrategy(strategy.Name);
          if (!isValidStrategy)
          {
            if (strategy.Name.Contains("TRIGGERED"))
            // remove levels already triggered, to show only currently waiting trigger
            {
              strategyText += "";
            }
            else if (strategy.Name.Contains("STATS"))
            // Avoid displaying advanced buy stats and completed level formulas
            {
              if (strategy.Name.Contains("STATS-TSL") && (!strategy.Name.Contains("TSL-STRAT")))
              {
                strategyText += "<span class=\"label label-warn\" data-toggle=\"tooltip\" data-placement=\"top\" title=\"" + strategy.Name + "\" >" + StrategyHelper.GetStrategyShortcut(strategy.Name, false) + "<i class=\"fa fa-flag text-success\"></i></span> ";
              }
              else
              {
                strategy.Name = "";
              }           
            }
            else if (strategy.Name.Contains("FORMULA"))
            // Avoid displaying formula details
            {
              if (strategy.Name.Contains("LEVEL"))
              {
                string level = strategy.Name.Substring(5, 1);
                strategyText += "<span class=\"label label-warning\" data-toggle=\"tooltip\" data-placement=\"top\" title=\"LEVEL FORMULA\">L " + level + "</span> ";
              }
              else
              {
                strategyText += "<span class=\"label label-warning\" data-toggle=\"tooltip\" data-placement=\"top\" title=\"CONDITIONAL FORMULA\">FORM</span> ";
              }
            }
            else
            {
              strategyText += "<span class=\"label label-warning\" data-toggle=\"tooltip\" data-placement=\"top\" title=\"" + strategy.Name + "\">" + StrategyHelper.GetStrategyShortcut(strategy.Name, false) + "</span> ";
            }

          }
          else
          {
            strategyText += "<span class=\"label " + textClass + "\" data-toggle=\"tooltip\" data-placement=\"top\" title=\"" + strategy.Name + "\">" + StrategyHelper.GetStrategyShortcut(strategy.Name, false) + "</span> ";
          }
        }

        if (isTrailingBuyActive)
        {
          strategyText += " <i class=\"fa fa-flag text-success\" data-toggle=\"tooltip\" data-placement=\"top\" title=\"Trailing active!\"></i>";
        }
      }
      else
      {
        if (isTrue)
        {
          strategyText = "<span class=\"label label-success\" data-toggle=\"tooltip\" data-placement=\"top\" title=\"" + strategyText + "\">" + StrategyHelper.GetStrategyShortcut(strategyText, true) + "</span> ";

          if (isTrailingBuyActive)
          {
            strategyText += " <i class=\"fa fa-flag text-success\" data-toggle=\"tooltip\" data-placement=\"top\" title=\"Trailing active!\"></i>";
          }
        }
        else
        {
          isValidStrategy = StrategyHelper.IsValidStrategy(strategyText);
          if (isValidStrategy)
          {
            strategyText = "<span class=\"label label-danger\" data-toggle=\"tooltip\" data-placement=\"top\" title=\"" + strategyText + "\">" + StrategyHelper.GetStrategyShortcut(strategyText, true) + "</span> ";
          }
          else if (strategyText.Equals("") && isValidStrategy == false)
          {
            strategyText = "<span class=\"label label-muted\" data-toggle=\"tooltip\" data-placement=\"top\" title=\"Not Applicable: Not using DCA!\"></span>";
          }
          else
          {
            strategyText = "<span class=\"label label-warning\" data-toggle=\"tooltip\" data-placement=\"top\" title=\"" + strategyText + "\">" + StrategyHelper.GetStrategyShortcut(strategyText, false) + "</span> ";
          }
        }
      }
      return strategyText;
    }
    public static string GetCurrentValueText(List<Strategy> strategies, string strategyText, double bbValue, double simpleValue, bool includeShortcut)
    {
      string result = "";

      if (strategies.Count > 0)
      {
        foreach (Strategy strategy in strategies)
        {
          if (StrategyHelper.IsValidStrategy(strategy.Name))
          {
            if (!result.Equals("")) result += "<br />";

            string decimalFormat = "";
            int decimals = StrategyHelper.GetStrategyValueDecimals(strategy.Name);
            for (int d = 1; d <= decimals; d++)
            {
              decimalFormat += "0";
            }

            if (includeShortcut)
            {
              result += "<span class=\"text-muted\">" + StrategyHelper.GetStrategyShortcut(strategy.Name, true) + "</span> ";
            }

            if (StrategyHelper.GetStrategyShortcut(strategy.Name, true).IndexOf("and", StringComparison.InvariantCultureIgnoreCase) > -1)
            {
              result += simpleValue.ToString("#,#0.00", new System.Globalization.CultureInfo("en-US"));
            }
            else
            {
              if (decimals == 0)
              {
                if (!SystemHelper.IsInteger(strategy.CurrentValue))
                {
                  result += strategy.CurrentValue.ToString("#,#", new System.Globalization.CultureInfo("en-US"));
                }
                else
                {
                  result += strategy.CurrentValue.ToString("#,#0", new System.Globalization.CultureInfo("en-US"));
                }
              }
              else
              {
                result += strategy.CurrentValue.ToString("#,#0." + decimalFormat, new System.Globalization.CultureInfo("en-US"));
              }
            }
          }
        }
      }
      else
      {
        if (StrategyHelper.GetStrategyShortcut(strategyText, true).IndexOf("bb", StringComparison.InvariantCultureIgnoreCase) > -1)
        {
          result = bbValue.ToString("#,#0.00000000", new System.Globalization.CultureInfo("en-US"));
        }
        else
        {
          result = simpleValue.ToString("#,#0.00", new System.Globalization.CultureInfo("en-US")) + "%";
        }
      }
      return result;
    }

    public static string GetTriggerValueText(Summary summary, List<Strategy> strategies, string strategyText, double bbValue, double simpleValue, int buyLevel, bool includeShortcut)
    {
      string result = "";
      if (strategies.Count > 0)
      {
        foreach (Strategy strategy in strategies)
        {
          if (StrategyHelper.IsValidStrategy(strategy.Name))
          {
            if (!result.Equals("")) result += "<br />";

            string decimalFormat = "";
            int decimals = StrategyHelper.GetStrategyValueDecimals(strategy.Name);
            for (int d = 1; d <= decimals; d++)
            {
              decimalFormat += "0";
            }
            if (includeShortcut)
            {
              result += "<span class=\"text-muted\">" + StrategyHelper.GetStrategyShortcut(strategy.Name, true) + "</span> ";
            }
            if (StrategyHelper.GetStrategyShortcut(strategy.Name, true).IndexOf("and", StringComparison.InvariantCultureIgnoreCase) > -1)
            {
              result += strategy.TriggerValue.ToString("#,#0.00", new System.Globalization.CultureInfo("en-US"));
            }
            else
            {
              if (decimals == 0)
              {
                if (!SystemHelper.IsInteger(strategy.EntryValue))
                {
                  result += strategy.EntryValue.ToString(new System.Globalization.CultureInfo("en-US"));
                }
                else
                {
                  result += strategy.EntryValue.ToString("#,#0", new System.Globalization.CultureInfo("en-US"));
                }
              }
              else
              {
                result += strategy.EntryValue.ToString("#,#0." + decimalFormat, new System.Globalization.CultureInfo("en-US"));
              }
            }
          }
        }
      }
      else
      {
        if (StrategyHelper.GetStrategyShortcut(strategyText, true).IndexOf("bb", StringComparison.InvariantCultureIgnoreCase) > -1)
        {
          result = bbValue.ToString("#,#0.00000000", new System.Globalization.CultureInfo("en-US"));
        }
        else
        {
          if (simpleValue == Constants.MinTrendChange)
          {
            if (summary.DCATriggers.ContainsKey(buyLevel + 1))
            {
              simpleValue = summary.DCATriggers[buyLevel + 1];
            }
          }
          result = simpleValue.ToString("#,#0.00", new System.Globalization.CultureInfo("en-US")) + "%";
        }
      }
      return result;
    }
  }
}
