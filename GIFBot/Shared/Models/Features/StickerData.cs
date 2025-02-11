using System;
using System.Collections.Generic;

namespace GIFBot.Shared.Models.Features
{
   public class StickerData
   {
      // For S.T.J.
      public StickerData() { }

      /// <summary>
      /// Feature enabled or not.
      /// </summary>
      public bool Enabled { get; set; } = false;

      //
      // Limitation Properties
      //

      public int MaxStickers { get; set; } = 100;
      public int CanvasWidth { get; set; } = 1920;
      public int CanvasHeight { get; set; } = 1080;
      public int SecondaryCanvasWidth { get; set; } = 1920;
      public int SecondaryCanvasHeight { get; set; } = 1080;

      //
      // Global Behavioral Flags
      //

      public bool IncludeBits { get; set; } = false;
      public int BitMinimum { get; set; } = 1;
      public bool IncludeTips { get; set; } = false;
      public double TipMinimum { get; set; } = 0;
      public bool IncludeTiltifyDonations { get; set; } = false;
      public double TiltifyDonationMinimum { get; set; } = 0;
      public bool IncludeSubs { get; set; } = false;
      public bool IncludeGiftSubs { get; set; } = false;
      public int GiftSubMinimum { get; set; } = 0;
      public bool IncludeFollows { get; set; } = false;
      public bool IncludeRaids { get; set; } = false;
      public bool IncludeHosts { get; set; } = false;
      public bool IncludeChannelPoints { get; set; } = false;
      public int ChannelPointsMinimum { get; set; } = 1;

      //
      // Command Features
      //

      public string Command { get; set; } = "!sticker";
      public bool CanUseCommand { get; set; } = false;
      public int CommandCooldownSeconds { get; set; } = 30;
      public AnimationEnums.AccessType Access { get; set; } = AnimationEnums.AccessType.Anyone;
      public Guid RestrictedToUserGroup { get; set; } = Guid.Empty;
      public string RestrictedToUser { get; set; } = String.Empty;
      public bool RestrictedUserMustBeSub { get; set; } = false;

      //
      // Feature Behavior
      //

      // Optional: Audio file to play when the sticker is placed.
      public string Audio { get; set; } = String.Empty;
      public double Volume { get; set; } = 0.5;

      // Number of seconds a sticker is visible. Set to 0 for indefinite during the bot's session.
      // Restarting the bot will get rid of all stickers as they do not persist.
      public int NumSecondsStickerVisible { get; set; } = 0;

      //
      // Sticker Categories with Entries
      //

      public List<StickerCategory> Categories { get; set; } = new List<StickerCategory>();

      #region Version History

      /// <summary>
      /// The version of this data. Used for advancing and converting data when loaded.
      /// </summary>
      public int Version { get; set; } = 1;

      /// <summary>
      /// Determines if a bump in versions is required. Handles converting data as upgrades are made.
      /// </summary>
      public bool BumpVersion()
      {
         // Removed "Follower" as an option for defining access to use features of GIFBot. (6-8-2024)
         const int kFollowerRemoval = 3;

         if (Version < kFollowerRemoval)
         {
            if (Access == AnimationEnums.AccessType.Follower)
            {
               Access = AnimationEnums.AccessType.Anyone;
            }

            Version = BotSettings.skCurrentBotSettingsVersion;
            return true;
         }

         return false;
      }

      #endregion
   }
}
