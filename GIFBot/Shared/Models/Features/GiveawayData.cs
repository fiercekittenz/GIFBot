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
   }
}
