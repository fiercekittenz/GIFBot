using GIFBot.Shared.Models.Visualization;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TwitchLib.Client.Enums;
using static GIFBot.Shared.Utility.Enumerations;

namespace GIFBot.Shared.Models.Features
{
   public class CountdownTimerData
   {
      #region Properties

      public bool Enabled { get; set; } = false;

      public int TimerStartValueInMinutes { get; set; } = 60;

      public TimeSpan Current { get; set; } = TimeSpan.Zero;

      public CaptionData Caption { get; set; } = new CaptionData();

      public ObservableCollection<CountdownTimerAction> Actions 
      {
         get;
         set;
      } = new ObservableCollection<CountdownTimerAction>();

      #endregion
   }

   #region Enums

   public enum CountdownTimerActionBehavior
   {
      RemoveTime,
      AddTime,
      SlowTime,
      SpeedUpTime,
      PauseTime,
   }

   public enum CountdownTimerSpeedType
   {
      //////////////////////////////////////////////
      // Slow Down
      //////////////////////////////////////////////
      Sloth,      // 3500ms
      Slug,       // 2500ms
      Turtle,     // 1500ms

      //////////////////////////////////////////////
      // Speed Up
      //////////////////////////////////////////////
      Rabbit,     // 750ms
      Ostrich,    // 500ms
      Sonic,      // 350ms
   }

   public enum CountdownRedemptionType
   {
      Cheer,
      ChannelPoints,
      Tiltify,
      Tip
   }

   #endregion

   #region Helper Classes

   public class CountdownTimerAction : ICloneable
   {
      public CountdownTimerAction()
      {
         Id = Guid.NewGuid();
      }

      public object Clone()
      {
         return this.MemberwiseClone();
      }

      public bool Enabled { get; set; } = true;

      public Guid Id { get; set; } = Guid.Empty;

      public string Name { get; set; } = String.Empty;

      public string Animation { get; set; } = String.Empty;

      public CountdownTimerActionBehavior Behavior { get; set; } = CountdownTimerActionBehavior.AddTime;

      public CountdownTimerSpeedType SpeedType { get; set; } = CountdownTimerSpeedType.Sloth;

      // Seconds for Add/Remove Time. Seconds for Time to SpeedUp, SlowDown, or Pause.
      public int SecondsValue { get; set; } = 0;

      public CostRedemptionType RedemptionType { get; set; } = CostRedemptionType.ChannelPoints;

      // Based on the redemption type, we will either require a flat cost, or 
      // a corresponding subscription tier.
      public double Cost { get; set; } = 0.0f;
      public SubscriptionPlan SubscriptionTierRequired { get; set; } = 0;
   }

   #endregion
}
