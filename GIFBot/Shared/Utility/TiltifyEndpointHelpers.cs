using GIFBot.Shared.Models.Tiltify;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
   public class TiltifyEndpointHelpers
   {
      /// <summary>
      /// Grabs the user's information by its slug.
      /// </summary>
      public static int GetUserId(HttpClient client, string authToken, string slug)
      {
         if (!String.IsNullOrEmpty(slug))
         {
            string url = string.Format("https://v5api.tiltify.com/api/public/users/by/slug/{0}", slug);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {authToken}");

            HttpResponseMessage response = client.Send(request);
            if (response.IsSuccessStatusCode)
            {
               string jsonData = response.Content.ReadAsStringAsync().Result;
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

         return -1;
      }

      /// <summary>
      /// Fetches a list of campaigns available under the specified user.
      /// </summary>
      public static List<TiltifyCampaign> GetCampaigns(HttpClient client, string authToken, int userId)
      {
         List<TiltifyCampaign> campaigns = new List<TiltifyCampaign>();

         if (userId > 0)
         {
            string url = string.Format("https://v5api.tiltify.com/api/public/users/{0}/campaigns", userId);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {authToken}");

            HttpResponseMessage response = client.Send(request);
            if (response.IsSuccessStatusCode)
            {
               string jsonData = response.Content.ReadAsStringAsync().Result;
               dynamic responseData = JsonConvert.DeserializeObject<object>(jsonData);
               if (responseData != null && responseData["data"] != null)
               {
                  JArray campaignData = responseData["data"];
                  foreach (var campaign in campaignData)
                  {
                     TiltifyCampaign current = new TiltifyCampaign() {
                        Id = (int)campaign["id"],
                        Name = campaign["name"].ToString(),
                        Slug = campaign["slug"].ToString()
                     };

                     campaigns.Add(current);
                  }
               }
            }
         }

         return campaigns;
      }


      /// <summary>
      /// Gets a list of the most recent donations (last 10) to the specified campaign.
      /// </summary>
      public static List<TiltifyDonation> GetCampaignDonations(HttpClient client, string authToken, long campaignId)
      {
         List<TiltifyDonation> donations = new List<TiltifyDonation>();

         if (campaignId > 0)
         {
            string url = string.Format("https://v5api.tiltify.com/api/campaigns/{0}/donations", campaignId);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {authToken}");

            HttpResponseMessage response = client.Send(request);
            if (response.IsSuccessStatusCode)
            {
               string jsonData = response.Content.ReadAsStringAsync().Result;
               dynamic responseData = JsonConvert.DeserializeObject<object>(jsonData);
               if (responseData != null && responseData["data"] != null)
               {
                  JArray donationData = responseData["data"];
                  foreach (var donation in donationData)
                  {
                     TiltifyDonation current = new TiltifyDonation() {
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

         return donations;
      }
   }
}
