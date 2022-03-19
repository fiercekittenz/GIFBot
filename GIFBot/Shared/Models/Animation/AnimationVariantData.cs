using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json.Serialization;

namespace GIFBot.Shared.Models.Animation
{
   /// <summary>
   /// A stub of animation data specifically for variants of a parent animation.
   /// They will take all of the placement data of the original, but only override
   /// the visual, audio, volume, and duration settings.
   /// </summary>
   public class AnimationVariantData : ICloneable
   {
      public AnimationVariantData()
      {
         Id = Guid.NewGuid();
      }

      /// <summary>
      /// ICloneable Implementation
      /// </summary>
      public object Clone()
      {
         return MemberwiseClone();
      }

      public Guid Id { get; set; }

      public string Visual { get; set; }

      public string Audio { get; set; }

      [Range(0.0, 1.0)]
      public double Volume { get; set; } = 0.5;

      [Range(1000, 600000)]
      public int DurationMilliseconds { get; set; } = 2000;

      [Range(0, 600000)]
      public int AudioTimingOffsetMilliseconds { get; set; } = 0;

      public string PrePlayText { get; set; } = String.Empty;

      public string PostPlayText { get; set; } = String.Empty;

      public bool HasPlayedOnce { get; set; } = false;
   }
}
