using System;
using System.Collections.Generic;
using System.Text;

namespace GIFBot.Shared.Models.GIFBot
{
   public class UserGroup
   {
      // For S.T.J.
      public UserGroup() { }

      public UserGroup(string name)
      {
         Id = Guid.NewGuid();
         Name = name;
      }

      public Guid Id { get; set; } = Guid.Empty;
      public string Name { get; set; } = String.Empty;
      public List<UserEntry> UserEntries { get; set; } = new List<UserEntry>();

      // DEPRECATED
      [System.Text.Json.Serialization.JsonIgnore]
      public List<string> Users { get; set; } = new List<string>();
   }

   public class UserEntry
   {
      public UserEntry()
      {
      }

      public UserEntry(string name)
      {
         Name = name;
      }

      public string Name { get; set; } = String.Empty;
   }
}
