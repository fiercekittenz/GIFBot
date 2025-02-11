using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIFBot.Shared.Models.Tiltify
{
   public class TiltifyDonation
   {
      // For S.T.J.
      public TiltifyDonation() { }

      public string Id { get; set; } = string.Empty;
      public double Amount { get; set; } = 0.0f;
      public string Name { get; set; } = string.Empty;
      public string Comment { get; set; } = string.Empty;
      public string CompletedAt { get; set; } = string.Empty;

      public DateTime GetCompletedAtAsDateTime()
      {
         return DateTime.Parse(CompletedAt, null, System.Globalization.DateTimeStyles.RoundtripKind);
      }
   }
}
