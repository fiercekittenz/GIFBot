using GIFBot.Shared.Models.Animation;
using GIFBot.Shared.Models.GIFBot;
using GIFBot.Shared.Models.Base;
using GIFBot.Shared.Models.Visualization;
using GIFBot.Shared.Utility;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using TwitchLib.Api.Helix.Models.Tags;
using TwitchLib.Client.Enums;
using TwitchLib.Client.Models;

namespace GIFBot.Shared
{
   /// <summary>
   /// Data model for an Animation.
   /// </summary>
   public class AnimationData : VisualBase, ICloneable
   {
      #region Public Methods

      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="command"></param>
      public AnimationData(string command)
      {
         Id = Guid.NewGuid();
         Command = command;
      }

      /// <summary>
      /// ICloneable Implementation
      /// </summary>
      public object Clone()
      {
         AnimationData clonedAnimation = (AnimationData)MemberwiseClone();
         clonedAnimation.Id = Guid.NewGuid();

         foreach (AnimationVariantData variant in clonedAnimation.Variants)
         {
            variant.Id = Guid.NewGuid();
         }

         return clonedAnimation;
      }

      /// <summary>
      /// Returns if the command can be triggered by chat or not.
      /// </summary>
      public bool CanBeTriggeredByChatCommand(BotSettings settings, string userTriggering, int bitsCheered = 0)
      {
         if (settings == null || Disabled)
         {
            return false;
         }

         if (IsBitAlert)
         {
            return false;
         }

         if (IsSubAlertTrigger ||
             IsGiftSubAlertTrigger ||
             IsStreamlabsTipTrigger ||
             IsTiltifyTrigger ||
             IsHostAlert ||
             IsRaidAlert ||
             ChannelPointRedemptionType != AnimationEnums.ChannelPointRedemptionTriggerType.None)
         {
            return false;
         }

         // Verify access points that are limited by username inclusivity.
         switch (Access)
         {
            case AnimationEnums.AccessType.SpecificViewer:
               {
                  if (!String.IsNullOrEmpty(userTriggering) && 
                      !String.IsNullOrEmpty(RestrictedToUser) && 
                      !userTriggering.Equals(RestrictedToUser, StringComparison.OrdinalIgnoreCase))
                  {
                     return false;
                  }
               }
               break;
            case AnimationEnums.AccessType.UserGroup:
               {
                  if (!String.IsNullOrEmpty(userTriggering))
                  {
                     UserGroup group = settings.UserGroups.FirstOrDefault(g => g.Id == RestrictedToUserGroup);
                     if (group == null)
                     {
                        // Group doesn't exist.
                        return false;
                     }

                     UserEntry qualifiedUser = group.UserEntries.FirstOrDefault(u => u.Name.Equals(userTriggering, StringComparison.OrdinalIgnoreCase));
                     if (qualifiedUser == null)
                     {
                        // Not in the group.
                        return false;
                     }
                  }
               }
               break;
            case AnimationEnums.AccessType.BotExecuteOnly:
               {
                  return false;
               }
         }

         return true;
      }

      /// <summary>
      /// Determines if the animation can play or not.
      /// </summary>
      public bool CanPlay(BotSettings settings, ChatMessage chatMessage)
      {
         return CanPlay(settings,
                        chatMessage.DisplayName,
                        chatMessage.UserId,
                        chatMessage.RoomId,
                        chatMessage.Bits,
                        chatMessage.IsBroadcaster,
                        chatMessage.IsSubscriber,
                        chatMessage.IsVip,
                        chatMessage.IsModerator);
      }

      /// <summary>
      /// Determines if the animation can play or not.
      /// </summary>
      public bool CanPlay(BotSettings settings, string displayName, string userId, string roomId, int bitsCheered, bool isBroadcaster, bool isSubscriber, bool isVIP, bool isModerator)
      {
         if (String.IsNullOrEmpty(Visual) && String.IsNullOrEmpty(Audio))
         {
            // Have to have one or the other.
            return false;
         }

         if (isBroadcaster)
         {
            // The broadcaster can always execute animations.
            return true;
         }

         if (!String.IsNullOrEmpty(displayName) && settings.BotName.Equals(displayName, StringComparison.OrdinalIgnoreCase))
         {
            // The bot can also always execute animations.
            return true;
         }

         if (!CanBeTriggeredByChatCommand(settings, displayName, bitsCheered))
         {
            return false;
         }

         if (IsOnCooldown())
         {
            return false;
         }

         // Check against user flags. Not all events will funnel through here with the appropriate information.
         // Username-specific filters are handled in the CanBeTriggeredByChat message.
         switch (Access)
         {
            case AnimationEnums.AccessType.Follower:
               {
                  if (settings == null)
                  {
                     return false;
                  }

                  if (!TwitchEndpointHelpers.CheckFollowChannelOnTwitch(settings.BotOauthToken, long.Parse(roomId), long.Parse(userId)))
                  {
                     return false;
                  }
               }
               break;
            case AnimationEnums.AccessType.Subscriber:
               {
                  if (!isSubscriber)
                  {
                     return false;
                  }
               }
               break;
            case AnimationEnums.AccessType.VIP:
               {
                  if (!isVIP)
                  {
                     return false;
                  }
               }
               break;
            case AnimationEnums.AccessType.Moderator:
               {
                  if (!isModerator)
                  {
                     return false;
                  }
               }
               break;
            case AnimationEnums.AccessType.SpecificViewer:
               {
                  if (!String.IsNullOrEmpty(displayName) &&
                      !String.IsNullOrEmpty(RestrictedToUser) && 
                      !displayName.Equals(RestrictedToUser, StringComparison.OrdinalIgnoreCase))
                  {
                     return false;
                  }
                  
                  if (RestrictedUserMustBeSub && !isSubscriber)
                  {
                     return false;
                  }
               }
               break;
         }

         return true;
      }

      /// <summary>
      /// Returns if this animation is on cooldown or not.
      /// </summary>
      public bool IsOnCooldown()
      {
         // Test the user-triggered cooldown.
         double minuteDiff = System.DateTime.Now.Subtract(mLastUsed).TotalMinutes;
         if (minuteDiff < MainCooldownMinutes)
         {
            return true;
         }

         return false;
      }

      /// <summary>
      /// Updates the cooldown time.
      /// </summary>
      public void SetOnCooldown()
      {
         mLastUsed = DateTime.Now;
      }

      /// <summary>
      /// Adds a new variant to the list.
      /// </summary>
      /// <param name="variant"></param>
      public void AddVariant(AnimationVariantData variant)
      {
         Variants.Add(variant);
      }

      /// <summary>
      /// Removes a variant from the list.
      /// </summary>
      public void RemoveVariant(Guid id)
      {
         AnimationVariantData variant = Variants.FirstOrDefault(v => v.Id == id);
         if (variant != null)
         {
            Variants.Remove(variant);
         }
      }

      /// <summary>
      /// Pulls a random variant. If there are none, this will return null;
      /// </summary>
      public AnimationVariantData PullAVariant()
      {
         // Based on desired variant behavior, pull the correct data from which we will choose a variant.
         List<AnimationVariantData> qualifyingVariants = new List<AnimationVariantData>(Variants);
         if (PlayAllVariantsBeforeRepeat)
         {
            qualifyingVariants = new List<AnimationVariantData>(Variants.Where(v => !v.HasPlayedOnce).ToList());
            if (!qualifyingVariants.Any() && Variants.Any() && HasPlayedOriginalOnce)
            {
               // All variants AND the original have played. Reset their played once state back to false.
               foreach (var variant in Variants)
               {
                  variant.HasPlayedOnce = false;
               }               

               // Reset the original too.
               HasPlayedOriginalOnce = false;

               // Pull new qualifiers.
               qualifyingVariants = new List<AnimationVariantData>(Variants.Where(v => !v.HasPlayedOnce).ToList());
            }
         }

         int variantCount = qualifyingVariants.Count;
         if (variantCount > 0)
         {
            // +1 is so that if we choose a random number out of the array bounds, the
            // default animation for the command can still be played. Otherwise, a
            // variant will ALWAYS be played.
            int maxRandomValue = variantCount + 1;
            if (HasPlayedOriginalOnce)
            {
               maxRandomValue = variantCount;
            }

            int randomIndex = Common.sRandom.Next(maxRandomValue);
            if (maxRandomValue == 1)
            {
               // Don't replay the main animation until all variants have at least gone through.
               // This catches the last one and it shouldn't be randomized.
               randomIndex = 0;
            }

            if (randomIndex < variantCount)
            {
               AnimationVariantData originalVariant = Variants.FirstOrDefault(v => v.Id == qualifyingVariants[randomIndex].Id);
               if (originalVariant != null)
               {
                  originalVariant.HasPlayedOnce = true;
               }                            

               return originalVariant;
            }
         }

         HasPlayedOriginalOnce = true;
         return null;
      }

      #endregion

      #region Properties

      [Required]
      [StringLength(20, ErrorMessage = "Command is too long.")]
      public string Command { get; set; }

      public string Audio { get; set; }
      public int AudioTimingOffsetMilliseconds { get; set; } = 0;
      public double Volume { get; set; } = 0.5;
      public int DurationMilliseconds { get; set; } = 2000;
      public int MainCooldownMinutes { get; set; } = 5;
      public AnimationEnums.AccessType Access { get; set; } = AnimationEnums.AccessType.Anyone;
      public bool Disabled { get; set; } = false;
      public bool HideFromChatOutput { get; set; } = false;
      public Guid RestrictedToUserGroup { get; set; } = Guid.Empty;
      public string RestrictedToUser { get; set; } = String.Empty;
      public bool RestrictedUserMustBeSub { get; set; } = false;
      public bool IsBitAlert { get; set; } = false;
      public AnimationEnums.BitAnimationTriggerBehavior BitTriggerBehavior { get; set; } = AnimationEnums.BitAnimationTriggerBehavior.ExactMatch;
      public bool BitsMustBePairedWithCommand { get; set; } = false;
      public int BitRequirement { get; set; } = 0;
      public bool IsSubAlertTrigger { get; set; } = false;
      public int SubscriptionMonthsRequired { get; set; } = 0;
      public SubscriptionPlan SubscriptionTierRequired { get; set; } = 0;
      public bool IsGiftSubAlertTrigger { get; set; } = false;
      public int GiftSubCountRequirement { get; set; } = 0;
      public AnimationEnums.ChannelPointRedemptionTriggerType ChannelPointRedemptionType { get; set; } = AnimationEnums.ChannelPointRedemptionTriggerType.None;
      public int ChannelPointsRequired { get; set; } = 1;
      public bool IsStreamlabsTipTrigger { get; set; } = false;
      public double StreamlabsTipRequirement { get; set; } = 0;
      public bool IsTiltifyTrigger { get; set; } = false;
      public double TiltifyDonationRequirement { get; set; } = 0;
      public bool IsHostAlert { get; set; } = false;
      public string HostRestrictedToUsername { get; set; } = String.Empty;
      public bool IsRaidAlert { get; set; } = false;
      public string RaidRestrictedToUsername { get; set; } = String.Empty;
      public bool IsFollowerAlert { get; set; } = false;
      public CaptionData Caption { get; set; } = new CaptionData();
      public bool IsHypeTrainTrigger { get; set; } = false;
      public int HypeTrainLevel { get; set; } = 1;
      public string PrePlayText { get; set; } = String.Empty;
      public string PostPlayText { get; set; } = String.Empty;

      /// <summary>
      /// Variant Animation Data:
      ///   Will replace the primary animation if any Variants exist. Completely random. Pray to RNGesus.
      /// </summary>
      public List<AnimationVariantData> Variants { get; set; } = new List<AnimationVariantData>();
      public bool PlayAllVariantsBeforeRepeat { get; set; } = false;
      public bool HasPlayedOriginalOnce { get; set; } = false;

      /// <summary>
      /// List of chained animation commands.
      /// </summary>
      public List<string> ChainedAnimations { get; set; } = new List<string>();

      #endregion

      #region Private Members

      private DateTime mLastUsed = DateTime.Now.AddDays(-1);

      #endregion
   }
}
