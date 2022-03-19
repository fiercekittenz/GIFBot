using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIFBot.Shared.Models.Features
{
   public class GreeterEntry : ICloneable
   {
      public GreeterEntry()
      {
      }

      public GreeterEntry(string name)
      {
         Name = name;
      }

      public Guid Id { get; set; } = Guid.NewGuid();

      public bool Enabled { get; set; } = true;

      public string Name { get; set; } = String.Empty;

      public Guid AnimationId { get; set; } = Guid.Empty;

      public string ChatMessage { get; set; } = String.Empty;

      public List<GreetedPersonality> Recipients { get; set; } = new List<GreetedPersonality>();

      public object Clone()
      {
         return this.MemberwiseClone();
      }

      public string GetRecipientsDisplayList()
      {
         string retval = String.Empty;
         if (Recipients.Any())
         {
            retval = String.Join(",", Recipients.Select(r => r.Name).ToArray());
         }

         return retval;
      }
   }
}
