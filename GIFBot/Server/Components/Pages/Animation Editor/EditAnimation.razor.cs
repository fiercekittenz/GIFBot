using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using global::GIFBot.Shared;
using global::GIFBot.Shared.Models.Animation;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Http.Connections;
using MudBlazor;
namespace GIFBot.Server.Components.Pages.Animation_Editor
{
   public partial class EditAnimation : ComponentBase, IAsyncDisposable
   {
      [Parameter]
      public string CategoryId { get; set; } = String.Empty;

      [Parameter]
      public string AnimationId { get; set; } = String.Empty;

      // TODO: Replace DialogFactory with IDialogService
      // [CascadingParameter]
      // public DialogFactory Dialogs { get; set; }

      protected override async Task OnInitializedAsync()
      {
         NavigationManager.LocationChanged += NavigationManager_LocationChanged;

         // Build the connection to the main bot hub.
         mHubConnection = new HubConnectionBuilder()
            .WithUrl(NavigationManager.ToAbsoluteUri("/gifbothub"), HttpTransportType.LongPolling) 
            .WithAutomaticReconnect()
            .Build();

         mHubConnection.On<int, int>("UpdatePosition", async (top, left) =>
         {
            await InvokeAsync(() => UpdateVisualPosition(top, left));
         });

         // Start the connection.
         await mHubConnection.StartAsync();

         // Get a string list of user groups.
         string userGroupsRaw = await mHubConnection.InvokeAsync<string>("GetUserGroupList");
         if (!String.IsNullOrEmpty(userGroupsRaw))
         {
            mUserGroupNames = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(userGroupsRaw);
            mUserGroupNames.Sort();
         }

         // Fetch the animation data based on the provided identifier.
         await UpdateAnimationData(Guid.Parse(AnimationId));
      }

      /// <summary>
      /// IAsyncDisposable Implementation
      /// </summary>
      public async ValueTask DisposeAsync()
      {
         mIsDisplayTestMode = false;
         await mHubConnection.InvokeAsync("SetDisplayTestMode", false, Guid.Empty);

         await mHubConnection.DisposeAsync();
      }

      private void NavigationManager_LocationChanged(object sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
      {
         // If the user leaves the animation manager, toggle off the display test mode.
         mIsDisplayTestMode = false;
         _ = mHubConnection.InvokeAsync("SetDisplayTestMode", false, Guid.Empty);
      }

      private void UpdateVisualPosition(int top, int left)
      {
         if (mSelectedAnimation != null)
         {
            mSelectedAnimation.Placement.Top = top;
            mSelectedAnimation.Placement.Left = left;

            Console.WriteLine($"UpdateVisualPosition: Top = {top}, Left = {left}");

            StateHasChanged();
         }
      }

      private async Task UpdateAnimationData(Guid id)
      {
         string animationData = await mHubConnection.InvokeAsync<string>("GetAnimationById", id);
         if (!String.IsNullOrEmpty(animationData))
         {
            mSelectedAnimation = Newtonsoft.Json.JsonConvert.DeserializeObject<AnimationData>(animationData);
            if (mSelectedAnimation != null)
            {
               // Map the animation's user group to the name.
               if (mSelectedAnimation.Access == AnimationEnums.AccessType.UserGroup)
               {
                  mSelectedAnimationUserGroupName = await mHubConnection.InvokeAsync<string>("GetUserGroupNameById", mSelectedAnimation.RestrictedToUserGroup);
               }

               StateHasChanged();
            }
         }
      }

      private async Task HandleDeleteAnimation()
      {
         if (mSelectedAnimation != null)
         {
            bool confirmed = await Task.FromResult(true); // TODO: Replace with MudBlazor dialog confirmation
         // was: await Task.FromResult(true); // TODO: Replace with MudBlazor IDialogService confirmation

         // was: await Task.FromResult(true) /* TODO: MudBlazor dialog confirm was: Dialogs.ConfirmAsync($"Are you sure you want to delete {mSelectedAnimation.Command}?", "Delete Animation?") */;
            if (confirmed)
            { 
               bool results = await mHubConnection.InvokeAsync<bool>("DeleteAnimations", JsonConvert.SerializeObject(new List<Guid>(){ mSelectedAnimation.Id }));
               if (results)
               {
                  NavigationManager.NavigateTo("/animationseditor");
               }
               else
               {
                  Snackbar.Add($"Error - The animation could not be deleted.", Severity.Error);
                  await InvokeAsync(() => { StateHasChanged(); });
               }
            }
         }
      }

      private async Task HandleTestAnimation()
      {
         if (mSelectedAnimation != null)
         {
            mIsDisplayTestMode = false;
            await mHubConnection.InvokeAsync("SetDisplayTestMode", false, Guid.Empty);

            await mHubConnection.InvokeAsync("TestAnimation", Newtonsoft.Json.JsonConvert.SerializeObject(mSelectedAnimation));
            Snackbar.Add($"Info - {mSelectedAnimation.Command} has been queued for testing with local changes.", Severity.Info);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private async Task HandleVisualFileChanged()
      {
         if (mSelectedAnimation != null)
         {
            mIsDisplayTestMode = false;
            await mHubConnection.InvokeAsync("SetDisplayTestMode", false, Guid.Empty);

            string dimensionsData = await mHubConnection.InvokeAsync<string>("GetVisualDimensions", mSelectedAnimation.Visual);
            if (!String.IsNullOrEmpty(dimensionsData))
            {
               Tuple<int, int> dimensions = Newtonsoft.Json.JsonConvert.DeserializeObject<Tuple<int, int>>(dimensionsData);
               mSelectedAnimation.Placement.Width = dimensions.Item1;
               mSelectedAnimation.Placement.Height = dimensions.Item2;
            }
         }
      }

      private async Task HandleVariantAdded(object args)
      {
         if (mSelectedAnimation != null && args is AnimationVariantData variant)
         {
            bool result = await mHubConnection.InvokeAsync<bool>("AddVariantToAnimation", mSelectedAnimation.Id, variant);
            if (result)
            {
               Snackbar.Add("Success - The variant was added.", Severity.Success);
               await UpdateAnimationData(mSelectedAnimation.Id);
               await InvokeAsync(() => { StateHasChanged(); });
            }
            else
            {
               Snackbar.Add($"Error - The variant could not be added.", Severity.Error);
               await InvokeAsync(() => { StateHasChanged(); });
            }
         }
      }

      private async Task HandleVariantPlay(object variantId)
      {
         if (mSelectedAnimation != null)
         {
            mIsDisplayTestMode = false;
            await mHubConnection.InvokeAsync("SetDisplayTestMode", false, Guid.Empty);

            await mHubConnection.InvokeAsync("PlayVariantAnimation", mSelectedAnimation.Id, (Guid)variantId);
         }
      }

      private async Task HandleVariantUpdated(object args)
      {
         if (mSelectedAnimation != null && args is AnimationVariantData variant)
         {
            bool result = await mHubConnection.InvokeAsync<bool>("UpdateAnimationVariant", mSelectedAnimation.Id, variant);
            if (result)
            {
               Snackbar.Add("Success - The variant was updated.", Severity.Success);
               await UpdateAnimationData(mSelectedAnimation.Id);
               await InvokeAsync(() => { StateHasChanged(); });
            }
            else
            {
               Snackbar.Add($"Error - The variant could not be updated.", Severity.Error);
               await InvokeAsync(() => { StateHasChanged(); });
            }
         }
      }

      private async Task HandleVariantDeleted(Guid variantId)
      {
         if (mSelectedAnimation != null)
         {
            bool result = await mHubConnection.InvokeAsync<bool>("DeleteVariantFromAnimation", mSelectedAnimation.Id, variantId);
            if (result)
            {
               Snackbar.Add("Success - The variant has been deleted.", Severity.Success);
               await UpdateAnimationData(mSelectedAnimation.Id);
               await InvokeAsync(() => { StateHasChanged(); });
            }
            else
            {
               Snackbar.Add($"Error - The variant could not be deleted.", Severity.Error);
               await InvokeAsync(() => { StateHasChanged(); });
            }
         }
      }

      private async Task HandleChainedCommandDeleted(string command)
      {
         if (mSelectedAnimation != null && !String.IsNullOrEmpty(command))
         {
            bool result = await mHubConnection.InvokeAsync<bool>("DeleteChainedCommandFromAnimation", mSelectedAnimation.Id, command);
            if (result)
            {
               Snackbar.Add("Success - The chained command was removed.", Severity.Success);
               await UpdateAnimationData(mSelectedAnimation.Id);
               await InvokeAsync(() => { StateHasChanged(); });
            }
            else
            {
               Snackbar.Add($"Error - The chained command could not be removed.", Severity.Error);
               await InvokeAsync(() => { StateHasChanged(); });
            }
         }
         else
         {
            Snackbar.Add($"Error - You didn't tell me what command to remove!", Severity.Error);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private async Task HandleChainedCommandAdded(string command)
      {
         if (mSelectedAnimation != null && !String.IsNullOrEmpty(command))
         {
            bool result = await mHubConnection.InvokeAsync<bool>("AddChainedCommandToAnimation", mSelectedAnimation.Id, command);
            if (result)
            {
               Snackbar.Add("Success - The chained command was added.", Severity.Success);
               await UpdateAnimationData(mSelectedAnimation.Id);
               await InvokeAsync(() => { StateHasChanged(); });
            }
            else
            {
               Snackbar.Add($"Error - The chained command could not be added.", Severity.Error);
               await InvokeAsync(() => { StateHasChanged(); });
            }
         }
         else
         {
            Snackbar.Add($"Error - You didn't tell me what command to add!", Severity.Error);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      /// <summary>
      /// Handles when the display test mode changes on the placement component.
      /// </summary>
      private async Task HandleDisplayTestModeChanged(bool isDisplayTestModeOn)
      {
         Guid animationId = Guid.Empty;
         if (mSelectedAnimation != null)
         {
            animationId = mSelectedAnimation.Id;
         }

         await mHubConnection.InvokeAsync("SetDisplayTestMode", isDisplayTestModeOn, animationId);
      }

      /// <summary>
      /// Handles when the width changes on the placement component.
      /// </summary>
      private async Task HandleNotifyDimensionsChanged(Tuple<int, int> dimensions)
      {
         if (mSelectedAnimation != null)
         {
            mSelectedAnimation.Placement.Width = dimensions.Item1;
            mSelectedAnimation.Placement.Height = dimensions.Item2;

            // TODO: Support a second layer.
            await mHubConnection.InvokeAsync("UpdateDisplayTestDimensions", dimensions.Item1, dimensions.Item2, AnimationEnums.AnimationLayer.Primary);
         }
      }

      private async Task HandleUserGroupSelected(string groupName)
      {
         if (!String.IsNullOrEmpty(groupName))
         {
            Guid groupId = await mHubConnection.InvokeAsync<Guid>("GetGroupIdByName", groupName);
            mSelectedAnimation.RestrictedToUserGroup = groupId;
            mSelectedAnimationUserGroupName = groupName;
            StateHasChanged();
         }
      }

      private async Task HandleCancelAnimationChanges()
      {
         if (mSelectedAnimation != null)
         {
            mIsDisplayTestMode = false;
            await mHubConnection.InvokeAsync("SetDisplayTestMode", false, Guid.Empty);
            NavigationManager.NavigateTo("/animationseditor/");
         }
      }

      private async Task HandleSaveAnimation(AnimationData model)
      {
         if (mSelectedAnimation != null && !String.IsNullOrEmpty(mSelectedAnimation.Command))
         {
            mIsDisplayTestMode = false;
            await mHubConnection.InvokeAsync("SetDisplayTestMode", false, Guid.Empty);

            await mHubConnection.InvokeAsync("SaveAnimation", Newtonsoft.Json.JsonConvert.SerializeObject(mSelectedAnimation));
            await UpdateAnimationData(model.Id);
            Snackbar.Add($"Success - {mSelectedAnimation.Command} was saved!", Severity.Success);
            await InvokeAsync(() => { StateHasChanged(); });
         }
         else
         {
            Snackbar.Add($"Error - The animation could not be saved. Missing a command maybe?", Severity.Error);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private void HandleCloneAnimation()
      {
         if (mSelectedAnimation != null)
         {
            mIsCloneDialogVisible = true;
         }
      }

      private void HandleCancelClone()
      {
         mIsCloneDialogVisible = false;
      }

      private async Task HandleConfirmClone()
      {
         if (mSelectedAnimation != null)
         {
            mIsDisplayTestMode = false;
            await mHubConnection.InvokeAsync("SetDisplayTestMode", false, Guid.Empty);

            Guid results = await mHubConnection.InvokeAsync<Guid>("CloneAnimation", mSelectedAnimation.Id, mTempAnimationCommand);
            if (results != Guid.Empty)
            {
               Snackbar.Add($"Success - {mSelectedAnimation.Command} has been cloned.", Severity.Success);
               await InvokeAsync(() => { StateHasChanged(); });
               NavigationManager.NavigateTo($"/animationseditor/editanimation/{CategoryId}/{results}", true);
            }
            else
            {
               Snackbar.Add($"Error - {mSelectedAnimation.Command} could not be cloned.", Severity.Error);
               await InvokeAsync(() => { StateHasChanged(); });
            }
         }

         mIsCloneDialogVisible = false;
      }

      #region Private Members

      private HubConnection mHubConnection;

      private AnimationData mSelectedAnimation = null;
      private string mSelectedAnimationUserGroupName = String.Empty;
      private List<string> mUserGroupNames = new List<string>();
      private bool mIsDisplayTestMode = false;

      // Animation Creation Variables
      private string mTempAnimationCommand = String.Empty;
      private bool mIsCloneDialogVisible = false;

      #endregion
   }
}
