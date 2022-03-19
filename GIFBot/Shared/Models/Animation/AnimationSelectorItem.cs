using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIFBot.Shared.Models.Animation
{
   public class AnimationSelectorItem
   {
      public Guid Id { get; set; } = Guid.Empty;

      public string DisplayName { get; set; }
   }
}
