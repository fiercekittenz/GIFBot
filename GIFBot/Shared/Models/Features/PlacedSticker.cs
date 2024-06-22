using GIFBot.Shared.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIFBot.Shared.Models.Features
{
   public class PlacedSticker : PlacedVisualBase
   {
      // For S.T.J.
      public PlacedSticker() { }

      public StickerEntryData Data { get; set; }
      public DateTime TimePlaced { get; set; }
      public int IntHeight { get; set; }
      public int IntWidth { get; set; }
   }
}
