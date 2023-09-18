using GIFBot.Client.Pages;
using GIFBot.Client.Pages.Features;
using GIFBot.Server.Features.Backdrop;
using GIFBot.Server.Features.ChannelPoints;
using GIFBot.Server.Features.CountdownTimer;
using GIFBot.Server.Features.Giveaway;
using GIFBot.Server.Features.GoalBar;
using GIFBot.Server.Features.Greeter;
using GIFBot.Server.Features.Regurgitator;
using GIFBot.Server.Features.Snapper;
using GIFBot.Server.Features.Stickers;
using GIFBot.Server.Features.StreamElements;
using GIFBot.Server.Features.Tiltify;
using GIFBot.Server.Hubs;
using GIFBot.Server.Interfaces;
using GIFBot.Server.Models;
using GIFBot.Shared;
using GIFBot.Shared.Models.Animation;
using GIFBot.Shared.Models.Features;
using GIFBot.Shared.Models.GIFBot;
using GIFBot.Shared.Models.Tiltify;
using GIFBot.Shared.Models.Twitch;
using GIFBot.Shared.Utility;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Web;
using TwitchLib.Api;
using TwitchLib.Api.Core;
using TwitchLib.Api.Core.Models.Undocumented.Chatters;
using TwitchLib.Api.Services;
using TwitchLib.Client;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;
using TwitchLib.Client.Extensions;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Models;
using TwitchLib.PubSub;
using static GIFBot.Shared.AnimationEnums;
using static GIFBot.Shared.Utility.Enumerations;

namespace GIFBot.Server.GIFBot
{
   public class GIFBot
   {
      #region Public Methods

      /// <summary>
      /// Constructor
      /// </summary>
      public GIFBot(IConfiguration configuration,
                    IHubContext<GIFBotHub> gifbotHub,
                    IHttpClientFactory httpClientFactory)
      {
         Configuration = configuration;
         GIFBotHub = gifbotHub;
         HttpClientFactory = httpClientFactory;

         Start();
      }

      /// <summary>
      /// Sends a log message to the client.
      /// </summary>
      public async Task SendLogMessage(string logMessage)
      {
         string formattedMessage = $"[{DateTime.Now.ToLongTimeString()}] {logMessage}";
         await GIFBotHub.Clients.All.SendAsync("LogMessage", formattedMessage);

         lock (LogMutex)
         {
            LogMessages.Add(formattedMessage);
         }

         // TODO: Log to file as well.

         // Truncate the list of logs as needed.
         lock (LogMutex)
         {
            if (LogMessages.Count > kMaxLogLines)
            {
               LogMessages.RemoveAt(0);
            }
         }
      }

      /// <summary>
      /// Sends all log entries that have been persisted temporarily. Necessary for when the user leaves and comes
      /// back to the dashboard.
      /// </summary>
      public async Task SendAllLogMessages()
      {
         List<string> logMessagesToSend = new List<string>();
         lock (LogMutex)
         {
            logMessagesToSend = new List<string>(LogMessages);
         }

         foreach (var logMessage in logMessagesToSend)
         {
            await GIFBotHub.Clients.All.SendAsync("LogMessage", logMessage);
         }
      }

      /// <summary>
      /// Sends a list of animations to chat.
      /// </summary>
      public void SendAnimationsListToChat()
      {
         IEnumerable<AnimationData> animations = AnimationManager.GetAllAnimations(AnimationManager.FetchType.EnabledOnly).Where(a => !a.HideFromChatOutput && a.CanBeTriggeredByChatCommand(BotSettings, String.Empty))?.OrderBy(a => a.Command);

         // Spit them out to the channel directly, but with a timer in between
         // messages to avoid globaling the bot.
         List<string> animMessages = new List<string>();

         StringBuilder sb = new StringBuilder();
         sb.Append("Available GIFBot interactives: ");

         var notRestrictedAnimations = animations.Where(a => a.Access == AccessType.Anyone);
         foreach (AnimationData animation in notRestrictedAnimations)
         {
            sb.Append(animation.Command);
            sb.Append(" ");

            if (sb.Length > skMaxCharactersPerAnimOutput)
            {
               animMessages.Add(sb.ToString());
               sb.Clear();
            }
         }

         if (notRestrictedAnimations.Count() > 0)
         {
            animMessages.Add(sb.ToString());
         }
         sb.Clear();

         var followerOnlyAnimations = animations.Where(g => g.Access == AccessType.Follower);
         sb.Append("Follower Only: ");
         foreach (AnimationData animation in followerOnlyAnimations)
         {
            sb.Append(animation.Command);
            sb.Append(" ");

            if (sb.Length > skMaxCharactersPerAnimOutput)
            {
               animMessages.Add(sb.ToString());
               sb.Clear();
            }
         }

         if (followerOnlyAnimations.Count() > 0)
         {
            animMessages.Add(sb.ToString());
         }
         sb.Clear();

         var subOnlyAnimations = animations.Where(g => g.Access == AccessType.Subscriber);
         sb.Append("Subscriber Only: ");
         foreach (AnimationData animation in subOnlyAnimations)
         {
            sb.Append(animation.Command);
            sb.Append(" ");

            if (sb.Length > skMaxCharactersPerAnimOutput)
            {
               animMessages.Add(sb.ToString());
               sb.Clear();
            }
         }

         if (subOnlyAnimations.Count() > 0)
         {
            animMessages.Add(sb.ToString());
         }
         sb.Clear();

         // Iterate over the messages and send them with pagination details
         // Print the first one so it doesn't have an odd delay. Then print
         // the rest of them in the timer.
         if (animMessages.Any())
         {
            int current = 0;
            int maxPages = animMessages.Count;
            SendChatMessage(animMessages.ElementAt<string>(current));
            ++current;

            if (animMessages.Count > 1)
            {
               System.Timers.Timer sendTimer = new System.Timers.Timer(1500);
               sendTimer.Enabled = true;
               sendTimer.Elapsed += delegate (object sender, ElapsedEventArgs e) {
                  SendChatMessage($"{animMessages.ElementAt<string>(current)}");
                  ++current;

                  if (current >= maxPages)
                  {
                     sendTimer.Stop();
                  }
               };

               sendTimer.Start();
            }
         }
      }

      /// <summary>
      /// Uses SignalR to notify the connected clients that the animation should play.
      /// </summary>
      public async Task SendStartAnimationMessage(AnimationRequest animationRequest)
      {
         if (!String.IsNullOrEmpty(animationRequest.Triggerer))
         {
            await SendLogMessage($"Start animation - viewer [{animationRequest.Triggerer}] triggered [{animationRequest.AnimationData.Command}].");
         }

         if (!String.IsNullOrEmpty(animationRequest.PrePlayText))
         {
            string prePlayFormatted = animationRequest.PrePlayText;
            if (!String.IsNullOrEmpty(animationRequest.Triggerer))
            {
               prePlayFormatted = prePlayFormatted.Replace("$user", animationRequest.Triggerer);
            }

            SendChatMessage(prePlayFormatted);
         }

         await GIFBotHub.Clients.All.SendAsync("PlayAnimation", JsonConvert.SerializeObject(animationRequest));

         if (!animationRequest.ManuallyTriggeredByStreamer)
         {
            animationRequest.AnimationData.SetOnCooldown();
            LastTimeAnimationTriggered = DateTime.Now;
         }
      }

      /// <summary>
      /// Starts the animation timer. The client is single-threaded, so this cannot be done there.
      /// Once the file has finished loading and is displayed to the user, the timer can be started on the
      /// server as notified through the animation hub.
      /// </summary>
      public async Task StartAnimationTimer(AnimationLayer layer, AnimationRequest animationRequest, int durationMilliseconds)
      {
         await Task.Delay(durationMilliseconds);

         AnimationProcessor processor = null;
         switch (layer)
         {
         case AnimationLayer.Primary:
            processor = AnimationManager.PrimaryProcessor;
            break;
         }

         if (processor != null)
         {
            await processor.DeactivateAnimation(animationRequest.AnimationData.Command, GIFBotHub.Clients, animationRequest.PostPlayText, animationRequest.Triggerer, animationRequest.Amount);
            OnAnimationCompleted?.Invoke(this, new AnimationCompletedEventArgs(animationRequest.AnimationData.Id));
         }
      }

      /// <summary>
      /// Stops the animation in its tracks.
      /// </summary>
      public async Task ForceStopAnimation(AnimationLayer layer, string command)
      {
         AnimationProcessor processor = null;
         switch (layer)
         {
         case AnimationLayer.Primary:
            processor = AnimationManager.PrimaryProcessor;
            break;
         }

         if (processor != null)
         {
            await processor.DeactivateAnimation(command, GIFBotHub.Clients);
         }
      }

      /// <summary>
      /// Returns if the bot is under a global cooldown or not. This is only used for commands coming from chat.
      /// </summary>
      public bool UnderGlobalCooldown()
      {
         double secondsDiff = DateTime.Now.Subtract(LastTimeAnimationTriggered).TotalSeconds;
         if (secondsDiff >= BotSettings.GlobalCooldownInSeconds)
         {
            return false;
         }

         return true;
      }

      /// <summary>
      /// Generic handling of tip events regardless of the platform used to deliver the tip.
      /// </summary>
      public void ProcessTip(double amount, string tipper, string formattedAmount, string message)
      {
         //
         // Handle any animations that qualify
         //

         IEnumerable<AnimationData> validAnimations = AnimationManager.GetAllAnimations(AnimationManager.FetchType.EnabledOnly).Where(a => !a.Disabled && a.IsStreamlabsTipTrigger && (a.StreamlabsTipRequirement == 0 || a.StreamlabsTipRequirement == amount));
         List<AnimationData> sortedAnimations = validAnimations.OrderBy(a => a.StreamlabsTipRequirement).ToList();
         foreach (var animation in sortedAnimations)
         {
            _ = SendLogMessage("Triggering [" + animation.Command + "] by user [" + tipper + "] with a tip of [" + formattedAmount + "].");
            AnimationManager.ForceQueueAnimation(animation, tipper, formattedAmount);
         }

         //
         // Handle any other commands that qualify.
         //

         if (RegurgitatorManager != null)
         {
            RegurgitatorPackage qualifyingPackage = null;            
            lock (RegurgitatorManager.PackagesMutex)
            {
               RegurgitatorManager.Data.Packages.FirstOrDefault(p => p.Settings.Enabled &&
                                                                     !p.Settings.PlayOnTimer &&
                                                                     p.Settings.IsStreamlabsTipTrigger &&
                                                                     (p.Settings.StreamlabsTipRequirement == 0 || p.Settings.StreamlabsTipRequirement == amount));
            }

            if (qualifyingPackage != null)
            {
               RegurgitatorManager.Play(qualifyingPackage);
            }
         }

         if (StickersManager != null &&
             StickersManager.Data != null &&
             StickersManager.Data.Enabled)
         {
            bool shouldLog = false;

            if (StickersManager.Data.IncludeTips && amount >= StickersManager.Data.TipMinimum)
            {
               shouldLog = true;
               _ = StickersManager.PlaceASticker();
            }
            else
            {
               // See if there is a specific sticker that qualifies for the amount.
               foreach (var category in StickersManager.Data.Categories)
               {
                  StickerEntryData sticker = category.Entries.FirstOrDefault(s => !s.AllowRandomPlacement && s.IncludeTips && s.TipAmount == amount);
                  if (sticker != null)
                  {
                     shouldLog = true;
                     _ = StickersManager.PlaceASticker(sticker);
                  }
               }
            }

            if (shouldLog)
            {
               _ = SendLogMessage($"Sticker placed for Streamlabs Tip of [{formattedAmount}] from [{tipper}].");
            }
         }

         if (SnapperManager != null && SnapperManager.Enabled)
         {
            foreach (var command in SnapperManager.EnabledCommands)
            {
               if (command.RedemptionType == SnapperRedemptionType.Tip && (int)(Math.Floor(amount)) == command.Cost)
               {
                  switch (command.BehaviorType)
                  {
                  case SnapperBehaviorType.SpecificViewer:
                     _ = SnapperManager.Snap(command, message, tipper);
                     break;

                  case SnapperBehaviorType.Revenge:
                  case SnapperBehaviorType.Thanos:
                  case SnapperBehaviorType.Self:
                     _ = SnapperManager.Snap(command, String.Empty, tipper);
                     break;
                  }
               }
            }
         }

         // Look for Backdrops
         if (BackdropManager?.Data?.Enabled == true &&
             BackdropManager.Data.RedemptionType == CostRedemptionType.Tip &&
             (int)(Math.Floor(amount)) == BackdropManager.Data.Cost)
         {
            BackdropManager.HandleBackdropEvent(String.Empty);
         }

         // Look for Countdown Timer
         if (CountdownTimerManager?.Data?.Enabled == true &&
             CountdownTimerManager.Data.Actions.Where(a => a.Enabled && a.RedemptionType == CostRedemptionType.Tip).Any())
         {
            CountdownTimerManager.HandleTimerEvent(amount, CostRedemptionType.Tip);
         }

         // Add to the goal, when applicable
         GoalBarManager.ApplyValue(tipper, amount, GoalBarManager.ApplySource.Tip);
      }

      #endregion

      #region Events

      /// <summary>
      /// Event fired when an animation has fully completed playing.
      /// </summary>

      public event EventHandler<AnimationCompletedEventArgs> OnAnimationCompleted;

      public class AnimationCompletedEventArgs : EventArgs
      {
         public AnimationCompletedEventArgs(Guid animationId)
         {
            AnimationId = animationId;
         }

         public Guid AnimationId { get; set; }
      }

      #endregion

      #region Settings

      public bool LoadSettings()
      {
         mBotSettings = new BotSettings();

         string settingsDir = Path.Combine(System.Environment.CurrentDirectory, kSettingsDirectoryName);
         if (!String.IsNullOrEmpty(Configuration["BotSettingsFile"]) && File.Exists(Path.Combine(settingsDir, Configuration["BotSettingsFile"])))
         {
            string fileData = File.ReadAllText(Path.Combine(settingsDir, Configuration["BotSettingsFile"]));
            mBotSettings = JsonConvert.DeserializeObject<BotSettings>(fileData);
            BumpSettingsVersion();

            return true;
         }

         return false;
      }

      private void BumpSettingsVersion()
      {
         // User groups were changed to group user entries by a specialized type and not a primative type in version 1.0b.
         const int kUserGroupsChange = 2;

         if (mBotSettings.Version < kUserGroupsChange)
         {
            foreach (var group in mBotSettings.UserGroups)
            {
               foreach (string user in group.Users)
               {
                  group.UserEntries.Add(new UserEntry(user));
               }

               group.Users.Clear();
            }

            mBotSettings.Version = BotSettings.skCurrentBotSettingsVersion;
            SaveSettings();
         }
      }

      public void SaveSettings()
      {
         if (mBotSettings != null)
         {
            string settingsDir = Path.Combine(System.Environment.CurrentDirectory, kSettingsDirectoryName);
            Directory.CreateDirectory(settingsDir);

            var jsonData = JsonConvert.SerializeObject(mBotSettings);
            File.WriteAllText(Path.Combine(settingsDir, Configuration["BotSettingsFile"]), jsonData);

            _ = SendLogMessage("Bot settings saved.");
         }
      }

      #endregion

      #region User Group Helpers

      public Guid GetUserGroupIdByName(string groupName)
      {
         UserGroup group = BotSettings.UserGroups.FirstOrDefault(g => g.Name.Equals(groupName, StringComparison.OrdinalIgnoreCase));
         if (group != null)
         {
            return group.Id;
         }

         return Guid.Empty;
      }

      public string GetUserGroupListAsJson()
      {
         List<string> groupNames = new List<string>();
         foreach (var group in BotSettings.UserGroups)
         {
            groupNames.Add(group.Name);
         }

         return JsonConvert.SerializeObject(groupNames);
      }

      public string GetUserGroupNameById(Guid groupId)
      {
         UserGroup group = BotSettings.UserGroups.FirstOrDefault(g => g.Id == groupId);
         if (group != null)
         {
            return group.Name;
         }

         return String.Empty;
      }

      #endregion

      #region TwitchLib Client

      /// <summary>
      /// Initializes and establishes the connection with Twitch.
      /// </summary>
      public void ConnectToTwitch(bool channelHasChanged = false)
      {
         if (BotSettings == null ||
             String.IsNullOrEmpty(BotSettings.BotName.Trim()) ||
             String.IsNullOrEmpty(BotSettings.BotOauthToken.Trim()) ||
             String.IsNullOrEmpty(BotSettings.ChannelName.Trim()))
         {
            _ = SendLogMessage("Unable to connect to Twitch. No credentials found.");
            return;
         }

         _ = SendLogMessage("Connecting to Twitch...");

         if (mBotTwitchClient != null)
         {
            mBotTwitchClient.OnConnected -= TwitchClient_OnConnected;
            mBotTwitchClient.OnDisconnected -= TwitchClient_OnDisconnected;
            mBotTwitchClient.OnMessageReceived -= TwitchClient_OnMessageReceived;
            mBotTwitchClient.OnJoinedChannel -= TwitchClient_OnJoinedChannel;
            mBotTwitchClient.OnNewSubscriber -= TwitchClient_OnNewSubscriber;
            mBotTwitchClient.OnGiftedSubscription -= TwitchClient_OnGiftedSubscription;
            mBotTwitchClient.OnReSubscriber -= TwitchClient_OnReSubscriber;
            mBotTwitchClient.OnCommunitySubscription -= TwitchClient_OnCommunitySubscription;
            mBotTwitchClient.OnRaidNotification -= TwitchClient_OnRaidNotification;
            mBotTwitchClient.OnUnaccountedFor -= TwitchClient_OnUnaccountedFor;
            mBotTwitchClient.OnWhisperReceived -= TwitchClient_OnWhisperReceived;

            mBotTwitchClient.Disconnect();
         }

         if (mStreamerTwitchClient != null)
         {
            mStreamerTwitchClient.OnBeingHosted -= TwitchClient_OnBeingHosted;

            mStreamerTwitchClient.Disconnect();
         }

         // Initialize the Twitch API.
         mTwitchApi = new TwitchAPI();
         mTwitchApi.Settings.ClientId = Common.skTwitchClientId;
         mTwitchApi.Settings.AccessToken = BotSettings.BotOauthToken;

         // Setup the BOT Twitch Client
         {
            ConnectionCredentials connectionCredentials = new ConnectionCredentials(BotSettings.BotName.Trim(), BotSettings.BotOauthToken.Trim());

            var clientOptions = new ClientOptions {
               MessagesAllowedInPeriod = 750,
               ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            WebSocketClient customClient = new WebSocketClient(clientOptions);

            mBotTwitchClient = new TwitchClient(customClient);
            mBotTwitchClient.Initialize(connectionCredentials, BotSettings.ChannelName.Trim());

            mBotTwitchClient.OnConnected += TwitchClient_OnConnected;
            mBotTwitchClient.OnDisconnected += TwitchClient_OnDisconnected;
            mBotTwitchClient.OnMessageReceived += TwitchClient_OnMessageReceived;
            mBotTwitchClient.OnJoinedChannel += TwitchClient_OnJoinedChannel;
            mBotTwitchClient.OnNewSubscriber += TwitchClient_OnNewSubscriber;
            mBotTwitchClient.OnGiftedSubscription += TwitchClient_OnGiftedSubscription;
            mBotTwitchClient.OnReSubscriber += TwitchClient_OnReSubscriber;
            mBotTwitchClient.OnCommunitySubscription += TwitchClient_OnCommunitySubscription;
            mBotTwitchClient.OnRaidNotification += TwitchClient_OnRaidNotification;
            mBotTwitchClient.OnUnaccountedFor += TwitchClient_OnUnaccountedFor;
            mBotTwitchClient.OnWhisperReceived += TwitchClient_OnWhisperReceived;

            mBotTwitchClient.Connect();
         }

         // Setup the STREAMER Twitch Client
         if (!String.IsNullOrEmpty(BotSettings.StreamerOauthToken))
         {
            ConnectionCredentials connectionCredentials = new ConnectionCredentials(BotSettings.ChannelName.Trim(), BotSettings.StreamerOauthToken.Trim());

            var clientOptions = new ClientOptions {
               MessagesAllowedInPeriod = 750,
               ThrottlingPeriod = TimeSpan.FromSeconds(30)
            };
            WebSocketClient customClient = new WebSocketClient(clientOptions);

            mStreamerTwitchClient = new TwitchClient(customClient);
            mStreamerTwitchClient.Initialize(connectionCredentials, BotSettings.ChannelName.Trim());

            mStreamerTwitchClient.OnBeingHosted += TwitchClient_OnBeingHosted;

            mStreamerTwitchClient.Connect();
         }

         // Reset the user channel list and fetch the latest if the channel name has changed.
         if (channelHasChanged)
         {
            lock (UsersInChannelMutex)
            {
               UsersInChannel.Clear();                 
               UsersInChannel.UnionWith(TwitchEndpointHelpers.GetUserList(HttpClientFactory.CreateClient(Common.skHttpClientName), BotSettings.BotOauthToken, BotSettings.ChannelName.ToLower()));
            }
         }
      }

      // TODO: Put this back, but as my own service using a poll against the new endpoint for the channel to scrape for followers.
      //       The original endpoint was deprecated in September 2023.
      //private void FollowerService_OnNewFollowersDetected(object sender, TwitchLib.Api.Services.Events.FollowerService.OnNewFollowersDetectedArgs e)
      //{
      //   if (e.NewFollowers.Any())
      //   {
      //      var follower = e.NewFollowers.FirstOrDefault();
      //      if (follower != null)
      //      {
      //         if ((DateTime.Now.Subtract(follower.FollowedAt).TotalMinutes < 5))
      //         {
      //            _ = SendLogMessage($"New Follower: {follower.FromUserName}");

      //            AnimationData animation = null;                  
      //            var qualifyingAnimations = AnimationManager.GetAllAnimations(AnimationManager.FetchType.EnabledOnly).Where(a => a.IsFollowerAlert).ToList();            
      //            if (qualifyingAnimations.Count > 0)
      //            {
      //               int randomIndex = Common.sRandom.Next(qualifyingAnimations.Count);
      //               if (randomIndex < qualifyingAnimations.Count)
      //               {
      //                  animation = qualifyingAnimations[randomIndex];
      //               }

      //               if (animation != null)
      //               {
      //                  AnimationManager.ForceQueueAnimation(animation, follower.FromUserName, String.Empty);
      //               }
      //            }

      //            // Place a sticker, if applicable.
      //            if (StickersManager != null &&
      //                StickersManager.Data != null &&
      //                StickersManager.Data.Enabled &&
      //                StickersManager.Data.IncludeFollows)
      //            {
      //               _ = SendLogMessage($"Sticker placed for follow from [{follower.FromUserName}].");
      //               _ = StickersManager.PlaceASticker();
      //            }
      //         }
      //      }
      //   }
      //}

      private void TwitchClient_OnWhisperReceived(object sender, TwitchLib.Client.Events.OnWhisperReceivedArgs e)
      {
         if (e.WhisperMessage.Username.Equals(BotSettings.ChannelName, StringComparison.OrdinalIgnoreCase))
         {
            AnimationData animation = AnimationManager.GetAnimationFromMessage(e.WhisperMessage.Message);
            if (animation != null && BotSettings.CanTriggerAnimationsByWhisper)
            {
               AnimationManager.ForceQueueAnimation(animation, e.WhisperMessage.Username, "0");
            }
         }
      }

      private void TwitchClient_OnUnaccountedFor(object sender, TwitchLib.Client.Events.OnUnaccountedForArgs e)
      {
         string[] splits = e.RawIRC.Split(';');

         string type = String.Empty;
         int amount = 0;
         string chatId = String.Empty;
         string displayName = String.Empty;
         bool isSystemMessage = false;

         foreach (var split in splits)
         {
            string[] tokens = split.Split('=');

            if (tokens.Length > 1)
            {
               if (split.StartsWith("msg-param-trigger-type"))
               {
                  type = tokens[1];

                  //kittenztodo - this came in as unaccounted for - don't look for the "cheer" because we need to actually parse this message properly and not do a string contains.
                  /*
                  [8:23:52 PM] Unaccounted: @badge-info=subscriber/1;badges=subscriber/0,sub-gift-leader/1;color=#F5A84B;display-name=ForestGrump8;emotes=;flags=;
                  id=9c89cc96-4e23-4e94-a64f-b4c3dec0eaf4;login=forestgrump8;mod=0;msg-id=rewardgift;msg-param-domain=kpop_megacommerce;msg-param-selected-count=10;
                  msg-param-total-reward-count=10;
                  msg-param-trigger-amount=500              <-- THIS
                  ;msg-param-trigger-type=CHEER;            <-- AND THIS
                  room-id=63208102;subscriber=1;
                  system-msg=ForestGrump8's\sCheer\sshared\srewards\sto\s10\sothers\sin\sChat!;tmi-sent-ts=1603502632191;user-id=475782127;
                  user-type= :tmi.twitch.tv USERNOTICE #fiercekittenz*/
               }
               else if (split.StartsWith("msg-param-trigger-amount"))
               {
                  amount = Int32.Parse(tokens[1]);
               }
               else if (split.StartsWith("id"))
               {
                  chatId = tokens[1];
               }
               else if (split.StartsWith("display-name"))
               {
                  displayName = tokens[1];
               }
               else if (split.StartsWith("system-msg"))
               {
                  isSystemMessage = true;
               }
            }
         }

         if (!String.IsNullOrEmpty(type) && amount > 0 && !isSystemMessage)
         {
            if (type.Equals("CHEER", StringComparison.OrdinalIgnoreCase))
            {
               _ = SendLogMessage($"Unaccounted CHEER message: {e.RawIRC}");

               AnimationManager.HandleBitMessage(String.Empty, chatId, displayName, amount);
               GoalBarManager.ApplyValue(displayName, amount, GoalBarManager.ApplySource.Cheer);

               if (StickersManager.Data.IncludeBits && amount != 0 && amount >= StickersManager.Data.BitMinimum)
               {
                  _ = StickersManager.PlaceASticker();
               }
            }
         }
         else
         {
            _ = SendLogMessage($"Unaccounted: {e.RawIRC}");
         }
      }

      public void SendChatMessage(string message)
      {
         if (!String.IsNullOrEmpty(mBotSettings.ChannelName) && mBotTwitchClient != null && mBotTwitchClient.JoinedChannels.Count != 0)
         {
            mBotTwitchClient.SendMessage(mBotSettings.ChannelName.Trim(), message);
         }
      }

      private void TwitchClient_OnMessageReceived(object sender, TwitchLib.Client.Events.OnMessageReceivedArgs e)
      {
         QueueTwitchMessage(new TwitchMessage(this, e));
      }

      private void TwitchClient_OnDisconnected(object sender, TwitchLib.Communication.Events.OnDisconnectedEventArgs e)
      {
         _ = SendLogMessage("Disconnected from Twitch.");
      }

      private void TwitchClient_OnConnected(object sender, TwitchLib.Client.Events.OnConnectedArgs e)
      {
         _ = SendLogMessage("Connected to Twitch!");

         ChannelId = TwitchEndpointHelpers.GetChannelId(HttpClientFactory.CreateClient(Common.skHttpClientName), BotSettings.ChannelName, BotSettings.BotOauthToken, out string result);

         ChannelPointManager.InitializePubSub();
         CheckForHypeTrainEvent(false);

         if (mFollowerService != null)
         {
            mFollowerService.Stop();
            mFollowerService = null;
         }

         ApiSettings apiSettings = new ApiSettings() {
            AccessToken = BotSettings.BotOauthToken,
            ClientId = Common.skTwitchClientId,
            Scopes = new List<TwitchLib.Api.Core.Enums.AuthScopes>() { TwitchLib.Api.Core.Enums.AuthScopes.Any, TwitchLib.Api.Core.Enums.AuthScopes.Chat_Login, TwitchLib.Api.Core.Enums.AuthScopes.Channel_Read, TwitchLib.Api.Core.Enums.AuthScopes.Channel_Subscriptions }
         };
      }

      private void TwitchClient_OnJoinedChannel(object sender, TwitchLib.Client.Events.OnJoinedChannelArgs e)
      {
         _ = SendLogMessage($"Joined channel {e.Channel} as {e.BotUsername}");
      }

      private void TwitchClient_OnBeingHosted(object sender, TwitchLib.Client.Events.OnBeingHostedArgs e)
      {
         AnimationData animation = null;                  
         var qualifyingAnimations = AnimationManager.GetAllAnimations(AnimationManager.FetchType.EnabledOnly).Where(a => a.IsHostAlert &&
                                                                                                                         (String.IsNullOrEmpty(a.HostRestrictedToUsername) ||
                                                                                                                         a.HostRestrictedToUsername.Equals(e.BeingHostedNotification.HostedByChannel, StringComparison.OrdinalIgnoreCase))).ToList();
         if (qualifyingAnimations.Count > 0)
         {
            int randomIndex = Common.sRandom.Next(qualifyingAnimations.Count);
            if (randomIndex < qualifyingAnimations.Count)
            {
               animation = qualifyingAnimations[randomIndex];
            }
            
            if (animation != null)
            {
               AnimationManager.ForceQueueAnimation(animation, e.BeingHostedNotification.HostedByChannel, String.Empty);
            }
         }

         _ = SendLogMessage($"Hosted by {e.BeingHostedNotification.HostedByChannel}.");

         // Place a sticker, if applicable.
         if (StickersManager != null &&
             StickersManager.Data != null &&
             StickersManager.Data.Enabled &&
             StickersManager.Data.IncludeHosts)
         {
            _ = SendLogMessage($"Sticker placed for host from [{e.BeingHostedNotification.HostedByChannel}].");
            _ = StickersManager.PlaceASticker();
         }
      }

      private void TwitchClient_OnRaidNotification(object sender, TwitchLib.Client.Events.OnRaidNotificationArgs e)
      {
         AnimationData animation = null;                  
         var allRaidAnimations = AnimationManager.GetAllAnimations(AnimationManager.FetchType.EnabledOnly).Where(a => a.IsRaidAlert).ToList();

         _ = SendLogMessage($"KITTENZDEBUG: Raid notification received! Raider DisplayName: {e.RaidNotification.DisplayName}");

         // See if there is a qualifying animation for this specific raider.
         foreach (var qualifyingAnim in allRaidAnimations)
         {
            if (!String.IsNullOrEmpty(qualifyingAnim.RaidRestrictedToUsername) && qualifyingAnim.RaidRestrictedToUsername.Equals(e.RaidNotification.DisplayName, StringComparison.OrdinalIgnoreCase))
            {
               animation = qualifyingAnim;
               _ = SendLogMessage($"KITTENZDEBUG: Found animation for {qualifyingAnim.RaidRestrictedToUsername}");
            }
         }

         // If we didn't find a specific animation and we have qualifying animations, just pick one raid defense.
         if (animation == null && allRaidAnimations.Count > 0)
         {
            var notSpecificRaidAnimations = allRaidAnimations.Where(a => String.IsNullOrEmpty(a.RaidRestrictedToUsername)).ToList();

            _ = SendLogMessage($"KITTENZDEBUG: Did not find an animation for Raider DisplayName: {e.RaidNotification.DisplayName}. Searching now for another without a specific raider.");

            int randomIndex = Common.sRandom.Next(notSpecificRaidAnimations.Count);
            if (randomIndex < notSpecificRaidAnimations.Count)
            {
               animation = notSpecificRaidAnimations[randomIndex];

               _ = SendLogMessage($"KITTENZDEBUG: Going to use animation: {animation.Command} for raider {e.RaidNotification.DisplayName}");
            }
         }

         if (animation != null)
         {
            AnimationManager.ForceQueueAnimation(animation, e.RaidNotification.DisplayName, String.Empty);
         }
         else
         {
            _ = SendLogMessage($"KITTENZDEBUG: No animation found for raider {e.RaidNotification.DisplayName}");
         }

         _ = SendLogMessage($"Raided by {e.RaidNotification.DisplayName}.");

         // Place a sticker, if applicable.
         if (StickersManager != null &&
             StickersManager.Data != null &&
             StickersManager.Data.Enabled &&
             StickersManager.Data.IncludeRaids)
         {
            _ = SendLogMessage($"Sticker placed for raid from [{e.RaidNotification.DisplayName}].");
            _ = StickersManager.PlaceASticker();
         }
      }

      private void TwitchClient_OnNewSubscriber(object sender, TwitchLib.Client.Events.OnNewSubscriberArgs e)
      {
         _ = SendLogMessage($"{e.Subscriber.DisplayName} is a new sub!");
         HandleSubscriptionEvent(e.Subscriber.DisplayName, 0, e.Subscriber.SubscriptionPlan, 0, false);
      }

      private void TwitchClient_OnReSubscriber(object sender, TwitchLib.Client.Events.OnReSubscriberArgs e)
      {
         _ = SendLogMessage($"{e.ReSubscriber.DisplayName} just resubscribed {e.ReSubscriber.SubscriptionPlan} for {e.ReSubscriber.Months}!");
         HandleSubscriptionEvent(e.ReSubscriber.DisplayName, e.ReSubscriber.Months, e.ReSubscriber.SubscriptionPlan, 0, false);
      }

      private void TwitchClient_OnGiftedSubscription(object sender, TwitchLib.Client.Events.OnGiftedSubscriptionArgs e)
      {
         //Kittenztodo: Appears to be bugged on the TwitchLib end of things.
         //_ = SendLogMessage($"{e.GiftedSubscription.DisplayName} was gifted subscription!");
         //HandleSubscriptionEvent(e.GiftedSubscription.DisplayName, 0, e.GiftedSubscription.MsgParamSubPlan, 1, true);
      }

      private void TwitchClient_OnCommunitySubscription(object sender, TwitchLib.Client.Events.OnCommunitySubscriptionArgs e)
      {
         _ = SendLogMessage($"{e.GiftedSubscription.DisplayName} gifted {e.GiftedSubscription.MsgParamMassGiftCount} subs!");
         HandleSubscriptionEvent(e.GiftedSubscription.DisplayName, 0, e.GiftedSubscription.MsgParamSubPlan, e.GiftedSubscription.MsgParamMassGiftCount, true);
      }

      public void HandleSubscriptionEvent(string viewerName, int subAlertMonths, SubscriptionPlan tier, int giftedCount, bool isGifted)
      {
         _ = SendLogMessage($"HandleSubscriptionEvent() - {viewerName}, {subAlertMonths}, {tier.ToString()}, {giftedCount}, {isGifted}");

         if (isGifted)
         {
            var validGiftSubAnims = AnimationManager.GetAllAnimations(AnimationManager.FetchType.EnabledOnly).Where(a => a.IsGiftSubAlertTrigger &&
                                                                                                                         (a.SubscriptionTierRequired == tier || a.SubscriptionTierRequired == SubscriptionPlan.NotSet) &&
                                                                                                                         (a.GiftSubCountRequirement == 0 || a.GiftSubCountRequirement == giftedCount));
            validGiftSubAnims.OrderBy(a => a.GiftSubCountRequirement);
            AnimationData highestSubAnimation = null;
            foreach (var anim in validGiftSubAnims)
            {
               if (highestSubAnimation == null || (highestSubAnimation.GiftSubCountRequirement < anim.GiftSubCountRequirement))
               {
                  highestSubAnimation = anim;
               }
            }

            if (highestSubAnimation != null)
            {
               // Queue these regardless of any other requirements or cooldowns.
               _ = SendLogMessage($"Triggering [{highestSubAnimation.Command}] by user [{viewerName}] thanks to a gifted subscription event.");
               AnimationManager.ForceQueueAnimation(highestSubAnimation, viewerName, String.Empty);
            }

            // Place a sticker, if applicable.
            if (StickersManager != null &&
                StickersManager.Data != null &&
                StickersManager.Data.Enabled &&
                StickersManager.Data.IncludeGiftSubs &&
                giftedCount >= StickersManager.Data.GiftSubMinimum)
            {
               _ = SendLogMessage($"Sticker(s) [{giftedCount}] placed for gifted sub from [{viewerName}].");

               for (int i = 0; i < giftedCount; ++i)
               {
                  _ = StickersManager.PlaceASticker();
               }
            }
         }
         else
         {
            var validSubAnims = AnimationManager.GetAllAnimations(AnimationManager.FetchType.EnabledOnly).Where(a => a.IsSubAlertTrigger &&
                                                                                                                     (a.SubscriptionTierRequired == tier || a.SubscriptionTierRequired == SubscriptionPlan.NotSet) &&
                                                                                                                     (a.SubscriptionMonthsRequired == 0 || a.SubscriptionMonthsRequired <= subAlertMonths));

            validSubAnims.OrderBy(a => a.SubscriptionMonthsRequired);
            AnimationData highestSubAnimation = null;
            foreach (var anim in validSubAnims)
            {
               if (highestSubAnimation == null || (highestSubAnimation.SubscriptionMonthsRequired < anim.SubscriptionMonthsRequired))
               {
                  highestSubAnimation = anim;
               }
            }

            if (highestSubAnimation != null)
            {
               // Queue these regardless of any other requirements or cooldowns.
               _ = SendLogMessage($"Triggering [{highestSubAnimation.Command}] by user [{viewerName}] thanks to a subscription event.");
               AnimationManager.ForceQueueAnimation(highestSubAnimation, viewerName, String.Empty);
            }

            // Place a sticker, if applicable.
            if (StickersManager != null &&
                StickersManager.Data != null &&
                StickersManager.Data.Enabled &&
                StickersManager.Data.IncludeSubs)
            {
               _ = SendLogMessage($"Sticker placed for sub from [{viewerName}].");
               _ = StickersManager.PlaceASticker();
            }
         }

         // Subscriptions can trigger hype train events.
         CheckForHypeTrainEvent();

         // Check for a countdown timer event.
         if (CountdownTimerManager?.Data?.Enabled == true &&
             CountdownTimerManager.Data.Actions.Where(a => a.Enabled && a.RedemptionType == CostRedemptionType.Subscription).Any())
         {
            CountdownTimerManager.HandleTimerSubscriptionEvent(tier);
         }

         // Apply any amounts to the goal, when applicable.         
         double subCount = 1;
         if (isGifted)
         {
            subCount = giftedCount;
         }

         GoalBarManager.ApplyValue(viewerName, subCount, GoalBarManager.ApplySource.Sub, tier);
      }

      #endregion

      #region TwitchLib API

      public async Task<List<ChatterFormatted>> GetChattersAsync()
      {
         List<ChatterFormatted> chatters = await mTwitchApi.Undocumented.GetChattersAsync(BotSettings.ChannelName);
         return chatters;
      }

      #endregion

      #region Private Methods

      private void Start()
      {
         _ = SendLogMessage("GIFBot starting up...");

         // Setup the channel point redemption manager. This is not a feature manager as it doesn't use chat or have persisted settings.
         ChannelPointManager = new ChannelPointRedemptionManager(this);

         BotSettingsLoaded = LoadSettings();

         _ = InitializeTwitchMessageHandler();

         if (!String.IsNullOrEmpty(BotSettings.BotName.Trim()) &&
             !String.IsNullOrEmpty(BotSettings.BotOauthToken.Trim()) &&
             !String.IsNullOrEmpty(BotSettings.BotRefreshToken.Trim()) &&
             !String.IsNullOrEmpty(BotSettings.ChannelName.Trim()))
         {
            ConnectToTwitch();
         }
         else
         {
            _ = SendLogMessage("No connection credentials found. Bot setup is required.");
         }

         // Initialize the hype train task.
         // Deprecated until Twitch allows this endpoint to be called again. It was closed off for a "security" risk.
         // See: https://discuss.dev.twitch.tv/t/get-hype-train-events-via-app-token/31727/6
         //_ = InitializeHypeTrainTask();

         // Initialize the user monitor pulse task.
         _ = InitializeUserMonitorTask();

         // Initialize feature managers AFTER the connection to Twitch. Many of them will try to send
         // chat messages up front.
         InitializeFeatureManagers();
      }

      private void InitializeFeatureManagers()
      {
         // All data for features will be in the settings directory.
         string settingsDir = Path.Combine(System.Environment.CurrentDirectory, kSettingsDirectoryName);

         // Create and add each of the managers for our features.
         {
            RegurgitatorManager regurgitatorManager = new RegurgitatorManager(this, Path.Combine(settingsDir, RegurgitatorManager.kFileName));
            FeatureManagers.Add(regurgitatorManager);

            AnimationManager animationManager = new AnimationManager(this, Path.Combine(settingsDir, AnimationManager.kFileName));
            FeatureManagers.Add(animationManager);

            GoalBarManager goalBarManager = new GoalBarManager(this, Path.Combine(settingsDir, GoalBarManager.kFileName));
            FeatureManagers.Add(goalBarManager);

            StickersManager stickersManager = new StickersManager(this, Path.Combine(settingsDir, StickersManager.kFileName));
            FeatureManagers.Add(stickersManager);

            SnapperManager snapperManager = new SnapperManager(this, Path.Combine(settingsDir, SnapperManager.kFileName));
            FeatureManagers.Add(snapperManager);

            BackdropManager backdropManager = new BackdropManager(this, Path.Combine(settingsDir, BackdropManager.kFileName));
            FeatureManagers.Add(backdropManager);

            CountdownTimerManager countdownTimerManager = new CountdownTimerManager(this, Path.Combine(settingsDir, CountdownTimerManager.kFileName));
            FeatureManagers.Add(countdownTimerManager);

            TiltifyManager tiltifyManager = new TiltifyManager(this);
            FeatureManagers.Add(tiltifyManager);

            GreeterManager greeterManager = new GreeterManager(this, Path.Combine(settingsDir, GreeterManager.kFileName));
            FeatureManagers.Add(greeterManager);

            GiveawayManager giveawayManager = new GiveawayManager(this, Path.Combine(settingsDir, GiveawayManager.kFileName));
            FeatureManagers.Add(giveawayManager);

            StreamElementsManager streamElementsManager = new StreamElementsManager(this);
            FeatureManagers.Add(streamElementsManager);
         }

         // Load their data.
         foreach (var manager in FeatureManagers)
         {
            if (manager is IFeatureManager featureManager)
            { 
               featureManager.LoadData();
            }

            _ = manager.Start();
         }

         RegurgitatorPackage package = new RegurgitatorPackage("test");
         package.Name = "test";
      }

      private async Task InitializeTwitchMessageHandler()
      {
         mTwitchMessageHandlerCancellationTokenSource = new CancellationTokenSource();

         try
         {
            Task messageHandler = ProcessTwitchMessageQueue(mTwitchMessageHandlerCancellationTokenSource.Token);
            await messageHandler;
         }
         catch (TaskCanceledException)
         {
            // Do Nothing. At this point, there is no UI to display a log anyway.
         }
      }

      private async Task InitializeUserMonitorTask()
      {
         mUserMonitorTaskCancellationToken = new CancellationTokenSource();

         try
         {
            Task processor = UserMonitorPulse(mUserMonitorTaskCancellationToken.Token);
            await processor;
         }
         catch (TaskCanceledException)
         {
            // Do Nothing.
         }
      }

      private async Task InitializeHypeTrainTask()
      {
         mHypeTrainTaskCancellationToken = new CancellationTokenSource();

         try
         {
            Task hypeTrainProcessor = HypeTrainPulse(mHypeTrainTaskCancellationToken.Token);
            await hypeTrainProcessor;
         }
         catch (TaskCanceledException)
         {
            // Do Nothing.
         }
      }

      #endregion

      #region Twitch Message Handling

      private void QueueTwitchMessage(TwitchMessage message)
      {
         mBotMessageQueue.Add(message);
      }

      private Task ProcessTwitchMessageQueue(CancellationToken cancellationToken)
      {
         Task task = null;

         task = Task.Run(() =>
         {
            while (true)
            {
               if (mBotMessageQueue.TryTake(out TwitchMessage message))
               {
                  if (message != null)
                  {
                     message.Process();
                  }
               }

               if (cancellationToken.IsCancellationRequested)
               {
                  throw new TaskCanceledException(task);
               }

               Thread.Sleep(100);
            }
         });

         return task;
      }

      #endregion

      #region Streamlabs Handling

      public void HandleStreamlabsTip(string queryString)
      {
         Uri uri = new Uri($"https://localhost:5000/streamlabs/tip{queryString}");
         double amount = Double.Parse(HttpUtility.ParseQueryString(uri.Query).Get("amount").Replace(',', '.'), CultureInfo.InvariantCulture);
         string formattedAmountStr = HttpUtility.ParseQueryString(uri.Query).Get("formatted_amount");
         string fromStr = HttpUtility.ParseQueryString(uri.Query).Get("from");
         string message = HttpUtility.ParseQueryString(uri.Query).Get("message");
         string eventId = HttpUtility.ParseQueryString(uri.Query).Get("eventid");

         //
         // Make sure this is unique!
         //
         if (mStreamLabsEventIds.Contains(eventId))
         {
            // It's a duplicate! Break out.
            return;
         }
         else
         {
            // Store this event ID and shave off the oldest, if needed.
            mStreamLabsEventIds.Enqueue(eventId);
            if (mStreamLabsEventIds.Count > kMaxStreamLabsEvents)
            {
               _ = mStreamLabsEventIds.Dequeue();
            }
         }

         ProcessTip(amount, fromStr, formattedAmountStr, message);
      }

      #endregion

      #region Tiltify Handling

      public void HandleTiltifyDonation(TiltifyDonation donation)
      {
         if (donation == null)
         {
            return;
         }

         //
         // Handle any animations that qualify.
         //

         IEnumerable<AnimationData> validAnimations = AnimationManager.GetAllAnimations(AnimationManager.FetchType.EnabledOnly).Where(a => a.IsTiltifyTrigger && (a.TiltifyDonationRequirement == 0 || a.TiltifyDonationRequirement == donation.Amount));
         List<AnimationData> sortedAnimations = validAnimations.OrderBy(a => a.TiltifyDonationRequirement).ToList();
         foreach (var animation in sortedAnimations)
         {
            _ = SendLogMessage("Triggering [" + animation.Command + "] by user [" + donation.Name + "] with a Tiltify donation of [" + donation.Amount + "].");
            AnimationManager.ForceQueueAnimation(animation, donation.Name, donation.Amount.ToString());
         }

         //
         // Handle any other commands that qualify.
         //

         if (RegurgitatorManager != null)
         {

            RegurgitatorPackage qualifyingPackage = null;
            lock (RegurgitatorManager.PackagesMutex)
            { 
               RegurgitatorManager.Data.Packages.FirstOrDefault(p => p.Settings.Enabled &&
                                                                     !p.Settings.PlayOnTimer &&
                                                                     p.Settings.IsTiltifyTrigger &&
                                                                     (p.Settings.TiltifyDonationRequirement == 0 || p.Settings.TiltifyDonationRequirement == donation.Amount));
            }

            if (qualifyingPackage != null)
            {
               RegurgitatorManager.Play(qualifyingPackage);
            }
         }

         if (StickersManager != null &&
             StickersManager.Data != null &&
             StickersManager.Data.Enabled)
         {
            bool shouldLog = false;

            if (StickersManager.Data.IncludeTiltifyDonations && donation.Amount >= StickersManager.Data.TiltifyDonationMinimum)
            {
               shouldLog = true;
               _ = StickersManager.PlaceASticker();
            }
            else
            {
               // See if there is a specific sticker that qualifies for the amount.
               foreach (var category in StickersManager.Data.Categories)
               {
                  StickerEntryData sticker = category.Entries.FirstOrDefault(s => !s.AllowRandomPlacement && s.IncludeTiltifyDonations && s.TiltifyDonationAmount == donation.Amount);
                  if (sticker != null)
                  {
                     shouldLog = true;
                     _ = StickersManager.PlaceASticker(sticker);
                  }
               }
            }

            if (shouldLog)
            {
               _ = SendLogMessage($"Sticker placed for Tiltify Donation of [{donation.Amount}] from [{donation.Name}].");
            }
         }

         if (SnapperManager != null && SnapperManager.Enabled)
         {
            foreach (var command in SnapperManager.EnabledCommands)
            {
               if (command.RedemptionType == SnapperRedemptionType.Tiltify && (int)(Math.Floor(donation.Amount)) == command.Cost)
               {
                  switch (command.BehaviorType)
                  {
                  case SnapperBehaviorType.SpecificViewer:
                     _ = SnapperManager.Snap(command, donation.Comment, donation.Name);
                     break;

                  case SnapperBehaviorType.Revenge:
                  case SnapperBehaviorType.Thanos:
                  case SnapperBehaviorType.Self:
                     _ = SnapperManager.Snap(command, String.Empty, donation.Name);
                     break;
                  }
               }
            }
         }

         // Look for Backdrops
         if (BackdropManager != null &&
             BackdropManager.Data != null &&
             BackdropManager.Data.Enabled &&
             BackdropManager.Data.RedemptionType == CostRedemptionType.Tiltify &&
             (int)(Math.Floor(donation.Amount)) == BackdropManager.Data.Cost)
         {
            BackdropManager.HandleBackdropEvent(String.Empty);
         }

         // Look for Countdown Timer
         if (CountdownTimerManager?.Data?.Enabled == true &&
             CountdownTimerManager.Data.Actions.Where(a => a.Enabled && a.RedemptionType == CostRedemptionType.Tiltify).Any())
         {
            CountdownTimerManager.HandleTimerEvent(donation.Amount, CostRedemptionType.Tiltify);
         }
      }

      #endregion

      #region Chat User Monitoring and Management

      private Task UserMonitorPulse(CancellationToken cancellationToken)
      {
         Task task = null;

         task = Task.Run(() =>
         {
            while (true)
            {               
               if (BotSettings != null && 
                   !String.IsNullOrEmpty(BotSettings.BotOauthToken) &&
                   !String.IsNullOrEmpty(BotSettings.ChannelName))
               {
                  List<string> usersInChat = TwitchEndpointHelpers.GetUserList(HttpClientFactory.CreateClient(Common.skHttpClientName), BotSettings.BotOauthToken, BotSettings.ChannelName.ToLower());

                  lock (UsersInChannelMutex)
                  {
                     UsersInChannel.UnionWith(usersInChat);
                  }

                  if (cancellationToken.IsCancellationRequested)
                  {
                     throw new TaskCanceledException(task);
                  }

                  Thread.Sleep(60000);
               }
               else
               {
                  Thread.Sleep(1000);
               }
            }
         });

         return task;
      }

      public void AddUserToChannelList(string username)
      {
         lock (UsersInChannelMutex)
         {
            UsersInChannel.Add(username);
         }
      }

      #endregion

      #region Hype Train Handling

      private Task HypeTrainPulse(CancellationToken cancellationToken)
      {
         Task task = null;

         task = Task.Run(() =>
         {
            while (true)
            {
               CheckForHypeTrainEvent(true);

               if (cancellationToken.IsCancellationRequested)
               {
                  throw new TaskCanceledException(task);
               }

               Thread.Sleep(1000);
            }
         });

         return task;
      }

      /// <summary>
      /// Pings Twitch for the last known Hype Train event for the channel and sees if an animation needs to 
      /// be triggered.
      /// </summary>
      public void CheckForHypeTrainEvent(bool triggerAnimation = true)
      {
         // Deprecated until Twitch allows this endpoint to be called again. It was closed off for a "security" risk.
         // See: https://discuss.dev.twitch.tv/t/get-hype-train-events-via-app-token/31727/6

         //if (ChannelId != 0)
         //{
         //   TwitchHypeTrainEvent lastKnownEvent = TwitchEndpointHelpers.GetHypeTrainEventData(mBotSettings.BotOauthToken, ChannelId, mBotSettings.BotAuthenticationVersion);
         //   if (lastKnownEvent != null)
         //   {
         //      var internalData = lastKnownEvent.data.FirstOrDefault();
         //      if (internalData != null)
         //      {
         //         if (!internalData.id.Equals(BotSettings.LastTriggeredHypeTrainEventId))
         //         {
         //            string previousHypeTrainId = BotSettings.LastTriggeredHypeTrainId;
         //            int previousLevel = BotSettings.LastTriggeredHypeTrainLevel;

         //            BotSettings.LastTriggeredHypeTrainId = internalData.event_data.id;
         //            BotSettings.LastTriggeredHypeTrainEventId = internalData.id;
         //            BotSettings.LastTriggeredHypeTrainLevel = internalData.event_data.level;
         //            SaveSettings();

         //            if (triggerAnimation &&
         //                (!previousHypeTrainId.Equals(internalData.event_data.id) ||
         //                previousLevel < internalData.event_data.level))
         //            {
         //               // Huzzah! A level event in the Hype Train occurred! Fire off dem animations!
         //               AnimationData hypeTrainAnimation = AnimationManager.GetAllAnimations(AnimationManager.FetchType.EnabledOnly).FirstOrDefault(a => a.IsHypeTrainTrigger && a.HypeTrainLevel == internalData.event_data.level);
         //               if (hypeTrainAnimation != null)
         //               {
         //                  _ = SendLogMessage($"Hype train level {internalData.event_data.level} achieved!");
         //                  AnimationManager.ForceQueueAnimation(hypeTrainAnimation, String.Empty, String.Empty);
         //               }
         //            }
         //         }
         //      }
         //   }
         //}
      }

      #endregion

      #region Properties

      public IConfiguration Configuration { get; private set; }

      public TwitchClient TwitchClient { get { return mBotTwitchClient; } }

      public TwitchAPI TwitchAPI { get { return mTwitchApi; } }

      public IHubContext<GIFBotHub> GIFBotHub { get; private set; }

      public IHttpClientFactory HttpClientFactory { get; private set; }

      public BotSettings BotSettings
      {
         get { return mBotSettings; }
         set {
            string oldStreamlabsValue = mBotSettings.StreamlabsOauthToken;
            if (oldStreamlabsValue != value.StreamlabsOauthToken)
            {
               if (GIFBotHub is GIFBotHub hub)
               {
                  _ = hub.SendStreamlabsAuthToken();
               }
            }

            mBotSettings = value;
            SaveSettings();
         }
      }

      public List<IBasicManager> FeatureManagers { get; private set; } = new List<IBasicManager>();

      public RegurgitatorManager RegurgitatorManager
      {
         get {
            return FeatureManagers.OfType<RegurgitatorManager>().FirstOrDefault();
         }
      }

      public AnimationManager AnimationManager
      {
         get {
            return FeatureManagers.OfType<AnimationManager>().FirstOrDefault();
         }
      }

      public GoalBarManager GoalBarManager
      {
         get {
            return FeatureManagers.OfType<GoalBarManager>().FirstOrDefault();
         }
      }

      public StickersManager StickersManager
      {
         get {
            return FeatureManagers.OfType<StickersManager>().FirstOrDefault();
         }
      }

      public SnapperManager SnapperManager
      {
         get {
            return FeatureManagers.OfType<SnapperManager>().FirstOrDefault();
         }
      }

      public BackdropManager BackdropManager
      {
         get {
            return FeatureManagers.OfType<BackdropManager>().FirstOrDefault();
         }
      }

      public CountdownTimerManager CountdownTimerManager
      {
         get {
            return FeatureManagers.OfType<CountdownTimerManager>().FirstOrDefault();
         }
      }

      public TiltifyManager TiltifyManager
      {
         get {
            return FeatureManagers.OfType<TiltifyManager>().FirstOrDefault();
         }
      }

      public GreeterManager GreeterManager
      {
         get {
            return FeatureManagers.OfType<GreeterManager>().FirstOrDefault();
         }
      }

      public GiveawayManager GiveawayManager
      {
         get {
            return FeatureManagers.OfType<GiveawayManager>().FirstOrDefault();
         }
      }



      public StreamElementsManager StreamElementsManager
      {
         get
         {
            return FeatureManagers.OfType<StreamElementsManager>().FirstOrDefault();
         }
      }

      public ChannelPointRedemptionManager ChannelPointManager { get; private set; }

      public HashSet<string> UsersInChannel { get; private set; } = new HashSet<string>();
      public object UsersInChannelMutex = new object();

      public List<string> LogMessages { get; set; } = new List<string>();

      public object LogMutex { get; set; } = new object();

      public bool BotSettingsLoaded { get; set; } = false;

      public bool StartupLogsSent { get; set; } = false;

      public bool IsStreamerOnlyMode { get; set; } = false;

      public uint ChannelId { get; set; } = 0;

      public bool CrazyModeEnabled { get; set; } = false;

      public DateTime LastTimeAnimationTriggered { get; private set; } = DateTime.Now.AddDays(-1);

      #endregion

      #region Private Members

      /// <summary>
      /// The active connection with Twitch services.
      /// </summary>
      private TwitchClient mBotTwitchClient;
      private TwitchClient mStreamerTwitchClient;

      /// <summary>
      /// Authorized access to the Twitch API.
      /// </summary>
      private TwitchAPI mTwitchApi = new TwitchAPI();

      /// <summary>
      /// Twitch follower service.
      /// </summary>
      private FollowerService mFollowerService;

      /// <summary>
      /// The loaded and active settings for the bot.
      /// </summary>
      private BotSettings mBotSettings;

      /// <summary>
      /// This is a queue of transactions that need to be processed by the bot handler. We have to keep
      /// this all in one thread to avoid having multiple threads talking to the database.
      /// </summary>
      private BlockingCollection<TwitchMessage> mBotMessageQueue = new BlockingCollection<TwitchMessage>();

      /// <summary>
      /// The cancellation token for the task handling transactions coming from Twitch.
      /// </summary>
      private CancellationTokenSource mTwitchMessageHandlerCancellationTokenSource;

      /// <summary>
      /// Task: The cancellation token for the hype train pulse.
      /// </summary>
      private CancellationTokenSource mHypeTrainTaskCancellationToken;

      /// <summary>
      /// Task: The cancellation token for the user monitor pulse.
      /// </summary>
      private CancellationTokenSource mUserMonitorTaskCancellationToken;

      /// <summary>
      /// Tracks the event ids sent by streamlabs to prevent duplicates from playing.
      /// </summary>
      private Queue<string> mStreamLabsEventIds = new Queue<string>();
      private const int kMaxStreamLabsEvents = 25;

      /// <summary>
      /// Private Statics and Consts
      /// </summary>
      private const string kSettingsDirectoryName = "settings";
      private const string kLogsDirectoryName = "logs";
      private const int kMaxLogLines = 100;
      private static int skMaxCharactersPerAnimOutput = 254;

      #endregion
   }
}