using System;
using System.Collections.Generic;
using System.Text;

namespace GIFBot.Shared.Models.Animation
{
   public class AnimationPlacement
   {
      public AnimationPlacement()
      {
      }

      public AnimationPlacement(AnimationPlacement other)
      {
         Width = other.Width;
         Height = other.Height;
         Top = other.Top;
         Left = other.Left;
         IsOutOfBounds = other.IsOutOfBounds;
      }

      public int Width { get; set; } = 0;

      public int Height { get; set; } = 0;

      public int Top { get; set; } = 0;

      public int Left { get; set; } = 0;

      public bool IsOutOfBounds { get; set; } = false;
   }
}
