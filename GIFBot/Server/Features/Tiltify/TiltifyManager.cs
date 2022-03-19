using GIFBot.Server.Interfaces;
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

      public long LastAlertedDonationId
      {
         get;
         set;
      } = -1;

      #endregion

      #region Public Methods

      public TiltifyManager(GIFBot.GIFBot bot)
      {
         Bot = bot;
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
         Task task = null;

         task = Task.Run(() =>
         {
            while (true)
            {
               // Poll for the latest donation data available on a specific campaign.
               if (Bot.BotSettings.TiltifyActiveCampaign > 0 &&
                   !String.IsNullOrEmpty(Bot.BotSettings.TiltifyAuthToken))
               {
                  List<TiltifyDonation> donations = TiltifyEndpointHelpers.GetCampaignDonations(Bot.BotSettings.TiltifyAuthToken, Bot.BotSettings.TiltifyActiveCampaign);
                  if (donations.Any())
                  {
                     TiltifyDonation firstDonation = donations.FirstOrDefault();

                     if (LastAlertedDonationId < 0)
                     {
                        // We have not set any alerted donation ids yet, so we don't want to 
                        // spin off a lot of alerts. Cache the topmost ID and break.
                        if (firstDonation != null)
                        {
                           LastAlertedDonationId = firstDonation.Id;
                        }
                     }
                     else
                     {
                        foreach (TiltifyDonation donation in donations)
                        {
                           if (donation.Id <= LastAlertedDonationId)
                           {
                              break;
                           }
                           else
                           {
                              Bot.HandleTiltifyDonation(donation);
                           }
                        }

                        LastAlertedDonationId = firstDonation.Id;
                     }
                  }
               }

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
