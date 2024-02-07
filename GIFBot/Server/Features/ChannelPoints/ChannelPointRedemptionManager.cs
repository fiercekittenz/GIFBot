using GIFBot.Shared;
using GIFBot.Shared.Models.Features;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchLib.Api.Helix;
using TwitchLib.PubSub;
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

      public void InitializePubSub(bool disconnectPrior = false)
      {
         try
         {
            if (mTwitchPubSub != null && disconnectPrior)
            {
               mTwitchPubSub.Disconnect();
               mTwitchPubSub = null;
            }

            mTwitchPubSub = new TwitchPubSub();
            mTwitchPubSub.OnPubSubServiceConnected += TwitchPubSub_OnPubSubServiceConnected;
            mTwitchPubSub.OnPubSubServiceClosed += TwitchPubSub_OnPubSubServiceClosed;
            mTwitchPubSub.OnPubSubServiceError += TwitchPubSub_OnPubSubServiceError;
            mTwitchPubSub.OnChannelPointsRewardRedeemed += TwitchPubSub_OnChannelPointsRewardRedeemed;

            if (!String.IsNullOrEmpty(Bot.BotSettings.StreamerOauthToken))
            {
               mTwitchPubSub.Connect();
            }
         }
         catch (Exception /*ex*/)
         {
            _ = Bot.SendLogMessage("Unable to start the TwitchPubSub client.");
         }
      }

      private void TwitchPubSub_OnPubSubServiceConnected(object sender, EventArgs e)
      {
         if (!String.IsNullOrEmpty(Bot.BotSettings.StreamerOauthToken) && Bot.ChannelId != 0)
         {
            _ = Bot.SendLogMessage("PubSub client connected! Sending topics.");

            mTwitchPubSub.ListenToChannelPoints(Bot.ChannelId.ToString());
            mTwitchPubSub.SendTopics(Bot.BotSettings.StreamerOauthToken);
         }
      }

      private void TwitchPubSub_OnPubSubServiceClosed(object sender, EventArgs e)
      {
         if (!String.IsNullOrEmpty(Bot.BotSettings.StreamerOauthToken))
         {
            _ = Bot.SendLogMessage("PubSub client disconnected. Reconnecting...");
            InitializePubSub();
         }
      }

      private void TwitchPubSub_OnPubSubServiceError(object sender, TwitchLib.PubSub.Events.OnPubSubServiceErrorArgs e)
      {
         if (!String.IsNullOrEmpty(Bot.BotSettings.StreamerOauthToken))
         {
            _ = Bot.SendLogMessage($"PubSub error: {e.Exception.Message}. Do you have the wrong oauth scopes?");
            InitializePubSub();
         }
      }

      private void TwitchPubSub_OnChannelPointsRewardRedeemed(object sender, TwitchLib.PubSub.Events.OnChannelPointsRewardRedeemedArgs e)
      {
         _ = Bot.SendLogMessage($"REWARD DETAILS: {JsonConvert.SerializeObject(e)}");

         if (e.RewardRedeemed == null ||
             e.RewardRedeemed.Redemption == null)
         {
            // Exit early. Invalid redemption information.
            return;
         }

         Guid redeemedRewardId = new Guid (e.RewardRedeemed.Redemption.Id);
         string rewardTitle = e.RewardRedeemed.Redemption.Reward.Title;
         int rewardCost = e.RewardRedeemed.Redemption.Reward.Cost;
         string userInput = e.RewardRedeemed.Redemption.UserInput;

         if (!mProcessedRewardIds.Contains(redeemedRewardId))
         {            
            _ = Bot.SendLogMessage($"PubSub: {rewardTitle} redeemed!");

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
               _ = Bot.SendLogMessage($"Sticker placed for channel points spent by [{e.RewardRedeemed.Redemption.User.DisplayName}].");
               _ = Bot.StickersManager.PlaceASticker(rewardTitle);
            }

            // Find and queue any of the animations flagged for alert mode.
            // Yes, allowing the users to have multiple.
            IEnumerable<AnimationData> cpAlertAnims = Bot.AnimationManager.GetAllAnimations(GIFBot.AnimationManager.FetchType.EnabledOnly).Where(a => a.ChannelPointRedemptionType == ChannelPointRedemptionTriggerType.All);
            if (cpAlertAnims.Any())
            {
               foreach (var alertAnim in cpAlertAnims)
               {
                  Bot.AnimationManager.ForceQueueAnimation(alertAnim, e.RewardRedeemed.Redemption.User.DisplayName, String.Empty);
               }
            }

            if (!string.IsNullOrEmpty(userInput))
            {
               // See if there's just an animation where the command matches the input text.
               AnimationData cpAnim = Bot.AnimationManager.GetAllAnimations(GIFBot.AnimationManager.FetchType.EnabledOnly).Where(a => a.ChannelPointRedemptionType == ChannelPointRedemptionTriggerType.MessageText && userInput.Contains(a.Command, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
               if (cpAnim != null)
               {
                  Bot.AnimationManager.ForceQueueAnimation(cpAnim, e.RewardRedeemed.Redemption.User.DisplayName, String.Empty);
               }
            }

            // Look for !animationroulette
            if (rewardTitle.Contains("!animationroulette", StringComparison.OrdinalIgnoreCase) && !Bot.BotSettings.AnimationRouletteChatEnabled)
            {
               Bot.AnimationManager.PlayRandomAnimation(e.RewardRedeemed.Redemption.User.DisplayName);
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
               Bot.GiveawayManager.HandleChannelPointEntry(e.RewardRedeemed.Redemption.User.DisplayName);
            }

            // See if there is a command in the title of the reward and if the reward cost matches the cost on the animation.
            AnimationData rewardTitleAnim = Bot.AnimationManager.GetAllAnimations(GIFBot.AnimationManager.FetchType.EnabledOnly).Where(a => a.ChannelPointRedemptionType == ChannelPointRedemptionTriggerType.PointsUsed && rewardTitle.Contains(a.Command, StringComparison.OrdinalIgnoreCase) && a.ChannelPointsRequired == rewardCost).FirstOrDefault();
            if (rewardTitleAnim != null)
            {
               Bot.AnimationManager.ForceQueueAnimation(rewardTitleAnim, e.RewardRedeemed.Redemption.User.DisplayName, String.Empty);
            }
            else
            {
               // Otherwise check to see if an animation should play based on the cost alone.
               AnimationData cpPointCostAnim = Bot.AnimationManager.GetAllAnimations(GIFBot.AnimationManager.FetchType.EnabledOnly).Where(a => a.ChannelPointRedemptionType == ChannelPointRedemptionTriggerType.PointsUsed && a.ChannelPointsRequired == rewardCost).FirstOrDefault();
               if (cpPointCostAnim != null)
               {
                  Bot.AnimationManager.ForceQueueAnimation(cpPointCostAnim, e.RewardRedeemed.Redemption.User.DisplayName, String.Empty);
               }
            }
         }
      }

      public GIFBot.GIFBot Bot { get; private set; }

      /// <summary>
      /// The PubSub Client from TwitchLib.
      /// </summary>
      private TwitchPubSub mTwitchPubSub;

      private Queue<Guid> mProcessedRewardIds = new Queue<Guid>();

      private const int skMaxRewardIdsToTrack = 200;
   }
}
