using System;
using System.Collections.Generic;
using System.Text;

namespace GIFBot.Shared.Models.Twitch
{
   public class TwitchHypeTrainEvent
   {
      public Data[] data { get; set; }
      public Pagination pagination { get; set; }
   }

   public class Pagination
   {
      public string cursor { get; set; }
   }

   public class Data
   {
      public string id { get; set; }
      public string event_type { get; set; }
      public DateTime event_timestamp { get; set; }
      public string version { get; set; }
      public Event_Data event_data { get; set; }
   }

   public class Event_Data
   {
      public string broadcaster_id { get; set; }
      public string cooldown_end_time { get; set; }
      public string expires_at { get; set; }
      public int goal { get; set; }
      public string id { get; set; }
      public Last_Contribution last_contribution { get; set; }
      public int level { get; set; }
      public string started_at { get; set; }
      public Top_Contributions[] top_contributions { get; set; }
      public int total { get; set; }
   }

   public class Last_Contribution
   {
      public int total { get; set; }
      public string type { get; set; }
      public string user { get; set; }
   }

   public class Top_Contributions
   {
      public int total { get; set; }
      public string type { get; set; }
      public string user { get; set; }
   }
}
