using GIFBot.Shared.Models.Animation;
using GIFBot.Shared.Models.Features;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using Radzen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telerik.Blazor.Components;

namespace GIFBot.Client.Pages.Features
{
   public partial class GreeterEditor
   {
      #region Properties

      public GreeterData Data { get; set; } = new GreeterData();

      public List<AnimationSelectorItem> AnimationOptions { get; set; } = new List<AnimationSelectorItem>();

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

         // Start the connection.
         await mHubConnection.StartAsync();

         // Get the greeter data.
         await GetGreeterDataFromHub();

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

      #region Editing

      private void OnCancel()
      {
         NavigationManager.NavigateTo("/");
      }

      private void HandleAddRequest()
      {
         mIsAddDialogVisible = true;
         mTempGreeterEntry = new GreeterEntry();
         StateHasChanged();
      }

      private void HandleCancelAddCommand()
      {
         mIsAddDialogVisible = false;
         mTempGreeterEntry = new GreeterEntry();
         StateHasChanged();
      }

      private async Task HandleConfirmAddCommand()
      {
         Guid result = await mHubConnection.InvokeAsync<Guid>("AddGreeterEntry", mTempGreeterEntry.Name);
         if (result != Guid.Empty)
         {
            NotificationService.Notify(NotificationSeverity.Success, "Success", "The entry has been added.", 5000);
            mIsAddDialogVisible = false;

            await GetGreeterDataFromHub();

            // Immediately open the edit dialog.
            mTempGreeterEntry = Data.Entries.FirstOrDefault(c => c.Id == result);
            if (mTempGreeterEntry != null)
            {
               mIsEditDialogVisible = true;
            }

            await InvokeAsync(() => { StateHasChanged(); });
         }
         else
         {
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"The entry could not be added. Either there was no text or the name is in use by another command.", 5000);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private void HandleEditRequest(GreeterEntry entry)
      {
         if (entry != null)
         {
            mTempGreeterEntry = entry.Clone() as GreeterEntry;
            mIsEditDialogVisible = true;
            StateHasChanged();
         }
      }

      private void HandleCancelEditCommand()
      {
         mIsEditDialogVisible = false;
         mTempGreeterEntry = new GreeterEntry();
         StateHasChanged();
      }

      private async Task HandleConfirmEditCommand()
      {
         bool result = await mHubConnection.InvokeAsync<bool>("UpdateGreeterEntry", JsonConvert.SerializeObject(mTempGreeterEntry));
         if (result)
         {
            NotificationService.Notify(NotificationSeverity.Success, "Success", "The entry has been updated.", 5000);
            mIsEditDialogVisible = false;
            await GetGreeterDataFromHub();
            await InvokeAsync(() => { StateHasChanged(); });
         }
         else
         {
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"The entry could not be updated.", 5000);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private async Task HandleDeleteRequest(GreeterEntry entry)
      {
         if (entry != null)
         {
            bool result = await mHubConnection.InvokeAsync<bool>("DeleteGreeterEntry", entry.Id);
            if (result)
            {
               await GetGreeterDataFromHub();
               NotificationService.Notify(NotificationSeverity.Success, "Delete Successful", "The Greeter entry has been deleted.", 5000);
               await InvokeAsync(() => { StateHasChanged(); });
            }
            else
            {
               NotificationService.Notify(NotificationSeverity.Error, "Delete Failed", "The Greeter entry was not deleted.", 5000);
               await InvokeAsync(() => { StateHasChanged(); });
            }
         }
      }

      private void HandleAnimationSelectionChanged(Guid id)
      {
         mTempGreeterEntry.AnimationId = id;
         StateHasChanged();
      }

      public void DeleteRecipientHandler(ListViewCommandEventArgs args)
      {
         GreetedPersonality person = args.Item as GreetedPersonality;
         if (person != null)
         {
            mTempGreeterEntry.Recipients.Remove(person);
         }
      }

      public void CreateRecipientHandler(ListViewCommandEventArgs args)
      {
         GreetedPersonality person = args.Item as GreetedPersonality;
         mTempGreeterEntry.Recipients.Add(person);
      }

      #endregion

      #region Bulk Import Recipients
      private void OnImportTextFileProgress(UploadProgressArgs e)
      {
         mUploadProgress = e.Progress;
      }

      private async Task OnImportTextFileComplete(UploadCompleteEventArgs e)
      {
         // Upload completed. Redownload the data and reset upload info.
         mUploadProgress = 0;
         mUploadErrorMessage = String.Empty;
         await GetGreeterRecipientsFromImport(mTempGreeterEntry.Id);
         StateHasChanged();
      }

      private void OnImportTextFileError(Radzen.UploadErrorEventArgs e)
      {
         mUploadErrorMessage = $"There was an error uploading the file.";
      }

      #endregion

      #region Private Methods

      private async Task GetGreeterDataFromHub()
      {
         string rawData = await mHubConnection.InvokeAsync<string>("GetGreeterData");
         Data = JsonConvert.DeserializeObject<GreeterData>(rawData);
      }

      private async Task GetAnimationOptions()
      {
         string rawData = await mHubConnection.InvokeAsync<string>("GetAnimationOptions");
         AnimationOptions = JsonConvert.DeserializeObject<List<AnimationSelectorItem>>(rawData);
      }

      private async Task GetGreeterRecipientsFromImport(Guid greeterEntryId)
      {
         string rawData = await mHubConnection.InvokeAsync<string>("GetGreeterRecipientsFromImport", greeterEntryId);
         mTempGreeterEntry.Recipients = JsonConvert.DeserializeObject<List<GreetedPersonality>>(rawData);
      }

      #endregion

      #region Private Members

      private HubConnection mHubConnection;

      private GreeterEntry mTempGreeterEntry = new GreeterEntry();

      private bool mIsAddDialogVisible = false;

      private bool mIsEditDialogVisible = false;

      private int mUploadProgress = 0;

      private string mUploadErrorMessage = String.Empty;

      #endregion
   }
}
