using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIFBot.Shared.Models.Features
{
   public class SnapperData
   {
      public bool Enabled { get; set; } = false;

      /// <summary>
      /// Note that to include moderators, the bot's account must be the broadcaster's account for this to work.
      /// Moderators cannot time out other moderators.
      /// </summary>
      public bool IncludeMods { get; set; } = false;
      public string ImmuneModerator { get; set; } = String.Empty;

      public List<SnapperCommand> Commands { get; set; } = new List<SnapperCommand>();
   }

   #region Snapper Command Class

   public class SnapperCommand : ICloneable
   {
      public SnapperCommand()
      {
         Id = Guid.NewGuid();
      }

      public object Clone()
      {
         return this.MemberwiseClone();
      }

      public Guid Id { get; set; } = Guid.Empty;

      public bool Enabled { get; set; } = true;

      // Do not allow spaces in the command name.
      public string Command { get; set; } = String.Empty;

      public SnapperRedemptionType RedemptionType { get; set; } = SnapperRedemptionType.Cheer;

      public SnapperBehaviorType BehaviorType { get; set; } = SnapperBehaviorType.SpecificViewer;

      [Range(1, 999999)]
      public int Cost { get; set; } = 0;

      // Timeout Value Range or Value
      // Users can choose to have a command execute with a timeout range or a specific value.
      // Values are in seconds per the Twitch command.
      public bool TimeoutValueUsesRange { get; set; } = false;

      [Range(1, 999999)]
      public int TimeoutValueMin { get; set; } = 60;

      [Range(1, 999999)]
      public int TimeoutValueMax { get; set; } = 600;

      // Timeout Damage Range or Value
      // Only available for the Thanos behavior option. The others default to either the cached user or the 
      // specific viewer. Represents a percent value of people to time out. Range is 1 to 100.
      public bool TimeoutDamageUsesRange { get; set; } = false;

      [Range(1, 100)]
      public int TimeoutDamageMin { get; set; } = 1;

      [Range(1,100)]
      public int TimeoutDamageMax { get; set; } = 100;

      public Guid PreAnimationId { get; set; } = Guid.Empty;

      public Guid PostAnimationId { get; set; } = Guid.Empty;

      public bool AlsoTimesOutThanos { get; set; } = false;

      public string SnapPhrase { get; set; } = String.Empty;

      public string ThanosPreAnnouncement { get; set; } = String.Empty;

      public bool OnlyTimeoutOverrideVictim { get; set; } = false;

      public string OverrideVictim { get; set; } = String.Empty;
   }

   #endregion

   #region Enumerations

   public enum SnapperRedemptionType
   {
      Cheer,
      ChannelPoints,
      Tiltify,
      Tip
   }

   public enum SnapperBehaviorType
   {
      SpecificViewer,            /* Reward redeemer chooses who to time out; if no viewer is named, it'll randomly select someone in chat */
      Revenge,                   /* Reward redeemer chooses to time out the person who last performed a timeout */
      Thanos,                    /* Reward redeemer times out 50% of viewers */
      Self,                      /* Reward redeemer snaps self out */
      ModOnly,                   /* Reward redeemer snaps out a random mod */
   }

   #endregion
}
