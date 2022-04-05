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
            mTwitchPubSub.OnRewardRedeemed += TwitchPubSub_OnRewardsRedeemed;

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

            mTwitchPubSub.ListenToRewards(Bot.ChannelId.ToString());
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

      private void TwitchPubSub_OnRewardsRedeemed(object sender, TwitchLib.PubSub.Events.OnRewardRedeemedArgs e)
      {
         _ = Bot.SendLogMessage($"REWARD DETAILS: {JsonConvert.SerializeObject(e)}");

         if (!mProcessedRewardIds.Contains(e.RedemptionId))
         {
            _ = Bot.SendLogMessage($"PubSub: {e.RewardTitle} redeemed!");

            mProcessedRewardIds.Enqueue(e.RedemptionId);
            if (mProcessedRewardIds.Count > skMaxRewardIdsToTrack)
            {
               mProcessedRewardIds.Dequeue();
            }

            // Place a sticker, if applicable.
            if (Bot.StickersManager != null &&
                Bot.StickersManager.Data != null &&
                Bot.StickersManager.Data.Enabled &&
                ((Bot.StickersManager.Data.IncludeChannelPoints && e.RewardCost >= Bot.StickersManager.Data.ChannelPointsMinimum) ||
                 (Bot.StickersManager.Data.CanUseCommand && e.RewardTitle.Contains(Bot.StickersManager.Data.Command, StringComparison.OrdinalIgnoreCase))))
            {
               _ = Bot.SendLogMessage($"Sticker placed for channel points spent by [{e.DisplayName}].");
               _ = Bot.StickersManager.PlaceASticker(e.RewardTitle);
            }

            // Find and queue any of the animations flagged for alert mode.
            // Yes, allowing the users to have multiple.
            IEnumerable<AnimationData> cpAlertAnims = Bot.AnimationManager.GetAllAnimations(GIFBot.AnimationManager.FetchType.EnabledOnly).Where(a => a.ChannelPointRedemptionType == ChannelPointRedemptionTriggerType.All);
            if (cpAlertAnims.Any())
            {
               foreach (var alertAnim in cpAlertAnims)
               {
                  Bot.AnimationManager.ForceQueueAnimation(alertAnim, e.DisplayName, String.Empty);
               }
            }

            if (!string.IsNullOrEmpty(e.Message))
            {
               // See if there's just an animation where the command matches the input text.
               AnimationData cpAnim = Bot.AnimationManager.GetAllAnimations(GIFBot.AnimationManager.FetchType.EnabledOnly).Where(a => a.ChannelPointRedemptionType == ChannelPointRedemptionTriggerType.MessageText && e.Message.Contains(a.Command, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
               if (cpAnim != null)
               {
                  Bot.AnimationManager.ForceQueueAnimation(cpAnim, e.DisplayName, String.Empty);
               }
            }

            // Look for !animationroulette
            if (e.RewardTitle.Contains("!animationroulette", StringComparison.OrdinalIgnoreCase) && !Bot.BotSettings.AnimationRouletteChatEnabled)
            {
               Bot.AnimationManager.PlayRandomAnimation(e.DisplayName);
            }

            // Look for a valid regurgitator package
            RegurgitatorPackage qualifyingPackage = null;
            lock (Bot.RegurgitatorManager.PackagesMutex)
            {
               qualifyingPackage = Bot.RegurgitatorManager.Data.Packages.FirstOrDefault(p => e.RewardTitle.Contains(p.Settings.Command, StringComparison.OrdinalIgnoreCase));
            }

            if (qualifyingPackage != null && qualifyingPackage.Settings.Enabled && !qualifyingPackage.Settings.PlayOnTimer)
            {
               Bot.RegurgitatorManager.Play(qualifyingPackage);
            }

            // Look for Thanos
            if (Bot.SnapperManager.Enabled)
            {
               foreach (var command in Bot.SnapperManager.EnabledCommands)
               {
                  if (e.RewardTitle.Contains(command.Command, StringComparison.OrdinalIgnoreCase) &&
                      command.RedemptionType == SnapperRedemptionType.ChannelPoints &&
                      e.RewardCost == command.Cost)
                  {
                     switch (command.BehaviorType)
                     {
                     case SnapperBehaviorType.SpecificViewer:
                        // This is a specific viewer, so we need to send the reward input as the name of the viewer to snap.
                        _ = Bot.SnapperManager.Snap(command, e.Message, e.DisplayName);
                        break;

                     case SnapperBehaviorType.Revenge:
                     case SnapperBehaviorType.Thanos:
                     case SnapperBehaviorType.Self:
                        _ = Bot.SnapperManager.Snap(command, String.Empty, e.DisplayName);
                        break;
                     }
                  }
               }
            }

            // Look for Backdrops
            if (Bot.BackdropManager?.Data?.Enabled == true &&
                Bot.BackdropManager.Data.RedemptionType == CostRedemptionType.ChannelPoints &&
                e.RewardTitle.Contains(Bot.BackdropManager.Data.Command) &&
                e.RewardCost == Bot.BackdropManager.Data.Cost)
            {
               Bot.BackdropManager.HandleBackdropEvent(e.RewardTitle);
            }

            // Look for Countdown Timer
            if (Bot.CountdownTimerManager?.Data?.Enabled == true &&
                Bot.CountdownTimerManager.Data.Actions.Where(a => a.Enabled && a.RedemptionType == CostRedemptionType.ChannelPoints).Any())
            {
               Bot.CountdownTimerManager.HandleTimerEvent(e.RewardCost, CostRedemptionType.ChannelPoints);
            }

            // Look for an active giveaway.
            if (Bot?.GiveawayManager?.Data?.IsOpenForEntries == true &&
                Bot?.GiveawayManager?.Data?.EntryBehavior == GiveawayData.GiveawayEntryBehaviorType.ChannelPoints &&
                Bot?.GiveawayManager?.Data?.ChannelPointRewardId == e.RewardId)
            {
               Bot.GiveawayManager.HandleChannelPointEntry(e.DisplayName);
            }

            // See if there is a command in the title of the reward and if the reward cost matches the cost on the animation.
            AnimationData rewardTitleAnim = Bot.AnimationManager.GetAllAnimations(GIFBot.AnimationManager.FetchType.EnabledOnly).Where(a => a.ChannelPointRedemptionType == ChannelPointRedemptionTriggerType.PointsUsed && e.RewardTitle.Contains(a.Command, StringComparison.OrdinalIgnoreCase) && a.ChannelPointsRequired == e.RewardCost).FirstOrDefault();
            if (rewardTitleAnim != null)
            {
               Bot.AnimationManager.ForceQueueAnimation(rewardTitleAnim, e.DisplayName, String.Empty);
            }
            else
            {
               // Otherwise check to see if an animation should play based on the cost alone.
               AnimationData cpPointCostAnim = Bot.AnimationManager.GetAllAnimations(GIFBot.AnimationManager.FetchType.EnabledOnly).Where(a => a.ChannelPointRedemptionType == ChannelPointRedemptionTriggerType.PointsUsed && a.ChannelPointsRequired == e.RewardCost).FirstOrDefault();
               if (cpPointCostAnim != null)
               {
                  Bot.AnimationManager.ForceQueueAnimation(cpPointCostAnim, e.DisplayName, String.Empty);
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
