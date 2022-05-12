using GIFBot.Server.Hubs;
using GIFBot.Server.Interfaces;
using GIFBot.Shared;
using GIFBot.Shared.Models.Features;
using GIFBot.Shared.Utility;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

namespace GIFBot.Server.Features.Giveaway
{
   public class GiveawayManager : IFeatureManager
   {
      #region Public Methods

      /// <summary>
      /// Constructor
      /// </summary>
      public GiveawayManager(GIFBot.GIFBot bot, string dataFilePath)
      {
         Bot = bot;
         Bot.OnAnimationCompleted += Bot_OnAnimationCompleted;
         DataFilePath = dataFilePath;
      }

      #endregion

      #region Properties

      public GIFBot.GIFBot Bot { get; private set; }

      public string DataFilePath { get; private set; }

      public GiveawayData Data
      {
         get { return mData; }
         set { mData = value; }
      }

      #endregion

      #region Public Methods

      public void HandleChannelPointEntry(string entrant)
      {
         if (Data.IsOpenForEntries && Data.EntryBehavior == GiveawayData.GiveawayEntryBehaviorType.ChannelPoints && !mData.BannedUsers.Contains(entrant.ToLower()))
         { 
            var existingEntriesForEntrant = Data.Entrants.Where(e => e.Equals(entrant, StringComparison.OrdinalIgnoreCase));
            if (existingEntriesForEntrant.Count() < Data.MaxNumberOfEntriesAllowed)
            {
               Data.Entrants.Add(entrant.ToLower());
               _ = Bot.GIFBotHub.Clients.All.SendAsync("SendNewGiveawayEntrant", entrant);
               SaveData();
            }
         }
      }

      public void Open()
      {
         if (!Data.IsOpenForEntries)
         {
            if (Data.EntryBehavior == GiveawayData.GiveawayEntryBehaviorType.ChannelPoints)
            { 
               if (Data.ChannelPointRewardId == Guid.Empty)
               { 
                  Guid rewardId = TwitchEndpointHelpers.CreateChannelPointReward(Bot.HttpClientFactory.CreateClient(Common.skHttpClientName), Bot.BotSettings.StreamerOauthToken, Bot.ChannelId, Data.ChannelPointRewardTitle, Data.ChannelPointsRequired, Data.MaxNumberOfEntriesAllowed);
                  if (rewardId == Guid.Empty)
                  {
                     _ = Bot.SendLogMessage("Unable to create channel point reward for giveaway. Please make sure you are authenticated with your streamer account.");
                     return;
                  }
                  else
                  {
                     Data.ChannelPointRewardId = rewardId;
                  }
               }
               else
               {
                  if (!TwitchEndpointHelpers.UpdateChannelPointReward(Bot.HttpClientFactory.CreateClient(Common.skHttpClientName), Bot.BotSettings.StreamerOauthToken, Bot.ChannelId, Data.ChannelPointRewardId, true))
                  {
                     _ = Bot.SendLogMessage("Unable to disable the channel point reward associated with the giveaway.");
                  }
               }
            }

            Data.IsOpenForEntries = true;
            SaveData();

            if (!String.IsNullOrEmpty(Data.GiveawayOpenAnnouncementText))
            {
               string tickets = $"(Max Redemptions Allowed = {Data.MaxNumberOfEntriesAllowed})";
               if (Data.EntryBehavior == GiveawayData.GiveawayEntryBehaviorType.Command)
               { 
                  tickets = String.Empty;
               }

               Bot.SendChatMessage($"{Data.GiveawayOpenAnnouncementText.Replace("$command", Data.Command)} {tickets}");
            }
         }
      }

      public void Close()
      {
         if (Data.IsOpenForEntries)
         {
            if (Data.EntryBehavior == GiveawayData.GiveawayEntryBehaviorType.ChannelPoints && 
                Data.ChannelPointRewardId != Guid.Empty &&
                !TwitchEndpointHelpers.UpdateChannelPointReward(Bot.HttpClientFactory.CreateClient(Common.skHttpClientName), Bot.BotSettings.StreamerOauthToken, Bot.ChannelId, Data.ChannelPointRewardId, false))
            {
               _ = Bot.SendLogMessage("Unable to disable the channel point reward associated with the giveaway.");
            }

            Data.IsOpenForEntries = false;
            SaveData();

            if (!String.IsNullOrEmpty(Data.GiveawayClosedAnnouncementText))
            {
               Bot.SendChatMessage(Data.GiveawayClosedAnnouncementText);
            }
         }
      }

      public void DrawWinner()
      {
         if (mData.Entrants.Any())
         {
            mIsDrawingWinner = true;

            if (mData.DrumrollAnimation != Guid.Empty)
            {
               AnimationData animation = Bot.AnimationManager.GetAnimationById(mData.DrumrollAnimation);
               if (animation != null)
               {
                  Bot.AnimationManager.PriorityQueueAnimation(animation);
               }
            }
            else
            {
               InternalDrawWinner();
            }
         }
      }

      public void Reset()
      {
         if (!TwitchEndpointHelpers.DeleteChannelPointReward(Bot.HttpClientFactory.CreateClient(Common.skHttpClientName), Bot.BotSettings.StreamerOauthToken, Bot.ChannelId, Data.ChannelPointRewardId))
         {
            _ = Bot.SendLogMessage("Unable to delete the channel reward for the giveaway! Please remove it yourself.");
         }

         Data.ChannelPointRewardId = Guid.Empty;
         Data.IsOpenForEntries = false;
         Data.Entrants.Clear();
         SaveData();
      }

      #endregion

      #region IFeatureManager Implementation 

      public bool CanHandleTwitchMessage(string message, bool isBroadcaster = false)
      {
         return (Data.IsOpenForEntries &&
                 message.StartsWith(Data.Command, StringComparison.OrdinalIgnoreCase));
      }

      public void HandleTwitchMessage(OnMessageReceivedArgs message)
      {
         if (Data.IsOpenForEntries && 
             Data.EntryBehavior == GiveawayData.GiveawayEntryBehaviorType.Command &&
             message.ChatMessage.Message.StartsWith(Data.Command, StringComparison.OrdinalIgnoreCase))
         {
            string entrant = message.ChatMessage.DisplayName.ToLower();

            bool isFollower = false;
            if (TwitchEndpointHelpers.CheckFollowChannelOnTwitch(Bot.HttpClientFactory.CreateClient(Common.skHttpClientName), Bot.BotSettings.BotOauthToken, long.Parse(message.ChatMessage.RoomId), long.Parse(message.ChatMessage.UserId)))
            {
               isFollower = true;
            }

            InternalAddEntrant(entrant, isFollower, message.ChatMessage.IsSubscriber, message.ChatMessage.IsVip, true);
         }
      }

      public void LoadData()
      {
         if (!String.IsNullOrEmpty(DataFilePath) && File.Exists(DataFilePath))
         {
            string fileData = File.ReadAllText(DataFilePath);
            mData = JsonConvert.DeserializeObject<GiveawayData>(fileData);

            _ = Bot?.SendLogMessage("Giveaway data loaded and enabled.");
         }
      }

      public void SaveData()
      {
         if (mData != null)
         {
            Directory.CreateDirectory(Path.GetDirectoryName(DataFilePath));

            var jsonData = JsonConvert.SerializeObject(mData);
            File.WriteAllText(DataFilePath, jsonData);

            _ = Bot?.SendLogMessage("Giveaway data saved.");
         }
      }

      public Task Start()
      {
         return Task.CompletedTask;
      }

      public void Stop()
      {
      }

      #endregion

      #region Private Methods

      private void InternalAddEntrant(string entrant, bool isFollower, bool isSub, bool isVIP, bool restrictDupes = true)
      {
         if ((mData.Access == AnimationEnums.AccessType.Follower && !isFollower) ||
             (mData.Access == AnimationEnums.AccessType.Subscriber && !isSub) ||
             (mData.Access == AnimationEnums.AccessType.VIP && !isVIP))
         {
            return;
         }

         if (((restrictDupes && !mData.Entrants.Contains(entrant)) || (!restrictDupes)) && !mData.BannedUsers.Contains(entrant))
         {
            mData.Entrants.Add(entrant);
            _ = Bot.GIFBotHub.Clients.All.SendAsync("SendNewGiveawayEntrant", entrant);

            if (isSub && mData.SubLuckMultiplier > 1)
            { 
               for (int i = 1; i < mData.SubLuckMultiplier; i++)
               {
                  mData.Entrants.Add(entrant);
                  _ = Bot.GIFBotHub.Clients.All.SendAsync("SendNewGiveawayEntrant", entrant);
               }
            }

            SaveData();
         }
      }

      private void Bot_OnAnimationCompleted(object sender, GIFBot.GIFBot.AnimationCompletedEventArgs e)
      {
         if (mIsDrawingWinner && mData.DrumrollAnimation == e.AnimationId)
         {
            InternalDrawWinner();
         }
      }

      private void Shuffle()
      {
         int n = mData.Entrants.Count;
         while (n > 1)
         {
            n--;
            int k = mRNGesus.Next(n + 1);
            string value = mData.Entrants[k];
            mData.Entrants[k] = mData.Entrants[n];
            Data.Entrants[n] = value;
         }
      }

      private void InternalDrawWinner()
      {
         mIsDrawingWinner = false;
         
         // Shuffle the list before beginning to pull the winner.
         Shuffle();

         int entrantCount = mData.Entrants.Count;
         int winnerIndex = mRNGesus.Next(0, entrantCount);
         string winner = mData.Entrants[winnerIndex];

         if (!String.IsNullOrEmpty(winner))
         {
            if (mData.WinnerAnimation != Guid.Empty)
            {
               AnimationData animation = Bot.AnimationManager.GetAnimationById(mData.WinnerAnimation);
               if (animation != null)
               {
                  Bot.AnimationManager.PriorityQueueAnimation(animation);
               }
            }

            while (true)
            {
               if (!mData.Entrants.Remove(winner))
               {
                  break;
               }
            }

            if (!String.IsNullOrEmpty(mData.WinnerAnnouncementText))
            {
               Bot.SendChatMessage(mData.WinnerAnnouncementText.Replace("$user", winner));
            }

            _ = Bot.GIFBotHub.Clients.All.SendAsync("SendGiveawayWinner", winner);

            SaveData();
         }
      }

      #endregion

      #region Private Data

      public const string kFileName = "gifbot_giveaway.json";

      private GiveawayData mData = new GiveawayData();

      private Random mRNGesus = new Random(Guid.NewGuid().GetHashCode());

      private bool mIsDrawingWinner = false;

      #endregion
   }
}
