using GIFBot.Shared.Models.Animation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static GIFBot.Shared.Utility.Enumerations;

namespace GIFBot.Shared.Models.Features
{
   public class BackdropData
   {
      // For S.T.J.
      public BackdropData() { }

      public bool Enabled { get; set; } = false;

      public string Command { get; set; } = "!backdrop";

      public ObservableCollection<BackdropVideoEntryData> Backdrops { get; set; } = new ObservableCollection<BackdropVideoEntryData>();

      public CostRedemptionType RedemptionType { get; set; } = CostRedemptionType.ChannelPoints;

      public int Cost { get; set; } = 0;

      public int MinimumMinutesActive { get; set; } = 5;
   }

   public class BackdropVideoEntryData : ICloneable
   {
      public BackdropVideoEntryData()
      {
         Id = Guid.NewGuid();
      }

      public object Clone()
      {
         return this.MemberwiseClone();
      }

      public Guid Id { get; set; } = Guid.Empty;

      public string Name { get; set; } = String.Empty;

      public string Visual { get; set; } = String.Empty;    
      
      public bool Enabled { get; set; } = true;

      public bool PlayedOnce { get; set; } = false;
   }
}
