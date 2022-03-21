using GIFBot.Server.GIFBot;
using GIFBot.Server.Interfaces;
using GIFBot.Shared;
using GIFBot.Shared.Models.StreamElements;
using GIFBot.Shared.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

namespace GIFBot.Server.Features.StreamElements
{
   public class StreamElementsManager : IBasicManager
   {
      #region Public Methods

      public StreamElementsManager(GIFBot.GIFBot bot)
      {
         Bot = bot;
         if (!String.IsNullOrEmpty(Bot?.BotSettings?.StreamElementsToken))
         {
            InitializeWithChannelId();
         }
      }

      public void InitializeWithChannelId()
      {
         mChannelId = StreamElementsEndpointHelpers.GetChannelId(Bot.BotSettings.StreamElementsToken, Bot.BotSettings.ChannelName);
      }

      #endregion

      #region Properties

      public GIFBot.GIFBot Bot { get; private set; }

      #endregion

      #region IBasicManager Impl

      public async Task Start()
      {
         mProcessorCancellationTokenSource = new CancellationTokenSource();

         try
         {
            Task processor = Process(mProcessorCancellationTokenSource.Token);
            await processor;
         }
         catch (TaskCanceledException)
         {
            // Do Nothing.
         }
      }

      public void Stop()
      {
         mProcessorCancellationTokenSource?.Cancel();
      }

      #endregion

      #region Private Methods

      private Task Process(CancellationToken cancellationToken)
      {
         Task task = null;

         task = Task.Run(async () =>
         {
            while (true)
            {
               if (!String.IsNullOrEmpty(Bot.BotSettings.StreamElementsToken) &&
                   !String.IsNullOrEmpty(mChannelId))
               {
                  mLastTipAlertedTimestamp = DateTime.UtcNow;

                  List<StreamElementsTipData> tips = StreamElementsEndpointHelpers.GetTips(Bot.BotSettings.StreamElementsToken, mChannelId);
                  foreach (var tip in tips)
                  {
                     if (tip.TimeTipped.Subtract(mLastTipAlertedTimestamp).TotalSeconds > 0)
                     {
                        HandleTheTip(tip);
                     }
                  }
               }

               if (cancellationToken.IsCancellationRequested)
               {
                  throw new TaskCanceledException(task);
               }

               await Task.Delay(1000);
            }
         });

         return task;
      }

      private void HandleTheTip(StreamElementsTipData tip)
      {
         Bot.ProcessTip(tip.Amount, tip.TipperName, $"{tip.Amount}", tip.Message);
      }

      #endregion

      #region Private Members

      private CancellationTokenSource mProcessorCancellationTokenSource;

      private string mChannelId = String.Empty;

      private DateTime mLastTipAlertedTimestamp = DateTime.Now;

      #endregion
   }
}
