using GIFBot.Shared;
using GIFBot.Shared.Models.Features;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.EventSub.Core.EventArgs.Channel;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using static GIFBot.Shared.AnimationEnums;
using static GIFBot.Shared.Utility.Enumerations;

namespace GIFBot.Server.Features.ChannelPoints
{
   public class ChannelPointRedemptionManager
   {
      public ChannelPointRedemptionManager(GIFBot.GIFBot bot)
      {
         Bot = bot;
      }

      public async Task InitializeEventSub(bool disconnectPrior = false)
      {
         try
         {
            if (mEventSubClient != null && disconnectPrior)
            {
               await mEventSubClient.DisconnectAsync();
               mEventSubClient = null;
            }

            mEventSubClient = new EventSubWebsocketClient();
            mEventSubClient.WebsocketConnected += OnWebsocketConnected;
            mEventSubClient.WebsocketDisconnected += OnWebsocketDisconnected;
            mEventSubClient.ErrorOccurred += OnErrorOccurred;
            mEventSubClient.WebsocketReconnected += OnWebsocketReconnected;
            mEventSubClient.ChannelPointsCustomRewardRedemptionAdd += OnChannelPointsRewardRedeemed;

            if (!String.IsNullOrEmpty(Bot.BotSettings.StreamerOauthToken))
            {
               await mEventSubClient.ConnectAsync();
            }
         }
         catch (Exception /*ex*/)
         {
            _ = Bot.SendLogMessage("Unable to start the EventSub websocket client.");
         }
      }

      private async Task OnWebsocketConnected(object sender, WebsocketConnectedArgs e)
      {
         if (!e.IsRequestedReconnect)
         {
            await CreateChannelPointRedemptionSubscription();
         }
      }

      private async Task OnWebsocketDisconnected(object sender, WebsocketDisconnectedArgs e)
      {
         if (!String.IsNullOrEmpty(Bot.BotSettings.StreamerOauthToken))
         {
            _ = Bot.SendLogMessage("EventSub client disconnected. Reconnecting...");
            await Task.Delay(1000);
            await InitializeEventSub();
         }
      }

      private Task OnErrorOccurred(object sender, ErrorOccuredArgs e)
      {
         if (!String.IsNullOrEmpty(Bot.BotSettings.StreamerOauthToken))
         {
            _ = Bot.SendLogMessage($"EventSub error: {e.Exception?.Message ?? e.Message}. Do you have the wrong oauth scopes?");
         }
         return Task.CompletedTask;
      }

      private Task OnWebsocketReconnected(object sender, WebsocketReconnectedArgs e)
      {
         _ = Bot.SendLogMessage("EventSub client reconnected successfully.");
         return Task.CompletedTask;
      }

      private async Task CreateChannelPointRedemptionSubscription()
      {
         try
         {
            if (Bot.ChannelId == 0 || String.IsNullOrEmpty(Bot.BotSettings.StreamerOauthToken))
            {
               _ = Bot.SendLogMessage("EventSub: Cannot create subscription - missing channel ID or OAuth token.");
               return;
            }

            _ = Bot.SendLogMessage("EventSub client connected! Creating channel point redemption subscription.");

            var requestBody = new
            {
               type = "channel.channel_points_custom_reward_redemption.add",
               version = "1",
               condition = new { broadcaster_user_id = Bot.ChannelId.ToString() },
               transport = new { method = "websocket", session_id = mEventSubClient.SessionId }
            };

            var json = JsonConvert.SerializeObject(requestBody);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.twitch.tv/helix/eventsub/subscriptions");
            request.Headers.Add("Authorization", $"Bearer {Bot.BotSettings.StreamerOauthToken.Trim()}");
            request.Headers.Add("Client-ID", Common.skTwitchClientId);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var httpClient = Bot.HttpClientFactory.CreateClient(Common.skHttpClientName);
            var response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
               _ = Bot.SendLogMessage("EventSub: Successfully subscribed to channel point redemptions.");
            }
            else
            {
               var responseBody = await response.Content.ReadAsStringAsync();
               _ = Bot.SendLogMessage($"EventSub: Failed to create subscription. Status: {response.StatusCode}. Response: {responseBody}");
            }
         }
         catch (Exception ex)
         {
            _ = Bot.SendLogMessage($"EventSub: Error creating subscription: {ex.Message}");
         }
      }

      private Task OnChannelPointsRewardRedeemed(object sender, ChannelPointsCustomRewardRedemptionArgs e)
      {
         _ = Bot.SendLogMessage($"REWARD DETAILS: {JsonConvert.SerializeObject(e)}");

         if (e.Payload?.Event == null)
         {
            // Exit early. Invalid redemption information.
            return Task.CompletedTask;
         }

         var redemption = e.Payload.Event;

         Guid redeemedRewardId = new Guid(redemption.Id);
         string rewardTitle = redemption.Reward.Title;
         int rewardCost = redemption.Reward.Cost;
         string userInput = redemption.UserInput;
         string userDisplayName = redemption.UserName;

         if (!mProcessedRewardIds.Contains(redeemedRewardId))
         {            
            _ = Bot.SendLogMessage($"EventSub: {rewardTitle} redeemed!");

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
               _ = Bot.SendLogMessage($"Sticker placed for channel points spent by [{userDisplayName}].");
               _ = Bot.StickersManager.PlaceASticker(rewardTitle);
            }

            // Find and queue any of the animations flagged for alert mode.
            // Yes, allowing the users to have multiple.
            IEnumerable<AnimationData> cpAlertAnims = Bot.AnimationManager.GetAllAnimations(GIFBot.AnimationManager.FetchType.EnabledOnly).Where(a => a.ChannelPointRedemptionType == ChannelPointRedemptionTriggerType.All);
            if (cpAlertAnims.Any())
            {
               foreach (var alertAnim in cpAlertAnims)
               {
                  Bot.AnimationManager.ForceQueueAnimation(alertAnim, userDisplayName, String.Empty);
               }
            }

            if (!string.IsNullOrEmpty(userInput))
            {
               // See if there's just an animation where the command matches the input text.
               AnimationData cpAnim = Bot.AnimationManager.GetAllAnimations(GIFBot.AnimationManager.FetchType.EnabledOnly).Where(a => a.ChannelPointRedemptionType == ChannelPointRedemptionTriggerType.MessageText && userInput.Contains(a.Command, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
               if (cpAnim != null)
               {
                  Bot.AnimationManager.ForceQueueAnimation(cpAnim, userDisplayName, String.Empty);
               }
            }

            // Look for !animationroulette
            if (rewardTitle.Contains("!animationroulette", StringComparison.OrdinalIgnoreCase) && !Bot.BotSettings.AnimationRouletteChatEnabled)
            {
               Bot.AnimationManager.PlayRandomAnimation(userDisplayName);
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
               Bot.GiveawayManager.HandleChannelPointEntry(userDisplayName);
            }

            // See if there is a command in the title of the reward and if the reward cost matches the cost on the animation.
            AnimationData rewardTitleAnim = Bot.AnimationManager.GetAllAnimations(GIFBot.AnimationManager.FetchType.EnabledOnly).Where(a => a.ChannelPointRedemptionType == ChannelPointRedemptionTriggerType.PointsUsed && rewardTitle.Contains(a.Command, StringComparison.OrdinalIgnoreCase) && a.ChannelPointsRequired == rewardCost).FirstOrDefault();
            if (rewardTitleAnim != null)
            {
               Bot.AnimationManager.ForceQueueAnimation(rewardTitleAnim, userDisplayName, String.Empty);
            }
            else
            {
               // Otherwise check to see if an animation should play based on the cost alone.
               AnimationData cpPointCostAnim = Bot.AnimationManager.GetAllAnimations(GIFBot.AnimationManager.FetchType.EnabledOnly).Where(a => a.ChannelPointRedemptionType == ChannelPointRedemptionTriggerType.PointsUsed && a.ChannelPointsRequired == rewardCost).FirstOrDefault();
               if (cpPointCostAnim != null)
               {
                  Bot.AnimationManager.ForceQueueAnimation(cpPointCostAnim, userDisplayName, String.Empty);
               }
            }
         }

         return Task.CompletedTask;
      }

      public GIFBot.GIFBot Bot { get; private set; }

      /// <summary>
      /// The EventSub Websocket Client from TwitchLib.
      /// </summary>
      private EventSubWebsocketClient mEventSubClient;

      private Queue<Guid> mProcessedRewardIds = new Queue<Guid>();

      private const int skMaxRewardIdsToTrack = 200;
   }
}
