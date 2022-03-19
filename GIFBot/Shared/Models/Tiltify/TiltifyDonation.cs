using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIFBot.Shared.Models.Tiltify
{
   public class TiltifyDonation
   {
      public int Id { get; set; }
      public double Amount { get; set; }
      public string Name { get; set; }
      public string Comment { get; set; }
      public long CompletedAt { get; set; }
   }
}
