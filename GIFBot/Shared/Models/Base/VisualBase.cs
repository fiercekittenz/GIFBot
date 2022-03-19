using GIFBot.Shared.Models.Animation;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIFBot.Shared.Models.Base
{
   public class VisualBase
   {
      public Guid Id { get; set; }
      public string Visual { get; set; }
      public AnimationPlacement Placement { get; set; } = new AnimationPlacement();
      public AnimationEnums.AnimationLayer Layer { get; set; } = AnimationEnums.AnimationLayer.Primary;
   }
}
