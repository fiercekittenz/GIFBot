using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GIFBot.Client.Components
{
   public class PlacementData
   {
      public int Width { get; set; }
      public int Height { get; set; }
      public int Top { get; set; }
      public int Left { get; set; }

      public bool IsOutOfBounds { get; set; }
   }
}
