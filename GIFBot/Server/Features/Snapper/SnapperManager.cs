using GIFBot.Server.Interfaces;
using GIFBot.Shared;
using GIFBot.Shared.Models.Features;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Api.Core.Models.Undocumented.Chatters;
using TwitchLib.Client.Events;

namespace GIFBot.Server.Features.Snapper
{
   public class SnapperManager : IFeatureManager
   {
      #region Properties

      public GIFBot.GIFBot Bot { get; private set; }

      public string DataFilePath { get; private set; }

      public SnapperData Data
      {
         get {
            return mData;
         }
         set {
            mData = value;
         }
      }

      public bool Enabled
      {
         get {
            return (mData != null && mData.Enabled);
         }
      }

      public List<SnapperCommand> EnabledCommands
      {
         get {
            if (mData != null)
            {
               return mData.Commands.Where(c => c.Enabled == true).ToList();
            }
            else
            {
               return new List<SnapperCommand>();
            }
         }
      }

      public const string kFileName = "gifbot_snapper.json";

      #endregion

      #region Public Methods

      /// <summary>
      /// Constructor
      /// </summary>
      public SnapperManager(GIFBot.GIFBot bot, string dataFilePath)
      {
         Bot = bot;
         DataFilePath = dataFilePath;
      }

      #endregion

      #region IFeatureManager Implementation

      public bool CanHandleTwitchMessage(string message, bool isBroadcaster = false)
      {
         if (mData.Enabled)
         {
            SnapperCommand command = GetCommandFromMessage(message);
            if (command != null && command.RedemptionType == SnapperRedemptionType.Cheer && command.Enabled)
            {
               return true;
            }
         }

         return false;
      }

      public void HandleTwitchMessage(OnMessageReceivedArgs message)
      {
         if (mData.Enabled)
         {
            SnapperCommand command = GetCommandFromMessage(message.ChatMessage.Message);
            if (command != null &&
                command.RedemptionType == SnapperRedemptionType.Cheer &&
                command.Cost == message.ChatMessage.Bits &&
                command.Enabled)
            {
               _ = Snap(command, message.ChatMessage.Message, message.ChatMessage.DisplayName);
            }
         }
      }

      public void LoadData()
      {
         if (!String.IsNullOrEmpty(DataFilePath) && File.Exists(DataFilePath))
         {
            string fileData = File.ReadAllText(DataFilePath);
            mData = JsonConvert.DeserializeObject<SnapperData>(fileData);

            if (mData.Enabled)
            {
               _ = Bot?.SendLogMessage("Snapper data loaded and enabled.");
            }
            else
            {
               _ = Bot?.SendLogMessage("Snapper data loaded and is not currently enabled.");
            }
         }
      }

      public void SaveData()
      {
         if (mData != null)
         {
            Directory.CreateDirectory(Path.GetDirectoryName(DataFilePath));

            var jsonData = JsonConvert.SerializeObject(mData);
            File.WriteAllText(DataFilePath, jsonData);

            _ = Bot?.SendLogMessage("Snapper data saved.");
         }
      }

      public SnapperCommand GetCommandFromMessage(string message)
      {
         foreach (var command in mData.Commands)
         {
            if (message.Contains(command.Command, StringComparison.OrdinalIgnoreCase))
            {
               return command;
            }
         }

         return null;
      }

      public async Task Start()
      {
         mCancellationTokenSource = new CancellationTokenSource();

         try
         {
            Task processor = ModeratorSwordCheck(mCancellationTokenSource.Token);
            await processor;
         }
         catch (TaskCanceledException)
         {
            // Do Nothing.
         }
      }

      public void Stop()
      {
         mCancellationTokenSource.Cancel();
      }

      #endregion

      #region Snap Handler Methods

      /// <summary>
      /// Handles the snap command. Assumes the costs associated with the command
      /// have been validated before getting here.
      /// </summary>
      /// <param name="command"></param>
      /// <param name="message"></param>
      public async Task Snap(SnapperCommand command, string message, string thanosName)
      {
         string targetText = String.Empty;

         switch (command.BehaviorType)
         {
         case SnapperBehaviorType.SpecificViewer:
            {
               string viewerName = GetViewerNameFromMessage(message);
               targetText = await HandleSnapViewer(command, viewerName, thanosName);
            }
            break;

         case SnapperBehaviorType.Revenge:
            {
               targetText = await HandleRevengeSnap(command);
            }
            break;

         case SnapperBehaviorType.Thanos:
            {
               targetText = await HandleThanosSnap(command, thanosName);
            }
            break;

         case SnapperBehaviorType.Self:
            {
               targetText = HandleSnapSelf(command, thanosName);
            }
            break;

         case SnapperBehaviorType.ModOnly:
            {
               targetText = await HandleModOnlySnap(command, thanosName);
            }
            break;
         }

         if (!String.IsNullOrEmpty(targetText) && !String.IsNullOrEmpty(command.SnapPhrase))
         {
            Bot.SendChatMessage($"{command.SnapPhrase.Replace("$result", targetText)}");
         }
      }

      private async Task<string> HandleSnapViewer(SnapperCommand command, string viewerName, string thanosName)
      {
         if (command == null)
         {
            return null;
         }

         if (command.OnlyTimeoutOverrideVictim && !String.IsNullOrEmpty(command.OverrideVictim))
         {
            viewerName = command.OverrideVictim;
         }

         List<ChatterFormatted> qualifyingSnapees = await GetQualifyingChatters();
         if (qualifyingSnapees.Count == 0)
         {
            _ = Bot.SendLogMessage($"Could not snap viewer [{viewerName}]. No chatters found.");
            return null;
         }

         ChatterFormatted viewerToTimeout = qualifyingSnapees.FirstOrDefault(c => c.Username.Equals(viewerName, StringComparison.OrdinalIgnoreCase));
         if (viewerToTimeout == null && !command.OnlyTimeoutOverrideVictim)
         {
            if (!String.IsNullOrEmpty(viewerName))
            {
               _ = Bot.SendLogMessage($"Viewer [{viewerName}] not found. Choosing another victim...");
            }

            // This user is null or not of the right tier for the bot to timeout. Randomly choose another viewer.
            int randomIndex = Common.sRandom.Next(qualifyingSnapees.Count);
            viewerToTimeout = qualifyingSnapees.ElementAt(randomIndex);
         }

         if (viewerToTimeout != null)
         {
            int timeoutValue = GetTimeoutValue(command);

            InternalPerformSnap(command, viewerToTimeout, thanosName, true);

            if (command.AlsoTimesOutThanos)
            {
               Bot.SendChatMessage($"/timeout {thanosName} {timeoutValue}");
            }

            TriggerPairedAnimation(command, thanosName);

            return viewerToTimeout.Username;
         }
         else
         {
            _ = Bot.SendLogMessage($"Unable to timeout viewer [{viewerName}]. User not found.");
         }

         return null;
      }

      private string HandleSnapSelf(SnapperCommand command, string thanosName)
      {
         if (command != null && !String.IsNullOrEmpty(thanosName))
         {
            int timeoutValue = GetTimeoutValue(command);
            _ = Bot.SendLogMessage($"Viewer [{thanosName}] timed out themselves [{timeoutValue}] seconds.");
            Bot.SendChatMessage($"/timeout {thanosName} {timeoutValue}");

            TriggerPairedAnimation(command, String.Empty);

            return thanosName;
         }

         return null;
      }

      private async Task<string> HandleRevengeSnap(SnapperCommand command)
      {
         if (command != null && !String.IsNullOrEmpty(mPreviousThanos))
         {
            List<ChatterFormatted> qualifyingSnapees = await GetQualifyingChatters();

            ChatterFormatted viewerToTimeout = qualifyingSnapees.FirstOrDefault(c => c.Username.Equals(mPreviousThanos, StringComparison.OrdinalIgnoreCase));
            if (viewerToTimeout != null)
            {
               InternalPerformSnap(command, viewerToTimeout, String.Empty, false);

               TriggerPairedAnimation(command, String.Empty);

               return viewerToTimeout.Username;
            }
            else
            {
               _ = Bot.SendLogMessage($"Thanos [{mPreviousThanos}] not found. Revenge snap failed!");
            }
         }
         else
         {
            _ = Bot.SendLogMessage("Revenge snap requested, but there was no Thanos to rebuke!");
         }

         return null;
      }

      private async Task<string> HandleModOnlySnap(SnapperCommand command, string thanosName)
      {
         if (command != null && mData.IncludeMods)
         {
            List<ChatterFormatted> qualifyingSnapees = await GetQualifyingChatters();
            List<ChatterFormatted> moderators = qualifyingSnapees.Where(c => c.UserType == TwitchLib.Api.Core.Enums.UserType.Moderator).ToList();

            if (moderators.Count > 0)
            {
               int randomIndex = Common.sRandom.Next(moderators.Count);
               ChatterFormatted modToTimeOut = qualifyingSnapees.ElementAt(randomIndex);

               if (modToTimeOut != null)
               {
                  InternalPerformSnap(command, modToTimeOut, thanosName, true);

                  TriggerPairedAnimation(command, String.Empty);

                  return modToTimeOut.Username;
               }
            }
         }

         return null;
      }

      private async Task<string> HandleThanosSnap(SnapperCommand command, string thanosName)
      {
         if (command != null)
         {
            List<ChatterFormatted> qualifyingSnapees = await GetQualifyingChatters();

            // Determine the number of viewers snapped. If range is used, it is a percentage value.
            int totalQualifyingUsers = qualifyingSnapees.Count;
            int percentage = command.TimeoutDamageMin;
            int numUsersToSnap = command.TimeoutDamageMin;

            if (command.TimeoutDamageUsesRange && command.TimeoutDamageMin < command.TimeoutDamageMax)
            {
               percentage = Common.sRandom.Next(command.TimeoutDamageMin, command.TimeoutDamageMax);
            }

            numUsersToSnap = (int)Math.Floor((totalQualifyingUsers * (percentage / 100.0f)));

            if (numUsersToSnap > 0 && !String.IsNullOrEmpty(command.ThanosPreAnnouncement))
            {
               Bot.SendChatMessage($"{command.ThanosPreAnnouncement}");
            }

            int numSnapped = 0;
            int batchNumSnapped = 0;
            while (numUsersToSnap > 0)
            {
               int index = Common.sRandom.Next(qualifyingSnapees.Count);
               ChatterFormatted snapped = qualifyingSnapees.ElementAt(index);

               InternalPerformSnap(command, snapped, thanosName, true);

               qualifyingSnapees.Remove(snapped);
               ++numSnapped;
               ++batchNumSnapped;
               --numUsersToSnap;

               // Rate limit this shit or Twitch will global the bot.
               if (batchNumSnapped > kGeneralSnapRateLimit)
               {
                  batchNumSnapped = 0;
                  Thread.Sleep(30000);
               }
            }

            if (command.AlsoTimesOutThanos)
            {
               Bot.SendChatMessage($"/timeout {thanosName} {GetTimeoutValue(command)}");
            }

            if (numSnapped > 0)
            {
               TriggerPairedAnimation(command, thanosName);
               return numSnapped.ToString();
            }
         }

         return null;
      }

      /// <summary>
      /// Keeps tabs on snapped moderators and automatically grants them moderator again.
      /// </summary>
      private Task ModeratorSwordCheck(CancellationToken cancellationToken)
      {
         Task task = null;

         task = Task.Run(() =>
         {
            while (true)
            {
               List<SnappedModerator> moderatorsToSword = new List<SnappedModerator>();
               lock (mSnappedModeratorsLock)
               {
                  foreach (var mod in mSnappedModerators)
                  {
                     // Add 2 seconds to the timeout value to make sure our clock is lined up
                     // with the Twitch clock. Trying to re-mod while the person is still timed
                     // out will result in a chat warning.
                     double secondsDiff = DateTime.Now.Subtract(mod.TimeoutDateTime).TotalSeconds;
                     if (secondsDiff >= (mod.TimeoutValue + 2))
                     {
                        moderatorsToSword.Add(mod);
                     }
                  }
               }

               if (moderatorsToSword.Any())
               {
                  lock (mSnappedModeratorsLock)
                  {
                     foreach (var mod in moderatorsToSword)
                     {
                        Bot.SendChatMessage($"/mod {mod.Username}");
                        mSnappedModerators.Remove(mod);
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

      #endregion

      #region Utility Methods

      private int GetTimeoutValue(SnapperCommand command)
      {
         int timeoutValue = command.TimeoutValueMin;
         if (command.TimeoutValueUsesRange && command.TimeoutValueMin < command.TimeoutValueMax)
         {
            timeoutValue = Common.sRandom.Next(command.TimeoutValueMin, command.TimeoutValueMax);
         }

         return timeoutValue;
      }

      private void TriggerPairedAnimation(SnapperCommand command, string thanosName)
      {
         if (command.PostAnimationId != Guid.Empty)
         {
            AnimationData snapAnim = Bot.AnimationManager.GetAnimationById(command.PostAnimationId);
            if (snapAnim != null)
            {
               Bot.AnimationManager.ForceQueueAnimation(snapAnim, thanosName, "0");
            }
         }
      }

      public static string GetViewerNameFromMessage(string message)
      {
         // Glean the viewer's name from the message.
         if (!String.IsNullOrEmpty(message))
         {
            string viewerName = String.Empty;
            string[] tokens = message.Split(' ');
            for (int i = 0; i < tokens.Length; ++i)
            {
               if (tokens[i].StartsWith('@'))
               {
                  try
                  {
                     viewerName = tokens[i].Substring(1);
                     break;
                  }
                  catch (Exception)
                  {
                  }
               }
            }

            return viewerName;
         }

         return String.Empty;
      }

      private async Task<List<ChatterFormatted>> GetQualifyingChatters()
      {
         List<ChatterFormatted> chatters = await Bot.GetChattersAsync();

         if (mData.IncludeMods)
         {
            return chatters.Where(c => (c.UserType == TwitchLib.Api.Core.Enums.UserType.Viewer ||
                                        c.UserType == TwitchLib.Api.Core.Enums.UserType.VIP ||
                                        c.UserType == TwitchLib.Api.Core.Enums.UserType.Moderator) &&
                                       !c.Username.Equals(mData.ImmuneModerator, StringComparison.OrdinalIgnoreCase)).ToList();
         }

         return chatters.Where(c => c.UserType == TwitchLib.Api.Core.Enums.UserType.Viewer ||
                                    c.UserType == TwitchLib.Api.Core.Enums.UserType.VIP).ToList();
      }

      private void InternalPerformSnap(SnapperCommand command, ChatterFormatted viewerToTimeout, string thanosName, bool cacheThanos)
      {
         int timeoutValue = GetTimeoutValue(command);

         if (viewerToTimeout.UserType == TwitchLib.Api.Core.Enums.UserType.Moderator)
         {
            lock (mSnappedModeratorsLock)
            {
               mSnappedModerators.Add(new SnappedModerator() {
                  Username = viewerToTimeout.Username,
                  TimeoutValue = timeoutValue,
                  TimeoutDateTime = DateTime.Now
               });
            }
         }

         if (!String.IsNullOrEmpty(thanosName))
         {
            _ = Bot.SendLogMessage($"Viewer [{viewerToTimeout.Username}] timed out for [{timeoutValue}] seconds by [{thanosName}]");
         }
         else
         {
            _ = Bot.SendLogMessage($"Viewer [{viewerToTimeout.Username}] timed out for [{timeoutValue}] seconds.");
         }

         Bot.SendChatMessage($"/timeout {viewerToTimeout.Username} {timeoutValue}");

         if (cacheThanos)
         {
            mPreviousThanos = thanosName;
         }
      }

      #endregion

      #region Private Members

      private SnapperData mData = new SnapperData();

      private string mPreviousThanos = String.Empty;

      private static int kGeneralSnapRateLimit = 75;

      private List<SnappedModerator> mSnappedModerators = new List<SnappedModerator>();

      private CancellationTokenSource mCancellationTokenSource;

      private object mSnappedModeratorsLock = new object();

      #endregion        
   }

   internal class SnappedModerator
   {
      public string Username { get; set; } = String.Empty;
      public int TimeoutValue { get; set; } = 0;
      public DateTime TimeoutDateTime { get; set; } = DateTime.Now.AddDays(-1);
   }
}
