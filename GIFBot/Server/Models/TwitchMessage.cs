using GIFBot.Server.Interfaces;
using GIFBot.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GIFBot.Server.Models
{
   public class TwitchMessage
   {
      #region Public Methods

      /// <summary>
      /// Constructor
      /// </summary>
      public TwitchMessage(GIFBot.GIFBot bot, TwitchLib.Client.Events.OnMessageReceivedArgs messageData)
      {
         Bot = bot;
         MessageData = messageData;
      }

      /// <summary>
      /// Processor - will look at the message and determine what actions should be taken, if any.
      /// </summary>
      public void Process()
      {
         System.Diagnostics.Debug.WriteLine($"{MessageData.ChatMessage.RawIrcMessage}");

         if (MessageData.ChatMessage.Message.Equals(Bot.BotSettings.AnimationCommand, StringComparison.OrdinalIgnoreCase) && Bot.BotSettings.AnimationCommandEnabled)
         {
            Bot.SendAnimationsListToChat();
            return;
         }

         if (MessageData.ChatMessage.Message.Equals("!animationroulette", StringComparison.OrdinalIgnoreCase) && Bot.BotSettings.AnimationRouletteChatEnabled)
         {
            Bot.AnimationManager.PlayRandomAnimation(MessageData.ChatMessage.DisplayName);
            return;
         }

         foreach (var manager in Bot.FeatureManagers.OfType<IFeatureManager>())
         {
            if (manager.CanHandleTwitchMessage(MessageData.ChatMessage.Message, MessageData.ChatMessage.IsBroadcaster))
            {
               manager.HandleTwitchMessage(MessageData);
            }
         }

         // Bits can trigger hype train events. Check for those events only after queuing the bit-related animation(s).
         // Deprecated until Twitch allows this endpoint to be called again. It was closed off for a "security" risk.
         // See: https://discuss.dev.twitch.tv/t/get-hype-train-events-via-app-token/31727/6
         //if (MessageData.ChatMessage.Bits >= skMinBitsForHypeTrain)
         //{
         //   Bot.CheckForHypeTrainEvent();
         //}
      }

      #endregion

      #region Properties

      public GIFBot.GIFBot Bot { get; private set; }

      public TwitchLib.Client.Events.OnMessageReceivedArgs MessageData { get; private set; }

      /// <summary>
      /// The minimum number of bits required to contribute to a hype train.
      /// </summary>
      //private static int skMinBitsForHypeTrain = 100;

      #endregion
   }
}
