using System;
using System.IO;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;

namespace Core.Helper
{
  public static class TelegramHelper
  {
    public async static void SendMessage(string botToken, Int64 chatId, string message, bool useSilentMode, LogHelper log)
    {
      if (!botToken.Equals("") && chatId != 0)
      {
        try
        {
          TelegramBotClient botClient = new TelegramBotClient(botToken);
          await botClient.SendTextMessageAsync(chatId: chatId, text: message, parseMode: ParseMode.Markdown,disableNotification: useSilentMode);
          log.DoLogDebug("Telegram message sent to ChatId " + chatId.ToString() + " on Bot Token '" + botToken + "'");
        }
        catch (Exception ex)
        {
          log.DoLogCritical("Exception sending telegram message to ChatId " + chatId.ToString() + " on Bot Token '" + botToken + "'", ex);
        }
      }
    }
  }
}
