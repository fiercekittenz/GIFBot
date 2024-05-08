using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIFBot.Shared.Models.Tiltify
{
   public class TiltifyDonation
   {
      public string Id { get; set; }
      public double Amount { get; set; }
      public string Name { get; set; }
      public string Comment { get; set; }
      public string CompletedAt { get; set; }

      public DateTime GetCompletedAtAsDateTime()
      {
         return DateTime.Parse(CompletedAt, null, System.Globalization.DateTimeStyles.RoundtripKind);
      }
   }
}
