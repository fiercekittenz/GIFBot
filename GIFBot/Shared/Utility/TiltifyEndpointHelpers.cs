using GIFBot.Shared.Models.Tiltify;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GIFBot.Shared.Utility
{
   public class TiltifyEndpointHelpers
   {
      /// <summary>
      /// Authenticates the user's registration of their GIFBot copy against Tiltify's servers to get an auth token.
      /// </summary>
      public static string Authenticate(HttpClient client, string clientId, string clientSecret)
      {
         if (!String.IsNullOrEmpty(clientId) && !String.IsNullOrEmpty(clientSecret)) 
         {
            var urlParams = new Dictionary<string, string> {
               { "client_id", clientId },
               { "client_secret", clientSecret },
               { "grant_type", "client_credentials" }
            };

            var encodedParams = new FormUrlEncodedContent(urlParams);
            var paramText = encodedParams.ReadAsStringAsync().Result;

            string url = string.Format("https://v5api.tiltify.com/oauth/token?{0}", paramText);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            HttpResponseMessage response = client.Send(request);
            if (response.IsSuccessStatusCode)
            {
               string jsonData = response.Content.ReadAsStringAsync().Result;
               JsonObject responseData = JsonSerializer.Deserialize<JsonObject>(jsonData);
               if (responseData != null && responseData["access_token"] != null)
               {
                  return responseData["access_token"].ToString();
               }
            }
         }

         return string.Empty;
      }

      /// <summary>
      /// Grabs the user's information by its slug.
      /// </summary>
      public static string GetUserId(HttpClient client, string authToken, string slug)
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
               JsonObject responseData = JsonSerializer.Deserialize<JsonObject>(jsonData);
               if (responseData != null && responseData["data"] != null)
               {
                  return responseData["data"]["id"].ToString();
               }
            }
         }

         return string.Empty;
      }

      /// <summary>
      /// Fetches a list of campaigns available under the specified user.
      /// </summary>
      public static List<TiltifyCampaign> GetCampaigns(HttpClient client, string authToken, string userId)
      {
         List<TiltifyCampaign> campaigns = new List<TiltifyCampaign>();

         GetCampaignsInternal(client, authToken, userId, null, ref campaigns);

         return campaigns;
      }

      /// <summary>
      /// Internal method to recursively collect a user's active campaigns.
      /// </summary>
      private static void GetCampaignsInternal(HttpClient client, string authToken, string userId, string afterCursor, ref List<TiltifyCampaign> campaigns)
      {
         if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(authToken) && campaigns != null)
         {
            string url = string.Format("https://v5api.tiltify.com/api/public/users/{0}/campaigns?limit=10", userId);
            if (!string.IsNullOrEmpty(afterCursor))
            {
               url = (string.Format("{0}&after={1}", url, afterCursor));
            }

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {authToken}");

            HttpResponseMessage response = client.Send(request);
            if (response.IsSuccessStatusCode)
            {
               string jsonData = response.Content.ReadAsStringAsync().Result;
               JsonObject responseData = JsonSerializer.Deserialize<JsonObject>(jsonData);
               if (responseData != null)
               {
                  if (responseData["data"] != null)
                  {
                     JsonArray campaignData = responseData["data"].AsArray();
                     foreach (var campaign in campaignData)
                     {
                        bool active = IsCampaignActive(client, authToken, campaign["id"].ToString());
                        if (active)
                        {
                           TiltifyCampaign current = new TiltifyCampaign() {
                              Id = campaign["id"].ToString(),
                              Name = campaign["name"].ToString(),
                              Slug = campaign["slug"].ToString()
                           };

                           campaigns.Add(current);
                        }
                     }
                  }

                  if (responseData["metadata"] != null && responseData["metadata"]["after"] != null)
                  {
                     GetCampaignsInternal(client, authToken, userId, responseData["metadata"]["after"].ToString(), ref campaigns);
                  }
               }
            }
         }
      }

      /// <summary>
      /// Returns if a campaign is active or not.
      /// </summary>
      public static bool IsCampaignActive(HttpClient client, string authToken, string campaignId)
      {
         if (!string.IsNullOrEmpty(campaignId))
         {
            string url = string.Format("https://v5api.tiltify.com/api/public/campaigns/{0}", campaignId);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {authToken}");

            HttpResponseMessage response = client.Send(request);
            if (response.IsSuccessStatusCode)
            {
               string jsonData = response.Content.ReadAsStringAsync().Result;
               JsonObject responseData = JsonSerializer.Deserialize<JsonObject>(jsonData);
               if (responseData != null && responseData["data"] != null)
               {
                  if (responseData["data"]["retired_at"] == null) 
                  {
                     return true;
                  }
               }
            }
         }

         return false;
      }

      /// <summary>
      /// Gets a list of the most recent donations (last 10) to the specified campaign.
      /// </summary>
      public static List<TiltifyDonation> GetCampaignDonations(HttpClient client, string authToken, string campaignId, DateTime lastPollTime)
      {
         List<TiltifyDonation> donations = new List<TiltifyDonation>();

         GetCampaignDonationsInternal(client, authToken, campaignId, lastPollTime, null, ref donations);

         return donations;
      }

      /// <summary>
      /// Internal method to recursively gather the donations for the specified campaign.
      /// </summary>
      private static void GetCampaignDonationsInternal(HttpClient client, string authToken, string campaignId, DateTime lastPollTime, string afterCursor, ref List<TiltifyDonation> donations)
      {
         if (!string.IsNullOrEmpty(campaignId) && !string.IsNullOrEmpty(authToken) && donations != null)
         {
            string url = string.Format("https://v5api.tiltify.com/api/public/campaigns/{0}/donations?completed_after={1}&limit=100", campaignId, lastPollTime.ToString("o"));
            if (!string.IsNullOrEmpty(afterCursor))
            {
               url = string.Format("{0}&after={1}", url, afterCursor);
            }

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {authToken}");

            HttpResponseMessage response = client.Send(request);
            if (response.IsSuccessStatusCode)
            {
               string jsonData = response.Content.ReadAsStringAsync().Result;
               JsonObject responseData = JsonSerializer.Deserialize<JsonObject>(jsonData);
               if (responseData != null)
               {
                  if (responseData["data"] != null)
                  {
                     JsonArray donationData = responseData["data"].AsArray();
                     foreach (var donation in donationData)
                     {
                        TiltifyDonation current = new TiltifyDonation() {
                           Id = donation["id"].ToString(),
                           Amount = (double)donation["amount"]["value"],
                           Name = donation["donor_name"].ToString(),
                           Comment = donation["donor_comment"].ToString(),
                           CompletedAt = donation["completed_at"].ToString()
                        };

                        donations.Add(current);
                     }
                  }

                  if (responseData["metadata"] != null && responseData["metadata"]["after"] != null)
                  {
                     GetCampaignDonationsInternal(client, authToken, campaignId, lastPollTime, responseData["metadata"]["after"].ToString(), ref donations);
                  }
               }
            }
         }
      }
   }
}
