using GIFBot.Shared.Models.Features;
using GIFBot.Shared.Models.Visualization;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using System.Text.Json;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static GIFBot.Shared.Utility.Enumerations;

namespace GIFBot.Client.Pages.Features
{
   public partial class CountdownTimer : IAsyncDisposable
   {
      #region Properties

      public CountdownTimerData Data { get; set; } = new CountdownTimerData();

      public int ActiveTabIndex { get; set; } = 0;

      public int CurrentlyEditedBehavior { get; set; } = 0;

      public int CurrentlyEditedRedemption { get; set; } = 0;

      public int SubTierSelection { get; set; } = 0;

      public int CurrentlyEditedSpeedPreset { get; set; } = 0;

      public string WebPath { get; set; } = String.Empty;

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

         // Get the feature data.
         await GetCountdownDataFromHub();

         // Get the url.
         WebPath = await mHubConnection.InvokeAsync<string>("GetCountdownTimerWebPath");

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
         await mHubConnection.InvokeAsync("UpdateCountdownTimerData", JsonSerializer.Serialize(Data));
         await GetCountdownDataFromHub();
         NotificationService.Notify(NotificationSeverity.Success, "Save Successful", "The countdown data has been saved.", 5000);
         await InvokeAsync(() => { StateHasChanged(); });
      }

      private void HandleAddRequest()
      {
         mIsAddDialogVisible = true;
         mTempData = new CountdownTimerAction();
         StateHasChanged();
      }

      private async Task HandleConfirmAddCommand()
      {
         Guid result = await mHubConnection.InvokeAsync<Guid>("AddCountdownTimerAction", mTempData.Name);
         if (result != Guid.Empty)
         {
            NotificationService.Notify(NotificationSeverity.Success, "Success", "The action has been added.", 5000);
            mIsAddDialogVisible = false;

            await GetCountdownDataFromHub();

            // Immediately open the edit dialog.
            mTempData = Data.Actions.FirstOrDefault(c => c.Id == result);
            if (mTempData != null)
            {
               HandleEditRequest(mTempData);
            }

            await InvokeAsync(() => { StateHasChanged(); });
         }
         else
         {
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"The action could not be added. Either there was no text or the name is in use by another action.", 5000);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private void HandleCancelAddCommand()
      {
         mIsAddDialogVisible = false;
         mTempData = new CountdownTimerAction();
         StateHasChanged();
      }

      private async Task HandlePlayActionRequest(CountdownTimerAction action)
      {
         if (action != null)
         {
            await mHubConnection.InvokeAsync("PlayCountdownTimerAction", action.Id);
            NotificationService.Notify(NotificationSeverity.Success, "Success", "The action has been played.", 5000);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private void HandleEditRequest(CountdownTimerAction action)
      {
         mTempData = action.Clone() as CountdownTimerAction;

         CurrentlyEditedBehavior = (int)mTempData.Behavior;
         CurrentlyEditedRedemption = (int)mTempData.RedemptionType;
         CurrentlyEditedSpeedPreset = (int)mTempData.SpeedType;

         mIsEditDialogVisible = true;
         StateHasChanged();
      }

      private void HandleCancelEditCommand()
      {
         mIsEditDialogVisible = false;
         mTempData = new CountdownTimerAction();
         StateHasChanged();
      }

      private async Task HandleConfirmEditCommand()
      {
         bool result = await mHubConnection.InvokeAsync<bool>("UpdateCountdownTimerAction", JsonSerializer.Serialize(mTempData));
         if (result)
         {
            NotificationService.Notify(NotificationSeverity.Success, "Success", "The action has been updated.", 5000);
            mIsEditDialogVisible = false;
            await GetCountdownDataFromHub();
            await InvokeAsync(() => { StateHasChanged(); });
         }
         else
         {
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"The action could not be updated.", 5000);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private async Task HandleDeleteRequest(CountdownTimerAction action)
      {
         if (action != null)
         {
            bool result = await mHubConnection.InvokeAsync<bool>("DeleteCountdownTimerAction", action.Id);
            if (result)
            {
               await GetCountdownDataFromHub();
               NotificationService.Notify(NotificationSeverity.Success, "Delete Successful", "The action has been deleted.", 5000);
               await InvokeAsync(() => { StateHasChanged(); });
            }
            else
            {
               NotificationService.Notify(NotificationSeverity.Error, "Delete Failed", "The action was not deleted.", 5000);
               await InvokeAsync(() => { StateHasChanged(); });
            }
         }
      }

      private void OnRedemptionTypeChange(int? value)
      {
         if (value.HasValue)
         {
            CurrentlyEditedRedemption = value.Value;
            mTempData.RedemptionType = (CostRedemptionType)value.Value;
         }
      }

      private void OnSubTierChange(int? value)
      {
         if (value.HasValue)
         {
            SubTierSelection = value.Value;
            mTempData.SubscriptionTierRequired = (TwitchLib.Client.Enums.SubscriptionPlan)value.Value;
         }
      }

      private void OnBehaviorTypeChange(int? value)
      {
         if (value.HasValue)
         {
            int previousValue = CurrentlyEditedBehavior;
            CurrentlyEditedBehavior = value.Value;
            mTempData.Behavior = (CountdownTimerActionBehavior)value.Value;

            if (mTempData.Behavior == CountdownTimerActionBehavior.SpeedUpTime && mTempData.SpeedType < CountdownTimerSpeedType.Rabbit)
            {
               CurrentlyEditedSpeedPreset = (int)CountdownTimerSpeedType.Rabbit;
               mTempData.SpeedType = CountdownTimerSpeedType.Rabbit;
               StateHasChanged();
            }
            else if (mTempData.Behavior == CountdownTimerActionBehavior.SlowTime && mTempData.SpeedType > CountdownTimerSpeedType.Turtle)
            {
               CurrentlyEditedSpeedPreset = (int)CountdownTimerSpeedType.Sloth;
               mTempData.SpeedType = CountdownTimerSpeedType.Sloth;
               StateHasChanged();
            }
         }
      }

      private void OnSpeedPresetTypeChange(int? value)
      {
         if (value.HasValue)
         {
            CurrentlyEditedSpeedPreset = value.Value;
            mTempData.SpeedType = (CountdownTimerSpeedType)value.Value;
         }
      }

      private async Task OnStartTimer()
      {
         await mHubConnection.InvokeAsync("StartCountdownTimer");
      }

      private async Task OnPauseTimer()
      {
         await mHubConnection.InvokeAsync("PauseCountdownTimer");
      }

      private async Task OnResetTimer()
      {
         await mHubConnection.InvokeAsync("ResetCountdownTimer");
      }

      private async Task OnHideTimer()
      {
         await mHubConnection.InvokeAsync("HideTimer");
      }

      private async Task HandleCopyUrl(string elementName)
      {
         await JSRuntime.InvokeVoidAsync("CopyToClipboard", elementName);
         NotificationService.Notify(NotificationSeverity.Success, "Success", "The URL was copied to your clipboard!", 5000);
         await InvokeAsync(() => { StateHasChanged(); });
      }

      #endregion

      #region Private Methods

      private async Task GetCountdownDataFromHub()
      {
         string rawData = await mHubConnection.InvokeAsync<string>("GetCountdownTimerData");
         Data = JsonSerializer.Deserialize<CountdownTimerData>(rawData);
         mCaptionFontSelection = (int)Data.Caption.FontFamily;
         Data.Caption.Text = "01:23:45";
         UpdateCaptionPreview(null);
      }

      /// <summary>
      /// Handle a caption font change.
      /// </summary>
      private void OnCaptionFontChange(int? value)
      {
         if (value.HasValue)
         {
            mCaptionFontSelection = value.Value;
            Data.Caption.FontFamily = (FontFamily)value.Value;
            UpdateCaptionPreview(null);
         }
      }

      /// <summary>
      /// Handles updating the variables needed for the caption preview.
      /// </summary>
      private void UpdateCaptionPreview(object unused)
      {
         StateHasChanged();
      }

      #endregion

      #region Private Members

      private bool mIsAddDialogVisible = false;

      private bool mIsEditDialogVisible = false;

      private int mCaptionFontSelection = 0;

      private HubConnection mHubConnection;

      private CountdownTimerAction mTempData = new CountdownTimerAction();

      #endregion
   }
}
