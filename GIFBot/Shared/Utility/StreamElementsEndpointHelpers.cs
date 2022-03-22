using GIFBot.Shared.Models.StreamElements;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GIFBot.Shared.Utility
{
   public class StreamElementsEndpointHelpers
   {
      /// <summary>
      /// Grabs the user's information by its slug.
      /// </summary>
      public static string GetChannelId(string token, string channelName)
      {
         if (!String.IsNullOrEmpty(token))
         {
            string url = $"https://api.streamelements.com/kappa/v2/channels/{channelName}";

            try
            {
               HttpWebRequest outboundRequest = (HttpWebRequest)WebRequest.Create(url);
               if (outboundRequest != null)
               {
                  // Add the header information for oauth by application token.
                  outboundRequest.Method = "GET";
                  outboundRequest.Timeout = 12000;
                  outboundRequest.ContentType = "application/json";
                  outboundRequest.Headers.Add("Authorization", $"Bearer {token}");

                  using (HttpWebResponse inboundResponse = (HttpWebResponse)outboundRequest.GetResponse())
                  {
                     if (inboundResponse.StatusCode == HttpStatusCode.OK)
                     {
                        using (StreamReader stream = new StreamReader(inboundResponse.GetResponseStream()))
                        {
                           string jsonData = stream.ReadToEnd();
                           dynamic responseData = JsonConvert.DeserializeObject<object>(jsonData);
                           if (responseData != null && responseData["_id"] != null)
                           {
                              return responseData["_id"];
                           }
                        }
                     }
                  }
               }
            }
            catch (Exception /*e*/)
            {
               // This exception occurs even on a valid response with a 404. It's the 404 we are looking for here.
               // Do not log, because it is misleading.
               return String.Empty;
            }
         }

         return String.Empty;
      }
      /// <summary>
      /// Pulls the latest tips from the user's channel data on StreamElements.
      /// </summary>
      public static List<StreamElementsTipData> GetTips(string token, string channelId)
      {
         if (!String.IsNullOrEmpty(token))
         {
            string url = $"https://api.streamelements.com/kappa/v2/tips/{channelId}?sort=-createdAt&limit=10";

            try
            {
               HttpWebRequest outboundRequest = (HttpWebRequest)WebRequest.Create(url);
               if (outboundRequest != null)
               {
                  // Add the header information for oauth by application token.
                  outboundRequest.Method = "GET";
                  outboundRequest.Timeout = 12000;
                  outboundRequest.ContentType = "application/json";
                  outboundRequest.Headers.Add("Authorization", $"Bearer {token}");

                  using (HttpWebResponse inboundResponse = (HttpWebResponse)outboundRequest.GetResponse())
                  {
                     if (inboundResponse.StatusCode == HttpStatusCode.OK)
                     {
                        using (StreamReader stream = new StreamReader(inboundResponse.GetResponseStream()))
                        {
                           string jsonData = stream.ReadToEnd();
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
                  }
               }
            }
            catch (Exception /*e*/)
            {
               // This exception occurs even on a valid response with a 404. It's the 404 we are looking for here.
               // Do not log, because it is misleading.
               return new List<StreamElementsTipData>();
            }
         }

         return new List<StreamElementsTipData>();
      }
   }
}
