﻿using GIFBot.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.IO;
using TwitchLib.Api.Helix.Models.Channels;
using GIFBot.Shared.Models.Twitch;
using System.Text;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GIFBot.Shared.Utility
{
   public class TwitchEndpointHelpers
   {
      /// <summary>
      /// As of Twitch API v5 or higher, the user ID is required for fetching details from the API. 
      /// When the application successfully connects to Twitch, it will grab this information before it
      /// begins polling for additional info.
      /// </summary>
      /// <returns>ID of the user's channel</returns>
      public static uint GetChannelId(HttpClient client, string channelName, string oauth, out string errorMessage)
      {
         errorMessage = string.Empty;

         if (!string.IsNullOrEmpty(channelName))
         {
            string url = string.Format("https://api.twitch.tv/helix/users?login={0}", channelName.ToLower().Trim());

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {oauth.Trim()}");
            request.Headers.Add("Client-ID", Common.skTwitchClientId);

            HttpResponseMessage response = client.Send(request);
            if (response.IsSuccessStatusCode)
            {
               string jsonData = response.Content.ReadAsStringAsync().Result;
               JsonObject responseData = JsonSerializer.Deserialize<JsonObject>(jsonData);
               if (responseData["data"][0]["id"] != null)
               {
                  return UInt32.Parse(responseData["data"][0]["id"].ToString());
               }
            }
         }

         return 0;
      }

      public class ChannelPointRewardCreationData
      {
         public string title { get; set; }
         public int cost { get; set; }
         public bool is_max_per_user_per_stream_enabled { get; set; } = true;
         public int max_per_user_per_stream { get; set; } = 1;
         public bool should_redemptions_skip_request_queue {  get; private set; } = true;
      }

      public static Guid CreateChannelPointReward(HttpClient client, string streamerOauth, long channelId, string title, int pointsRequired, int maxUsesAllowed)
      {
         string url = string.Format("https://api.twitch.tv/helix/channel_points/custom_rewards?broadcaster_id={0}", channelId);

         HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
         request.Headers.Add("Authorization", $"Bearer {streamerOauth.Trim()}");
         request.Headers.Add("Client-ID", Common.skTwitchClientId);

         ChannelPointRewardCreationData channelPointRewardCreationData = new ChannelPointRewardCreationData() {
            title = title,
            cost = pointsRequired,
            max_per_user_per_stream = maxUsesAllowed
         };

         string channelPointRewardJson = JsonSerializer.Serialize(channelPointRewardCreationData);
         byte[] channelPointRewardRaw = Encoding.ASCII.GetBytes(channelPointRewardJson);
         request.Content = new ByteArrayContent(channelPointRewardRaw);

         HttpResponseMessage response = client.Send(request);
         if (response.IsSuccessStatusCode)
         {
            string jsonData = response.Content.ReadAsStringAsync().Result;
            JsonObject responseData = JsonSerializer.Deserialize<JsonObject>(jsonData);
            if (responseData["data"][0]["id"] != null)
            {
               Guid rewardId = Guid.Parse((string)responseData["data"][0]["id"]);
               return rewardId;
            }
         }

         return Guid.Empty;
      }

      public class ChannelPointRewardUpdateData
      {
         public bool is_enabled { get; set; }
      }

      public static bool UpdateChannelPointReward(HttpClient client, string streamerOauth, long channelId, Guid rewardId, bool isEnabled)
      {
         string url = string.Format("https://api.twitch.tv/helix/channel_points/custom_rewards?broadcaster_id={0}&id={1}", channelId, rewardId.ToString());

         HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Patch, url);
         request.Headers.Add("Authorization", $"Bearer {streamerOauth.Trim()}");
         request.Headers.Add("Client-ID", Common.skTwitchClientId);

         ChannelPointRewardUpdateData channelPointRewardUpdateData = new ChannelPointRewardUpdateData() {
            is_enabled = isEnabled
         };

         string channelPointRewardJson = JsonSerializer.Serialize(channelPointRewardUpdateData);
         byte[] channelPointRewardRaw = Encoding.ASCII.GetBytes(channelPointRewardJson);
         request.Content = new ByteArrayContent(channelPointRewardRaw);

         HttpResponseMessage response = client.Send(request);
         if (response.IsSuccessStatusCode)
         {
            return true;
         }

         return false;
      }

      public static bool DeleteChannelPointReward(HttpClient client, string streamerOauth, long channelId, Guid rewardId)
      {
         string url = string.Format("https://api.twitch.tv/helix/channel_points/custom_rewards?broadcaster_id={0}&id={1}", channelId, rewardId.ToString());
         
         HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, url);
         request.Headers.Add("Authorization", $"Bearer {streamerOauth.Trim()}");
         request.Headers.Add("Client-ID", Common.skTwitchClientId);

         HttpResponseMessage response = client.Send(request);
         if (response.StatusCode == HttpStatusCode.NoContent)
         {
            return true;
         }
         
         return false;
      }

      /// <summary>
      /// Retrieves the latest information on the most recent Hype Train of the given channel ID.
      /// </summary>
      /// <returns>ID of the user's channel</returns>
      public static TwitchHypeTrainEvent GetHypeTrainEventData(HttpClient client, string oauth, long channelId, int authVersion)
      {
         // Deprecated until Twitch allows this endpoint to be called again. It was closed off for a "security" risk.
         // See: https://discuss.dev.twitch.tv/t/get-hype-train-events-via-app-token/31727/6

         //string result = String.Empty;

         //// Verify that the user is on the correct data version. Authenticated against version 2 or higher to use the hype train.
         //if (channelId != 0 && authVersion >= 2)
         //{
         //   string url = string.Format("https://api.twitch.tv/helix/hypetrain/events?broadcaster_id={0}&first=1", channelId);

         //   try
         //   {
         //      HttpWebRequest outboundRequest = (HttpWebRequest)WebRequest.Create(url);
         //      if (outboundRequest != null)
         //      {
         //         // Add the header information for oauth v5
         //         outboundRequest.Method = "GET";
         //         outboundRequest.Timeout = 12000;
         //         outboundRequest.ContentType = "application/json";
         //         outboundRequest.Headers.Add("Authorization", $"Bearer {oauth.Trim()}");
         //         outboundRequest.Headers.Add("Client-ID", Common.skClientId);

         //         using (HttpWebResponse inboundResponse = (HttpWebResponse)outboundRequest.GetResponse())
         //         {
         //            if (inboundResponse.StatusCode == HttpStatusCode.OK)
         //            {
         //               using (StreamReader stream = new StreamReader(inboundResponse.GetResponseStream()))
         //               {
         //                  string jsonData = stream.ReadToEnd();
         //                  TwitchHypeTrainEvent responseData = JsonSerializer.Deserialize<TwitchHypeTrainEvent>(jsonData);
         //                  return responseData;
         //               }
         //            }
         //         }
         //      }
         //   }
         //   catch (Exception /*e*/)
         //   {
         //      // This exception occurs even on a valid response with a 404. It's the 404 we are looking for here.
         //      // Do not log, because it is misleading.
         //   }
         //}

         return null;
      }

      /// <summary>
      /// Retrieves the logged in user information based solely on the bearer token.
      /// </summary>
      public static TwitchUserData GetCurrentUser(HttpClient client, string oauth)
      {
         string result = String.Empty;

         if (!String.IsNullOrEmpty(oauth))
         {
            string url = "https://api.twitch.tv/helix/users";

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("Authorization", $"Bearer {oauth.Trim()}");
            request.Headers.Add("Client-ID", Common.skTwitchClientId);

            HttpResponseMessage response = client.Send(request);
            if (response.IsSuccessStatusCode)
            {
               string jsonData = response.Content.ReadAsStringAsync().Result;
               TwitchGetUserResponse responseData = JsonSerializer.Deserialize<TwitchGetUserResponse>(jsonData);
               if (responseData.data != null && responseData.data.Any())
               {
                  return responseData.data.FirstOrDefault();
               }
            }
         }
         
         return null;
      }   
   }
}
