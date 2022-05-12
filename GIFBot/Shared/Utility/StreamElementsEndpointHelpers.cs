using GIFBot.Shared.Models.StreamElements;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
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
               dynamic responseData = JsonConvert.DeserializeObject<object>(jsonData);
               if (responseData != null && responseData["_id"] != null)
               {
                  return responseData["_id"];
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
               dynamic responseData = JsonConvert.DeserializeObject<object>(jsonData);
               if (responseData != null && responseData["docs"] != null)
               {
                  List<StreamElementsTipData> tips = new List<StreamElementsTipData>();

                  foreach (var rawTipData in responseData["docs"])
                  {
                     StreamElementsTipData tip = new StreamElementsTipData() {
                        Id = rawTipData["_id"],
                        TipperName = rawTipData["donation"]["user"]["username"],
                        Message = rawTipData["donation"]["message"],
                        Amount = rawTipData["donation"]["amount"],
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
