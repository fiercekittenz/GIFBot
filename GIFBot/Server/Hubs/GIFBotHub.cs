using GIFBot.Server.Azure;
using GIFBot.Server.GIFBot;
using GIFBot.Shared;
using GIFBot.Shared.Models.Animation;
using GIFBot.Shared.Models.Features;
using GIFBot.Shared.Models.GIFBot;
using GIFBot.Shared.Models.Tiltify;
using GIFBot.Shared.Models.Twitch;
using GIFBot.Shared.Models.Visualization;
using GIFBot.Shared.Utility;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Threading;
using System.Threading.Tasks;
using Telerik.DataSource;
using Telerik.DataSource.Extensions;

namespace GIFBot.Server.Hubs
{
   public class GIFBotHub : Hub
   {
      #region Public Methods

      /// <summary>
      /// Constructor
      /// </summary>
      public GIFBotHub(GIFBot.GIFBot bot, IWebHostEnvironment webHostEnvironment)
      {
         Bot = bot;
         WebHostEnvironment = webHostEnvironment;
      }

      /// <summary>
      /// Override called when a new client connects.
      /// </summary>
      public override async Task OnConnectedAsync()
      {
         await Bot.SendAllLogMessages();
         await SendStreamlabsAuthToken();

         if (Bot != null && Bot.StickersManager != null && Bot.StickersManager.Data != null)
         {
            await Clients.All.SendAsync("SendAllPlacedStickers", JsonConvert.SerializeObject(Bot.StickersManager.PlacedStickers));
            await Clients.All.SendAsync("UpdateStickerAudioSettings", Bot.StickersManager.Data.Audio, Bot.StickersManager.Data.Volume);
         }

         if (Bot != null && Bot.BackdropManager != null && Bot.BackdropManager.ActiveBackdrop != null)
         {
            await Clients.All.SendAsync("HangBackdrop", JsonConvert.SerializeObject(Bot.BackdropManager.ActiveBackdrop));
         }
      }

      #endregion

      #region Bot Settings

      /// <summary>
      /// Fetches the current user based on the oauth token provided.
      /// </summary>
      public TwitchUserData GetCurrentUser(string oauth)
      {
         return TwitchEndpointHelpers.GetCurrentUser(oauth);
      }

      /// <summary>
      /// Verifies the bot settings has been loaded.
      /// </summary>
      public bool VerifyBotSettings()
      {
         if (Bot != null)
         {
            return Bot.BotSettingsLoaded;
         }

         return false;
      }

      /// <summary>
      /// Returns the bot's settings.
      /// </summary>
      public string GetBotSettings()
      {
         if (Bot != null)
         {
            return JsonConvert.SerializeObject(Bot.BotSettings);
         }

         return String.Empty;
      }

      /// <summary>
      /// Updates the bot settings.
      /// </summary>
      public void UpdateBotSettings(string jsonData, bool reconnectToTwitch)
      {
         if (Bot != null)
         {
            // Cache old settings for comparison.
            string oldChannelName = Bot.BotSettings.ChannelName;
            long oldTiltifyCampaignId = Bot.BotSettings.TiltifyActiveCampaign;

            Bot.BotSettings = JsonConvert.DeserializeObject<BotSettings>(jsonData);
            if (reconnectToTwitch)
            {
               bool channelHasChanged = ((String.IsNullOrEmpty(oldChannelName) && 
                                          !String.IsNullOrEmpty(Bot.BotSettings.ChannelName)) ||
                                         (!String.IsNullOrEmpty(oldChannelName) && 
                                          !String.IsNullOrEmpty(Bot.BotSettings.ChannelName) && 
                                          !oldChannelName.Equals(Bot.BotSettings.ChannelName, StringComparison.OrdinalIgnoreCase)));

               Bot.ConnectToTwitch(channelHasChanged);
            }

            if (oldTiltifyCampaignId != Bot.BotSettings.TiltifyActiveCampaign && 
                Bot.BotSettings.TiltifyActiveCampaign > 0)
            {
               Bot.TiltifyManager.LastAlertedDonationId = -1;
            }

            Bot.StreamElementsManager.InitializeWithChannelId();
         }
      }

      #endregion

      #region Throttled Users

      public bool AddThrottledUser(string data)
      {
         ThrottledUserData throttledUser = JsonConvert.DeserializeObject<ThrottledUserData>(data);
         if (Bot != null &&
             throttledUser != null &&
             !Bot.BotSettings.ThrottledUsers.Where(t => t.Name.Equals(throttledUser.Name, StringComparison.OrdinalIgnoreCase)).Any())
         {
            Bot.BotSettings.ThrottledUsers.Add(throttledUser);
            Bot.SaveSettings();
            return true;
         }

         return false;
      }

      public bool DeleteThrottledUser(string throttledUserName)
      {
         if (Bot != null && !String.IsNullOrEmpty(throttledUserName))
         {
            ThrottledUserData existingUser = Bot.BotSettings.ThrottledUsers.FirstOrDefault(t => t.Name.Equals(throttledUserName, StringComparison.OrdinalIgnoreCase));
            if (existingUser != null)
            {
               Bot.BotSettings.ThrottledUsers.Remove(existingUser);
               Bot.SaveSettings();
               return true;
            }
         }

         return false;
      }

      public bool UpdateThrottledUser(string data)
      {
         ThrottledUserData throttledUser = JsonConvert.DeserializeObject<ThrottledUserData>(data);
         if (Bot != null && throttledUser != null)
         {
            ThrottledUserData existingUser = Bot.BotSettings.ThrottledUsers.FirstOrDefault(t => t.Name.Equals(throttledUser.Name, StringComparison.OrdinalIgnoreCase));
            if (existingUser != null)
            {
               existingUser.IsBanned = throttledUser.IsBanned;
               existingUser.PersonalThrottleRate = throttledUser.PersonalThrottleRate;
               Bot.SaveSettings();
               return true;
            }
         }

         return false;
      }

      #endregion

      #region User Groups

      /// <summary>
      /// Adds a user group to the bot settings.
      /// </summary>
      public Guid AddUserGroup(string name)
      {
         if (Bot != null)
         {
            if (!Bot.BotSettings.UserGroups.Where(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).Any())
            {
               UserGroup group = new UserGroup(name);
               Bot.BotSettings.UserGroups.Add(group);
               Bot.SaveSettings();
               return group.Id;
            }
         }

         return Guid.Empty;
      }

      /// <summary>
      /// Deletes the specified user group.
      /// </summary>
      public bool DeleteUserGroup(Guid id)
      {
         if (Bot != null)
         {
            UserGroup group = Bot.BotSettings.UserGroups.FirstOrDefault(g => g.Id == id);
            if (group != null)
            {
               var dependentAnimations = Bot.AnimationManager.GetAllAnimations().Where(a => a.Access == AnimationEnums.AccessType.UserGroup && a.RestrictedToUserGroup == id);
               if (dependentAnimations.Any())
               {
                  // Cannot remove a group if there are dependent animations.
                  return false;
               }

               Bot.BotSettings.UserGroups.Remove(group);
               Bot.SaveSettings();
               return true;
            }
         }

         return false;
      }

      /// <summary>
      /// Clones the specified group into a new one.
      /// </summary>
      public bool CloneUserGroup(Guid id)
      {
         if (Bot != null)
         {
            UserGroup group = Bot.BotSettings.UserGroups.FirstOrDefault(g => g.Id == id);
            if (group != null)
            {
               UserGroup clonedGroup = new UserGroup($"{group.Name} Copy");
               foreach (var user in group.UserEntries)
               {
                  clonedGroup.UserEntries.Add(new UserEntry(user.Name));
               }

               Bot.BotSettings.UserGroups.Add(clonedGroup);
               Bot.SaveSettings();
               return true;
            }
         }

         return false;
      }

      /// <summary>
      /// Renames the specified user group.
      /// </summary>
      public bool RenameUserGroup(Guid id, string newName)
      {
         if (Bot != null)
         {
            UserGroup group = Bot.BotSettings.UserGroups.FirstOrDefault(g => g.Id == id);
            UserGroup groupByName = Bot.BotSettings.UserGroups.FirstOrDefault(g => g.Name.Equals(newName, StringComparison.OrdinalIgnoreCase));
            if (group != null && groupByName == null)
            {
               group.Name = newName;
               Bot.SaveSettings();
               return true;
            }
         }

         return false;
      }

      /// <summary>
      /// Adds the specified user to the group.
      /// </summary>
      public bool AddUserToGroup(Guid groupId, string username)
      {
         if (Bot != null)
         {
            UserGroup group = Bot.BotSettings.UserGroups.FirstOrDefault(g => g.Id == groupId);
            if (group != null)
            {
               UserEntry existingUser = group.UserEntries.FirstOrDefault(u => u.Name.Equals(username, StringComparison.OrdinalIgnoreCase));
               if (existingUser == null)
               {
                  group.UserEntries.Add(new UserEntry(username));
                  Bot.SaveSettings();
                  return true;
               }
            }
         }

         return false;
      }

      /// <summary>
      /// Removes the specified user from the group.
      /// </summary>
      public bool RemoveUserFromGroup(Guid groupId, string username)
      {
         if (Bot != null)
         {
            UserGroup group = Bot.BotSettings.UserGroups.FirstOrDefault(g => g.Id == groupId);
            if (group != null)
            {
               UserEntry existingUser = group.UserEntries.FirstOrDefault(u => u.Name.Equals(username, StringComparison.OrdinalIgnoreCase));
               if (existingUser != null)
               {
                  group.UserEntries.Remove(existingUser);
                  Bot.SaveSettings();
                  return true;
               }
            }
         }

         return false;
      }

      /// <summary>
      /// Gets a list of user groups by name.
      /// </summary>
      public string GetUserGroupList()
      {
         if (Bot != null)
         {
            return Bot.GetUserGroupListAsJson();
         }

         return String.Empty;
      }

      /// <summary>
      /// Finds the group by its name and returns its ID.
      /// </summary>
      public Guid GetGroupIdByName(string groupName)
      {
         if (Bot != null)
         {
            return Bot.GetUserGroupIdByName(groupName);
         }

         return Guid.Empty;
      }

      /// <summary>
      /// Finds the group by its ID and returns the name.
      /// </summary>
      public string GetUserGroupNameById(Guid groupId)
      {
         if (Bot != null)
         {
            return Bot.GetUserGroupNameById(groupId);
         }

         return String.Empty;
      }

      #endregion

      #region Animations

      /// <summary>
      /// Retrieve the file path to the animations layer's HTML page that is used in broadcaster software.
      /// </summary>
      public string GetAnimationsPath(AnimationEnums.AnimationLayer layer)
      {
         string htmlPage = "animations.html";
         if (layer == AnimationEnums.AnimationLayer.Secondary)
         {
            htmlPage = "animations_secondary.html";
         }

         string currentDirectory = System.Environment.CurrentDirectory.Replace("Server", "Client");
         return Path.Combine(currentDirectory, "wwwroot", htmlPage);
      }

      /// <summary>
      /// Gets a list of just the animation commands for selection.
      /// </summary>
      public string GetAnimationCommandsList()
      {
         List<string> commands = new List<string>();

         foreach (var anim in Bot.AnimationManager.GetAllAnimations())
         {
            commands.Add(anim.Command);
         }

         commands.Sort();

         return JsonConvert.SerializeObject(commands);
      }

      /// <summary>
      /// Returns a serialized list of animation identifiers and basic information
      /// for use with a selection component.
      /// </summary>
      public string GetAnimationOptions()
      {
         List<AnimationSelectorItem> options = new List<AnimationSelectorItem>();

         foreach (var anim in Bot.AnimationManager.GetAllAnimations())
         {
            options.Add(new AnimationSelectorItem() { Id = anim.Id, DisplayName = anim.Command });
         }

         return JsonConvert.SerializeObject(options);
      }

      /// <summary>
      /// Manually trigger an animation.
      /// </summary>
      public void PlayAnimation(string animationCommand)
      {
         AnimationData animation = Bot.AnimationManager.GetAnimationByCommand(animationCommand);
         if (animation != null)
         {
            Bot.AnimationManager.ForceQueueAnimation(animation, Bot.BotSettings.BotName, "123");
         }
      }

      /// <summary>
      /// Retrieves a simplified version of the animations data in tree format for web display purposes.
      /// </summary>
      public string GetAnimationTreeData()
      {
         IEnumerable<AnimationTreeItem> treeData = Bot?.AnimationManager.Data.GetTreeData();
         return JsonConvert.SerializeObject(treeData);
      }

      /// <summary>
      /// Adds a new category to the animation library. Returns false if it was unable to do so.
      /// </summary>
      public bool AddAnimationCategory(string categoryName)
      {
         if (Bot != null && Bot.AnimationManager != null)
         {
            return Bot.AnimationManager.AddCategory(categoryName);
         }

         return false;
      }

      public bool UpdateAnimationCategory(Guid id, string categoryName)
      {
         if (Bot != null && Bot.AnimationManager != null)
         {
            return Bot.AnimationManager.UpdateCategory(id, categoryName);
         }

         return false;
      }

      /// <summary>
      /// Moves animations to the specified category.
      /// </summary>
      public bool MoveAnimations(string rawAnimationIds, Guid categoryId)
      {
         AnimationCategory category = Bot.AnimationManager.GetCategoryById(categoryId);
         List<Guid> animationIds = JsonConvert.DeserializeObject<List<Guid>>(rawAnimationIds);

         if (animationIds.Any() && category != null)
         {
            foreach (var animationId in animationIds)
            {
               AnimationData animation = Bot.AnimationManager.GetAnimationById(animationId);
               if (animation != null && !category.Animations.Contains(animation))
               {
                  AnimationCategory oldCategory = Bot.AnimationManager.GetCategoryForAnimation(animation);
                  if (oldCategory != null && oldCategory != category)
                  {
                     oldCategory.Animations.Remove(animation);
                     category.Animations.Add(animation);
                  }
               }
            }

            Bot.AnimationManager.SaveData();
            return true;
         }

         return false;
      }

      public Guid CloneAnimation(Guid sourceAnimationId, string clonedCommand)
      {
         AnimationData sourceAnimation = Bot.AnimationManager.GetAnimationById(sourceAnimationId);
         if (sourceAnimation != null)
         {
            AnimationData clonedAnimation = (AnimationData)sourceAnimation.Clone();
            clonedAnimation.Command = clonedCommand;

            AnimationCategory category = Bot.AnimationManager.GetCategoryForAnimation(sourceAnimation);
            if (category != null)
            {
               category.Animations.Add(clonedAnimation);
               Bot.AnimationManager.SaveData();
               return clonedAnimation.Id;
            }
         }

         return Guid.Empty;
      }

      /// <summary>
      /// Saves the category's updated information.
      /// </summary>
      public bool SaveAnimationCategory(Guid id, string categoryName)
      {
         AnimationCategory category = Bot.AnimationManager.GetCategoryById(id);
         if (category != null)
         {
            category.Title = categoryName;
            Bot.AnimationManager.SaveData();
            return true;
         }

         return false;
      }

      /// <summary>
      /// Returns the data for the specified animation by name.
      /// </summary>
      public string GetAnimationByName(string animationCommand)
      {
         AnimationData animation = Bot.AnimationManager.GetAnimationByCommand(animationCommand);
         if (animation != null)
         {
            return JsonConvert.SerializeObject(animation);
         }

         return String.Empty;
      }

      /// <summary>
      /// Returns the data for the specified animation by ID.
      /// </summary>
      public string GetAnimationById(Guid id)
      {
         AnimationData animation = Bot.AnimationManager.GetAnimationById(id);
         if (animation != null)
         {
            return JsonConvert.SerializeObject(animation);
         }

         return String.Empty;
      }

      /// <summary>
      /// Adds a new animation to the specified category. Returns false if unsuccessful.
      /// </summary>
      public bool AddAnimationToCategory(string categoryName, string animationCommand)
      {
         if (Bot != null && Bot.AnimationManager != null)
         {
            return Bot.AnimationManager.AddAnimationToCategory(categoryName, animationCommand);
         }

         return false;
      }

      /// <summary>
      /// Adds a new animation to the specified category. Returns the guid of the animation.
      /// </summary>
      public Guid AddAnimationToCategoryById(Guid categoryId, string animationCommand)
      {
         if (Bot != null && Bot.AnimationManager != null && categoryId != Guid.Empty)
         {
            return Bot.AnimationManager.AddAnimationToCategoryById(categoryId, animationCommand);
         }

         return Guid.Empty;
      }

      /// <summary>
      /// Sets the visual file name on the provided animation. Returns false if unsuccessful.
      /// </summary>
      public bool SetAnimationVisual(string animationCommand, string visualFileName)
      {
         if (Bot != null && Bot.AnimationManager != null)
         {
            AnimationData animation = Bot.AnimationManager.GetAnimationByCommand(animationCommand);
            if (animation != null)
            {
               animation.Visual = visualFileName;
               if (!String.IsNullOrEmpty(animation.Visual))
               {
                  Tuple<int, int> dimensions = AnimationLibrary.GetVisualFileDimensions(animation.Visual);
                  animation.Placement.Width = dimensions.Item1;
                  animation.Placement.Height = dimensions.Item2;
               }

               Bot.AnimationManager.SaveData();
               return true;
            }
         }

         return false;
      }

      /// <summary>
      /// Sets the audio file name on the provided animation. Returns false if unsuccessful.
      /// </summary>
      public bool SetAnimationAudio(string animationCommand, string audioFileName)
      {
         if (Bot != null && Bot.AnimationManager != null)
         {
            AnimationData animation = Bot.AnimationManager.GetAnimationByCommand(animationCommand);
            if (animation != null)
            {
               animation.Audio = audioFileName;
               Bot.AnimationManager.SaveData();
               return true;
            }
         }

         return false;
      }

      /// <summary>
      /// Sets the volume the provided animation. Returns false if unsuccessful.
      /// </summary>
      public bool SetAnimationVolume(string animationCommand, double volume)
      {
         if (Bot != null && Bot.AnimationManager != null)
         {
            AnimationData animation = Bot.AnimationManager.GetAnimationByCommand(animationCommand);
            if (animation != null)
            {
               animation.Volume = volume;
               Bot.AnimationManager.SaveData();
               return true;
            }
         }

         return false;
      }

      /// <summary>
      /// Returns a json-encoded string Tuple<int, int> with the width and height of the specified visual file.
      /// </summary>
      public string GetVisualDimensions(string visualFileName)
      {
         Tuple<int, int> dimensions = AnimationLibrary.GetVisualFileDimensions(visualFileName);
         return JsonConvert.SerializeObject(dimensions);
      }

      /// <summary>
      /// Retrieves the placement values (width, height, top, left) for the specified command.
      /// </summary>
      public AnimationPlacement GetAnimationVisualPlacement(string animationCommand)
      {
         AnimationData animation = Bot.AnimationManager.GetAnimationByCommand(animationCommand);
         if (animation != null)
         {
            return animation.Placement;
         }

         return null;
      }

      /// <summary>
      /// Sets the placement values for the animation.
      /// </summary>
      public bool SetAnimationPosition(string animationCommand, AnimationPlacement placementData)
      {
         AnimationData animation = Bot.AnimationManager.GetAnimationByCommand(animationCommand);
         if (animation != null)
         {
            animation.Placement.Width = placementData.Width;
            animation.Placement.Height = placementData.Height;
            animation.Placement.Top = placementData.Top;
            animation.Placement.Left = placementData.Left;

            Bot.AnimationManager.SaveData();

            return true;
         }

         return false;
      }

      /// <summary>
      /// Sets the very basic settings. Largely coming from the tutorial.
      /// </summary>
      public bool SetAnimationBasicSettings(string animationCommand, int durationMs, int cooldownMinutes, AnimationEnums.AccessType access)
      {
         AnimationData animation = Bot.AnimationManager.GetAnimationByCommand(animationCommand);
         if (animation != null)
         {
            animation.DurationMilliseconds = durationMs;
            animation.MainCooldownMinutes = cooldownMinutes;
            animation.Access = access;

            Bot.AnimationManager.SaveData();

            return true;
         }

         return false;
      }

      /// <summary>
      /// Tests the animation by forcing it into the queue. No permissions check.
      /// </summary>
      public void TestAnimation(string animationData)
      {
         AnimationData animation = JsonConvert.DeserializeObject<AnimationData>(animationData);
         if (animation != null)
         {
            Bot.AnimationManager.ForceQueueAnimation(animation, Bot.BotSettings.BotName, "123");
         }
      }

      /// <summary>
      /// Deletes the specified animation.
      /// </summary>
      public bool DeleteAnimations(string rawIds)
      {
         List<Guid> ids = JsonConvert.DeserializeObject<List<Guid>>(rawIds);
         if (ids.Any())
         {
            bool result = true;

            foreach (var id in ids)
            {
               AnimationData animation = Bot.AnimationManager.GetAnimationById(id);
               if (animation != null)
               {
                  result &= Bot.AnimationManager.RemoveAnimation(animation);
               }
            }

            return result;
         }

         return false;
      }

      /// <summary>
      /// Deletes the specified category.
      /// </summary>
      public bool DeleteCategory(Guid id)
      {
         AnimationCategory category = Bot.AnimationManager.GetCategoryById(id);
         if (category != null && !category.Animations.Any())
         {
            Bot.AnimationManager.RemoveCategory(category);
            Bot.AnimationManager.SaveData();
            return true;
         }

         return false;
      }

      /// <summary>
      /// Returns the selected category.
      /// </summary>
      public string GetCategoryById(Guid id)
      {
         AnimationCategory category = Bot.AnimationManager.GetCategoryById(id);
         if (category != null)
         {
            return JsonConvert.SerializeObject(category);
         }

         return String.Empty;
      }

      /// <summary>
      /// Takes the raw data of an updated animation and applies it to the existing data.
      /// </summary>
      /// <param name="animationData"></param>
      public void SaveAnimation(string animationData)
      {
         AnimationData animation = JsonConvert.DeserializeObject<AnimationData>(animationData);
         if (animation != null)
         {
            Bot.AnimationManager.UpdateAnimation(animation);
         }
      }

      /// <summary>
      /// Adds a new variant to the specified animation.
      /// </summary>
      public bool AddVariantToAnimation(Guid animationId, AnimationVariantData variantData)
      {
         AnimationData animation = Bot.AnimationManager.GetAnimationById(animationId);
         if (animation != null)
         {
            animation.AddVariant(variantData);
            Bot.AnimationManager.SaveData();
            return true;
         }

         return false;
      }

      /// <summary>
      /// Updates the specified variation on the animation.
      /// </summary>
      public bool UpdateAnimationVariant(Guid animationId, AnimationVariantData updatedVariantData)
      {
         AnimationData animation = Bot.AnimationManager.GetAnimationById(animationId);
         if (animation != null)
         {
            AnimationVariantData existingVariantData = animation.Variants.FirstOrDefault(v => v.Id == updatedVariantData.Id);
            if (existingVariantData != null)
            {
               // Only update some of the fields. Do not allow updating of the audio or visual files,
               // because the front-end won't allow for upload fields to be displayed in place of a simple
               // text field.
               existingVariantData.DurationMilliseconds = updatedVariantData.DurationMilliseconds;
               existingVariantData.AudioTimingOffsetMilliseconds = updatedVariantData.AudioTimingOffsetMilliseconds;
               existingVariantData.Volume = updatedVariantData.Volume;
               existingVariantData.PrePlayText = updatedVariantData.PrePlayText;
               existingVariantData.PostPlayText = updatedVariantData.PostPlayText;

               Bot.AnimationManager.SaveData();

               return true;
            }
         }

         return false;
      }

      /// <summary>
      /// Plays the specified variant of the designated animation.
      /// </summary>
      public void PlayVariantAnimation(Guid animationId, Guid variantId)
      {
         AnimationData animation = Bot.AnimationManager.GetAnimationById(animationId);
         if (animation != null)
         {
            Bot.AnimationManager.ForceQueueAnimation(animation, String.Empty, "Tester", "123", variantId);
         }
      }

      /// <summary>
      /// Removes the specified variant from the designated animation.
      /// </summary>
      public bool DeleteVariantFromAnimation(Guid animationId, Guid variantId)
      {
         AnimationData animation = Bot.AnimationManager.GetAnimationById(animationId);
         if (animation != null)
         {
            animation.RemoveVariant(variantId);
            Bot.AnimationManager.SaveData();
            return true;
         }

         return false;
      }

      /// <summary>
      /// Handles the adding of a chained animation command to the specified animation by its ID.
      /// </summary>
      public bool AddChainedCommandToAnimation(Guid animationId, string command)
      {
         if (!String.IsNullOrEmpty(command))
         {
            AnimationData animation = Bot.AnimationManager.GetAnimationById(animationId);
            if (animation != null)
            {
               if (!animation.ChainedAnimations.Contains(command))
               {
                  animation.ChainedAnimations.Add(command);
                  Bot.AnimationManager.SaveData();
                  return true;
               }
            }
         }

         return false;
      }

      /// <summary>
      /// Remove a chained command from the specified animation.
      /// </summary>
      public bool DeleteChainedCommandFromAnimation(Guid animationId, string command)
      {
         if (!String.IsNullOrEmpty(command))
         {
            AnimationData animation = Bot.AnimationManager.GetAnimationById(animationId);
            if (animation != null)
            {
               if (animation.ChainedAnimations.Contains(command))
               {
                  animation.ChainedAnimations.Remove(command);
                  Bot.AnimationManager.SaveData();
                  return true;
               }
            }
         }

         return false;
      }

      /// <summary>
      /// Sets the display test mode of the primary animations pane.
      /// </summary>
      public async Task SetDisplayTestMode(bool isDisplayTestModeOn, Guid animationId)
      {
         if (Bot != null && Bot.AnimationManager != null)
         {
            AnimationData animation = Bot.AnimationManager.GetAnimationById(animationId);
            if (animation != null || !isDisplayTestModeOn)
            {
               // TODO: Send the appropriate IHubClients list based on the animations layer.
               await Bot.AnimationManager.SetDisplayTestMode(isDisplayTestModeOn, animation, AnimationEnums.AnimationLayer.Primary, Bot.GIFBotHub.Clients);
            }
         }
      }

      /// <summary>
      /// Updates the active display test dimensions.
      /// </summary>
      public async Task UpdateDisplayTestDimensions(int width, int height, AnimationEnums.AnimationLayer layer)
      {
         if (Bot != null && Bot.AnimationManager != null)
         {
            await Bot.AnimationManager.UpdateDisplayTestDimensions(width, height, layer, Bot.GIFBotHub.Clients);
         }
      }

      public void ForceStopAnimation(string command)
      {
         // TODO: Support stopping based on the layer.
         _ = Bot.ForceStopAnimation(AnimationEnums.AnimationLayer.Primary, command);
      }

      public bool EnableAnimations(string animationGuidsRaw)
      {
         return ToggleAnimations(animationGuidsRaw, false);
      }

      public bool DisableAnimations(string animationGuidsRaw)
      {
         return ToggleAnimations(animationGuidsRaw, true);
      }

      private bool ToggleAnimations(string animationGuidsRaw, bool newValue)
      {
         if (Bot != null && Bot.AnimationManager != null && !String.IsNullOrEmpty(animationGuidsRaw))
         {
            List<Guid> animationGuids = JsonConvert.DeserializeObject<List<Guid>>(animationGuidsRaw);
            foreach (var guid in animationGuids)
            {
               AnimationData animation = Bot.AnimationManager.GetAnimationById(guid);
               if (animation != null)
               {
                  animation.Disabled = newValue;
               }
            }

            Bot.AnimationManager.SaveData();
            return true;
         }

         return false;
      }

      #endregion

      #region Streamlabs

      public async Task SendStreamlabsAuthToken()
      {
         await Clients.All.SendAsync("SendStreamlabsAuthToken", Bot.BotSettings.StreamlabsOauthToken);
      }

      public string GetStreamlabsAuthToken()
      {
         return Bot.BotSettings.StreamlabsOauthToken;
      }

      #endregion

      #region Regurgitator

      /// <summary>
      /// Fetches a stub of basic information on the available packages.
      /// </summary>
      public string GetRegurgitatorPackages()
      {
         List<RegurgitatorPackageBase> results = new List<RegurgitatorPackageBase>();

         lock (Bot.RegurgitatorManager.PackagesMutex)
         { 
            foreach (var package in Bot?.RegurgitatorManager?.Data?.Packages)
            {
               results.Add(package);
            }
         }

         return JsonConvert.SerializeObject(results);
      }

      /// <summary>
      /// Retrieves the settings associated with the Regurgitator.
      /// </summary>
      /// <returns>Fluff you Carole Baskin.</returns>
      public RegurgitatorSettings GetRegurgitatorSettings(Guid packageId)
      {
         lock (Bot.RegurgitatorManager.PackagesMutex)
         {
            RegurgitatorPackage package = Bot?.RegurgitatorManager?.Data.Packages.FirstOrDefault(p => p.Id == packageId);
            if (package != null)
            { 
               return package.Settings;
            }
         }
         
         return new RegurgitatorSettings();
      }

      /// <summary>
      /// Adds a new package to the regurgitator.
      /// </summary>
      public Guid AddRegurgitatorPackage(string name)
      {
         if (!string.IsNullOrEmpty(name))
         {
            lock (Bot.RegurgitatorManager.PackagesMutex)
            {
               RegurgitatorPackage existing = Bot?.RegurgitatorManager?.Data.Packages.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
               if (existing == null)
               {
                  RegurgitatorPackage package = new RegurgitatorPackage(name);
                  Bot.RegurgitatorManager.Data.Packages.Add(package);
                  Bot.RegurgitatorManager.SaveData();
                  return package.Id;
               }
            }
         }

         return Guid.Empty;
      }

      /// <summary>
      /// Deletes the specified package.
      /// </summary>
      public void DeleteRegurgitatorPackage(Guid packageId)
      {
         lock (Bot.RegurgitatorManager.PackagesMutex)
         {
            RegurgitatorPackage existing = Bot?.RegurgitatorManager?.Data.Packages.FirstOrDefault(p => p.Id == packageId);
            if (existing != null)
            {
               Bot.RegurgitatorManager.Data.Packages.Remove(existing);
               Bot.RegurgitatorManager.SaveData();
            }
         }
      }

      /// <summary>
      /// Adds a new regurgitator entry.
      /// </summary>
      public RegurgitatorEntry AddRegurgitatorEntry(Guid packageId, string entry)
      {
         RegurgitatorPackage package = null;
         lock (Bot.RegurgitatorManager.PackagesMutex)
         {
            package = Bot?.RegurgitatorManager?.Data.Packages.FirstOrDefault(p => p.Id == packageId);
         }

         if (package != null)
         {
            RegurgitatorEntry newEntry = new RegurgitatorEntry(entry);
            package.Entries.Add(newEntry);
            Bot.RegurgitatorManager.SaveData();

            return newEntry;
         }

         return null;
      }

      /// <summary>
      /// Removes a single regurgitator entry by its id.
      /// </summary>
      public void RemoveRegurgitatorEntry(Guid packageId, Guid id)
      {
         RegurgitatorPackage package = null;
         lock (Bot.RegurgitatorManager.PackagesMutex)
         {
            package = Bot?.RegurgitatorManager?.Data.Packages.FirstOrDefault(p => p.Id == packageId);
         }

         if (package != null)
         {
            RegurgitatorEntry entry = package.Entries.FirstOrDefault(e => e.Id == id);
            if (entry != null)
            {
               package.Entries.Remove(entry);
               Bot.RegurgitatorManager.SaveData();
            }
         }
      }

      /// <summary>
      /// Force clears all entry data.
      /// </summary>
      public void ClearRegurgitatorEntries(Guid packageId)
      {
         RegurgitatorPackage package = null;
         lock (Bot.RegurgitatorManager.PackagesMutex)
         {
            package = Bot?.RegurgitatorManager?.Data.Packages.FirstOrDefault(p => p.Id == packageId);
         }

         if (package != null)
         {
            package.Entries.Clear();
            Bot.RegurgitatorManager.SaveData();
         }
      }

      /// <summary>
      /// Updates the regurgitator feature settings.
      /// </summary>
      public void SetRegurgitatorSettings(Guid packageId, RegurgitatorSettings settings)
      {
         RegurgitatorPackage package = null;
         lock (Bot.RegurgitatorManager.PackagesMutex)
         {
            package = Bot?.RegurgitatorManager?.Data.Packages.FirstOrDefault(p => p.Id == packageId);
         }

         if (package != null)
         {
            package.Settings = settings;
            Bot.RegurgitatorManager.SaveData();
         }
      }

      /// <summary>
      /// Fetches regurgitator entries based on the Telerik data source request.
      /// </summary>
      public async Task<DataEnvelope<RegurgitatorEntry>> GetRegurgitatorEntries(Guid packageId, DataSourceRequest request)
      {
         RegurgitatorPackage package = null;
         lock (Bot.RegurgitatorManager.PackagesMutex)
         {
            package = Bot?.RegurgitatorManager?.Data.Packages.FirstOrDefault(p => p.Id == packageId);
         }

         if (package != null)
         {
            DataSourceResult processedData = await package.GetQueryableDataSource().ToDataSourceResultAsync(request);

            DataEnvelope<RegurgitatorEntry> result = new DataEnvelope<RegurgitatorEntry>() {
               CurrentPageData = processedData.Data as List<RegurgitatorEntry>,
               TotalItemCount = processedData.Total
            };

            return result;
         }

         return new DataEnvelope<RegurgitatorEntry>();
      }

      #endregion

      #region Mode Toggles

      /// <summary>
      /// Toggles bonkers mode.
      /// </summary>
      public async Task ToggleBonkersMode()
      {
         Bot.CrazyModeEnabled = !Bot.CrazyModeEnabled;
         await Clients.All.SendAsync("UpdateBonkersModeState", Bot.CrazyModeEnabled);
      }

      /// <summary>
      /// Toggles streamer only mode.
      /// </summary>
      public async Task ToggleStreamerOnlyMode()
      {
         Bot.IsStreamerOnlyMode = !Bot.IsStreamerOnlyMode;
         await Clients.All.SendAsync("UpdateStreamerOnlyModeState", Bot.IsStreamerOnlyMode);
      }

      /// <summary>
      /// Executes the correct commands to clear queued animations and stop any active ones.
      /// </summary>
      public async Task StopAllAnimations()
      {
         // Clear any animations that are in the queue.
         Bot.AnimationManager.ClearAnimations();

         // Force stop all animations.
         await Bot.AnimationManager.StopAllAnimations();
      }

      #endregion

      #region Stickers Hooks

      public async Task ClearAllStickers()
      {
         if (Bot != null && Bot.StickersManager != null)
         {
            await Bot.StickersManager.ClearAllStickers();
         }
      }

      #endregion

      #region TTS

      /// <summary>
      /// Tests the TTS voice with the provided parameters.
      /// </summary>
      public void TestTTSVoice(string voice, double volume)
      {
         using (SpeechSynthesizer synth = new SpeechSynthesizer())
         {
            synth.SetOutputToDefaultAudioDevice(); 
            synth.Volume = (int)(volume * 100);             
            synth.Speak("This is a test of the regurgitator feature.");
         }
      }

      #endregion

      #region Snapper

      public string GetSnapperData()
      {
         if (Bot != null && Bot.SnapperManager != null)
         {
            return JsonConvert.SerializeObject(Bot.SnapperManager.Data);
         }

         return null;
      }

      public void UpdateSnapperData(string rawData)
      {
         if (!String.IsNullOrEmpty(rawData) && Bot != null && Bot.SnapperManager != null)
         {
            Bot.SnapperManager.Data = JsonConvert.DeserializeObject<SnapperData>(rawData);
            Bot.SnapperManager.SaveData();
         }
      }

      public Guid AddSnapperCommand(string command)
      {
         if (!String.IsNullOrEmpty(command) && Bot != null && Bot.SnapperManager != null)
         {
            SnapperCommand existing = Bot.SnapperManager.Data.Commands.FirstOrDefault(c => c.Command.Equals(command, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
               return Guid.Empty;
            }
            else
            {
               SnapperCommand added = new SnapperCommand() {
                  Command = command
               };

               Bot.SnapperManager.Data.Commands.Add(added);
               Bot.SnapperManager.SaveData();

               return added.Id;
            }
         }

         return Guid.Empty;
      }

      public bool UpdateSnapperCommand(string rawData)
      {
         if (!String.IsNullOrEmpty(rawData) && Bot != null && Bot.SnapperManager != null)
         {
            SnapperCommand command = JsonConvert.DeserializeObject<SnapperCommand>(rawData);
            if (command != null)
            {
               if (command.RedemptionType == SnapperRedemptionType.Cheer)
               {
                  // Remove spaces. This command is for chat use and can't have spaces.
                  command.Command = command.Command.Replace(" ", "");
               }

               // First make sure no other commands exist with this command text.
               SnapperCommand existing = Bot.SnapperManager.Data.Commands.FirstOrDefault(c => c.Command.Equals(command.Command, StringComparison.OrdinalIgnoreCase) && c.Id != command.Id);
               if (existing != null)
               {
                  // Duplicate Command text! Not allowed.
                  return false;
               }

               // Command check passed, so now we can update.
               SnapperCommand original = Bot.SnapperManager.Data.Commands.FirstOrDefault(c => c.Id == command.Id);
               if (original != null)
               {
                  int index = Bot.SnapperManager.Data.Commands.IndexOf(original);
                  Bot.SnapperManager.Data.Commands.RemoveAt(index);
                  Bot.SnapperManager.Data.Commands.Insert(index, command);
                  Bot.SnapperManager.SaveData();
                  return true;
               }
            }
         }

         return false;
      }

      public bool DeleteSnapperCommand(Guid id)
      {
         if (Bot != null && Bot.SnapperManager != null)
         {
            SnapperCommand command = Bot.SnapperManager.Data.Commands.FirstOrDefault(c => c.Id == id);
            if (command != null)
            {
               Bot.SnapperManager.Data.Commands.Remove(command);
               Bot.SnapperManager.SaveData();
               return true;
            }
         }

         return false;
      }

      public async Task<bool> TestSnapperCommand(Guid id)
      {
         if (Bot != null && Bot.SnapperManager != null)
         {
            SnapperCommand command = Bot.SnapperManager.Data.Commands.FirstOrDefault(c => c.Id == id);
            if (command != null)
            {
               await Bot.SnapperManager.Snap(command, String.Empty, Bot.BotSettings.ChannelName);
               return true;
            }
         }

         return false;
      }

      #endregion

      #region Greeter

      public string GetGreeterData()
      {
         if (Bot != null && Bot.GreeterManager != null)
         {
            return JsonConvert.SerializeObject(Bot.GreeterManager.Data);
         }

         return null;
      }

      public Guid AddGreeterEntry(string name)
      {
         if (!String.IsNullOrEmpty(name) && Bot != null && Bot.GreeterManager != null)
         {
            GreeterEntry greeterEntry = new GreeterEntry(name);
            Bot.GreeterManager.Data.Entries.Add(greeterEntry);
            Bot.GreeterManager.SaveData();

            return greeterEntry.Id;
         }

         return Guid.Empty;
      }

      public bool UpdateGreeterEntry(string rawData)
      {
         if (!String.IsNullOrEmpty(rawData) && Bot != null && Bot.GreeterManager != null)
         {
            GreeterEntry entry = JsonConvert.DeserializeObject<GreeterEntry>(rawData);
            if (entry != null)
            {
               GreeterEntry original = Bot.GreeterManager.Data.Entries.FirstOrDefault(e => e.Id == entry.Id);
               if (original != null)
               {
                  int index = Bot.GreeterManager.Data.Entries.IndexOf(original);
                  Bot.GreeterManager.Data.Entries.RemoveAt(index);
                  Bot.GreeterManager.Data.Entries.Insert(index, entry);
                  Bot.GreeterManager.SaveData();
                  return true;
               }
            }
         }

         return false;
      }

      public bool DeleteGreeterEntry(Guid id)
      {
         if (id != Guid.Empty && Bot != null && Bot.GreeterManager != null)
         {
            GreeterEntry entry = Bot.GreeterManager.Data.Entries.FirstOrDefault(e => e.Id == id);
            if (entry != null)
            {
               Bot.GreeterManager.Data.Entries.Remove(entry);
               Bot.GreeterManager.SaveData();
               return true;
            }
         }

         return false;
      }

      public string GetGreeterRecipientsFromImport(Guid id)
      {
         if (id != Guid.Empty && Bot != null && Bot.GreeterManager != null)
         {
            GreeterEntry entry = Bot.GreeterManager.Data.Entries.FirstOrDefault(e => e.Id == id);
            if (entry != null)
            {
               return JsonConvert.SerializeObject(entry.Recipients);
            }
         }

         return String.Empty;
      }

      #endregion

      #region Stickers

      public string GetStickerData()
      {
         if (Bot != null && Bot.StickersManager != null)
         {
            return JsonConvert.SerializeObject(Bot.StickersManager.Data);
         }

         return null;
      }

      public void UpdateStickerData(string rawData)
      {
         if (!String.IsNullOrEmpty(rawData) && Bot != null && Bot.StickersManager != null)
         {
            Bot.StickersManager.Data = JsonConvert.DeserializeObject<StickerData>(rawData);
            Bot.StickersManager.Data.Command.Replace(" ", "");
            Bot.StickersManager.SaveData();

            Clients.All.SendAsync("UpdateStickerAudioSettings", Bot.StickersManager.Data.Audio, Bot.StickersManager.Data.Volume);
         }
      }

      public string GetStickerFileDimensions(string stickerFile)
      {
         if (Bot != null && Bot.StickersManager != null && !String.IsNullOrEmpty(stickerFile))
         {
            Tuple<int, int> dimensions = AnimationLibrary.GetVisualFileDimensions(stickerFile);
            return JsonConvert.SerializeObject(dimensions);
         }

         return String.Empty;
      }

      public bool AddStickerCategory(string categoryName)
      {
         if (Bot != null && Bot.StickersManager != null && !String.IsNullOrEmpty(categoryName))
         {
            StickerCategory existing = Bot.StickersManager.Data.Categories.FirstOrDefault(c => c.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
            {
               StickerCategory stickerCategory = new StickerCategory() {
                  Name = categoryName
               };

               Bot.StickersManager.Data.Categories.Add(stickerCategory);
               Bot.StickersManager.SaveData();
               return true;
            }
         }

         return false;
      }

      public bool UpdateStickerCategory(Guid categoryId, string categoryName)
      {
         if (Bot != null && Bot.StickersManager != null && categoryId != Guid.Empty && !String.IsNullOrEmpty(categoryName))
         {
            StickerCategory existing = Bot.StickersManager.Data.Categories.FirstOrDefault(c => c.Id == categoryId);
            if (existing != null)
            {
               existing.Name = categoryName;
               Bot.StickersManager.SaveData();
               return true;
            }
         }

         return false;
      }

      public bool DeleteStickerCategory(Guid categoryId)
      {
         if (Bot != null && Bot.StickersManager != null && categoryId != Guid.Empty)
         {
            StickerCategory existing = Bot.StickersManager.Data.Categories.FirstOrDefault(c => c.Id == categoryId);
            if (existing != null && !existing.Entries.Any())
            {
               Bot.StickersManager.Data.Categories.Remove(existing);
               Bot.StickersManager.SaveData();
               return true;
            }
         }

         return false;
      }

      public bool DeleteStickers(string stickerGuidsRaw)
      {
         if (Bot != null && Bot.StickersManager != null && !String.IsNullOrEmpty(stickerGuidsRaw))
         {
            List<Guid> stickerGuids = JsonConvert.DeserializeObject<List<Guid>>(stickerGuidsRaw);
            foreach (var guid in stickerGuids)
            {
               Tuple<StickerCategory, StickerEntryData> sticker = Bot.StickersManager.GetStickerEntryById(guid);
               if (sticker != null)
               {
                  sticker.Item1.Entries.Remove(sticker.Item2);
               }
            }

            Bot.StickersManager.SaveData();
            return true;
         }

         return false;
      }

      public bool EnableStickers(string stickerGuidsRaw)
      {
         if (Bot != null && Bot.StickersManager != null && !String.IsNullOrEmpty(stickerGuidsRaw))
         {
            List<Guid> stickerGuids = JsonConvert.DeserializeObject<List<Guid>>(stickerGuidsRaw);
            foreach (var guid in stickerGuids)
            {
               Tuple<StickerCategory, StickerEntryData> sticker = Bot.StickersManager.GetStickerEntryById(guid);
               if (sticker != null)
               {
                  sticker.Item2.Enabled = true;
               }
            }

            Bot.StickersManager.SaveData();
            return true;
         }

         return false;
      }

      public bool DisableStickers(string stickerGuidsRaw)
      {
         if (Bot != null && Bot.StickersManager != null && !String.IsNullOrEmpty(stickerGuidsRaw))
         {
            List<Guid> stickerGuids = JsonConvert.DeserializeObject<List<Guid>>(stickerGuidsRaw);
            foreach (var guid in stickerGuids)
            {
               Tuple<StickerCategory, StickerEntryData> sticker = Bot.StickersManager.GetStickerEntryById(guid);
               if (sticker != null)
               {
                  sticker.Item2.Enabled = false;
               }
            }

            Bot.StickersManager.SaveData();
            return true;
         }

         return false;
      }

      public bool MoveStickerCategory(string stickerGuidsRaw, Guid newCategoryId)
      {
         if (Bot != null && Bot.StickersManager != null && newCategoryId != Guid.Empty && !String.IsNullOrEmpty(stickerGuidsRaw))
         {
            StickerCategory newCategory = Bot.StickersManager.Data.Categories.FirstOrDefault(c => c.Id == newCategoryId);
            if (newCategory != null)
            {
               List<Guid> stickerGuids = JsonConvert.DeserializeObject<List<Guid>>(stickerGuidsRaw);
               foreach (var guid in stickerGuids)
               {
                  Tuple<StickerCategory, StickerEntryData> sticker = Bot.StickersManager.GetStickerEntryById(guid);
                  if (sticker != null)
                  {
                     sticker.Item1.Entries.Remove(sticker.Item2);
                     newCategory.Entries.Add(sticker.Item2);
                  }
               }

               Bot.StickersManager.SaveData();
               return true;
            }
         }

         return false;
      }

      public bool AddStickerEntry(string stickerData, Guid categoryId)
      {
         if (Bot != null && Bot.StickersManager != null && !String.IsNullOrEmpty(stickerData))
         {
            StickerEntryData entry = JsonConvert.DeserializeObject<StickerEntryData>(stickerData);
            if (entry != null && !String.IsNullOrEmpty(entry.Visual))
            {
               int topRandomToken = Bot.StickersManager.Data.CanvasHeight - entry.Placement.Height;
               if (topRandomToken < 1)
               {
                  return false;
               }

               int leftRandomToken = Bot.StickersManager.Data.CanvasWidth - entry.Placement.Width;
               if (leftRandomToken < 1)
               {
                  return false;
               }

               StickerCategory category = Bot.StickersManager.Data.Categories.FirstOrDefault(c => c.Id == categoryId);
               if (category != null)
               {
                  category.Entries.Add(entry);
                  Bot.StickersManager.SaveData();

                  return true;
               }
            }
         }

         return false;
      }

      public bool UpdateStickerEntry(string updatedDataRaw)
      {
         StickerEntryData updatedStickerEntry = JsonConvert.DeserializeObject<StickerEntryData>(updatedDataRaw);

         if (Bot != null && Bot.StickersManager != null && updatedStickerEntry != null)
         {
            foreach (var category in Bot.StickersManager.Data.Categories)
            {
               StickerEntryData sticker = category.Entries.FirstOrDefault(s => s.Id == updatedStickerEntry.Id);
               if (sticker != null)
               {
                  int index = category.Entries.IndexOf(sticker);
                  category.Entries.RemoveAt(index);
                  category.Entries.Insert(index, (StickerEntryData)updatedStickerEntry.Clone());
                  Bot.StickersManager.SaveData();
                  return true;
               }
            }
         }

         return false;
      }

      public bool DeleteStickerEntry(Guid id)
      {
         if (Bot != null && Bot.StickersManager != null)
         {
            foreach (var category in Bot.StickersManager.Data.Categories)
            {
               StickerEntryData sticker = category.Entries.FirstOrDefault(s => s.Id == id);
               if (sticker != null)
               {
                  category.Entries.Remove(sticker);
                  Bot.StickersManager.SaveData();
                  return true;
               }
            }
         }

         return false;
      }

      public async Task PlaceSticker(Guid id)
      {
         if (Bot != null && Bot.StickersManager != null)
         {
            foreach (var category in Bot.StickersManager.Data.Categories)
            {
               StickerEntryData sticker = category.Entries.FirstOrDefault(s => s.Id == id);
               if (sticker != null)
               {
                  await Bot.StickersManager.PlaceASticker(sticker);
               }
            }
         }
      }

      public void SetStickerEnabledFlags(bool enabled)
      {
         if (Bot != null && Bot.StickersManager != null)
         {
            Bot.StickersManager.SetStickerEnabledFlags(enabled);
         }
      }

      /// <summary>
      /// Sets the display test mode of the primary animations pane.
      /// </summary>
      public async Task SetStickerDisplayTestMode(bool isDisplayTestModeOn, Guid stickerId, AnimationEnums.AnimationLayer layer)
      {
         if (Bot != null && Bot.StickersManager != null)
         {
            StickerEntryData stickerEntry = null;
            foreach (var category in Bot.StickersManager.Data.Categories)
            {
               stickerEntry = category.Entries.FirstOrDefault(s => s.Id == stickerId);
            }

            if (stickerEntry != null && isDisplayTestModeOn)
            {
               if (stickerEntry != null)
               {
                  await Bot.StickersManager.SetDisplayTestMode(isDisplayTestModeOn, stickerEntry, layer, Bot.GIFBotHub.Clients);
               }
            }
            else
            {
               await Bot.StickersManager.SetDisplayTestMode(false, null, Bot.StickersManager.CurrentTestLayer, Bot.GIFBotHub.Clients);
            }
         }
      }

      /// <summary>
      /// Updates the active display test dimensions.
      /// </summary>
      public async Task UpdateStickerDisplayTestDimensions(int width, int height)
      {
         if (Bot != null && Bot.StickersManager != null)
         {
            IHubClients clients = Bot.GIFBotHub.Clients;
            await Bot.StickersManager.UpdateDisplayTestDimensions(width, height, Bot.StickersManager.CurrentTestLayer, clients);
         }
      }

      /// <summary>
      /// Retrieve the file path to the stickers's HTML pages that is used in broadcaster software.
      /// </summary>
      public string GetStickersWebPaths()
      {
         string currentDirectory = System.Environment.CurrentDirectory.Replace("Server", "Client");
         string paths = $"{Path.Combine(currentDirectory, "wwwroot", "stickers.html")},{Path.Combine(currentDirectory, "wwwroot", "secondarystickers.html")}";
         return paths;
      }

      #endregion

      #region Backdrop

      /// <summary>
      /// Retrieve the file path to the backdrop's HTML pages that is used in broadcaster software.
      /// </summary>
      public string GetBackdropWebPath()
      {
         string currentDirectory = System.Environment.CurrentDirectory.Replace("Server", "Client");
         return $"{Path.Combine(currentDirectory, "wwwroot", "backdrop.html")}";
      }

      /// <summary>
      /// Fetches the data for the backdrop feature.
      /// </summary>
      public string GetBackdropData()
      {
         if (Bot != null && Bot.BackdropManager != null)
         {
            return JsonConvert.SerializeObject(Bot.BackdropManager.Data);
         }

         return String.Empty;
      }

      public bool UpdateBackdropData(string rawData)
      {
         if (!String.IsNullOrEmpty(rawData) && Bot != null && Bot.BackdropManager != null)
         {
            Bot.BackdropManager.Data = JsonConvert.DeserializeObject<BackdropData>(rawData);
            Bot.BackdropManager.SaveData();

            return true;
         }

         return false;
      }

      public Guid AddBackdrop(string name)
      {
         if (!String.IsNullOrEmpty(name) && Bot != null && Bot.BackdropManager != null)
         {
            BackdropVideoEntryData existing = Bot.BackdropManager.Data.Backdrops.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
               return Guid.Empty;
            }
            else
            {
               BackdropVideoEntryData added = new BackdropVideoEntryData() {
                  Name = name
               };

               Bot.BackdropManager.Data.Backdrops.Add(added);
               Bot.BackdropManager.SaveData();

               return added.Id;
            }
         }

         return Guid.Empty;
      }

      public bool UpdateBackdrop(string rawData)
      {
         if (!String.IsNullOrEmpty(rawData) && Bot != null && Bot.BackdropManager != null)
         {
            var backdrop = JsonConvert.DeserializeObject<BackdropVideoEntryData>(rawData);
            if (backdrop != null)
            {
               var existing = Bot.BackdropManager.Data.Backdrops.FirstOrDefault(c => c.Name.Equals(backdrop.Name, StringComparison.OrdinalIgnoreCase) && c.Id != backdrop.Id);
               if (existing != null)
               {
                  // Duplicate! Not allowed.
                  return false;
               }

               var original = Bot.BackdropManager.Data.Backdrops.FirstOrDefault(c => c.Id == backdrop.Id);
               if (original != null)
               {
                  int index = Bot.BackdropManager.Data.Backdrops.IndexOf(original);
                  Bot.BackdropManager.Data.Backdrops.RemoveAt(index);
                  Bot.BackdropManager.Data.Backdrops.Insert(index, backdrop);
                  Bot.BackdropManager.SaveData();
                  return true;
               }
            }
         }

         return false;
      }

      public bool DeleteBackdrop(Guid id)
      {
         if (Bot != null && Bot.BackdropManager != null)
         {
            var backdrop = Bot.BackdropManager.Data.Backdrops.FirstOrDefault(c => c.Id == id);
            if (backdrop != null)
            {
               Bot.BackdropManager.Data.Backdrops.Remove(backdrop);
               Bot.BackdropManager.SaveData();
               return true;
            }
         }

         return false;
      }

      public void HangBackdrop(Guid id)
      {
         if (Bot != null && Bot.BackdropManager != null)
         {
            var backdrop = Bot.BackdropManager.Data.Backdrops.FirstOrDefault(c => c.Id == id);
            if (backdrop != null)
            {
               Bot.BackdropManager.QueueBackdrop(backdrop);
            }
         }
      }

      public void TakeDownBackdrop()
      {
         if (Bot != null && Bot.BackdropManager != null)
         {
            _ = Bot.BackdropManager.TakeDownBackdrop();
         }
      }

      #endregion

      #region Countdown Timer


      /// <summary>
      /// Retrieve the file path to the countdown timer's HTML pages that is used in broadcaster software.
      /// </summary>
      public string GetCountdownTimerWebPath()
      {
         string currentDirectory = System.Environment.CurrentDirectory.Replace("Server", "Client");
         return $"{Path.Combine(currentDirectory, "wwwroot", "countdowntimer.html")}";
      }

      /// <summary>
      /// Fetches the data for the countdown timer feature.
      /// </summary>
      public string GetCountdownTimerData()
      {
         if (Bot != null && Bot.CountdownTimerManager != null)
         {
            return JsonConvert.SerializeObject(Bot.CountdownTimerManager.Data);
         }

         return String.Empty;
      }

      public bool UpdateCountdownTimerData(string rawData)
      {
         if (!String.IsNullOrEmpty(rawData) && Bot != null && Bot.CountdownTimerManager != null)
         {
            int currentTimerStartValue = Bot.CountdownTimerManager.Data.TimerStartValueInMinutes;

            Bot.CountdownTimerManager.Data = JsonConvert.DeserializeObject<CountdownTimerData>(rawData);

            if (Bot.CountdownTimerManager.Data.Current == TimeSpan.Zero ||
                currentTimerStartValue != Bot.CountdownTimerManager.Data.TimerStartValueInMinutes)
            {
               Bot.CountdownTimerManager.ResetTimer();
            }

            Bot.CountdownTimerManager.SaveData();

            return true;
         }

         return false;
      }

      public Guid AddCountdownTimerAction(string name)
      {
         if (!String.IsNullOrEmpty(name) && Bot != null && Bot.CountdownTimerManager != null)
         {
            CountdownTimerAction existing = Bot.CountdownTimerManager.Data.Actions.FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
               return Guid.Empty;
            }
            else
            {
               CountdownTimerAction added = new CountdownTimerAction() {
                  Name = name
               };

               Bot.CountdownTimerManager.Data.Actions.Add(added);
               Bot.CountdownTimerManager.SaveData();

               return added.Id;
            }
         }

         return Guid.Empty;
      }

      public bool UpdateCountdownTimerAction(string rawData)
      {
         if (!String.IsNullOrEmpty(rawData) && Bot != null && Bot.CountdownTimerManager != null)
         {
            var action = JsonConvert.DeserializeObject<CountdownTimerAction>(rawData);
            if (action != null)
            {
               var existing = Bot.BackdropManager.Data.Backdrops.FirstOrDefault(c => c.Name.Equals(action.Name, StringComparison.OrdinalIgnoreCase) && c.Id != action.Id);
               if (existing != null)
               {
                  // Duplicate! Not allowed.
                  return false;
               }

               var original = Bot.CountdownTimerManager.Data.Actions.FirstOrDefault(c => c.Id == action.Id);
               if (original != null)
               {
                  int index = Bot.CountdownTimerManager.Data.Actions.IndexOf(original);
                  Bot.CountdownTimerManager.Data.Actions.RemoveAt(index);
                  Bot.CountdownTimerManager.Data.Actions.Insert(index, action);
                  Bot.CountdownTimerManager.SaveData();
                  return true;
               }
            }
         }

         return false;
      }

      public bool DeleteCountdownTimerAction(Guid id)
      {
         if (Bot != null && Bot.CountdownTimerManager != null)
         {
            var action = Bot.CountdownTimerManager.Data.Actions.FirstOrDefault(c => c.Id == id);
            if (action != null)
            {
               Bot.CountdownTimerManager.Data.Actions.Remove(action);
               Bot.CountdownTimerManager.SaveData();
               return true;
            }
         }

         return false;
      }

      public void PlayCountdownTimerAction(Guid actionId)
      {
         if (Bot != null && Bot.CountdownTimerManager != null)
         {
            var action = Bot.CountdownTimerManager.Data.Actions.FirstOrDefault(c => c.Id == actionId);
            if (action != null)
            {
               Bot.CountdownTimerManager.ApplyAction(action);
            }
         }
      }

      public void StartCountdownTimer()
      {
         Bot?.CountdownTimerManager?.StartTimer();
      }

      public void PauseCountdownTimer()
      {
         Bot?.CountdownTimerManager?.PauseTimer();
      }

      public void ResetCountdownTimer()
      {
         Bot?.CountdownTimerManager?.ResetTimer();
      }

      public void HideTimer()
      {
         Bot?.CountdownTimerManager?.HideTimer();
      }

      #endregion

      #region Goal Bar

      public string GetGoalBarData()
      {
         if (Bot != null && Bot.GoalBarManager != null)
         {
            return JsonConvert.SerializeObject(Bot.GoalBarManager.Data);
         }

         return null;
      }

      public async Task UpdateGoalBarData(string rawData)
      {
         if (!String.IsNullOrEmpty(rawData) && Bot != null && Bot.GoalBarManager != null)
         {
            Bot.GoalBarManager.Data = JsonConvert.DeserializeObject<GoalBarData>(rawData);
            Bot.GoalBarManager.RebalanceGoals();

            Bot.GoalBarManager.SaveData();
            await Bot.GIFBotHub.Clients.All.SendAsync("GoalBarDataUpdated", JsonConvert.SerializeObject(Bot.GoalBarManager.Data));
         }
      }

      public string GetGoals()
      {
         if (Bot != null && Bot.GoalBarManager != null)
         {
            return JsonConvert.SerializeObject(Bot.GoalBarManager.Data.Goals);
         }

         return String.Empty;
      }

      public bool AddGoal(string title)
      {
         if (Bot != null && Bot.GoalBarManager != null)
         {
            GoalData existingGoal = Bot.GoalBarManager.Data.Goals.FirstOrDefault(g => g.Title.Equals(title));
            if (existingGoal == null)
            {
               Bot.GoalBarManager.Data.Goals.Add(new GoalData() { Title = title });
               Bot.GoalBarManager.SaveData();
               return true;
            }
         }

         return false;
      }

      public async Task<bool> MakeGoalActive(Guid goalId)
      {
         if (Bot != null && Bot.GoalBarManager != null)
         {
            GoalData existingGoal = Bot.GoalBarManager.Data.Goals.FirstOrDefault(g => g.Id == goalId);
            if (existingGoal != null)
            {
               foreach (var goal in Bot.GoalBarManager.Data.Goals)
               {
                  goal.IsActive = false;
                  if (goal.Id == goalId)
                  {
                     goal.IsActive = true;
                  }
               }

               Bot.GoalBarManager.SaveData();
               await Bot.GIFBotHub.Clients.All.SendAsync("GoalBarDataUpdated", JsonConvert.SerializeObject(Bot.GoalBarManager.Data));
               return true;
            }
         }

         return false;
      }

      public async Task<bool> DeleteGoal(Guid goalId)
      {
         if (Bot != null && Bot.GoalBarManager != null)
         {
            GoalData existingGoal = Bot.GoalBarManager.Data.Goals.FirstOrDefault(g => g.Id == goalId);
            if (existingGoal != null)
            {
               Bot.GoalBarManager.Data.Goals.Remove(existingGoal);
               Bot.GoalBarManager.SaveData();
               await Bot.GIFBotHub.Clients.All.SendAsync("GoalBarDataUpdated", JsonConvert.SerializeObject(Bot.GoalBarManager.Data));
               return true;
            }
         }

         return false;
      }

      /// <summary>
      /// Retrieve the file path to the goalbar's HTML page that is used in broadcaster software.
      /// </summary>
      public string GetGoalBarWebPath()
      {
         string currentDirectory = System.Environment.CurrentDirectory.Replace("Server", "Client");
         return Path.Combine(currentDirectory, "wwwroot", "goalbar.html");
      }

      #endregion

      #region Twitch Channel Data

      public string GetUserList()
      {
         List<TwitchUserViewModel> userViewModels = new List<TwitchUserViewModel>();
         foreach (var user in Bot.UsersInChannel)
         {
            userViewModels.Add(new TwitchUserViewModel(user));
         }

         return JsonConvert.SerializeObject(userViewModels);
      }

      public void BanUser(string username)
      {
         if (!String.IsNullOrEmpty(username))
         {
            Bot.SendChatMessage($"/ban {username}");
         }
      }

      public void BanUsers(string rawUsernames)
      {
         if (!String.IsNullOrEmpty(rawUsernames))
         {
            List<TwitchUserViewModel> users = JsonConvert.DeserializeObject<List<TwitchUserViewModel>>(rawUsernames);
            if (users.Any())
            {
               _ = Task.Run(() =>
               {
                  // Ban the fuckers.
                  int numberBanned = 0;
                  foreach (var user in users)
                  {
                     ++numberBanned;
                     Bot.SendChatMessage($"/ban {user.Name}");

                     if (numberBanned > kMaxChatCommandsPerFrame)
                     {
                        numberBanned = 0;
                        Thread.Sleep(30000);
                     }
                  }
               });
            }
         }
      }

      public void TimeoutUser(string username)
      {
         if (!String.IsNullOrEmpty(username))
         {
            Bot.SendChatMessage($"/timeout {username}");
         }
      }

      public void TimeoutUsers(string rawUsernames)
      {
         if (!String.IsNullOrEmpty(rawUsernames))
         {
            List<TwitchUserViewModel> users = JsonConvert.DeserializeObject<List<TwitchUserViewModel>>(rawUsernames);
            if (users.Any())
            {
               _ = Task.Run(() =>
               {
                  // Timeout the fuckers.
                  int numberTimedOut = 0;
                  foreach (var user in users)
                  {
                     ++numberTimedOut;
                     Bot.SendChatMessage($"/timeout {user.Name}");

                     if (numberTimedOut > kMaxChatCommandsPerFrame)
                     {
                        numberTimedOut = 0;
                        Thread.Sleep(30000);
                     }
                  }
               });
            }
         }
      }

      #endregion

      #region Tiltify

      public string GetTiltifyCampaigns()
      {
         List<TiltifyCampaign> campaigns = new List<TiltifyCampaign>();

         if (Bot != null && 
             !String.IsNullOrEmpty(Bot.BotSettings.TiltifySlug) && 
             !String.IsNullOrEmpty(Bot.BotSettings.TiltifyAuthToken))
         {
            int tiltifyUserId = TiltifyEndpointHelpers.GetUserId(Bot.BotSettings.TiltifyAuthToken, Bot.BotSettings.TiltifySlug);
            if (tiltifyUserId > 0)
            {
               campaigns = TiltifyEndpointHelpers.GetCampaigns(Bot.BotSettings.TiltifyAuthToken, tiltifyUserId);
            }
         }

         return JsonConvert.SerializeObject(campaigns);
      }

      #endregion

      #region Giveaway

      public string GetGiveawayData()
      {
         if (Bot?.GiveawayManager?.Data != null)
         {
            return JsonConvert.SerializeObject(Bot.GiveawayManager.Data);
         }

         return String.Empty;
      }

      public void UpdateGiveawayData(string giveawayDataRaw)
      {
         if (Bot?.GiveawayManager?.Data != null)
         {
            Bot.GiveawayManager.Data = JsonConvert.DeserializeObject<GiveawayData>(giveawayDataRaw);
            Bot.GiveawayManager.SaveData();
         }
      }

      public void OpenGiveaway()
      {
         if (Bot?.GiveawayManager != null)
         {
            Bot?.GiveawayManager.Open();
         }
      }

      public void CloseGiveaway()
      {
         if (Bot?.GiveawayManager != null)
         {
            Bot?.GiveawayManager.Close();
         }
      }

      public void ResetGiveaway()
      {
         if (Bot?.GiveawayManager != null)
         {
            Bot?.GiveawayManager.Reset();
         }
      }

      public void DrawWinner()
      {
         if (Bot?.GiveawayManager != null)
         {
            Bot?.GiveawayManager.DrawWinner();
         }
      }

      public bool AddBannedGiveawayUser(string bannedUserName)
      {
         if (Bot?.GiveawayManager != null)
         {
            if (!Bot.GiveawayManager.Data.BannedUsers.Contains(bannedUserName.ToLower()))
            {
               Bot.GiveawayManager.Data.BannedUsers.Add(bannedUserName.ToLower());
               Bot.GiveawayManager.SaveData();
               return true;
            }
         }

         return false;
      }

      public bool RemoveBannedGiveawayUser(string bannedUserName)
      {
         if (Bot?.GiveawayManager != null)
         {
            if (Bot.GiveawayManager.Data.BannedUsers.Contains(bannedUserName.ToLower()))
            {
               Bot.GiveawayManager.Data.BannedUsers.Remove(bannedUserName.ToLower());
               Bot.GiveawayManager.SaveData();
               return true;
            }
         }

         return false;
      }

      #endregion

      #region Properties

      /// <summary>
      /// Reference to the GIFBot instance.
      /// </summary>
      public GIFBot.GIFBot Bot { get; private set; }

      /// <summary>
      /// Reference to the hosted environment.
      /// </summary>
      public IWebHostEnvironment WebHostEnvironment { get; private set; }

      #endregion

      #region Private Members

      private static int kMaxChatCommandsPerFrame = 75;

      #endregion
   }
}
