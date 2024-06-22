using GIFBot.Shared.Models.Animation;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;
using TwitchLib.Api.Core.Enums;

namespace GIFBot.Shared
{
   /// <summary>
   /// Defines the priority of the animation being queued for processing.
   /// </summary>
   public enum Priority
   {
      Normal,
      High,
   }

   /// <summary>
   /// Representation for a single request to play an animation.
   /// </summary>
   public class AnimationRequest
   {
      // For S.T.J.
      public AnimationRequest() { }

      public AnimationRequest(AnimationData animationData, 
                              string channelName, 
                              string chatId, 
                              string triggerer, 
                              string target, 
                              string amount, 
                              bool manuallyTriggered = false, 
                              bool triggeredByTimer = false)
      {
         ChannelName = channelName;
         ChatId = chatId;
         AnimationData = animationData;
         Triggerer = triggerer;
         Target = target;
         Amount = amount;
         ManuallyTriggeredByStreamer = manuallyTriggered;
         TriggeredByTimer = triggeredByTimer;

         if (animationData != null)
         {
            PrePlayText = animationData.PrePlayText;
            PostPlayText = animationData.PostPlayText;
         }
      }
      [JsonInclude]
      public string ChannelName { get; private set; } = String.Empty;

      [JsonInclude]
      public string ChatId { get; private set; } = String.Empty;

      [JsonInclude]
      public AnimationData AnimationData { get; private set; }

      [JsonInclude]
      public string Triggerer { get; private set; }

      [JsonInclude]
      public string Target { get; private set; }

      [JsonInclude]
      public string Amount { get; private set; }

      public AnimationVariantData Variant { get; set; } = null;

      public int ChainedAnimationCount { get; set; } = 0;

      public string PrePlayText { get; set; } = String.Empty;

      public string PostPlayText { get; set; } = String.Empty;

      public bool ManuallyTriggeredByStreamer { get; set; } = false;

      public bool TriggeredByTimer { get; set; } = false;

      public Priority Priority { get; set; } = Priority.Normal;

      /// <summary>
      /// Instance data for the placement for this particular request. 
      /// Could be a global override from bot settings, or the actual position 
      /// data from the AnimationData object referenced by the request.
      /// </summary>
      public AnimationPlacement Placement { get; set; } = new AnimationPlacement();
   }
}
