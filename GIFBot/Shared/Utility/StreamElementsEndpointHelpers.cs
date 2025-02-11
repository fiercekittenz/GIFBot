using GIFBot.Shared.Models.StreamElements;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace GIFBot.Shared.Utility
{
   public class StreamElementsEndpointHelpers
   {
      /// <summary>
      /// Grabs the user's information by its slug.
      /// </summary>
      public static string GetChannelId(HttpClient client, string token, string channelName)
      {
         if (!String.IsNullOrEmpty(token))
         {
            string url = $"https://api.streamelements.com/kappa/v2/channels/{channelName}";

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {token}");

            HttpResponseMessage response = client.Send(request);
            if (response.IsSuccessStatusCode)
            {
               string jsonData = response.Content.ReadAsStringAsync().Result;
               JsonObject responseData = JsonSerializer.Deserialize<JsonObject>(jsonData);
               if (responseData != null && responseData["_id"] != null)
               {
                  return responseData["_id"].ToString();
               }
            }
         }

         return String.Empty;
      }
      /// <summary>
      /// Pulls the latest tips from the user's channel data on StreamElements.
      /// </summary>
      public static List<StreamElementsTipData> GetTips(HttpClient client, string token, string channelId)
      {
         if (!String.IsNullOrEmpty(token))
         {
            string url = $"https://api.streamelements.com/kappa/v2/tips/{channelId}?sort=-createdAt&limit=10";

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {token}");

            HttpResponseMessage response = client.Send(request);
            if (response.IsSuccessStatusCode)
            {
               string jsonData = response.Content.ReadAsStringAsync().Result;
               JsonObject responseData = JsonSerializer.Deserialize<JsonObject>(jsonData);
               if (responseData != null && responseData["docs"] != null)
               {
                  List<StreamElementsTipData> tips = new List<StreamElementsTipData>();

                  foreach (var rawTipData in responseData["docs"].AsArray())
                  {
                     StreamElementsTipData tip = new StreamElementsTipData() {
                        Id = rawTipData["_id"].ToString(),
                        TipperName = rawTipData["donation"]["user"]["username"].ToString(),
                        Message = rawTipData["donation"]["message"].ToString(),
                        Amount = Double.Parse(rawTipData["donation"]["amount"].ToString()),
                        TimeTipped = DateTime.Parse(rawTipData["createdAt"].ToString())
                     };

                     tips.Add(tip);
                  }

                  return tips;
               }
            }
         }

         return new List<StreamElementsTipData>();
      }
   }
}
