using System;
using System.Collections.Generic;
using System.Text;

namespace GIFBot.Shared.Models.Twitch
{
   // Used for the user list manager.
   public class TwitchUserViewModel
   {
      public TwitchUserViewModel(string name)
      {
         Name = name;
      }

      public string Name { get; set; } = String.Empty;
   }

   public class TwitchGetUserResponse
   {
      public TwitchUserData[] data { get; set; } = null;
   }

   public class TwitchUserData
   {
      public string id { get; set; } = String.Empty;
      public string login { get; set; } = String.Empty;
      public string display_name { get; set; } = String.Empty;
      public string type { get; set; } = String.Empty;
      public string broadcaster_type { get; set; } = String.Empty;
      public string description { get; set; } = String.Empty;
      public string profile_image_url { get; set; } = String.Empty;
      public string offline_image_url { get; set; } = String.Empty;
      public string view_count { get; set; } = String.Empty;
      public string email { get; set; } = String.Empty;
   }
}
