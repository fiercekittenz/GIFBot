using GIFBot.Shared.Models.Twitch;
using Microsoft.AspNetCore.Components;
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
   public partial class UserListManager : IAsyncDisposable
   {
      #region Properties

      public List<TwitchUserViewModel> Users
      {
         get;
         set;
      } = new List<TwitchUserViewModel>();

      public IEnumerable<TwitchUserViewModel> SelectedUsers 
      {
         get;
         set;
      } = Enumerable.Empty<TwitchUserViewModel>();

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

         // Get the user list data.
         await GetUserListFromHub();

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

      private async Task GetUserListFromHub()
      {
         string rawData = await mHubConnection.InvokeAsync<string>("GetUserList");
         Users = JsonConvert.DeserializeObject<List<TwitchUserViewModel>>(rawData);
      }

      private async Task OnBanUser(TwitchUserViewModel user)
      {
         if (user != null)
         {
            await mHubConnection.InvokeAsync("BanUser", user.Name);
            NotificationService.Notify(NotificationSeverity.Success, "Success", "The user was banned.", 5000);
            await GetUserListFromHub();
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private async Task OnTimeoutUser(TwitchUserViewModel user)
      {
         if (user != null)
         {
            await mHubConnection.InvokeAsync("TimeoutUser", user.Name);
            NotificationService.Notify(NotificationSeverity.Success, "Success", "The user was timed out for 10 minutes.", 5000);
            await GetUserListFromHub();
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private void UserSelectionChanged(IEnumerable<TwitchUserViewModel> users)
      {
         SelectedUsers = users;
         StateHasChanged();
      }

      private void HandleBanSelected()
      {
         mIsBanSelectedConfirmationVisible = true;
         StateHasChanged();
      }

      private void HandleCancelBanSelected()
      {
         mIsBanSelectedConfirmationVisible = false;
      }

      private async Task HandleConfirmBanSelected()
      {
         if (SelectedUsers.Any())
         {
            await mHubConnection.InvokeAsync<bool>("BanUsers", JsonConvert.SerializeObject(SelectedUsers));

            NotificationService.Notify(NotificationSeverity.Success, "Success", $"The selected users have been banned.", 5000);
            await GetUserListFromHub();
            await InvokeAsync(() => { StateHasChanged(); });
         }

         mIsBanSelectedConfirmationVisible = false;
         StateHasChanged();
      }

      private void HandleTimeoutSelected()
      {
         mIsTimeoutSelectedConfirmationVisible = true;
         StateHasChanged();
      }

      private void HandleCancelTimeoutSelected()
      {
         mIsTimeoutSelectedConfirmationVisible = false;
      }

      private async Task HandleConfirmTimeoutSelected()
      {
         if (SelectedUsers.Any())
         {
            await mHubConnection.InvokeAsync<bool>("TimeoutUsers", JsonConvert.SerializeObject(SelectedUsers));

            NotificationService.Notify(NotificationSeverity.Success, "Success", $"The selected users have been timed out.", 5000);
            await GetUserListFromHub();
            await InvokeAsync(() => { StateHasChanged(); });
         }

         mIsTimeoutSelectedConfirmationVisible = false;
         StateHasChanged();
      }

      #endregion

      #region Private Members

      private HubConnection mHubConnection;
      private bool mIsBanSelectedConfirmationVisible = false;
      private bool mIsTimeoutSelectedConfirmationVisible = false;

      #endregion
   }
}
