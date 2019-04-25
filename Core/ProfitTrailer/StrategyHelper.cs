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
  public static class StrategyHelper
  {
    public static string GetStrategyShortcut(string strategyName, bool onlyValidStrategies)
    {
      string result = strategyName;
      string time = "";
      string strategyLetter = "";
      string strategyNameOnly = strategyName;

      // strategy labels that are variable, so can't be caught by the switch statement
      if (result.Contains("REBUY"))
      {
        time = strategyName.Remove(0,14);
        result = "REBUY " + time;
      }
      if (result.Contains("CHANGE PERC"))
      {
        result = "CHANGE";
      }

      // buy/sell strategies beginning with PT 2.3.3 contain the stragegy designation letter followed by a colon and space.

      // remove the letter and colon, change to shortcut, then reapply the letter and colon
      if (strategyName.Contains(":"))
      {
        int strategyLength = strategyName.Length-3;
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
          result = String.Concat(strategyLetter, "SG");
          break;
        case "emagain":
          result = String.Concat(strategyLetter, "EG");
          break;
        case "hmagain":
          result = String.Concat(strategyLetter, "EG");
          break;
        case "dmagain":
          result = String.Concat(strategyLetter, "EG");
          break;
        case "smaspread":
          result = String.Concat(strategyLetter, "SS");
          break;
        case "emaspread":
          result = String.Concat(strategyLetter, "ES");
          break;
        case "hmaspread":
          result = String.Concat(strategyLetter, "HES");
          break;
        case "dmaspread":
          result = String.Concat(strategyLetter, "DES");
          break;
        case "smacross":
          result = String.Concat(strategyLetter, "SC");
          break;
        case "emacross":
          result = String.Concat(strategyLetter, "EC");
          break;
        case "hmacross":
          result = String.Concat(strategyLetter, "HSC");
          break;
        case "dmacross":
          result = String.Concat(strategyLetter, "DEC");
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
          result = String.Concat(strategyLetter, "SRSIK");
          break;
        case "stochrsid":
          result = String.Concat(strategyLetter, "SRSID");
          break;
        case "stochrsicross":
          result = String.Concat(strategyLetter, "SRSIC");
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
        case "som enabled":
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

      // buy/sell strategies beginning with PT 2.3.3 contain the letter followed by a colon and space.
      if (strategyName.Contains(":"))
      {
        result = true;
      }

      // Prior to PT 2.3.3
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
          case "signal":
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

      if (strategies.Count > 0)
      {
        foreach (Strategy strategy in strategies)
        {
          string textClass = (strategy.IsTrue) ? "label-success" : "label-danger";

          isValidStrategy = StrategyHelper.IsValidStrategy(strategy.Name);

          if (!isValidStrategy )
          {
            // Temporary until a fix for formula true/false
            if (strategy.Name.Contains("FORMULA"))
            {
              strategyText += "<span class=\"label label-warning\" data-toggle=\"tooltip\" data-placement=\"top\" title=\"AND/OR FORMULA\">FORM</span> ";
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
          strategyText = "<span class=\"label label-success\" data-toggle=\"tooltip\" data-placement=\"top\" title=\"" + strategyText + "\">" + StrategyHelper.GetStrategyShortcut(strategyText, true) + "</span>";

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
            strategyText = "<span class=\"label label-danger\" data-toggle=\"tooltip\" data-placement=\"top\" title=\"" + strategyText + "\">" + StrategyHelper.GetStrategyShortcut(strategyText, true) + "</span>";
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
