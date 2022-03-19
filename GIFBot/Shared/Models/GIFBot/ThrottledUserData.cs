using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIFBot.Shared.Models.GIFBot
{
   public class ThrottledUserData
   {
      public string Name { get; set; } = String.Empty;

      [Range(0, 999999)]
      public int PersonalThrottleRate { get; set; } = 0; // In seconds

      public DateTime LastThrottled { get; set; } = DateTime.Now.AddDays(-10);

      public bool IsBanned { get; set; } = false;
   }
}
