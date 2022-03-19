using System;
using System.Collections.Generic;
using System.Text;

namespace GIFBot.Shared.Models.Visualization
{
   public class DataEnvelope<T>
   {
      public List<T> CurrentPageData { get; set; } = new List<T>();
      public int TotalItemCount { get; set; } = 0;
   }
}
