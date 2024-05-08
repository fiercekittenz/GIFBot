using GIFBot.Server.Interfaces;
using GIFBot.Shared;
using GIFBot.Shared.Models.Tiltify;
using GIFBot.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

namespace GIFBot.Server.Features.Tiltify
{
   public class TiltifyManager : IFeatureManager
   {
      #region Properties

      public GIFBot.GIFBot Bot { get; private set; }

      public DateTime LastDonationPollTime
      {
         get;
         set;
      } = DateTime.UnixEpoch;

      #endregion

      #region Public Methods

      public TiltifyManager(GIFBot.GIFBot bot)
      {
         Bot = bot;

         // The Tiltify API for authenticating a personal, connected application doesn't provide a refresh token.
         // We need to reauthenticate when we load the bot or make any settings changes.
         Authenticate();
      }

      public void Authenticate()
      {
         if (!string.IsNullOrEmpty(Bot.BotSettings.TiltifyClientId) && 
             !string.IsNullOrEmpty(Bot.BotSettings.TiltifyClientSecret))
         {
            Bot.BotSettings.TiltifyAuthToken = TiltifyEndpointHelpers.Authenticate(Bot.HttpClientFactory.CreateClient(Common.skHttpClientName), Bot.BotSettings.TiltifyClientId, Bot.BotSettings.TiltifyClientSecret);
            Bot.SaveSettings();
         }
      }

      #endregion

      #region IFeatureManager Implementation

      public async Task Start()
      {
         mCancellationTokenSource = new CancellationTokenSource();

         try
         {
            Task processor = DonationCheckPulse(mCancellationTokenSource.Token);
            await processor;
         }
         catch (TaskCanceledException)
         {
            // Do Nothing.
         }
      }

      public void Stop()
      {
         mCancellationTokenSource.Cancel();
      }

      public void LoadData()
      {
         // Do Nothing.
      }

      public void SaveData()
      {
         // Do Nothing.
      }

      public bool CanHandleTwitchMessage(string message, bool isBroadcaster = false)
      {
         return false;
      }

      public void HandleTwitchMessage(OnMessageReceivedArgs message)
      {
         // Do Nothing.
      }

      #endregion

      #region Private Methods

      private Task DonationCheckPulse(CancellationToken cancellationToken)
      {
         LastDonationPollTime = DateTime.Now;
         Task task = null;

         task = Task.Run(() =>
         {
            while (true)
            {
               // Poll for the latest donation data available on a specific campaign.
               if (!string.IsNullOrEmpty(Bot.BotSettings.TiltifyActiveCampaignv5) &&
                   !string.IsNullOrEmpty(Bot.BotSettings.TiltifyAuthToken))
               {
                  List<TiltifyDonation> donations = TiltifyEndpointHelpers.GetCampaignDonations(Bot.HttpClientFactory.CreateClient(Common.skHttpClientName), Bot.BotSettings.TiltifyAuthToken, Bot.BotSettings.TiltifyActiveCampaignv5, LastDonationPollTime);
                  foreach (TiltifyDonation donation in donations)
                  {
                     Bot.HandleTiltifyDonation(donation);
                  }
               }

               LastDonationPollTime = DateTime.Now;
               Thread.Sleep(skGeneralTiltifyRateLimit);

               if (cancellationToken.IsCancellationRequested)
               {
                  throw new TaskCanceledException(task);
               }
            }
         });

         return task;
      }

      #endregion

      #region Private Members

      private CancellationTokenSource mCancellationTokenSource;

      private static int skGeneralTiltifyRateLimit = 1500; // ms

      #endregion
   }
}
