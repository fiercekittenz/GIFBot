using GIFBot.Shared.Models.Tiltify;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GIFBot.Shared.Utility
{
   public class TiltifyEndpointHelpers
   {
      /// <summary>
      /// Grabs the user's information by its slug.
      /// </summary>
      public static int GetUserId(string authToken, string slug)
      {
         if (!String.IsNullOrEmpty(slug))
         {
            string url = string.Format("https://tiltify.com/api/v3/users/{0}", slug);

            try
            {
               HttpWebRequest outboundRequest = (HttpWebRequest)WebRequest.Create(url);
               if (outboundRequest != null)
               {
                  // Add the header information for oauth by application token.
                  outboundRequest.Method = "GET";
                  outboundRequest.Timeout = 12000;
                  outboundRequest.ContentType = "application/json";
                  outboundRequest.Headers.Add("Authorization", $"Bearer {authToken}");

                  using (HttpWebResponse inboundResponse = (HttpWebResponse)outboundRequest.GetResponse())
                  {
                     if (inboundResponse.StatusCode == HttpStatusCode.OK)
                     {
                        using (StreamReader stream = new StreamReader(inboundResponse.GetResponseStream()))
                        {
                           string jsonData = stream.ReadToEnd();
                           dynamic responseData = JsonConvert.DeserializeObject<object>(jsonData);
                           if (responseData != null && responseData["data"] != null)
                           {
                              if (Int32.TryParse(responseData["data"]["id"].ToString(), out int resultId))
                              {
                                 return resultId;
                              }
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
               return -1;
            }
         }

         return -1;
      }

      /// <summary>
      /// Fetches a list of campaigns available under the specified user.
      /// </summary>
      public static List<TiltifyCampaign> GetCampaigns(string authToken, int userId)
      {
         List<TiltifyCampaign> campaigns = new List<TiltifyCampaign>();

         if (userId > 0)
         {
            string url = string.Format("https://tiltify.com/api/v3/users/{0}/campaigns", userId);

            try
            {
               HttpWebRequest outboundRequest = (HttpWebRequest)WebRequest.Create(url);
               if (outboundRequest != null)
               {
                  // Add the header information for oauth by application token.
                  outboundRequest.Method = "GET";
                  outboundRequest.Timeout = 12000;
                  outboundRequest.ContentType = "application/json";
                  outboundRequest.Headers.Add("Authorization", $"Bearer {authToken}");

                  using (HttpWebResponse inboundResponse = (HttpWebResponse)outboundRequest.GetResponse())
                  {
                     if (inboundResponse.StatusCode == HttpStatusCode.OK)
                     {
                        using (StreamReader stream = new StreamReader(inboundResponse.GetResponseStream()))
                        {
                           string jsonData = stream.ReadToEnd();
                           dynamic responseData = JsonConvert.DeserializeObject<object>(jsonData);
                           if (responseData != null && responseData["data"] != null)
                           {
                              JArray campaignData = responseData["data"];
                              foreach (var campaign in campaignData)
                              {
                                 TiltifyCampaign current = new TiltifyCampaign() 
                                 {
                                    Id = (int)campaign["id"],
                                    Name = campaign["name"].ToString(),
                                    Slug = campaign["slug"].ToString()
                                 };

                                 campaigns.Add(current);
                              }
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
               return campaigns;
            }
         }

         return campaigns;
      }


      /// <summary>
      /// Gets a list of the most recent donations (last 10) to the specified campaign.
      /// </summary>
      public static List<TiltifyDonation> GetCampaignDonations(string authToken, long campaignId)
      {
         List<TiltifyDonation> donations = new List<TiltifyDonation>();

         if (campaignId > 0)
         {
            string url = string.Format("https://tiltify.com/api/v3/campaigns/{0}/donations", campaignId);

            try
            {
               HttpWebRequest outboundRequest = (HttpWebRequest)WebRequest.Create(url);
               if (outboundRequest != null)
               {
                  // Add the header information for oauth by application token.
                  outboundRequest.Method = "GET";
                  outboundRequest.Timeout = 12000;
                  outboundRequest.ContentType = "application/json";
                  outboundRequest.Headers.Add("Authorization", $"Bearer {authToken}");

                  using (HttpWebResponse inboundResponse = (HttpWebResponse)outboundRequest.GetResponse())
                  {
                     if (inboundResponse.StatusCode == HttpStatusCode.OK)
                     {
                        using (StreamReader stream = new StreamReader(inboundResponse.GetResponseStream()))
                        {
                           string jsonData = stream.ReadToEnd();
                           dynamic responseData = JsonConvert.DeserializeObject<object>(jsonData);
                           if (responseData != null && responseData["data"] != null)
                           {
                              JArray donationData = responseData["data"];
                              foreach (var donation in donationData)
                              {
                                 TiltifyDonation current = new TiltifyDonation() 
                                 {
                                    Id = (int)donation["id"],
                                    Amount = (double)donation["amount"],
                                    Name = donation["name"].ToString(),
                                    Comment = donation["comment"].ToString(),
                                    CompletedAt = (long)donation["completedAt"]
                                 };

                                 donations.Add(current);
                              }
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
               return donations;
            }
         }

         return donations;
      }
   }
}
