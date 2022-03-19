using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIFBot.Shared.Models.Features
{
   public class GreetedPersonality
   {
      public GreetedPersonality()
      {
      }

      public GreetedPersonality(string name)
      {
         Name = name;
      }

      public string Name { get; set; } = String.Empty;

      public DateTime LastGreeted { get; set; } = DateTime.MinValue;
   }
}
