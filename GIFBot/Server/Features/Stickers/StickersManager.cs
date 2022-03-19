using GIFBot.Server.Base;
using GIFBot.Server.Interfaces;
using GIFBot.Shared;
using GIFBot.Shared.Models.Features;
using GIFBot.Shared.Models.GIFBot;
using GIFBot.Shared.Utility;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

namespace GIFBot.Server.Features.Stickers
{
   public class StickersManager : VisualPreviewer, IFeatureManager
   {
      #region Public Methods

      /// <summary>
      /// Constructor
      /// </summary>
      public StickersManager(GIFBot.GIFBot bot, string dataFilePath)
      {
         Bot = bot;
         DataFilePath = dataFilePath;
      }

      public async Task Start()
      {
         mCancellationTokenSource = new CancellationTokenSource();

         try
         {
            Task processor = Process(mCancellationTokenSource.Token);
            await processor;
         }
         catch (TaskCanceledException)
         {
            // Do Nothing.
         }

         mStickerProcessorCancellationTokenSource = new CancellationTokenSource();

         try
         {
            Task stickerProcessor = ProcessStickerQueue(mStickerProcessorCancellationTokenSource.Token);
            await stickerProcessor;
         }
         catch (TaskCanceledException)
         {
            // Do Nothing.
         }
      }

      public void Stop()
      {
         mCancellationTokenSource.Cancel();
         mStickerProcessorCancellationTokenSource.Cancel();
      }

      /// <summary>
      /// Loads the data.
      /// </summary>
      public void LoadData()
      {
         if (!String.IsNullOrEmpty(DataFilePath) && File.Exists(DataFilePath))
         {
            string fileData = File.ReadAllText(DataFilePath);
            mData = JsonConvert.DeserializeObject<StickerData>(fileData);

            if (mData.BitMinimum == 0)
            {
               // Bit minimum was changed to being non-zero as of 1.0c.
               mData.BitMinimum = 1;
               SaveData();
            }

            if (mData.Enabled)
            {
               _ = Bot?.SendLogMessage("Stickers data loaded and enabled.");
            }
            else
            {
               _ = Bot?.SendLogMessage("Stickers data loaded and is not currently enabled.");
            }
         }
      }

      /// <summary>
      /// Saves the data.
      /// </summary>
      public void SaveData()
      {
         if (mData != null)
         {
            Directory.CreateDirectory(Path.GetDirectoryName(DataFilePath));

            var jsonData = JsonConvert.SerializeObject(mData);
            File.WriteAllText(DataFilePath, jsonData);

            _ = Bot?.SendLogMessage("Stickers data saved.");
         }
      }

      /// <summary>
      /// Determines if this message can be handled by this feature.
      /// </summary>
      public bool CanHandleTwitchMessage(string message, bool isBroadcaster = false)
      {
         if (!String.IsNullOrEmpty(message) && mData != null && Data.Enabled && (Data.IncludeBits || Data.CanUseCommand))
         {
            return true;
         }

         return false;
      }

      /// <summary>
      /// Handles the twitch message from chat, when applicable.
      /// </summary>
      public void HandleTwitchMessage(OnMessageReceivedArgs message)
      {
         if (Data.Enabled)
         {
            if (Data.CanUseCommand && message.ChatMessage.Message.StartsWith(Data.Command, StringComparison.OrdinalIgnoreCase) && !IsUnderCooldown())
            {
               // Validate access.
               switch (Data.Access)
               {
               case AnimationEnums.AccessType.BotExecuteOnly:
                  {
                     return;
                  }
               case AnimationEnums.AccessType.Follower:
                  {
                     if (!TwitchEndpointHelpers.CheckFollowChannelOnTwitch(Bot.BotSettings.BotOauthToken, long.Parse(message.ChatMessage.RoomId), long.Parse(message.ChatMessage.UserId)))
                     {
                        return;
                     }
                  }
                  break;
               case AnimationEnums.AccessType.Moderator:
                  {
                     if (!message.ChatMessage.IsModerator)
                     {
                        return;
                     }
                  }
                  break;
               case AnimationEnums.AccessType.Subscriber:
                  {
                     if (!message.ChatMessage.IsSubscriber)
                     {
                        return;
                     }
                  }
                  break;
               case AnimationEnums.AccessType.VIP:
                  {
                     if (!message.ChatMessage.IsVip)
                     {
                        return;
                     }
                  }
                  break;
               case AnimationEnums.AccessType.SpecificViewer:
                  {
                     if (!String.IsNullOrEmpty(message.ChatMessage.DisplayName) &&
                         !String.IsNullOrEmpty(Data.RestrictedToUser) &&
                         !message.ChatMessage.DisplayName.Equals(Data.RestrictedToUser, StringComparison.OrdinalIgnoreCase))
                     {
                        return;
                     }

                     if (Data.RestrictedUserMustBeSub && !message.ChatMessage.IsSubscriber)
                     {
                        return;
                     }
                  }
                  break;
               case AnimationEnums.AccessType.UserGroup:
                  {
                     if (!String.IsNullOrEmpty(message.ChatMessage.DisplayName))
                     {
                        UserGroup group = Bot.BotSettings.UserGroups.FirstOrDefault(g => g.Id == Data.RestrictedToUserGroup);
                        if (group == null)
                        {
                           // Group doesn't exist.
                           return;
                        }

                        UserEntry qualifiedUser = group.UserEntries.FirstOrDefault(u => u.Name.Equals(message.ChatMessage.DisplayName, StringComparison.OrdinalIgnoreCase));
                        if (qualifiedUser == null)
                        {
                           // Not in the group.
                           return;
                        }
                     }
                  }
                  break;
               }

               _ = PlaceASticker(message.ChatMessage.Message);
            }
            else if (Data.IncludeBits && 
                     message.ChatMessage.Bits != 0 && 
                     !String.IsNullOrEmpty(message.ChatMessage.DisplayName) && 
                     message.ChatMessage.Bits >= Data.BitMinimum &&
                     Data.BitMinimum > 0)
            {
               // Handle the message by checking for cheers. Only cheering goes through chat for this feature.
               _ = PlaceASticker();
            }
            else
            {
               // See if there is a specific sticker that qualifies for the amount.
               foreach (var category in Data.Categories)
               {
                  StickerEntryData sticker = category.Entries.FirstOrDefault(s => !s.AllowRandomPlacement && s.IncludeBits && s.BitAmount == message.ChatMessage.Bits);
                  if (sticker != null)
                  {
                     _ = PlaceASticker(sticker);
                     break;
                  }
               }
            }
         }
      }

      /// <summary>
      /// Sends a request to place a sticker on the overlay by providing a chat message string.
      /// </summary>
      public async Task PlaceASticker(string commandData)
      {
         StickerEntryData sticker = null;

         if (!String.IsNullOrEmpty(commandData) &&
             commandData.Contains(Data.Command, StringComparison.OrdinalIgnoreCase))
         {
            // A command was used. The name of the sticker may be a parameter. Let's get that sticker.
            string commandStart = commandData.Substring(commandData.IndexOf(Data.Command));
            commandStart = commandStart.Replace("[", "").Replace("]", "").Replace("(", "").Replace(")", "").Replace("<", "").Replace(">", "");

            string[] values = commandStart.Split(' ');
            if (values != null && values.Length > 1 && !String.IsNullOrEmpty(values[1]))
            {
               List<StickerEntryData> eligibleStickers = new List<StickerEntryData>();
               foreach (var category in Data.Categories)
               {
                  if (category.Name.Equals(values[1], StringComparison.OrdinalIgnoreCase))
                  {
                     // The entire category matches the name. Add all of its entries.
                     eligibleStickers.AddRange(category.Entries);
                  }
                  else
                  {
                     // Otherwise, look for a very specific sticker entry by name.
                     eligibleStickers.AddRange(category.Entries.Where(s => s.Enabled && s.Name.Equals(values[1], StringComparison.OrdinalIgnoreCase)));
                  }
               }

               int eligibleStickerCount = eligibleStickers.Count;
               if (eligibleStickerCount > 0)
               {
                  int randomIndex = Common.sRandom.Next(eligibleStickerCount);
                  if (randomIndex < eligibleStickerCount)
                  {
                     sticker = eligibleStickers[randomIndex];
                  }

                  if (sticker == null)
                  {
                     // No eligible sticker could be found for this command. Bail.
                     return;
                  }
               }
            }
         }

         await PlaceASticker(sticker);
      }

      /// <summary>
      /// Sends a request to place a sticker on the overlay by providing the sticker entry data.
      /// </summary>
      public async Task PlaceASticker(StickerEntryData sticker = null)
      { 
         if (sticker == null)
         {
            // No sticker was specified by name, so choose a sticker from the available pool of stickers.
            // Stickers that can be placed by command name cannot be placed randomly as they may have overrides.
            List<StickerEntryData> eligibleStickers = new List<StickerEntryData>();
            foreach (var category in Data.Categories)
            {
               eligibleStickers.AddRange(category.Entries.Where(s => s.Enabled && s.AllowRandomPlacement && !s.IncludeBits && !s.IncludeTiltifyDonations && !s.IncludeTips));
            }

            int eligibleStickerCount = eligibleStickers.Count;
            if (eligibleStickerCount > 0)
            {
               int randomIndex = Common.sRandom.Next(eligibleStickerCount);
               if (randomIndex < eligibleStickerCount)
               {
                  sticker = eligibleStickers[randomIndex];
               }
            }
         }

         if (sticker != null)
         {
            int sourceCanvasWidth = Data.CanvasWidth;
            int sourceCanvasHeight = Data.CanvasHeight;
            IHubClients clients = Bot.GIFBotHub.Clients;

            if (sticker.Layer == AnimationEnums.AnimationLayer.Secondary)
            {
               sourceCanvasWidth = mData.SecondaryCanvasWidth;
               sourceCanvasHeight = mData.SecondaryCanvasHeight;
            }

            int top = Common.sRandom.Next(sourceCanvasHeight - sticker.Placement.Height);
            int left = Common.sRandom.Next(sourceCanvasWidth - sticker.Placement.Width);
            if (sticker.UsePlacementOverride)
            {
               top = sticker.Placement.Top;
               left = sticker.Placement.Left;
            }

            PlacedSticker placedSticker = new PlacedSticker();

            placedSticker.Visual = $"media/{sticker.Visual}?{System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
            placedSticker.Top = $"{top}px";
            placedSticker.Left = $"{left}px";
            placedSticker.Width = $"{sticker.Placement.Width}px";
            placedSticker.Height = $"{sticker.Placement.Height}px";
            placedSticker.IntWidth = sticker.Placement.Width;
            placedSticker.IntHeight = sticker.Placement.Height;
            placedSticker.TimePlaced = DateTime.Now;
            placedSticker.Data = sticker;

            lock (mStickersLock)
            {
               if (mPlacedStickers.Count > 0 && mPlacedStickers.Count > Data.MaxStickers)
               {
                  // Pop off the oldest sticker.
                  var removed = mPlacedStickers[0];

                  IHubClients removeClients = Bot.GIFBotHub.Clients;
                  if (removed.Data.Layer == AnimationEnums.AnimationLayer.Secondary)
                  {
                     removeClients = Bot.GIFBotHub.Clients;
                  }

                  _ = removeClients.All.SendAsync("RemoveAsync", removed.Data.Id);
                  mPlacedStickers.RemoveAt(0);
               }

               mPlacedStickers.Add(placedSticker);

               // TODO: The idea is to queue these up, not immediately place them.
               //mPlacementQueue.TryAdd(placedSticker);
            }

            mLastTimeChatTriggered = DateTime.Now;

            await clients.All.SendAsync("PlaceSticker", JsonConvert.SerializeObject(placedSticker));
         }
      }

      /// <summary>
      /// Clears all stickers off the canvas.
      /// </summary>
      public async Task ClearAllStickers()
      {
         lock (mStickersLock)
         {
            mPlacedStickers.Clear();
         }

         await Bot.GIFBotHub.Clients.All.SendAsync("ClearAllStickers");
         await Bot.GIFBotHub.Clients.All.SendAsync("ClearAllStickers");
      }

      /// <summary>
      /// Toggles the enabled flag on all stickers.
      /// </summary>
      public void SetStickerEnabledFlags(bool enabled)
      {
         foreach (var category in mData.Categories)
         {
            foreach (var stickerEntry in category.Entries)
            {
               stickerEntry.Enabled = enabled;
            }
         }

         SaveData();
      }

      public Tuple<StickerCategory, StickerEntryData> GetStickerEntryById(Guid id)
      {
         foreach (var category in Data.Categories)
         {
            StickerEntryData sticker = category.Entries.FirstOrDefault(s => s.Id == id);
            if (sticker != null)
            {
               return new Tuple<StickerCategory, StickerEntryData>(category, sticker);
            }
         }

         return null;
      }

      #endregion

      #region Private Methods

      /// <summary>
      /// Processes entries on a thread. Only does processing if PlayOnTimer is enabled.
      /// </summary>
      private Task Process(CancellationToken cancellationToken)
      {
         Task task = null;

         task = Task.Run(() =>
         {
            while (true)
            {
               if (Data.Enabled)
               {
                  // See if the sticker has been visible for too long.
                  List<PlacedSticker> removedStickers = new List<PlacedSticker>();
                  lock (mStickersLock)
                  {
                     foreach (var sticker in mPlacedStickers)
                     {
                        int secondsTimeout = Data.NumSecondsStickerVisible;
                        if (sticker.Data.UseVisibilityTimeoutOverride)
                        {
                           secondsTimeout = sticker.Data.NumSecondsStickerVisibleOverride;
                        }

                        if (secondsTimeout > 0)
                        {
                           double secondsDiff = DateTime.Now.Subtract(sticker.TimePlaced).TotalSeconds;
                           if (secondsDiff >= Data.NumSecondsStickerVisible)
                           {
                              removedStickers.Add(sticker);
                           }
                        }
                     }
                  }

                  if (removedStickers.Any())
                  {
                     lock (mStickersLock)
                     {
                        foreach (var removed in removedStickers)
                        {
                           IHubClients removeClients = Bot.GIFBotHub.Clients;
                           if (removed.Data.Layer == AnimationEnums.AnimationLayer.Secondary)
                           {
                              removeClients = Bot.GIFBotHub.Clients;
                           }

                           _ = removeClients.All.SendAsync("RemoveSticker", removed.Data.Id);
                           mPlacedStickers.Remove(removed);
                        }
                     }
                  }
               }

               Thread.Sleep(1000);

               if (cancellationToken.IsCancellationRequested)
               {
                  throw new TaskCanceledException(task);
               }
            }
         });

         return task;
      }

      /// <summary>
      /// Processes the stickers queue.
      /// </summary>
      private Task ProcessStickerQueue(CancellationToken cancellationToken)
      {
         Task task = null;

         task = Task.Run(async () =>
         {
            while (true)
            {
               PlacedSticker request = mPlacementQueue.Take();
               if (mData.Enabled && request.Data != null)
               {
                  lock (mStickersLock)
                  {
                     mPlacedStickers.Add(request);
                  }

                  IHubClients clients = Bot.GIFBotHub.Clients;
                  if (request.Data.Layer == AnimationEnums.AnimationLayer.Secondary)
                  {
                     clients = Bot.GIFBotHub.Clients;
                  }

                  await Bot.GIFBotHub.Clients.All.SendAsync("PlaceSticker", JsonConvert.SerializeObject(request));
               }

               // Force this thread to sleep for N seconds between sticker placements to give us a nice buffer.
               Thread.Sleep(1000);

               if (cancellationToken.IsCancellationRequested)
               {
                  throw new TaskCanceledException(task);
               }
            }
         });

         return task;
      }

      private bool IsUnderCooldown()
      {
         double secondsDiff = DateTime.Now.Subtract(mLastTimeChatTriggered).TotalSeconds;
         if (secondsDiff >= mData.CommandCooldownSeconds)
         {
            return false;
         }

         return true;
      }      

      #endregion

      #region Properties

      public GIFBot.GIFBot Bot { get; private set; }

      public string DataFilePath { get; private set; }

      public StickerData Data
      {
         get { return mData; }
         set {
            mData = value;
            SaveData();
         }
      }

      public List<PlacedSticker> PlacedStickers
      {
         get {
            lock (mStickersLock)
            {
               return mPlacedStickers;
            }
         }
      }

      public const string kFileName = "gifbot_stickers.json";

      #endregion

      #region Private Members

      private StickerData mData = new StickerData();
      private object mStickersLock = new object();
      private List<PlacedSticker> mPlacedStickers = new List<PlacedSticker>();
      private CancellationTokenSource mCancellationTokenSource;
      private DateTime mLastTimeChatTriggered = DateTime.Now.AddDays(-1);

      /// <summary>
      /// Queue: This is a queue of requests that need to be triggered. All requests should go into this queue
      /// and get processed on a thread that pauses between requests.
      /// </summary>
      private BlockingCollection<PlacedSticker> mPlacementQueue = new BlockingCollection<PlacedSticker>();

      /// <summary>
      /// Task: The cancellation token for the main task that processes the stickers queue.
      /// </summary>
      private CancellationTokenSource mStickerProcessorCancellationTokenSource;

      #endregion
   }
}