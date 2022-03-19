using GIFBot.Shared;
using GIFBot.Shared.Models.Animation;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using static GIFBot.Shared.AnimationEnums;

namespace GIFBot.Server.GIFBot
{
   public class AnimationProcessor
   {
      #region Public Methods

      /// <summary>
      /// Constructor
      /// </summary>
      public AnimationProcessor(GIFBot bot, AnimationLayer layerAssignment)
      {
         Bot = bot;
         Layer = layerAssignment;
      }

      /// <summary>
      /// Starts the task for processing the animations queue.
      /// </summary>
      public async Task Start()
      {
         mAnimationProcessorCancellationTokenSource = new CancellationTokenSource();

         try
         {
            Task animationProcessor = ProcessAnimationQueue(mAnimationProcessorCancellationTokenSource.Token);
            await animationProcessor;
         }
         catch (TaskCanceledException)
         {
            // Do Nothing.
         }
      }

      /// <summary>
      /// Stops the animation processor.
      /// </summary>
      public void Stop()
      {
         mAnimationProcessorCancellationTokenSource.Cancel();
      }

      /// <summary>
      /// Adds the specified animation request to the queue.
      /// </summary>
      public void QueueAnimation(AnimationRequest request, string chatId, Guid targetedVariant)
      {
         List<AnimationRequest> animationsToAdd = new List<AnimationRequest>();

         // Before doing anything else, see if there is a request already queued that has the same chat ID.
         // This will avoid potential duplicates being handled.
         if (mPlayQueue.Count(r => !String.IsNullOrEmpty(r.ChatId) && r.ChatId == chatId) != 0)
         {
            // There's already one being handled and somehow we got a duplicate chat message. Abort!
            return;
         }

         // Adjust positioning, as needed.
         request.Placement = request.AnimationData.Placement;
         if (Bot.BotSettings.UseGlobalPositioning)
         {
            request.Placement = Bot.BotSettings.GlobalPlacement;
         }

         // Variant handling.
         AnimationVariantData variant = null;
         if (targetedVariant != Guid.Empty)
         {
            variant = request.AnimationData.Variants.FirstOrDefault(v => v.Id == targetedVariant);
            variant.HasPlayedOnce = true;
         }
         else
         {
            variant = request.AnimationData.PullAVariant();
         }

         if (variant != null)
         {
            request.Variant = variant;
            request.PrePlayText = variant.PrePlayText;
            request.PostPlayText = variant.PostPlayText;
         }

         // Handle chained animations.
         List<AnimationRequest> chainedAnimations = new List<AnimationRequest>();

         foreach (string chainedCommand in request.AnimationData.ChainedAnimations)
         {
            AnimationData animation = Bot.AnimationManager.GetAnimationByCommand(chainedCommand);
            if (animation != null && !animation.Disabled)
            {
               AnimationRequest chainedRequest = new AnimationRequest(animation, Bot.BotSettings.ChannelName, chatId, request.Triggerer, String.Empty, String.Empty, true, false);

               chainedRequest.Placement = chainedRequest.AnimationData.Placement;
               if (Bot.BotSettings.UseGlobalPositioning)
               {
                  chainedRequest.Placement = Bot.BotSettings.GlobalPlacement;
               }

               AnimationVariantData chainedVariant = animation.PullAVariant();
               if (chainedVariant != null)
               {
                  chainedRequest.Variant = chainedVariant;
                  chainedVariant.HasPlayedOnce = true;
               }

               chainedAnimations.Add(chainedRequest);
            }
         }

         request.ChainedAnimationCount = chainedAnimations.Count;
         animationsToAdd.Add(request);
         foreach (var chained in chainedAnimations)
         {
            animationsToAdd.Add(chained);
         }
         
         if (request.Priority == Priority.High)
         {
            // This isn't the most optimal solution. Unfortunately, I am way too lazy
            // to write my own priority blocking queue. Therefore, since the dataset is
            // so small, I am going to do the nastiest code thing I have ever done... in
            // my whole career.

            var currentPlayQueue = mPlayQueue.ToList();
            animationsToAdd.AddRange(currentPlayQueue);

            foreach (var animation in mPlayQueue)
            {
               mPlayQueue.Take();
            }

            foreach (var animation in animationsToAdd)
            {
               mPlayQueue.Add(animation);
            }
         }
         else
         {
            foreach (var animation in animationsToAdd)
            {
               mPlayQueue.Add(animation);
            }
         }

         // Save the data as the variations may have been altered.
         Bot.AnimationManager.SaveData();
      }

      /// <summary>
      /// Clears the animation queue.
      /// </summary>
      public void ClearQueue()
      {
         while (mPlayQueue.Count > 0)
         {
            AnimationRequest request;
            mPlayQueue.TryTake(out request);
         }
      }

      /// <summary>
      /// Deactivates the specified animation by removing it from the active animations collection.
      /// </summary>
      public async Task DeactivateAnimation(string command, IHubClients hubClients, string postPlayText = "", string triggerer = "")
      {
         mActiveAnimations.TryRemove(command, out AnimationRequest removed);
         await hubClients.All.SendAsync("StopAnimation");

         // Send post text as needed.
         if (!String.IsNullOrEmpty(postPlayText))
         {
            string postPlayFormatted = postPlayText;
            if (!String.IsNullOrEmpty(triggerer))
            {
               postPlayFormatted = postPlayFormatted.Replace("$user", triggerer);
            }

            Bot.SendChatMessage(postPlayFormatted);
         }
      }

      /// <summary>
      /// Stops all active animations in their tracks.
      /// </summary>
      public async Task StopAllActiveAnimations(IHubClients hubClients)
      {
         foreach (string command in mActiveAnimations.Keys)
         {
            await DeactivateAnimation(command, hubClients);
         }
      }

      #endregion

      #region Private Methods

      /// <summary>
      /// Processes the animation queue.
      /// </summary>
      private Task ProcessAnimationQueue(CancellationToken cancellationToken)
      {
         Task task = null;

         task = Task.Run(async () =>
         {
            while (true)
            {
               bool chainedAnimationIsNext = false;

               if (!IsPlayingAnimation)
               {
                  AnimationRequest animationRequest = mPlayQueue.Take();

                  if (Bot.IsStreamerOnlyMode && !animationRequest.ManuallyTriggeredByStreamer)
                  {
                     // Streamer only mode, only execute animations from the streamer.
                     // Put the animation back into the queue.
                     mPlayQueue.Add(animationRequest);
                  }
                  else 
                  {
                     if (animationRequest.AnimationData != null)
                     {
                        if (mActiveAnimations.TryAdd(animationRequest.AnimationData.Command, animationRequest))
                        {
                           int duration = animationRequest.AnimationData.DurationMilliseconds;
                           if (animationRequest.Variant != null)
                           {
                              duration = animationRequest.Variant.DurationMilliseconds;
                           }

                           await Bot.SendStartAnimationMessage(animationRequest);
                           await Bot.StartAnimationTimer(AnimationLayer.Primary, animationRequest, duration);
                        }

                        if (animationRequest.ChainedAnimationCount > 0)
                        {
                           mNumChainedAnimationsToProcess = animationRequest.ChainedAnimationCount;
                        }

                        if (mNumChainedAnimationsToProcess > 0)
                        {
                           chainedAnimationIsNext = true;
                           --mNumChainedAnimationsToProcess;
                        }
                     }
                  }

                  // Force this thread to sleep for N seconds between animations to give us a nice buffer.
                  if (chainedAnimationIsNext)
                  {
                     Thread.Sleep(skTimeBetweenChainedAnimationsMs);
                  }
                  else
                  {
                     Thread.Sleep(Bot.BotSettings.TimeBetweenAnimationsMs);
                  }
               }

               if (cancellationToken.IsCancellationRequested)
               {
                  throw new TaskCanceledException(task);
               }
            }
         });

         return task;
      }

      #endregion

      #region Properties

      /// <summary>
      /// A reference to the bot.
      /// </summary>
      public GIFBot Bot { get; private set; }

      /// <summary>
      /// The layer this processor is responsible for.
      /// </summary>
      public AnimationLayer Layer { get; private set; }

      /// <summary>
      /// Denotes if this processor is actively playing any animations.
      /// </summary>
      public bool IsPlayingAnimation
      {
         get { return mActiveAnimations.Any(); }
      }

      #endregion

      #region Private Members

      /// <summary>
      /// The animations that are currently active and playing.
      /// </summary>
      private ConcurrentDictionary<string, AnimationRequest> mActiveAnimations = new ConcurrentDictionary<string, AnimationRequest>();

      /// <summary>
      /// Queue: This is a queue of requests that need to be triggered. All requests should go into this queue
      /// and get processed on a thread that pauses between requests.
      /// </summary>
      private BlockingCollection<AnimationRequest> mPlayQueue = new BlockingCollection<AnimationRequest>();

      /// <summary>
      /// Task: The cancellation token for the main task that processes the animation queue.
      /// </summary>
      private CancellationTokenSource mAnimationProcessorCancellationTokenSource;

      /// <summary>
      /// Number of chained animations to be processed. Helps manage the shorter downtime between animations.
      /// </summary>
      private int mNumChainedAnimationsToProcess = 0;

      /// <summary>
      /// Number of milliseconds to pause between playing animations when there are chained ones in the queue.
      /// </summary>
      private static int skTimeBetweenChainedAnimationsMs = 500;

      #endregion
   }
}
