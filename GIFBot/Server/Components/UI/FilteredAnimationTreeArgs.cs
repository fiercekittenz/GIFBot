using global::GIFBot.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GIFBot.Server.Components.UI
{
   public class FilteredAnimationTreeArgs
   {
      public FilteredAnimationTreeArgs(AnimationTreeItem selected)
      {
         SelectedItem = selected;
      }

      public AnimationTreeItem SelectedItem { get; private set; }
   }
}
