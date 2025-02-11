using GIFBot.Shared.Models.Animation;
using GIFBot.Shared.Models.Base;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIFBot.Shared.Models.Visualization
{
   public class TestVisualRequest
   {
      // For S.T.J.
      public TestVisualRequest() { }

      public TestVisualRequest(string visual, bool mirrored, AnimationPlacement placement, AnimationEnums.AnimationLayer layer)
      {
         Visual = visual;
         IsMirrored = mirrored;
         Placement = placement;
         Layer = layer;
      }

      public string Visual { get; set; } = String.Empty;

      public bool IsMirrored { get; set; } = false;

      public AnimationPlacement Placement { get; set; } = new AnimationPlacement();

      public AnimationEnums.AnimationLayer Layer { get; set; } = AnimationEnums.AnimationLayer.Primary;
   }
}
