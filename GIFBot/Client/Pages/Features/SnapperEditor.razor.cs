using GIFBot.Shared.Models.Features;
using GIFBot.Shared.Models.Animation;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GIFBot.Client.Pages.Features
{
   public partial class SnapperEditor : IAsyncDisposable
   {
      #region Properties

      public SnapperData Data { get; set; } = new SnapperData();

      public List<AnimationSelectorItem> AnimationOptions { get; set; } = new List<AnimationSelectorItem>();

      public int ActiveTabIndex { get; set; } = 0;

      public int CurrentlyEditedCommandBehavior { get; set; } = 0;

      public int CurrentlyEditedCommandRedemption { get; set; } = 0;

      public const string kExampleChatUse = "!minisnap cheer666 @viewertosnap";

      #endregion

      #region Razor Component

      protected override async Task OnInitializedAsync()
      {
         // Build the connection to the main bot hub.
         mHubConnection = new HubConnectionBuilder()
            .WithUrl(NavigationManager.ToAbsoluteUri("/gifbothub"), HttpTransportType.LongPolling) 
            .WithAutomaticReconnect()
            .Build();

         // Start the connection.
         await mHubConnection.StartAsync();

         // Get the snapper data.
         await GetSnapperDataFromHub();

         // Get the animations list.
         await GetAnimationOptions();

         // Render.
         StateHasChanged();
      }

      /// <summary>
      /// IAsyncDisposable Implementation
      /// </summary>
      public async ValueTask DisposeAsync()
      {
         await mHubConnection.DisposeAsync();
      }

      #endregion

      #region UX Methods

      private void OnCancel()
      {
         NavigationManager.NavigateTo("/");
      }

      private async Task OnSaveChanges()
      {
         await mHubConnection.InvokeAsync("UpdateSnapperData", JsonConvert.SerializeObject(Data));
         await GetSnapperDataFromHub();
         NotificationService.Notify(NotificationSeverity.Success, "Save Successful", "The snapper data has been saved.", 5000);
         await InvokeAsync(() => { StateHasChanged(); });
      }

      private void HandleAddRequest()
      {
         mIsAddDialogVisible = true;
         mTempCommand = new SnapperCommand();
         StateHasChanged();
      }

      private void HandleCancelAddCommand()
      {
         mIsAddDialogVisible = false;
         mTempCommand = new SnapperCommand();
         StateHasChanged();
      }

      private async Task HandleConfirmAddCommand()
      {
         Guid result = await mHubConnection.InvokeAsync<Guid>("AddSnapperCommand", mTempCommand.Command);
         if (result != Guid.Empty)
         {
            NotificationService.Notify(NotificationSeverity.Success, "Success", "The command has been added.", 5000);
            mIsAddDialogVisible = false;

            await GetSnapperDataFromHub();

            // Immediately open the edit dialog.
            mTempCommand = Data.Commands.FirstOrDefault(c => c.Id == result);
            if (mTempCommand != null)
            {
               mIsEditDialogVisible = true;
            }

            await InvokeAsync(() => { StateHasChanged(); });
         }
         else
         {
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"The command could not be added. Either there was no text or the name is in use by another command.", 5000);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private async Task HandleTestSnapRequest(SnapperCommand command)
      {
         if (command != null)
         {
            bool result = await mHubConnection.InvokeAsync<bool>("TestSnapperCommand", command.Id);
            if (result)
            {
               NotificationService.Notify(NotificationSeverity.Success, "Success", "The command has been executed.", 5000);
            }
            else
            {
               NotificationService.Notify(NotificationSeverity.Error, "Error", "The command could not be tested.", 5000);
            }
         }
      }

      private void HandleEditRequest(SnapperCommand command)
      {
         if (command != null)
         {
            mTempCommand = command.Clone() as SnapperCommand;
            CurrentlyEditedCommandRedemption = (int)mTempCommand.RedemptionType;
            CurrentlyEditedCommandBehavior = (int)mTempCommand.BehaviorType;
            mIsEditDialogVisible = true;
            StateHasChanged();
         }
      }

      private void HandleCancelEditCommand()
      {
         mIsEditDialogVisible = false;
         mTempCommand = new SnapperCommand();
         StateHasChanged();
      }

      private async Task HandleConfirmEditCommand()
      {
         bool result = await mHubConnection.InvokeAsync<bool>("UpdateSnapperCommand", JsonConvert.SerializeObject(mTempCommand));
         if (result)
         {
            NotificationService.Notify(NotificationSeverity.Success, "Success", "The command has been updated.", 5000);
            mIsEditDialogVisible = false;
            await GetSnapperDataFromHub();
            await InvokeAsync(() => { StateHasChanged(); });
         }
         else
         {
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"The command could not be updated.", 5000);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private async Task HandleDeleteRequest(SnapperCommand command)
      {
         if (command != null)
         {
            bool result = await mHubConnection.InvokeAsync<bool>("DeleteSnapperCommand", command.Id);
            if (result)
            {
               await GetSnapperDataFromHub();
               NotificationService.Notify(NotificationSeverity.Success, "Delete Successful", "The snapper command has been deleted.", 5000);
               await InvokeAsync(() => { StateHasChanged(); });
            }
            else
            {
               NotificationService.Notify(NotificationSeverity.Error, "Delete Failed", "The snapper command was not deleted.", 5000);
               await InvokeAsync(() => { StateHasChanged(); });
            }
         }
      }

      private void OnRedemptionTypeChange(int? value)
      {
         if (value.HasValue)
         {
            CurrentlyEditedCommandRedemption = value.Value;
            mTempCommand.RedemptionType = (SnapperRedemptionType)value.Value;
         }
      }

      private void OnBehaviorTypeChange(int? value)
      {
         if (value.HasValue)
         {
            CurrentlyEditedCommandBehavior = value.Value;
            mTempCommand.BehaviorType = (SnapperBehaviorType)value.Value;
         }
      }

      private void HandlePreAnimationSelectionChanged(Guid id)
      {
         mTempCommand.PreAnimationId = id;
         StateHasChanged();
      }

      private void HandlePostAnimationSelectionChanged(Guid id)
      {
         mTempCommand.PostAnimationId = id;
         StateHasChanged();
      }

      #endregion

      #region Private Methods

      private async Task GetSnapperDataFromHub()
      {
         string rawData = await mHubConnection.InvokeAsync<string>("GetSnapperData");
         Data = JsonConvert.DeserializeObject<SnapperData>(rawData);
      }

      private async Task GetAnimationOptions()
      {
         string rawData = await mHubConnection.InvokeAsync<string>("GetAnimationOptions");
         AnimationOptions = JsonConvert.DeserializeObject<List<AnimationSelectorItem>>(rawData);
      }

      #endregion

      #region Private Members

      private HubConnection mHubConnection;

      private SnapperCommand mTempCommand = new SnapperCommand();

      private bool mIsAddDialogVisible = false;

      private bool mIsEditDialogVisible = false;

      #endregion
   }
}
