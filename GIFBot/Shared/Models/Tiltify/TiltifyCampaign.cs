using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIFBot.Shared.Models.Tiltify
{
   public class TiltifyCampaign
   {
      public string Id { get; set; } // in v5 Tiltify made this a guid
      public string Name { get; set; }
      public string Slug { get; set; }
   }
}
