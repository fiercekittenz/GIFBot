using GIFBot.Shared;
using GIFBot.Shared.Models.Animation;
using GIFBot.Shared.Models.Features;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using Radzen;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GIFBot.Client.Pages.Features
{
   public partial class GiveawayEditor
   {
      #region Properties

      public GiveawayData Data { get; set; } = new GiveawayData();

      public List<AnimationSelectorItem> AnimationOptions { get; set; } = new List<AnimationSelectorItem>();

      public string Winner { get; set; } = String.Empty;

      public int ActiveTabIndex { get; set; } = 0;

      #endregion

      #region Razor Component

      protected override async Task OnInitializedAsync()
      {
         // Build the connection to the main bot hub.
         mHubConnection = new HubConnectionBuilder()
            .WithUrl(NavigationManager.ToAbsoluteUri("/gifbothub"), HttpTransportType.LongPolling) 
            .WithAutomaticReconnect()
            .Build();

         // Handle an update message for adding a new entrant.
         mHubConnection.On<string>("SendNewGiveawayEntrant", (entrant) =>
         {
            Data.Entrants.Add(entrant);
            StateHasChanged();
         });

         // Handle an update message for when a winner has been selected.
         mHubConnection.On<string>("SendGiveawayWinner", (winner) =>
         {
            Winner = winner;

            while (true)
            {
               if (!Data.Entrants.Remove(winner))
               {
                  break;
               }
            }

            StateHasChanged();
         });

         // Start the connection.
         await mHubConnection.StartAsync();

         // Get the giveaway data.
         await GetGiveawayDataFromHub();

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

      #region Private Methods

      private async Task GetGiveawayDataFromHub()
      {
         string rawData = await mHubConnection.InvokeAsync<string>("GetGiveawayData");
         Data = JsonSerializer.Deserialize<GiveawayData>(rawData);
         mAccessSelection = (int)Data.Access;
         mEntryBehaviorSelection = (int)Data.EntryBehavior;
      }

      private async Task GetAnimationOptions()
      {
         string rawData = await mHubConnection.InvokeAsync<string>("GetAnimationOptions");
         AnimationOptions = JsonSerializer.Deserialize<List<AnimationSelectorItem>>(rawData);
      }

      private async Task OnStartGiveaway()
      {
         if (!Data.IsOpenForEntries)
         {
            Data.IsOpenForEntries = true;
            await mHubConnection.InvokeAsync("OpenGiveaway");
            StateHasChanged();
         }
      }

      private async Task OnCloseGiveaway()
      {
         if (Data.IsOpenForEntries)
         {
            Data.IsOpenForEntries = false;
            await mHubConnection.InvokeAsync("CloseGiveaway");
            StateHasChanged();
         }
      }

      private async Task OnDrawWinner()
      {
         await mHubConnection.InvokeAsync("DrawWinner");
      }

      private async Task OnResetGiveaway()
      {
         await mHubConnection.InvokeAsync("ResetGiveaway");
         Data.Entrants.Clear();
         Winner = String.Empty;
         StateHasChanged();
      }

      /// <summary>
      /// Handle the access level changing.
      /// </summary>
      private void OnAccessLevelChange(int? value)
      {
         if (value.HasValue)
         {
            mAccessSelection = value.Value;
            Data.Access = (AnimationEnums.AccessType)value.Value;
         }
      }

      private void OnEntryBehaviorSelectionChange(int? value)
      {
         if (value.HasValue)
         {
            mEntryBehaviorSelection = value.Value;
            Data.EntryBehavior = (GiveawayData.GiveawayEntryBehaviorType)value.Value;
         }
      }

      private void HandleAddBannedUserRequest()
      {
         mIsAddBannedUserDialogVisible = true;
         mTempBannedUserName = String.Empty;
         StateHasChanged();
      }

      private void HandleCancelAddCommand()
      {
         mIsAddBannedUserDialogVisible = false;
         StateHasChanged();
      }

      private async Task HandleConfirmAddCommand()
      {
         bool result = await mHubConnection.InvokeAsync<bool>("AddBannedGiveawayUser", mTempBannedUserName);
         if (result == true)
         {
            NotificationService.Notify(NotificationSeverity.Success, "Success", "The banned user has been added.", 5000);
            mIsAddBannedUserDialogVisible = false;

            await GetGiveawayDataFromHub();

            await InvokeAsync(() => { StateHasChanged(); });
         }
         else
         {
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"The banned user could not be added. Either there was no text or the name is already in the list.", 5000);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private async Task HandleDeleteBannedUserRequest(string bannedUserName)
      {
         bool result = await mHubConnection.InvokeAsync<bool>("RemoveBannedGiveawayUser", bannedUserName);
         if (result == true)
         {
            NotificationService.Notify(NotificationSeverity.Success, "Success", "The banned user has been removed.", 5000);

            await GetGiveawayDataFromHub();

            await InvokeAsync(() => { StateHasChanged(); });
         }
         else
         {
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"The banned user could not be removed. Either there was no text or the name doesn't exist in the list.", 5000);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private void HandleDrumrollAnimationSelectionChanged(Guid id)
      {
         Data.DrumrollAnimation = id;
         StateHasChanged();
      }

      private void HandleWinnerAnimationSelectionChanged(Guid id)
      {
         Data.WinnerAnimation = id;
         StateHasChanged();
      }

      private void OnCancel()
      {
         NavigationManager.NavigateTo("/");
      }

      private async Task OnSaveChanges()
      {
         await mHubConnection.InvokeAsync("UpdateGiveawayData", JsonSerializer.Serialize(Data));
         await GetGiveawayDataFromHub();
         NotificationService.Notify(NotificationSeverity.Success, "Save Successful", "The giveaway data has been saved.", 5000);
         await InvokeAsync(() => { StateHasChanged(); });
      }

      #endregion

      #region Private Members

      private HubConnection mHubConnection;
      private int mAccessSelection = 0;
      private int mEntryBehaviorSelection = 0;
      private bool mIsAddBannedUserDialogVisible = false;
      private string mTempBannedUserName = String.Empty;

      #endregion
   }
}
