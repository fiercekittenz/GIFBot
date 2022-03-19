using System;
using System.Collections.Generic;
using System.Text;

namespace GIFBot.Shared
{
   /// <summary>
   /// Helper class for accessing enums used by the bot.
   /// </summary>
   public class AnimationEnums
   {
      /// <summary>
      /// The type of file for the animation. Used to determine which HTML element to use
      /// in the front-end.
      /// </summary>
      public enum FileType
      {
         Unknown,
         Image,
         Video
      }

      /// <summary>
      /// The visual type for editors and pumping test display updates.
      /// </summary>
      public enum UpdateVisualType
      {
         Animation = 0,
         Sticker,
      }

      /// <summary>
      /// Defines the access type of the animation. It can only be one of these and the viewer
      /// executing the command must qualify.
      /// </summary>
      public enum AccessType
      {
         Anyone            = 0,
         Follower          = 1,
         Subscriber        = 2,
         VIP               = 3,
         Moderator         = 4,
         UserGroup         = 5,
         SpecificViewer    = 6,
         BotExecuteOnly    = 7,
      }

      /// <summary>
      /// Defines the trigger behavior of a bit-required animation.
      /// </summary>
      public enum BitAnimationTriggerBehavior
      {
         ExactMatch,
         MinimumAtLeast,
      }

      /// <summary>
      /// The layer on which the visual is rendered.
      /// </summary>
      public enum AnimationLayer
      {
         Primary,
         Secondary
      }

      /// <summary>
      /// The various sub tiers supported by Twitch.
      /// </summary>
      public enum SubTier
      {
         None = 0,
         Prime,
         One,
         Two,
         Three
      }

      /// <summary>
      /// The type of channel point redemption used.
      /// </summary>
      public enum ChannelPointRedemptionTriggerType
      {
         None = 0,
         All,
         MessageText,
         PointsUsed
      }

      /// <summary>
      /// The type of request for an animation when it is queued.
      /// Determines how it is treated once it has finished playing.
      /// </summary>
      public enum RequestType
      {
         Animation = 0,
         TTS = 1,
      }
   }
}
