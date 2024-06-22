using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIFBot.Shared.Models.Features
{
   public class GiveawayData
   {
      // For S.T.J.
      public GiveawayData() { }

      public enum GiveawayEntryBehaviorType
      {
         Command,
         ChannelPoints
      }

      public bool IsOpenForEntries { get; set; } = false;

      public GiveawayEntryBehaviorType EntryBehavior { get; set; } = GiveawayEntryBehaviorType.Command;

      // Command Behavior Fields (1-time with sub luck)
      public string Command { get; set; } = "!giveaway";
      public int SubLuckMultiplier { get; set; } = 1;

      // Channel Point Behavior Fields (ticket-based)
      public string ChannelPointRewardTitle { get; set; } = "Giveaway";
      public int ChannelPointsRequired { get; set; } = 0;
      public int MaxNumberOfEntriesAllowed { get; set; } = 1;

      public string GiveawayOpenAnnouncementText { get; set; } = "A giveaway has started! Type $command to enter.";

      public string GiveawayClosedAnnouncementText { get; set; } = "The giveaway has been closed. No more entries are allowed!";

      public string WinnerAnnouncementText { get; set; } = "Congratulations! You are a winner, $user!";

      public Guid DrumrollAnimation { get; set; } = Guid.Empty;

      public Guid WinnerAnimation { get; set; } = Guid.Empty;

      public AnimationEnums.AccessType Access { get; set; } = AnimationEnums.AccessType.Anyone;

      public List<string> BannedUsers { get; set; } = new List<string>();

      public ObservableCollection<string> Entrants { get; set; } = new ObservableCollection<string>();

      public Guid ChannelPointRewardId { get; set; } = Guid.Empty;

      /// <summary>
      /// The version of this data. Used for advancing and converting data when loaded.
      /// </summary>
      public int Version { get; set; } = 1;

      /// <summary>
      /// Determines if a bump in versions is required. Handles converting data as upgrades are made.
      /// </summary>
      public bool BumpVersion()
      {
         // Removed "Follower" as an option for defining access to use features of GIFBot. (6-8-2024)
         const int kFollowerRemoval = 3;

         if (Version < kFollowerRemoval)
         {
            if (Access == AnimationEnums.AccessType.Follower)
            {
               Access = AnimationEnums.AccessType.Anyone;
            }

            Version = BotSettings.skCurrentBotSettingsVersion;
            return true;
         }

         return false;
      }
   }
}
