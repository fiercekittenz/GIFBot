using GIFBot.Shared.Models.Animation;
using GIFBot.Shared.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIFBot.Shared.Models.Visualization
{
   public class TestVisualRequest
   {
      public TestVisualRequest(string visual, AnimationPlacement placement, AnimationEnums.AnimationLayer layer)
      {
         Visual = visual;
         Placement = placement;
         Layer = layer;
      }

      public string Visual { get; set; } = String.Empty;

      public AnimationPlacement Placement { get; set; } = new AnimationPlacement();

      public AnimationEnums.AnimationLayer Layer { get; set; } = AnimationEnums.AnimationLayer.Primary;
   }
}
