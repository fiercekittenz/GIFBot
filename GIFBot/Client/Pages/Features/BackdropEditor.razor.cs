using GIFBot.Shared.Models.Features;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static GIFBot.Shared.Utility.Enumerations;
using MudBlazor;

namespace GIFBot.Client.Pages.Features
{
   public partial class BackdropEditor : IAsyncDisposable
   {
      #region Properties

      public BackdropData Data { get; set; } = new BackdropData();

      public int ActiveTabIndex { get; set; } = 0;

      public int CurrentlyEditedBehavior { get; set; } = 0;

      public int CurrentlyEditedRedemption { get; set; } = 0;

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
         await GetBackdropDataFromHub();

         // Get the url.
         WebPath = await mHubConnection.InvokeAsync<string>("GetBackdropWebPath");

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

      private async Task OnTakeDownBackdrop()
      {
         await mHubConnection.InvokeAsync("TakeDownBackdrop");
      }

      private void OnCancel()
      {
         NavigationManager.NavigateTo("/");
      }

      private async Task OnSaveChanges()
      {
         await mHubConnection.InvokeAsync("UpdateBackdropData", JsonConvert.SerializeObject(Data));
         await GetBackdropDataFromHub();
         Snackbar.Add("Save Successful - The backdrop data has been saved.", Severity.Success);
         await InvokeAsync(() => { StateHasChanged(); });
      }

      private void HandleAddRequest()
      {
         mIsAddDialogVisible = true;
         mTempData = new BackdropVideoEntryData();
         StateHasChanged();
      }

      private async Task HandleConfirmAddCommand()
      {
         Guid result = await mHubConnection.InvokeAsync<Guid>("AddBackdrop", mTempData.Name);
         if (result != Guid.Empty)
         {
            Snackbar.Add("Success - The backdrop has been added.", Severity.Success);
            mIsAddDialogVisible = false;

            await GetBackdropDataFromHub();

            // Immediately open the edit dialog.
            mTempData = Data.Backdrops.FirstOrDefault(c => c.Id == result);
            if (mTempData != null)
            {
               mIsEditDialogVisible = true;
            }

            await InvokeAsync(() => { StateHasChanged(); });
         }
         else
         {
            Snackbar.Add($"Error - The backdrop could not be added. Either there was no text or the name is in use by another backdrop.", Severity.Error);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private void HandleCancelAddCommand()
      {
         mIsAddDialogVisible = false;
         mTempData = new BackdropVideoEntryData();
         StateHasChanged();
      }

      private void HandleEditRequest(BackdropVideoEntryData backdrop)
      {
         mTempData = backdrop.Clone() as BackdropVideoEntryData;
         mUploadVisualProgress = 0;
         mIsEditDialogVisible = true;
         StateHasChanged();
      }

      private void HandleCancelEditCommand()
      {
         mIsEditDialogVisible = false;
         mTempData = new BackdropVideoEntryData();
         mUploadVisualProgress = 0;
         StateHasChanged();
      }

      private async Task HandleConfirmEditCommand()
      {
         bool result = await mHubConnection.InvokeAsync<bool>("UpdateBackdrop", JsonConvert.SerializeObject(mTempData));
         if (result)
         {
            Snackbar.Add("Success - The backdrop has been updated.", Severity.Success);
            mIsEditDialogVisible = false;
            await GetBackdropDataFromHub();
            await InvokeAsync(() => { StateHasChanged(); });
         }
         else
         {
            Snackbar.Add($"Error - The backdrop could not be updated.", Severity.Error);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private async Task HandleDeleteRequest(BackdropVideoEntryData backdrop)
      {
         if (backdrop != null)
         {
            bool result = await mHubConnection.InvokeAsync<bool>("DeleteBackdrop", backdrop.Id);
            if (result)
            {
               await GetBackdropDataFromHub();
               Snackbar.Add("Delete Successful - The backdrop has been deleted.", Severity.Success);
               await InvokeAsync(() => { StateHasChanged(); });
            }
            else
            {
               Snackbar.Add("Delete Failed - The backdrop was not deleted.", Severity.Error);
               await InvokeAsync(() => { StateHasChanged(); });
            }
         }
      }

      private async Task HandleHangRequest(BackdropVideoEntryData backdrop)
      {
         if (backdrop != null)
         {
            await mHubConnection.InvokeAsync("HangBackdrop", backdrop.Id);
         }
      }

      private void OnRedemptionTypeChange(int? value)
      {
         if (value.HasValue)
         {
            CurrentlyEditedRedemption = value.Value;
            Data.RedemptionType = (CostRedemptionType)value.Value;
         }
      }

      private async Task HandleCopyUrl(string elementName)
      {
         await JSRuntime.InvokeVoidAsync("CopyToClipboard", elementName);
         Snackbar.Add("Success - The URL was copied to your clipboard!", Severity.Success);
         await InvokeAsync(() => { StateHasChanged(); });
      }

      #endregion

      #region Upload Visual

      private void OnImportVisualFileProgress(EventArgs e)
      {
         // Upload progress tracking not available with InputFile
         StateHasChanged();
      }

      private async Task OnImportVisualFileComplete(InputFileChangeEventArgs e)
      {
         // Upload completed.
         var file = e.File;
         using var stream = file.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
         using var ms = new System.IO.MemoryStream();
         await stream.CopyToAsync(ms);
         mTempData.Visual = Convert.ToBase64String(ms.ToArray());
         mUploadVisualProgress = 100;

         StateHasChanged();
      }

      private void OnImportVisualFileError(EventArgs e)
      {
         mUploadVisualErrorMessage = $"There was an error uploading the file.";
         StateHasChanged();
      }

      #endregion

      #region Private Methods

      private async Task GetBackdropDataFromHub()
      {
         string rawData = await mHubConnection.InvokeAsync<string>("GetBackdropData");
         Data = JsonConvert.DeserializeObject<BackdropData>(rawData);
         CurrentlyEditedRedemption = (int)Data.RedemptionType;
      }

      #endregion

      #region Private Members

      private bool mIsAddDialogVisible = false;

      private bool mIsEditDialogVisible = false;

      private string mUploadVisualErrorMessage = String.Empty;

      private int mUploadVisualProgress = 0;

      private HubConnection mHubConnection;

      private BackdropVideoEntryData mTempData = new BackdropVideoEntryData();

      #endregion
   }
}
