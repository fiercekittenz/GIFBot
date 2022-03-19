using GIFBot.Server.Base;
using GIFBot.Server.Interfaces;
using GIFBot.Shared;
using GIFBot.Shared.Models.Animation;
using GIFBot.Shared.Models.Base;
using GIFBot.Shared.Models.GIFBot;
using GIFBot.Shared.Models.Visualization;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.Client.Events;
using static GIFBot.Shared.AnimationEnums;

namespace GIFBot.Server.GIFBot
{
   public class AnimationManager : VisualPreviewer, IFeatureManager
   {
      #region Public Methods

      /// <summary>
      /// Constructor
      /// </summary>
      public AnimationManager(GIFBot bot, string dataFilePath)
      {
         Bot = bot;
         DataFilePath = dataFilePath;
      }

      /// <summary>
      /// Starts the tasks for processing the animations queue.
      /// </summary>
      public async Task Start()
      {
         mPrimaryProcessor = new AnimationProcessor(Bot, AnimationEnums.AnimationLayer.Primary);
         await mPrimaryProcessor.Start();
         await Bot.SendLogMessage("Animation processors started.");
      }

      /// <summary>
      /// Stops the animation processors.
      /// </summary>
      public void Stop()
      {
         mPrimaryProcessor.Stop();
      }

      /// <summary>
      /// Clears all animations from the queue and stops whatever is playing.
      /// </summary>
      public void ClearAnimations()
      {
         if (mPrimaryProcessor != null)
         {
            mPrimaryProcessor.ClearQueue();
         }
      }

      /// <summary>
      /// Stops all active animations on the various processors.
      /// </summary>
      /// <returns></returns>
      public async Task StopAllAnimations()
      {
         if (mPrimaryProcessor != null)
         {
            await mPrimaryProcessor.StopAllActiveAnimations(Bot.GIFBotHub.Clients);
         }
      }

      /// <summary>
      /// Loads the data.
      /// </summary>
      public void LoadData()
      {
         if (!String.IsNullOrEmpty(DataFilePath) && File.Exists(DataFilePath))
         {
            string fileData = File.ReadAllText(DataFilePath);
            mData = JsonConvert.DeserializeObject<AnimationLibrary>(fileData);

            if (mData != null)
            {
               if (mData.BumpVersion())
               {
                  SaveData();
               }

               _ = Bot?.SendLogMessage("Animation data loaded.");
            }
            else
            {
               _ = Bot?.SendLogMessage("An error occurred trying to load your animation data!");
            }
         }
         else
         {
            _ = Bot?.SendLogMessage("You have no animations! Go add some, ya goof!");
         }
      }

      /// <summary>
      /// Saves the data.
      /// </summary>
      public void SaveData()
      {
         if (mData != null)
         {
            Directory.CreateDirectory(Path.GetDirectoryName(DataFilePath));

            var jsonData = JsonConvert.SerializeObject(mData);
            File.WriteAllText(DataFilePath, jsonData);

            _ = Bot?.SendLogMessage("Animation data saved.");
         }
      }

      /// <summary>
      /// Selects a random animation for the user (must qualify) and queues it.
      /// </summary>
      public void PlayRandomAnimation(string userTriggered)
      {
         if (mIsDisplayTestModeOn)
         {
            // Early out. We're in test mode.
            return;
         }

         IEnumerable<AnimationData> potentialAnimations = GetAllAnimations(FetchType.EnabledOnly).Where(a => a.CanBeTriggeredByChatCommand(Bot.BotSettings, userTriggered) &&
                                                                                                             a.Access != AnimationEnums.AccessType.Moderator &&
                                                                                                             a.Access != AnimationEnums.AccessType.VIP &&
                                                                                                             a.Access != AnimationEnums.AccessType.BotExecuteOnly);
         if (potentialAnimations.ToList().Count > 0)
         {
            int randomIndex = Common.sRandom.Next(potentialAnimations.ToList().Count);
            AnimationData animToTrigger = potentialAnimations.ToList().ElementAt(randomIndex);
            if (animToTrigger != null)
            {
               ForceQueueAnimation(animToTrigger, userTriggered, String.Empty);
               _ = Bot.SendLogMessage($"Roulette [{animToTrigger.Command}] triggered by user [{userTriggered}]");
            }
         }
      }

      /// <summary>
      /// Determines if this message can be handled by this feature.
      /// </summary>
      public bool CanHandleTwitchMessage(string message, bool isBroadcaster = false)
      {
         if (mIsDisplayTestModeOn)
         {
            // Early out. We're in test mode.
            return false;
         }

         // Get the animation.
         AnimationData animation = GetAnimationFromMessage(message);
         if (isBroadcaster)
         {
            return true;
         }

         // Is the animation on cooldown?
         if (animation != null && !Bot.CrazyModeEnabled && Bot.UnderGlobalCooldown())
         {
            if (Bot.BotSettings.AnnounceAnimationCooldown)
            {
               Bot.SendChatMessage("Cool your jets man! Animations are on cooldown.");
            }

            return false;
         }

         return true;
      }

      /// <summary>
      /// Force an animation to queue for play. This can be used to trigger from within the bot, not via chat.
      /// This method will not check permissions as it is assumed to be an internal event.
      /// </summary>
      public void ForceQueueAnimation(AnimationData animation, string triggererName, string amount)
      {
         ForceQueueAnimation(animation, String.Empty, triggererName, amount, Guid.Empty);
      }

      public void ForceQueueAnimation(AnimationData animation, string chatId, string triggererName, string amount)
      {
         ForceQueueAnimation(animation, chatId, triggererName, amount, Guid.Empty);
      }

      public void ForceQueueAnimation(AnimationData animation, string chatId, string triggererName, string amount, Guid targetedVariant)
      {
         if (animation != null && !mIsDisplayTestModeOn)
         {
            // Manually triggered.
            AnimationRequest request = new AnimationRequest(animation, Bot.BotSettings.ChannelName, chatId, triggererName, String.Empty, amount, true, false);
            ForceQueueAnimationRequest(request, chatId, targetedVariant);
         }
      }

      public void PriorityQueueAnimation(AnimationData animation)
      {
         if (animation != null && !mIsDisplayTestModeOn)
         {
            // Manually triggered.
            AnimationRequest request = new AnimationRequest(animation, Bot.BotSettings.ChannelName, String.Empty, String.Empty, String.Empty, String.Empty, true, false);
            request.Priority = Priority.High;
            ForceQueueAnimationRequest(request, String.Empty, Guid.Empty);
         }
      }

      public void ForceQueueAnimationRequest(AnimationRequest request, string chatId, Guid targetedVariant)
      {
         if (request != null && !mIsDisplayTestModeOn)
         {
            // TODO: Check to see which processor to use.
            PrimaryProcessor.QueueAnimation(request, chatId, targetedVariant);
         }
      }

      /// <summary>
      /// Updates the existing animation with the updated data.
      /// </summary>
      public void UpdateAnimation(AnimationData animation)
      {
         if (animation != null)
         {
            // Find the actual animation we have in memory and remove it.
            AnimationData originalAnimation = GetAnimationById(animation.Id);
            AnimationCategory category = GetCategoryForAnimation(originalAnimation);
            category.Animations.Remove(originalAnimation);
            category.Animations.Add(animation);

            // Persist to disk.
            SaveData();
         }
      }

      /// <summary>
      /// Handles the twitch message, when applicable.
      /// </summary>
      public void HandleTwitchMessage(OnMessageReceivedArgs message)
      {
         if (mIsDisplayTestModeOn)
         {
            // Early out. We're in test mode.
            _ = Bot.SendLogMessage($"[KITTENZDEBUG] DisplayTestMode is on!");
            return;
         }

         // Is this user throttled or banned?
         ThrottledUserData throttledUser = null;
         if (UserIsThrottled(message.ChatMessage.DisplayName, out throttledUser))
         {
            _ = Bot.SendLogMessage($"User [{message.ChatMessage.DisplayName}] has been throttled. Their animation will not be played.");
            return;
         }

         bool applyThrottleToUser = false;

         // Check for bits.
         if (message.ChatMessage.Bits > 0)
         {
            _ = Bot.SendLogMessage($"[KITTENZDEBUG] A bit message got in: {message.ChatMessage.Message}, {message.ChatMessage.Id}, {message.ChatMessage.DisplayName}, {message.ChatMessage.Bits}");
            HandleBitMessage(message.ChatMessage.Message, message.ChatMessage.Id, message.ChatMessage.DisplayName, message.ChatMessage.Bits);
            applyThrottleToUser = true;
         }
         else
         {
            // Play the animation.
            AnimationData animationToPlay = GetAnimationFromMessage(message.ChatMessage.Message);
            if (animationToPlay != null && animationToPlay.CanPlay(Bot.BotSettings, message.ChatMessage))
            {
               // Need to override the manual trigger so cooldowns are applied.
               AnimationRequest request = new AnimationRequest(animationToPlay, Bot.BotSettings.ChannelName, message.ChatMessage.Id, message.ChatMessage.DisplayName, String.Empty, String.Empty, message.ChatMessage.IsBroadcaster, false);
               ForceQueueAnimationRequest(request, message.ChatMessage.Id, Guid.Empty);
               applyThrottleToUser = true;
            }
         }

         if (applyThrottleToUser && throttledUser != null)
         {
            _ = Bot.SendLogMessage($"User [{message.ChatMessage.DisplayName}] has had their throttle timestamp updated.");
            throttledUser.LastThrottled = DateTime.Now;
            Bot.SaveSettings();
         }
      }

      /// <summary>
      /// Handles playing bit-based messages.
      /// </summary>
      public void HandleBitMessage(string messageOrCommand, string chatId, string triggerer, int bits)
      {
         if (mIsDisplayTestModeOn)
         {
            // Early out. We're in test mode.
            _ = Bot.SendLogMessage($"[KITTENZDEBUG] DisplayTestMode is on!");
            return;
         }

         if (String.IsNullOrEmpty(triggerer))
         {
            _ = Bot.SendLogMessage($"[KITTENZDEBUG] There was no triggerer name in this message. WTF Twitch?");

            // A second chat message will come through and be flagged as a bit alert, when really it isn't.
            // Twitch started doing this weird thing December 2020. Thanks, 2020.
            return;
         }

         AnimationData bitAlertAnimation = GetAnimationFromMessage(messageOrCommand);
         AnimationData chatAnimation = GetAnimationFromMessage(messageOrCommand);
         AnimationData animationToPlay = null;

         // Get an alert animation, if applicable.
         bitAlertAnimation = GetAnimationForBits(bits, null, BitAnimationTriggerBehavior.MinimumAtLeast);

         // Get a specific animation, if applicable.
         animationToPlay = GetAnimationForBits(bits, chatAnimation, BitAnimationTriggerBehavior.ExactMatch);

         // Play the generic bit alert, but only if there wasn't another bit animation so as to not double up the alerts.
         if (bitAlertAnimation != null && animationToPlay == null)
         {
            _ = Bot.SendLogMessage($"[KITTENZDEBUG] Playing generic alert {bitAlertAnimation.Command} triggered by {triggerer} for {bits} bits. ChatId = {chatId}");
            ForceQueueAnimation(bitAlertAnimation, chatId, triggerer, $"{bits}");
         }

         if (animationToPlay == null)
         {
            // No eligible animation could be found for this command. Bail.
            _ = Bot.SendLogMessage($"[KITTENZDEBUG] No alert message: {messageOrCommand}, triggerer: {triggerer} for {bits} bits. ChatId = {chatId}");
            return;
         }

         _ = Bot.SendLogMessage($"[KITTENZDEBUG] Playing specific alert {animationToPlay.Command} triggered by {triggerer} for {bits} bits. ChatId = {chatId}");
         ForceQueueAnimation(animationToPlay, chatId, triggerer, $"{bits}");
      }

      /// <summary>
      /// Retrieves the animation for the bit amount and possible matching animation command.
      /// </summary>
      public AnimationData GetAnimationForBits(int bits, AnimationData optionalAnimation, BitAnimationTriggerBehavior searchType)
      {
         List<AnimationData> qualifyingAnimations = new List<AnimationData>();

         switch (searchType)
         {
         case BitAnimationTriggerBehavior.ExactMatch:
            {
               //
               // When performing an exact match against bits cheered, we can also consider an animation being tied in with it.
               //

               if (optionalAnimation != null)
               {
                  // This was paired WITH a command. Look for bit required animations that also require it be paired with a command.
                  qualifyingAnimations = GetAllAnimations(FetchType.EnabledOnly).Where(a => a.IsBitAlert && a.BitsMustBePairedWithCommand && a.Command.Equals(optionalAnimation.Command, StringComparison.OrdinalIgnoreCase) && a.BitRequirement == bits && a.BitTriggerBehavior == BitAnimationTriggerBehavior.ExactMatch).ToList();
               }
               else
               {
                  // Just look for bit qualified animations.
                  qualifyingAnimations = GetAllAnimations(FetchType.EnabledOnly).Where(a => a.IsBitAlert && a.BitRequirement == bits && a.BitTriggerBehavior == BitAnimationTriggerBehavior.ExactMatch).ToList();
               }
            }
            break;

         case BitAnimationTriggerBehavior.MinimumAtLeast:
            {
               //
               // Need to look for a minimum match and choose the highest one.
               //

               List<AnimationData> queryResults = GetAllAnimations(FetchType.EnabledOnly).Where(a => a.IsBitAlert && bits >= a.BitRequirement && a.BitTriggerBehavior == BitAnimationTriggerBehavior.MinimumAtLeast).ToList();
               queryResults.Sort((x, y) => x.BitRequirement.CompareTo(y.BitRequirement));
               if (queryResults.Any())
               {
                  qualifyingAnimations = new List<AnimationData>() { queryResults.Last() };
               }
            }
            break;
         }

         // If we have qualifying animations, choose a random one from the resulting list.
         if (qualifyingAnimations.Count > 0)
         {
            AnimationData animation = null;

            int randomIndex = Common.sRandom.Next(qualifyingAnimations.Count);
            if (randomIndex < qualifyingAnimations.Count)
            {
               animation = qualifyingAnimations[randomIndex];
            }

            if (animation == null)
            {
               // No eligible animation could be found for this command. Bail.
               return null;
            }
            else
            {
               return animation;
            }
         }

         return null;
      }

      /// <summary>
      /// Retrieves all animations.
      /// </summary>

      public enum FetchType
      {
         All,
         EnabledOnly
      }

      public List<AnimationData> GetAllAnimations(FetchType fetchType = FetchType.All)
      {
         List<AnimationData> animations = new List<AnimationData>();
         foreach (var category in mData.Categories)
         {
            if (fetchType == FetchType.All)
            {
               animations.AddRange(category.Animations);
            }
            else if (fetchType == FetchType.EnabledOnly)
            {
               animations.AddRange(category.Animations.Where(a => !a.Disabled));
            }
         }

         return animations;
      }

      /// <summary>
      /// Retrieves a single animation based on the command.
      /// </summary>
      public AnimationData GetAnimationByCommand(string command)
      {
         if (!String.IsNullOrEmpty(command))
         {
            foreach (AnimationCategory category in mData.Categories)
            {
               AnimationData animation = category.Animations.Where(a => a.Command.Equals(command, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
               if (animation != null)
               {
                  return animation;
               }
            }
         }

         return null;
      }

      /// <summary>
      /// Retrieves a single animation based on the ID.
      /// </summary>
      public AnimationData GetAnimationById(Guid id)
      {
         foreach (AnimationCategory category in mData.Categories)
         {
            AnimationData animation = category.Animations.Where(a => a.Id == id).FirstOrDefault();
            if (animation != null)
            {
               return animation;
            }
         }

         return null;
      }

      /// <summary>
      /// Deletes the specified animation from the data.
      /// </summary>
      public bool RemoveAnimation(AnimationData animation)
      {
         if (animation != null)
         {
            AnimationCategory category = GetCategoryForAnimation(animation);
            if (category != null)
            {
               category.Animations.Remove(animation);
               SaveData();
               return true;
            }
         }

         return false;
      }

      /// <summary>
      /// Deletes the specified category from the library.
      /// </summary>
      public void RemoveCategory(AnimationCategory category)
      {
         if (mData.Categories.Contains(category))
         {
            mData.Categories.Remove(category);
         }
      }

      /// <summary>
      /// Gets the category for an animation.
      /// </summary>
      public AnimationCategory GetCategoryForAnimation(AnimationData animation)
      {
         foreach (var category in mData.Categories)
         {
            if (category.Animations.Contains(animation))
            {
               return category;
            }
         }

         return null;
      }

      /// <summary>
      /// Gets the animation category by the provided name.
      /// </summary>
      public AnimationCategory GetCategoryByName(string categoryName)
      {
         return mData.Categories.FirstOrDefault(c => c.Title.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
      }

      /// <summary>
      /// Gets the animation category by the provided ID.
      /// </summary>
      public AnimationCategory GetCategoryById(Guid id)
      {
         return mData.Categories.FirstOrDefault(c => c.Id == id);
      }

      /// <summary>
      /// Adds a new category. Returns false if it finds a duplicate.
      /// </summary>
      public bool AddCategory(string categoryName)
      {
         if (mData.Categories.FirstOrDefault(c => c.Title.Equals(categoryName, StringComparison.OrdinalIgnoreCase)) != null)
         {
            return false;
         }

         mData.Categories.Add(new AnimationCategory(categoryName));
         SaveData();
         return true;
      }

      /// <summary>
      /// Updates an existing category.
      /// </summary>
      public bool UpdateCategory(Guid id, string categoryName)
      {
         // Check to see if there is a duplicate name.
         if (mData.Categories.FirstOrDefault(c => c.Title.Equals(categoryName, StringComparison.OrdinalIgnoreCase)) != null)
         {
            return false;
         }

         AnimationCategory category = mData.Categories.FirstOrDefault(c => c.Id == id);
         if (category != null)
         {
            category.Title = categoryName;
            SaveData();
            return true;
         }

         return false;
      }

      /// <summary>
      /// Adds a new animation command to the provided category, by name. Returns false if it wasn't able to do so.
      /// </summary>
      public bool AddAnimationToCategory(string categoryName, string animationCommand)
      {
         if (!String.IsNullOrEmpty(categoryName) && !String.IsNullOrEmpty(animationCommand))
         {
            AnimationCategory category = GetCategoryByName(categoryName);
            if (category != null)
            {
               // Make sure no animations exist under this command already.
               AnimationData existingAnimation = GetAnimationByCommand(animationCommand);
               if (existingAnimation == null)
               {
                  category.Animations.Add(new AnimationData(animationCommand));
                  SaveData();
                  return true;
               }
            }
         }

         return false;
      }

      /// <summary>
      /// Adds a new animation command to the provided category, by ID. Returns false if it wasn't able to do so.
      /// </summary>
      public Guid AddAnimationToCategoryById(Guid categoryId, string animationCommand)
      {
         if (categoryId != Guid.Empty && !String.IsNullOrEmpty(animationCommand))
         {
            AnimationCategory category = GetCategoryById(categoryId);
            if (category != null)
            {
               // Make sure no animations exist under this command already.
               AnimationData existingAnimation = GetAnimationByCommand(animationCommand);
               if (existingAnimation == null)
               {
                  AnimationData newAnimation = new AnimationData(animationCommand);
                  category.Animations.Add(newAnimation);
                  SaveData();
                  return newAnimation.Id;
               }
            }
         }

         return Guid.Empty;
      }

      /// <summary>
      /// Combs the provided chat message string for an animation command.
      /// Only grabs the first qualifying command.
      /// </summary>
      public AnimationData GetAnimationFromMessage(string message)
      {
         if (!String.IsNullOrEmpty(message) && mData != null)
         {
            string[] wordyWords = message.Split(' ');
            foreach (string wordyWord in wordyWords)
            {
               // Just grab the first one.
               return GetAnimationByCommand(wordyWord);
            }
         }

         return null;
      }

      #endregion

      #region Private Methods

      private bool UserIsThrottled(string username, out ThrottledUserData user)
      {
         user = Bot.BotSettings.ThrottledUsers.FirstOrDefault(t => t.Name.Equals(username, StringComparison.OrdinalIgnoreCase));
         if (user != null &&
             (user.IsBanned || DateTime.Now.Subtract(user.LastThrottled).TotalSeconds < user.PersonalThrottleRate))
         {
            return true;
         }

         return false;
      }

      #endregion

      #region Properties

      /// <summary>
      /// A reference to the bot.
      /// </summary>
      public GIFBot Bot { get; private set; }

      /// <summary>
      /// The path to where the data is stored.
      /// </summary>
      public string DataFilePath { get; private set; }

      /// <summary>
      /// Accessor for the animation data.
      /// </summary>
      public AnimationLibrary Data { get { return mData; } }

      /// <summary>
      /// Accessor for the primary processor.
      /// </summary>
      public AnimationProcessor PrimaryProcessor { get { return mPrimaryProcessor; } }

      /// <summary>
      /// The animations data file.
      /// </summary>
      public const string kFileName = "gifbot_animations.json";

      #endregion

      #region Private Members

      /// <summary>
      /// The animations that are currently active and playing.
      /// </summary>
      private ConcurrentDictionary<string, AnimationRequest> mActiveAnimations = new ConcurrentDictionary<string, AnimationRequest>();

      /// <summary>
      /// Queue: This is a queue of animations that need to be triggered. All animations should go into this queue
      /// and get processed on a thread that pauses between animations.
      /// </summary>
      private BlockingCollection<AnimationRequest> mAnimationQueue = new BlockingCollection<AnimationRequest>();

      /// <summary>
      /// The data for the animation commands.
      /// </summary>
      private AnimationLibrary mData = new AnimationLibrary();

      /// <summary>
      /// The primary animation processor.
      /// </summary>
      private AnimationProcessor mPrimaryProcessor;

      #endregion
   }
}
