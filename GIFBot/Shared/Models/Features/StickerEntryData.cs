using GIFBot.Shared.Models.Animation;
using GIFBot.Shared.Models.Base;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace GIFBot.Shared.Models.Features
{
   public class StickerEntryData : VisualBase, ICloneable
   {
      public StickerEntryData()
      {
         Id = Guid.NewGuid();
      }

      public object Clone()
      {
         return this.MemberwiseClone();
      }

      public bool Enabled { get; set; } = true;
      public string Name { get; set; } = String.Empty;

      /// <summary>
      /// Allows this sticker to be randomly placed by events.
      /// </summary>
      public bool AllowRandomPlacement { get; set; } = true;

      //
      // Per-Sticker Behavioral Flags (Only considered if random placement is NOT allowed)
      //

      public bool IncludeBits { get; set; } = false;

      [Range(0, 999999)]
      public int BitAmount { get; set; } = 0;

      public bool IncludeTips { get; set; } = false;

      [Range(0, 999999)]
      public double TipAmount { get; set; } = 0;

      public bool IncludeTiltifyDonations { get; set; } = false;

      [Range(0, 999999)]
      public double TiltifyDonationAmount { get; set; } = 0;

      /// <summary>
      /// When the placement is overridden, the permanent placement is used instead of randomly placing
      /// the sticker on the canvas. Placement override only works with command placement.
      /// </summary>
      public bool UsePlacementOverride { get; set; } = false;

      /// <summary>
      /// When the timeout is overridden, it'll use this instead of the global behavioral one. Only
      /// works for stickers that do not allow random placement.
      /// </summary>
      public bool UseVisibilityTimeoutOverride { get; set; } = false;
      public int NumSecondsStickerVisibleOverride { get; set; } = 0;

      /// <summary>
      /// Denotes if the sticker entry is selected in the editor or not.
      /// </summary>
      [JsonIgnore]
      public bool IsSelected { get; set; } = false;
   }
}
