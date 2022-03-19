using GIFBot.Server.Interfaces;
using GIFBot.Shared.Models.Features;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Client.Events;
using static GIFBot.Shared.Utility.Enumerations;

namespace GIFBot.Server.Features.CountdownTimer
{
   public class CountdownTimerManager : IFeatureManager
   {
      #region Public Methods

      /// <summary>
      /// Constructor
      /// </summary>
      public CountdownTimerManager(GIFBot.GIFBot bot, string dataFilePath)
      {
         Bot = bot;
         DataFilePath = dataFilePath;
      }

      public async Task Start()
      {
         mProcessorCancellationTokenSource = new CancellationTokenSource();

         try
         {
            Task processor = ProcessTimer(mProcessorCancellationTokenSource.Token);
            await processor;
         }
         catch (TaskCanceledException)
         {
            // Do Nothing.
         }
      }

      public void Stop()
      {
         mProcessorCancellationTokenSource?.Cancel();
      }

      /// <summary>
      /// Loads the data.
      /// </summary>
      public void LoadData()
      {
         if (!String.IsNullOrEmpty(DataFilePath) && File.Exists(DataFilePath))
         {
            string fileData = File.ReadAllText(DataFilePath);
            mData = JsonConvert.DeserializeObject<CountdownTimerData>(fileData);

            if (mData.Enabled)
            {
               _ = Bot?.SendLogMessage("Countdown Timer data loaded and enabled.");
            }
            else
            {
               _ = Bot?.SendLogMessage("Countdown Timer data loaded and is not currently enabled.");
            }
         }
         else if (!File.Exists(DataFilePath))
         {
            // This will set initial timespan values and save the data to create the data file.
            ResetTimer();
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

            _ = Bot?.SendLogMessage("Countdown Timer data saved.");
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
             mData.Actions.Where(a => a.Enabled && a.RedemptionType == CostRedemptionType.Cheer).Any())
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
         if (mData != null && mData.Enabled)
         {
            var qualifyingAction = mData.Actions.FirstOrDefault(a => a.Enabled && a.RedemptionType == CostRedemptionType.Cheer && a.Cost == message.ChatMessage.Bits);
            if (qualifyingAction != null)
            {
               ApplyAction(qualifyingAction);
            }
         }
      }

      public void HandleTimerEvent(double cost, CostRedemptionType redemptionType)
      {
         var qualifyingAction = mData.Actions.FirstOrDefault(a => a.Enabled && a.RedemptionType == redemptionType && a.Cost == cost);
         if (qualifyingAction != null)
         {
            ApplyAction(qualifyingAction);
         }
      }

      public void HandleTimerSubscriptionEvent(TwitchLib.Client.Enums.SubscriptionPlan subTier)
      {
         var qualifyingAction = mData.Actions.FirstOrDefault(a => a.Enabled && a.RedemptionType == CostRedemptionType.Subscription && 
                                                                                (a.SubscriptionTierRequired == subTier ||
                                                                                 a.SubscriptionTierRequired == TwitchLib.Client.Enums.SubscriptionPlan.NotSet));
         if (qualifyingAction != null)
         {
            ApplyAction(qualifyingAction);
         }
      }

      /// <summary>
      /// Starts the timer by applying the start minutes value to the current DateTime.
      /// </summary>
      public void StartTimer()
      {
         mTicking = true;
      }

      /// <summary>
      /// Stops the timer by toggling off the ticking flag and saving the data so that the
      /// current time is properly persisted.
      /// </summary>
      public void StopTimer()
      {
         mTicking = false;
         SendTimerValueToClient();

         SaveData();
      }

      public void ResetTimer()
      {
         mTicking = false;
         mIsTrackingPauseTime = false;
         mData.Current = TimeSpan.FromMinutes(mData.TimerStartValueInMinutes);

         SendTimerValueToClient();

         SaveData();
      }

      public void PauseTimer(CountdownTimerAction action = null)
      {
         mIsTrackingPauseTime = false;
         mTicking = false;

         if (action != null && action.Behavior == CountdownTimerActionBehavior.PauseTime)
         {
            mIsTrackingPauseTime = true;
            mLastPausedTimer = DateTime.Now;
            mSecondsToPause = action.SecondsValue;
         }

         SendTimerValueToClient();

         SaveData();
      }

      public void HideTimer()
      {
         IHubClients clients = Bot.GIFBotHub.Clients;
         _ = clients.All.SendAsync("HideTimer");
      }

      /// <summary>
      /// Handles the provided action by executing on making changes to the countdown timer.
      /// </summary>
      public void ApplyAction(CountdownTimerAction action)
      {
         if (action != null &&
             action.Enabled &&
             mData.Enabled &&
             (mTicking || (!mTicking && mIsTrackingPauseTime)))
         {
            if (!String.IsNullOrEmpty(action.Animation))
            {
               var animation = Bot.AnimationManager.GetAnimationByCommand(action.Animation);
               if (animation != null)
               {
                  Bot.AnimationManager.ForceQueueAnimation(animation, String.Empty, String.Empty);
               }
            }

            switch (action.Behavior)
            {
            case CountdownTimerActionBehavior.AddTime:
               {
                  mData.Current = mData.Current.Add(TimeSpan.FromSeconds(action.SecondsValue));
               }
               break;

            case CountdownTimerActionBehavior.RemoveTime:
               {
                  mData.Current = mData.Current.Subtract(TimeSpan.FromSeconds(action.SecondsValue));
               }
               break;

            case CountdownTimerActionBehavior.SlowTime:
               {
                  mTimerTickValue = 750;
                  mTimeToRevertToNormal = DateTime.Now.AddSeconds(action.SecondsValue);
               }
               break;

            case CountdownTimerActionBehavior.SpeedUpTime:
               {
                  mTimerTickValue = 1500;
                  mTimeToRevertToNormal = DateTime.Now.AddSeconds(action.SecondsValue);
               }
               break;

            case CountdownTimerActionBehavior.PauseTime:
               {
                  PauseTimer(action);
               }
               break;
            }
         }
      }

      #endregion

      #region Private Methods

      private Task ProcessTimer(CancellationToken cancellationToken)
      {
         Task task = null;

         task = Task.Run(async () =>
         {
            while (true)
            {
               if (mData.Enabled)
               {
                  bool forceSave = false;

                  if (mTicking)
                  {
                     mData.Current = mData.Current.Subtract(TimeSpan.FromMilliseconds(mTimerTickValue));
                     if (mData.Current <= TimeSpan.Zero)
                     {
                        forceSave = true;
                        mTicking = false;
                        mData.Current = TimeSpan.Zero;
                     }

                     SendTimerValueToClient();

                     if (mTimeToRevertToNormal < DateTime.Now)
                     {
                        mTimerTickValue = 1000;
                     }

                     if (forceSave || DateTime.Now.Subtract(mLastSavedOnProcessTick).TotalMinutes > skMinutesBetweenSaves)
                     {
                        mLastSavedOnProcessTick = DateTime.Now;
                        SaveData();
                     }
                  }
                  else
                  {
                     if (mIsTrackingPauseTime && DateTime.Now.Subtract(mLastPausedTimer).TotalSeconds > mSecondsToPause)
                     {
                        mIsTrackingPauseTime = false;
                        mTicking = true;
                     }
                  }
               }

               if (cancellationToken.IsCancellationRequested)
               {
                  throw new TaskCanceledException(task);
               }

               await Task.Delay(1000);
            }
         });

         return task;
      }

      private void SendTimerValueToClient()
      {
         mData.Caption.Text = $"{mData.Current.ToString(@"hh\:mm\:ss")}";

         IHubClients clients = Bot.GIFBotHub.Clients;
         clients.All.SendAsync("UpdateTime", JsonConvert.SerializeObject(mData.Caption));
      }

      #endregion

      #region Properties

      public GIFBot.GIFBot Bot { get; private set; }

      public string DataFilePath { get; private set; }

      public CountdownTimerData Data
      {
         get { return mData; }
         set {
            mData = value;
            SaveData();
         }
      }

      public const string kFileName = "gifbot_countdowntimer.json";

      #endregion

      #region Private Members

      private CountdownTimerData mData = new CountdownTimerData();

      private CancellationTokenSource mProcessorCancellationTokenSource;

      private DateTime mLastSavedOnProcessTick = DateTime.Now.AddDays(-1);

      private DateTime mLastPausedTimer = DateTime.Now.AddDays(-1);

      private DateTime mTimeToRevertToNormal = DateTime.Now.AddDays(-1);

      /// <summary>
      /// The number of milliseconds by which the process method ticks.
      /// </summary>
      private int mTimerTickValue = 1000; // Default: 1 second

      /// <summary>
      /// The number of minutes to pause, grabbed from the action that caused the pause.
      /// </summary>
      private int mSecondsToPause = 0;

      /// <summary>
      /// Indicates if the timer is currently running.
      /// </summary>
      private bool mTicking = false;

      private bool mIsTrackingPauseTime = false;

      private const int skMinutesBetweenSaves = 1;

      #endregion
   }
}
