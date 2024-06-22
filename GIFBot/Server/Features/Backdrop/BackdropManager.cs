using GIFBot.Server.Interfaces;
using GIFBot.Shared;
using GIFBot.Shared.Models.Base;
using GIFBot.Shared.Models.Features;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Client.Events;
using static GIFBot.Shared.Utility.Enumerations;

namespace GIFBot.Server.Features.Backdrop
{
   public class BackdropManager : IFeatureManager
   {
      #region Public Methods

      /// <summary>
      /// Constructor
      /// </summary>
      public BackdropManager(GIFBot.GIFBot bot, string dataFilePath)
      {
         Bot = bot;
         DataFilePath = dataFilePath;
      }

      public async Task Start()
      {
         mBackdropProcessorCancellationTokenSource = new CancellationTokenSource();

         try
         {
            Task backdropProcessor = ProcessBackdropQueue(mBackdropProcessorCancellationTokenSource.Token);
            await backdropProcessor;
         }
         catch (TaskCanceledException)
         {
            // Do Nothing.
         }
      }

      public void Stop()
      {
         mBackdropProcessorCancellationTokenSource.Cancel();
      }

      /// <summary>
      /// Loads the data.
      /// </summary>
      public void LoadData()
      {
         if (!String.IsNullOrEmpty(DataFilePath) && File.Exists(DataFilePath))
         {
            string fileData = File.ReadAllText(DataFilePath);
            mData = JsonSerializer.Deserialize<BackdropData>(fileData);

            if (mData.Enabled)
            {
               _ = Bot?.SendLogMessage("Backdrop data loaded and enabled.");
            }
            else
            {
               _ = Bot?.SendLogMessage("Backdrop data loaded and is not currently enabled.");
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

            var jsonData = JsonSerializer.Serialize(mData);
            File.WriteAllText(DataFilePath, jsonData);

            _ = Bot?.SendLogMessage("Backdrop data saved.");
         }
      }

      /// <summary>
      /// Determines if this message can be handled by this feature.
      /// </summary>
      public bool CanHandleTwitchMessage(string message, bool isBroadcaster = false)
      {
         if (!String.IsNullOrEmpty(message) &&
             mData != null &&
             mData.Enabled &&
             message.Contains(mData.Command, StringComparison.OrdinalIgnoreCase) &&
             mData.RedemptionType == CostRedemptionType.Cheer)
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
         if (mData != null &&
             mData.Enabled &&
             message.ChatMessage.Message.Contains(mData.Command, StringComparison.OrdinalIgnoreCase) &&
             mData.RedemptionType == CostRedemptionType.Cheer &&
             mData.Cost == message.ChatMessage.Bits)
         {
            HandleBackdropEvent(message.ChatMessage.Message);
         }
      }

      public void HandleBackdropEvent(string message)
      {
         if (mData.Backdrops.Any() && (String.IsNullOrEmpty(message) || message.Contains(mData.Command, StringComparison.OrdinalIgnoreCase)))
         {
            BackdropVideoEntryData backdropToPlay = GetBackdropVideoFromMessage(message);
            if (backdropToPlay == null)
            {
               var qualifyingBackdrops = mData.Backdrops.Where(b => b.Enabled && b.PlayedOnce == false);
               if (qualifyingBackdrops.Count() == 0)
               {
                  foreach (var b in mData.Backdrops)
                  {
                     b.PlayedOnce = false;
                  }
               }

               qualifyingBackdrops = mData.Backdrops.Where(b => b.Enabled && b.PlayedOnce == false);
               if (qualifyingBackdrops.Count() > 0)
               {
                  int randomIndex = Common.sRandom.Next(qualifyingBackdrops.Count());
                  backdropToPlay = qualifyingBackdrops.ElementAt(randomIndex);
               }
            }
            
            if (backdropToPlay != null)
            {
               QueueBackdrop(backdropToPlay);
            }
         }
      }

      /// <summary>
      /// Handles hanging the backdrop.
      /// </summary>
      public void QueueBackdrop(BackdropVideoEntryData backdrop)
      {
         if (backdrop != null)
         {
            backdrop.PlayedOnce = true;
            SaveData();

            PlacedVisualBase placedBackdrop = new PlacedVisualBase();
            placedBackdrop.Visual = $"media/{backdrop.Visual}";
            placedBackdrop.Top = $"0px";
            placedBackdrop.Left = $"0px";
            placedBackdrop.Width = $"1920px";
            placedBackdrop.Height = $"1080px";

            mBackdropQueue.Add(placedBackdrop);
            _ = Bot.SendLogMessage($"Queueing backdrop {backdrop.Name}.");
         }
         else
         {
            _ = Bot.SendLogMessage("A backdrop was requested, but no qualifying backdrop was found!");
         }
      }

      /// <summary>
      /// Removes the backdrop.
      /// </summary>
      public async Task TakeDownBackdrop()
      {
         IHubClients clients = Bot.GIFBotHub.Clients;
         await clients.All.SendAsync("TakeDownBackdrop");
         IsBackdropActive = false;
         _ = Bot.SendLogMessage("Removing all backdrops.");
      }

      /// <summary>
      /// Retrieves the backdrop from the message.
      /// </summary>
      public BackdropVideoEntryData GetBackdropVideoFromMessage(string message)
      {
         foreach (var backdrop in mData.Backdrops)
         {
            if (backdrop.Enabled &&
                message.Contains(backdrop.Name, StringComparison.OrdinalIgnoreCase))
            {
               return backdrop;
            }
         }

         return null;
      }

      #endregion

      #region Private Methods

      private Task ProcessBackdropQueue(CancellationToken cancellationToken)
      {
         Task task = null;

         task = Task.Run(async () =>
         {
            while (true)
            {
               if (!IsBackdropActive)
               {
                  //
                  // If there isn't an active backdrop, pull the next from the queue and set it to play.
                  //

                  PlacedVisualBase backdropToPlace = mBackdropQueue.Take();
                  if (backdropToPlace != null)
                  {
                     IHubClients clients = Bot.GIFBotHub.Clients;
                     ActiveBackdrop = backdropToPlace;
                     await clients.All.SendAsync("HangBackdrop", JsonSerializer.Serialize(backdropToPlace));
                     _ = Bot.SendLogMessage($"Hanging backdrop {backdropToPlace.Visual}.");
                     mLastTimeBackdropHung = DateTime.Now;
                     IsBackdropActive = true;
                  }
               }
               else
               {
                  //
                  // If there IS a backdrop active, see how long it's been active and determine if it needs
                  // to be deactivated or not, but ONLY if there are backdrops waiting. You don't want to drop
                  // the backdrop, because it could lead to weird stream appearances.
                  //

                  var timespan = DateTime.Now.Subtract(mLastTimeBackdropHung).TotalMinutes;
                  if (timespan >= mData.MinimumMinutesActive && mBackdropQueue.Any())
                  {
                     ActiveBackdrop = null;
                     await TakeDownBackdrop();
                  }
               }

               if (cancellationToken.IsCancellationRequested)
               {
                  throw new TaskCanceledException(task);
               }

               // Sleep a bit so the loop isn't running full hog.
               Thread.Sleep(250);
            }
         });

         return task;
      }

      #endregion

      #region Properties

      public GIFBot.GIFBot Bot { get; private set; }

      public string DataFilePath { get; private set; }

      public bool IsBackdropActive { get; private set; } = false;

      public PlacedVisualBase ActiveBackdrop { get; private set; } = null;

      public BackdropData Data
      {
         get { return mData; }
         set {
            mData = value;
            SaveData();
         }
      }

      public const string kFileName = "gifbot_backdrop.json";

      #endregion

      #region Private Members

      private BackdropData mData = new BackdropData();

      private BlockingCollection<PlacedVisualBase> mBackdropQueue = new BlockingCollection<PlacedVisualBase>();

      private CancellationTokenSource mBackdropProcessorCancellationTokenSource;

      private DateTime mLastTimeBackdropHung = DateTime.Now.AddDays(-1);
      

      #endregion
   }
}
