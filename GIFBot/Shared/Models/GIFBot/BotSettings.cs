using GIFBot.Shared.Models.Animation;
using GIFBot.Shared.Models.GIFBot;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIFBot.Shared
{
   public class BotSettings
   {
      #region Statics

      /// <summary>
      /// The current version of authentication. Needs to be bumped whenever a reauth
      /// of the bot and streamer accounts are required. This is done as a measure to
      /// ensure that users have the latest access to new features or any changes made
      /// on Twitch's end for scope.
      /// </summary>
      public static int skCurrentAuthenticationVersion = 2;

      /// <summary>
      /// Denotes the maximum level that can be reached on a Twitch Hype Train.
      /// </summary>
      public static int skMaxHypeTrainLevel = 5;

      /// <summary>
      /// Denotes the history of changes to the bot settings format in case we need to 
      /// perform conversions when the data is loaded.
      /// 
      ///   Version 1: Launch
      ///   Version 2: Deprecated "Users" on UserGroup in favor of UserEntry list instead.
      /// </summary>
      public static int skCurrentBotSettingsVersion = 2;

      #endregion

      #region Versioning

      /// <summary>
      /// Denotes the history of changes to the bot settings format in case we need to 
      /// perform conversions when the data is loaded.
      /// </summary>
      public int Version { get; set; } = 1;

      #endregion

      #region Credentials

      public string BotName { get; set; } = String.Empty;
      public string ChannelName { get; set; } = String.Empty;
      public string BotOauthToken { get; set; } = String.Empty;
      public string BotRefreshToken { get; set; } = String.Empty;
      public string StreamerOauthToken { get; set; } = String.Empty;
      public string StreamerRefreshToken { get; set; } = String.Empty;
      public string StreamlabsOauthToken { get; set; } = String.Empty;
      public string StreamElementsToken { get; set; } = String.Empty;
      public int BotAuthenticationVersion { get; set; } = 1;

      #endregion

      #region Azure TTS Credentials (optional)

      public string AzureCognitiveServicesKey { get; set; } = String.Empty;
      public string AzureCognitiveServicesRegion { get; set; } = String.Empty;

      #endregion

      #region Setup State

      public SetupStep CurrentSetupStep { get; set; } = SetupStep.Welcome;

      #endregion

      #region General Settings

      public int GlobalCooldownInSeconds { get; set; } = 60;
      public string AnimationCommand { get; set; } = "!animations";
      public int AnimationCommandCooldownSeconds { get; set; } = 0;
      public bool AnimationCommandEnabled { get; set; } = true;
      public int TimeBetweenAnimationsMs { get; set; } = 5000;
      public bool AnnounceAnimationCooldown { get; set; } = true;
      public bool AnimationRouletteChatEnabled { get; set; } = false;

      // Whispers are only allowed from the channel account.
      public bool CanTriggerAnimationsByWhisper { get; set; } = false;

      #endregion

      #region Global Positioning

      public bool UseGlobalPositioning { get; set; } = false;

      public AnimationPlacement GlobalPlacement { get; set; } = new AnimationPlacement();

      #endregion

      #region User Groups

      public List<UserGroup> UserGroups { get; set; } = new List<UserGroup>();

      #endregion

      #region Hype Train Data

      public string LastTriggeredHypeTrainId { get; set; } = String.Empty;
      public string LastTriggeredHypeTrainEventId { get; set; } = String.Empty;
      public int LastTriggeredHypeTrainLevel { get; set; } = 0;

      #endregion

      #region Per User Controls

      public List<ThrottledUserData> ThrottledUsers { get; set; } = new List<ThrottledUserData>();

      #endregion

      #region Tiltify Settings

      public string TiltifySlug { get; set; } = String.Empty;
      public string TiltifyAuthToken { get; set; } = String.Empty;
      public string TiltifyClientId { get; set; } = String.Empty;
      public string TiltifyClientSecret { get; set; } = String.Empty;
      public long TiltifyActiveCampaign { get; set; } = 0; // deprecated
      public string TiltifyActiveCampaignv5 { get; set; } = String.Empty;
      public bool TiltifyDonationAlertChat { get; set; } = false;

      #endregion
   }

   public enum SetupStep
   {
      Welcome,
      BotOauth,
      ChannelConnection,
      StreamerOauth,
      DEPRECATED_StreamlabsOauth, // DEPRECATED - Using the Socket API Token Instead. Leave intact to avoid breaking existing users on load.
      Finished,
      Error
   }
}
