using GIFBot.Server.Interfaces;
using GIFBot.Shared;
using GIFBot.Shared.Models.Features;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Events;

namespace GIFBot.Server.Features.GoalBar
{
   public class GoalBarManager : IFeatureManager
   {
      #region Public Methods

      /// <summary>
      /// Constructor
      /// </summary>
      public GoalBarManager(GIFBot.GIFBot bot, string dataFilePath)
      {
         Bot = bot;
         DataFilePath = dataFilePath;
      }

      public Task Start()
      {
         return Task.CompletedTask;
      }

      public void Stop()
      {
      }

      /// <summary>
      /// Loads the data.
      /// </summary>
      public void LoadData()
      {
         if (!String.IsNullOrEmpty(DataFilePath) && File.Exists(DataFilePath))
         {
            string fileData = File.ReadAllText(DataFilePath);
            mData = JsonSerializer.Deserialize<GoalBarData>(fileData);

            if (mData.Settings.Enabled)
            {
               _ = Bot?.SendLogMessage("GoalBar data loaded and enabled.");
            }
            else
            {
               _ = Bot?.SendLogMessage("GoalBar data loaded and is not currently enabled.");
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

            _ = Bot?.SendLogMessage("GoalBar data saved.");
         }
      }

      /// <summary>
      /// Determines if this message can be handled by this feature.
      /// </summary>
      public bool CanHandleTwitchMessage(string message, bool isBroadcaster = false)
      {
         return Data.Settings.Enabled;
      }

      /// <summary>
      /// Handles the twitch message, when applicable.
      /// </summary>
      public void HandleTwitchMessage(OnMessageReceivedArgs message)
      {
         if (!CanHandleTwitchMessage(message.ChatMessage.Message))
         {
            return;
         }

         ApplyValue(message.ChatMessage.DisplayName, message.ChatMessage.Bits, ApplySource.Cheer);
      }

      /// <summary>
      /// Handles applying a tip, cheer, or sub to the goal.
      /// </summary>
      public void ApplyValue(string userName, double amount, ApplySource source, SubscriptionPlan subTier = SubscriptionPlan.Prime)
      {
         if ((source == ApplySource.Cheer && !Data.Settings.IncludeCheers) ||
             (source == ApplySource.Sub && !Data.Settings.IncludeSubs) ||
             (source == ApplySource.Tip && !Data.Settings.IncludeTips))
         {
            return;
         }

         if (!Data.Goals.Any() || Data.Goals.FirstOrDefault(g => g.IsActive == true) == null)
         {
            return;
         }

         if (amount > 0)
         {
            double pointMultiplier = (source, subTier) switch
            {
               (ApplySource.Cheer, _)                       => Data.Settings.BitPoints,
               (ApplySource.Tip, _)                         => Data.Settings.TipPoints,
               (ApplySource.Sub, SubscriptionPlan.Prime)    => Data.Settings.SubTier1Points,
               (ApplySource.Sub, SubscriptionPlan.Tier1)    => Data.Settings.SubTier1Points,
               (ApplySource.Sub, SubscriptionPlan.Tier2)    => Data.Settings.SubTier2Points,
               (ApplySource.Sub, SubscriptionPlan.Tier3)    => Data.Settings.SubTier3Points,
               (ApplySource.RollOver, _)                    => 1.0,
               _ => throw new NotImplementedException()
            };

            GoalData goal = Data.Goals.FirstOrDefault(g => g.IsActive == true);
            double appliedValue = amount * pointMultiplier;
            double oldAmount = goal.CurrentAmount;
            goal.CurrentAmount += appliedValue;

            _ = Bot.SendLogMessage($"[Goal ({source.ToString()})] {userName} gave {amount} [{appliedValue}] toward the goal!");

            _ = Bot.GIFBotHub.Clients.All.SendAsync("UpdateGoal", goal.CurrentAmount, goal.GoalAmount, goal.Title);

            // Get the current percentage and see if a milestone needs to be triggered.
            CheckMilestones(goal, userName);

            // See if the goal has been met.
            if (goal.CurrentAmount >= goal.GoalAmount)
            {
               double rollOverAmount = goal.CurrentAmount - goal.GoalAmount;

               if (goal.ResetWhenComplete)
               {
                  //
                  // This goal is configured to reset whenever it hits its goal. Apply the rollover amount to itself.
                  // Do not move on to another goal. The user had been warned about this when activating this feature.
                  //

                  goal.Milestones.ForEach(m => m.HasTriggered = false);
                  goal.CurrentAmount = 0;
                  goal.IsActive = true;
                  if (rollOverAmount > 0)
                  {
                     ApplyValue(userName, rollOverAmount, ApplySource.RollOver, subTier);
                  }
               }
               else
               {
                  //
                  // We need to roll into the next goal or just show this goal as having been met.
                  //

                  goal.CurrentAmount = goal.GoalAmount;
                  goal.Milestones.ForEach(m => m.HasTriggered = false);
                  goal.IsActive = false;

                  bool exitOnNext = false;
                  foreach (var existingGoal in Data.Goals)
                  {
                     if (exitOnNext)
                     {
                        // Set this goal as the active goal.
                        existingGoal.IsActive = true;

                        // If we have any to roll over, recursively call this method to apply the value.
                        if (rollOverAmount > 0)
                        {
                           ApplyValue(userName, rollOverAmount, ApplySource.RollOver, subTier);
                        }
                        else
                        {
                           // No need to apply a new value, just update the goal with the new data.
                           _ = Bot.GIFBotHub.Clients.All.SendAsync("UpdateGoal", existingGoal.CurrentAmount, existingGoal.GoalAmount, existingGoal.Title);
                        }

                        break;
                     }

                     if (existingGoal.Id == goal.Id)
                     {
                        exitOnNext = true;
                     }
                  }
               }
            }

            // Persist all changes.
            SaveData();
         }
      }

      /// <summary>
      /// Looks at the goal data and rebalances the active goal amounts and performs rollovers
      /// where necessary. Should only happen on an edit operation. Does not fire milestones
      /// as this could be disruptive.
      /// </summary>
      public void RebalanceGoals()
      {
         double rollOverAmount = 0.0;

         // Deactivate all goals prior to rebalancing.
         foreach (var goal in Data.Goals)
         {
            goal.Milestones.ForEach(m => m.HasTriggered = false);
            goal.IsActive = false;
         }

         // Rebalance.
         foreach (var goal in Data.Goals)
         {
            // Apply the roll over amount to the current amount of this goal.
            goal.CurrentAmount += rollOverAmount;
                        
            if (goal.CurrentAmount >= goal.GoalAmount)
            {
               // The goal has been met or exceeded. Calculate the roll over amount, if applicable.
               rollOverAmount = goal.CurrentAmount - goal.GoalAmount;
               goal.CurrentAmount = goal.GoalAmount;

               // Make this goal inactive.
               goal.IsActive = false;
            }
            else
            {
               // This goal has not been satisfied yet and should be flagged as the
               // active goal.
               goal.IsActive = true;
               break;
            }
         }
      }

      #endregion

      #region Private Methods

      private void CheckMilestones(GoalData goal, string userName)
      {
         if (goal != null)
         {
            double percentage = Math.Floor((goal.CurrentAmount / goal.GoalAmount) * 100);
            var sortedMilestones = goal.Milestones.OrderBy(m => m.PercentageTriggerPoint);
            foreach (var milestone in sortedMilestones)
            {
               if (!milestone.HasTriggered && milestone.PercentageTriggerPoint <= percentage)
               {
                  milestone.HasTriggered = true;
                  AnimationData animation = Bot.AnimationManager.GetAnimationByCommand(milestone.AnimationCommand);
                  if (animation != null)
                  {
                     Bot.AnimationManager.ForceQueueAnimation(animation, userName, String.Empty);
                  }
               }
            }
         }
      }

      #endregion

      #region Properties

      public GIFBot.GIFBot Bot { get; private set; }

      public string DataFilePath { get; private set; }

      public GoalBarData Data
      {
         get { return mData; }
         set {
            mData = value;
            SaveData();
         }
      }

      public const string kFileName = "gifbot_goalbar.json";

      public enum ApplySource
      {
         Cheer,
         Tip,
         Sub,
         RollOver
      }

      #endregion

      #region Private Members

      private GoalBarData mData = new GoalBarData();

      #endregion
   }
}
