using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIFBot.Shared.Models.StreamElements
{
   public class StreamElementsTipData
   {
      public string Id { get; set; } = String.Empty;

      public string TipperName { get; set; } = String.Empty;

      public string Message { get; set; } = String.Empty;

      public DateTime TimeTipped { get; set; } = DateTime.Now;

      public double Amount { get; set; } = 0.0;
   }
}
