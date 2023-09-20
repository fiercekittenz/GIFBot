using System;
using System.Collections.Generic;
using System.Text;

namespace GIFBot.Shared.Models.Visualization
{
   /// <summary>
   /// Defines a single caption and its various properties.
   /// </summary>
   public class CaptionData : ICloneable
   {
      public object Clone()
      {
         return MemberwiseClone();
      }

      public string Text { get; set; } = String.Empty;

      public Location Location { get; set; } = Location.Below;

      public FontFamily FontFamily { get; set; } = FontFamily.Impact;

      public int FontSize { get; set; } = 48;

      public bool IsBold { get; set; } = false;

      public int StrokeThickness { get; set; } = 1;

      public string FontColor { get; set; } = "#FFFFFF";

      public string FontStrokeColor { get; set; } = "#000000";
   }

   public enum Location
   {
      Above,
      Below,
      Center
   }

   public enum FontFamily
   {
      ArialHelveticaSansSerif,
      ComicSans,
      Impact,
      Monospace,
      Aldrich,
      Anton,
      Barrio,
      Creepster,
      Eater,
      Galada,
      Ranchers,
      Lobster,
      Righteous,
      BenchNine,
      Oswald,
      Ultra,
      Frijole,
      Viga,
      Quantico,
      Bungee,
      Sriracha,
      Arsenal,
      Gaegu,
      VT323,
   }
}
