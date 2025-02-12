using GIFBot.Shared;
using GIFBot.Shared.Models.Features;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchLib.Api.Helix;
using TwitchLib.EventSub;
using static GIFBot.Shared.AnimationEnums;
using static GIFBot.Shared.Utility.Enumerations;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using Microsoft.Build.Framework;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api;

namespace GIFBot.Server.Features.EventSub
{
   public class EventSubManager
   {
      public EventSubManager(GIFBot.GIFBot bot)
      {
         Bot = bot;
      }

      public void InitializeEventSub(bool disconnectPrior = false)
      {
         try
         {
            if (mTwitchEventSubWebsocketClient != null && disconnectPrior)
            {
               mTwitchEventSubWebsocketClient.DisconnectAsync().Wait();
               mTwitchEventSubWebsocketClient = null;
            }

            mTwitchEventSubWebsocketClient = new EventSubWebsocketClient();
            mTwitchEventSubWebsocketClient.WebsocketConnected += TwitchEventSub_OnWebsocketConnected;
            mTwitchEventSubWebsocketClient.WebsocketDisconnected += TwitchEventSub_OnWebsocketClosed;
            mTwitchEventSubWebsocketClient.WebsocketReconnected += TwitchEventSub_OnWebsocketReconnected;
            mTwitchEventSubWebsocketClient.ErrorOccurred += TwitchEventSub_OnWebsocketError;

            // Channel Point Redemptions
            mTwitchEventSubWebsocketClient.ChannelPointsAutomaticRewardRedemptionAdd += TwitchEventSub_ChannelPointsAutomaticRewardRedemptionAdd;
            mTwitchEventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += TwitchEventSub_ChannelPointsCustomRewardRedemptionAdd;

            // Advertisement Alerts
            mTwitchEventSubWebsocketClient.ChannelAdBreakBegin += TwitchEventSubWebsocketClient_ChannelAdBreakBegin;

            if (!String.IsNullOrEmpty(Bot.BotSettings.StreamerOauthToken))
            {
               if (disconnectPrior)
               {
                  _ = mTwitchEventSubWebsocketClient.ReconnectAsync();
               }
               else
               {
                  _ = mTwitchEventSubWebsocketClient.ConnectAsync();
               }
            }
         }
         catch (Exception /*ex*/)
         {
            _ = Bot.SendLogMessage("Unable to start the TwitchEventSub client.");
         }
      }

      private Task TwitchEventSub_OnWebsocketConnected(object sender, WebsocketConnectedArgs e)
      {
         if (!String.IsNullOrEmpty(Bot.BotSettings.StreamerOauthToken) && Bot.ChannelId != 0)
         {
            _ = Bot.SendLogMessage("EventSub client connected! Sending topics.");

            if (!e.IsRequestedReconnect)
            {
               var conditions = new Dictionary<string, string>()
               {
                  { "broadcaster_user_id", Bot.ChannelId.ToString() },
                  { "moderator_id", Bot.ChannelId.ToString() }
               };

               _ = Bot.TwitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.channel_points_automatic_reward_redemption.add", "1",
                     conditions, EventSubTransportMethod.Websocket, mTwitchEventSubWebsocketClient.SessionId);
               _ = Bot.TwitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.channel_points_custom_reward_redemption.add", "1",
                     conditions, EventSubTransportMethod.Websocket, mTwitchEventSubWebsocketClient.SessionId);
               _ = Bot.TwitchAPI.Helix.EventSub.CreateEventSubSubscriptionAsync("channel.ad_break.begin", "1",
                  conditions, EventSubTransportMethod.Websocket, mTwitchEventSubWebsocketClient.SessionId);
            }
         }

         return Task.CompletedTask;
      }

      private async Task TwitchEventSub_OnWebsocketClosed(object sender, EventArgs e)
      {
         if (!String.IsNullOrEmpty(Bot.BotSettings.StreamerOauthToken))
         {
            _ = Bot.SendLogMessage($"EventSub client disconnected. [{e.ToString()}]  Reconnecting...");

            // This isn't good to do in a prod env, but this is just a tiny little bot running localhost. 
            // In other words, I don't care as long as the damn thing works, because I AINT GOT THE TIME FOR IT.
            while (!await mTwitchEventSubWebsocketClient.ReconnectAsync())
            {
               _ = Bot.SendLogMessage("EventSub error: Websocket reconnect failed!");
               await Task.Delay(1000);
            }
         }
      }

      private Task TwitchEventSub_OnWebsocketReconnected(object sender, EventArgs e)
      {
         if (!String.IsNullOrEmpty(Bot.BotSettings.StreamerOauthToken))
         {
            _ = Bot.SendLogMessage("EventSub client reconnected!");
         }

         return Task.CompletedTask;
      }

      private async Task TwitchEventSub_OnWebsocketError(object sender, ErrorOccuredArgs e)
      {
         if (!String.IsNullOrEmpty(Bot.BotSettings.StreamerOauthToken))
         {
            _ = Bot.SendLogMessage($"EventSub error: {e.Exception.ToString()}. Do you have the wrong oauth scopes?");

            // This isn't good to do in a prod env, but this is just a tiny little bot running localhost. 
            // In other words, I don't care as long as the damn thing works, because I AINT GOT THE TIME FOR IT.
            while (!await mTwitchEventSubWebsocketClient.ReconnectAsync())
            {
               _ = Bot.SendLogMessage("EventSub error: Websocket reconnect failed!");
               await Task.Delay(1000);
            }
         }
      }

      private Task TwitchEventSub_ChannelPointsCustomRewardRedemptionAdd(object sender, TwitchLib.EventSub.Websockets.Core.EventArgs.Channel.ChannelPointsCustomRewardRedemptionArgs args)
      {
         var eventData = args.Notification.Payload.Event;
         HandleRedemption(eventData.Reward.Id, eventData.Reward.Title, eventData.Reward.Cost, eventData.UserName, eventData.UserInput);

         return Task.CompletedTask;
      }

      private Task TwitchEventSub_ChannelPointsAutomaticRewardRedemptionAdd(object sender, TwitchLib.EventSub.Websockets.Core.EventArgs.Channel.ChannelPointsAutomaticRewardRedemptionArgs args)
      {
         var eventData = args.Notification.Payload.Event;
         HandleRedemption(Guid.Empty.ToString(), "BuiltInTwitchReward", eventData.Reward.Cost, eventData.UserName, eventData.UserInput);

         return Task.CompletedTask;
      }

      private async Task TwitchEventSubWebsocketClient_ChannelAdBreakBegin(object sender, TwitchLib.EventSub.Websockets.Core.EventArgs.Channel.ChannelAdBreakBeginArgs args)
      {
         if (Bot.BotSettings.AnnounceAdBreaks && !string.IsNullOrEmpty(Bot.BotSettings.AdBreakStartAnnouncement))
         {
            Bot.SendChatMessage($"{Bot.BotSettings.AdBreakStartAnnouncement} ({args.Notification.Payload.Event.DurationSeconds}s)");
            await AnnounceAdBreakEnding(args.Notification.Payload.Event.DurationSeconds * 1000);
         }
      }

      async Task AnnounceAdBreakEnding(int delayInMs = 60000)
      {
         await Task.Delay(delayInMs);
         if (!string.IsNullOrEmpty(Bot.BotSettings.AdBreakEndAnnouncement))
         {
            Bot.SendChatMessage(Bot.BotSettings.AdBreakEndAnnouncement);
         }
      }

      private void HandleRedemption(string rewardId, string rewardTitle, int rewardCost, string redeemerName, string redeemerInput)
      {
         Guid redeemedRewardId = new Guid(rewardId);

         if (!mProcessedRewardIds.Contains(redeemedRewardId))
         {
            _ = Bot.SendLogMessage($"EventSub: {rewardTitle} redeemed by {redeemerName}!");

            mProcessedRewardIds.Enqueue(redeemedRewardId);
            if (mProcessedRewardIds.Count > skMaxRewardIdsToTrack)
            {
               mProcessedRewardIds.Dequeue();
            }

            // Place a sticker, if applicable.
            if (Bot.StickersManager != null &&
                Bot.StickersManager.Data != null &&
                Bot.StickersManager.Data.Enabled &&
                ((Bot.StickersManager.Data.IncludeChannelPoints && rewardCost >= Bot.StickersManager.Data.ChannelPointsMinimum) ||
                 (Bot.StickersManager.Data.CanUseCommand && rewardTitle.Contains(Bot.StickersManager.Data.Command, StringComparison.OrdinalIgnoreCase))))
            {
               _ = Bot.SendLogMessage($"Sticker placed for channel points spent by [{redeemerName}].");
               _ = Bot.StickersManager.PlaceASticker(rewardTitle);
            }

            // Find and queue any of the animations flagged for alert mode.
            // Yes, allowing the users to have multiple.
            IEnumerable<AnimationData> cpAlertAnims = Bot.AnimationManager.GetAllAnimations(GIFBot.AnimationManager.FetchType.EnabledOnly).Where(a => a.ChannelPointRedemptionType == ChannelPointRedemptionTriggerType.All);
            if (cpAlertAnims.Any())
            {
               foreach (var alertAnim in cpAlertAnims)
               {
                  Bot.AnimationManager.ForceQueueAnimation(alertAnim, redeemerName, String.Empty);
               }
            }

            if (!string.IsNullOrEmpty(redeemerInput))
            {
               // See if there's just an animation where the command matches the input text.
               AnimationData cpAnim = Bot.AnimationManager.GetAllAnimations(GIFBot.AnimationManager.FetchType.EnabledOnly).Where(a => a.ChannelPointRedemptionType == ChannelPointRedemptionTriggerType.MessageText && redeemerInput.Contains(a.Command, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
               if (cpAnim != null)
               {
                  Bot.AnimationManager.ForceQueueAnimation(cpAnim, redeemerName, String.Empty);
               }
            }

            // Look for !animationroulette
            if (rewardTitle.Contains("!animationroulette", StringComparison.OrdinalIgnoreCase) && !Bot.BotSettings.AnimationRouletteChatEnabled)
            {
               Bot.AnimationManager.PlayRandomAnimation(redeemerName);
            }

            // Look for a valid regurgitator package
            RegurgitatorPackage qualifyingPackage = null;
            lock (Bot.RegurgitatorManager.PackagesMutex)
            {
               qualifyingPackage = Bot.RegurgitatorManager.Data.Packages.FirstOrDefault(p => rewardTitle.Contains(p.Settings.Command, StringComparison.OrdinalIgnoreCase));
            }

            if (qualifyingPackage != null && qualifyingPackage.Settings.Enabled && !qualifyingPackage.Settings.PlayOnTimer)
            {
               Bot.RegurgitatorManager.Play(qualifyingPackage);
            }

            // Look for Backdrops
            if (Bot.BackdropManager?.Data?.Enabled == true &&
                Bot.BackdropManager.Data.RedemptionType == CostRedemptionType.ChannelPoints &&
                rewardTitle.Contains(Bot.BackdropManager.Data.Command) &&
                rewardCost == Bot.BackdropManager.Data.Cost)
            {
               Bot.BackdropManager.HandleBackdropEvent(rewardTitle);
            }

            // Look for Countdown Timer
            if (Bot.CountdownTimerManager?.Data?.Enabled == true &&
                Bot.CountdownTimerManager.Data.Actions.Where(a => a.Enabled && a.RedemptionType == CostRedemptionType.ChannelPoints).Any())
            {
               Bot.CountdownTimerManager.HandleTimerEvent(rewardCost, CostRedemptionType.ChannelPoints);
            }

            // Look for an active giveaway.
            if (Bot?.GiveawayManager?.Data?.IsOpenForEntries == true &&
                Bot?.GiveawayManager?.Data?.EntryBehavior == GiveawayData.GiveawayEntryBehaviorType.ChannelPoints &&
                Bot?.GiveawayManager?.Data?.ChannelPointRewardId == redeemedRewardId)
            {
               Bot.GiveawayManager.HandleChannelPointEntry(redeemerName);
            }

            // See if there is a command in the title of the reward and if the reward cost matches the cost on the animation.
            AnimationData rewardTitleAnim = Bot.AnimationManager.GetAllAnimations(GIFBot.AnimationManager.FetchType.EnabledOnly).Where(a => a.ChannelPointRedemptionType == ChannelPointRedemptionTriggerType.PointsUsed && rewardTitle.Contains(a.Command, StringComparison.OrdinalIgnoreCase) && a.ChannelPointsRequired == rewardCost).FirstOrDefault();
            if (rewardTitleAnim != null)
            {
               Bot.AnimationManager.ForceQueueAnimation(rewardTitleAnim, redeemerName, String.Empty);
            }
            else
            {
               // Otherwise check to see if an animation should play based on the cost alone.
               AnimationData cpPointCostAnim = Bot.AnimationManager.GetAllAnimations(GIFBot.AnimationManager.FetchType.EnabledOnly).Where(a => a.ChannelPointRedemptionType == ChannelPointRedemptionTriggerType.PointsUsed && a.ChannelPointsRequired == rewardCost).FirstOrDefault();
               if (cpPointCostAnim != null)
               {
                  Bot.AnimationManager.ForceQueueAnimation(cpPointCostAnim, redeemerName, String.Empty);
               }
            }
         }
      }

      public GIFBot.GIFBot Bot { get; private set; }

      /// <summary>
      /// The EventSub Websocket Client from TwitchLib.
      /// </summary>
      private EventSubWebsocketClient mTwitchEventSubWebsocketClient;

      private Queue<Guid> mProcessedRewardIds = new Queue<Guid>();

      private const int skMaxRewardIdsToTrack = 200;
   }
}
