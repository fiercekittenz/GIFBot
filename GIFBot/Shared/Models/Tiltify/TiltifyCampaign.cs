using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIFBot.Shared.Models.Tiltify
{
   public class TiltifyCampaign
   {
      // For S.T.J.
      public TiltifyCampaign() { }

      public string Id { get; set; } = string.Empty; // in v5 Tiltify made this a guid
      public string Name { get; set; } = string.Empty;
      public string Slug { get; set; } = string.Empty;
   }
}
