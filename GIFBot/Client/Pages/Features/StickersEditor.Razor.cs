using GIFBot.Shared.Models.Features;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Radzen;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Telerik.Blazor.Components;
using Microsoft.AspNetCore.Http.Connections;
using Telerik.Blazor;

namespace GIFBot.Client.Pages.Features
{
   public partial class StickersEditor : IAsyncDisposable
   {
      /// <summary>
      /// Keeps track of which tabs are selected.
      /// </summary>
      public int ActiveTabIndex { get; set; } = 0;
      public int ActiveStickerTabIndex { get; set; } = 0;

      public StickerEntryData CurrentlyEditedSticker { get; set; } = new StickerEntryData();
      public int CurrentlyEditedStickerLayer { get; set; } = 0;

      [CascadingParameter]
      public DialogFactory Dialogs { get; set; }

      protected override async Task OnInitializedAsync()
      {
         // Build the connection to the main bot hub.
         mHubConnection = new HubConnectionBuilder()
            .WithUrl(NavigationManager.ToAbsoluteUri("/gifbothub"), HttpTransportType.LongPolling) 
            .WithAutomaticReconnect()
            .Build();

         mHubConnection.On<int, int>("UpdatePosition", (top, left) =>
         {
            UpdateStickerPosition(top, left);
         });

         // Start the connection.
         await mHubConnection.StartAsync();

         // Get the sticker data.
         await GetStickerDataFromHub();

         // Get a string list of user groups.
         string userGroupsRaw = await mHubConnection.InvokeAsync<string>("GetUserGroupList");
         if (!String.IsNullOrEmpty(userGroupsRaw))
         {
            mUserGroupNames = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(userGroupsRaw);
            mUserGroupNames.Sort();
         }

         // Get the url.
         string pathResult = await mHubConnection.InvokeAsync<string>("GetStickersWebPaths");
         string[] paths = pathResult.Split(',');
         mURL = paths[0];
         mSecondaryURL = paths[1];

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

      private async Task GetStickerDataFromHub()
      {
         string rawData = await mHubConnection.InvokeAsync<string>("GetStickerData");
         mStickerData = JsonConvert.DeserializeObject<StickerData>(rawData);

         mFormVolume = (int)(mStickerData.Volume * 100);
         mAccessSelection = (int)(mStickerData.Access);

         // Map the sticker's access user group to the name.
         if (mStickerData.Access == GIFBot.Shared.AnimationEnums.AccessType.UserGroup)
         {
            mUserGroupName = await mHubConnection.InvokeAsync<string>("GetUserGroupNameById", mStickerData.RestrictedToUserGroup);
         }

         // Build the tree list data.
         mStickerTreeListData.Clear();
         int categoryCount = 1;
         foreach (var category in mStickerData.Categories)
         {
            StickerTreeListItem parent = new StickerTreeListItem() {
               Id = category.Id,
               TreeId = categoryCount,
               ParentTreeId = null,
               Name = category.Name,
               Type = StickerTreeListItem.ItemType.Category,
            };

            int stickerCount = 1;
            foreach (var entry in category.Entries)
            {
               StickerTreeListItem child = new StickerTreeListItem() {
                  Id = entry.Id,
                  TreeId = Int32.Parse($"{categoryCount}{stickerCount}"),
                  ParentTreeId = categoryCount,
                  Name = entry.Name,
                  Visual = entry.Visual,
                  Enabled = entry.Enabled,
                  Type = StickerTreeListItem.ItemType.Entry,
               };

               mStickerTreeListData.Add(child);
               ++stickerCount;
            }

            mStickerTreeListData.Add(parent);
            ++categoryCount;
         }
      }

      private async Task HandleCopyUrl(string elementName)
      {
         await JSRuntime.InvokeVoidAsync("CopyToClipboard", elementName);
         NotificationService.Notify(NotificationSeverity.Success, "Success", "The URL was copied to your clipboard!", 5000);
         await InvokeAsync(() => { StateHasChanged(); });
      }

      private void OnImportAudioFileProgress(UploadProgressArgs e)
      {
         mUploadAudioProgress = e.Progress;
         StateHasChanged();
      }

      private void OnImportAudioFileComplete(UploadCompleteEventArgs e)
      {
         // Upload completed.
         mStickerData.Audio = e.RawResponse;
         mUploadAudioProgress = 100;
         StateHasChanged();
      }

      private void OnImportAudioFileError(Radzen.UploadErrorEventArgs e)
      {
         mUploadAudioErrorMessage = $"There was an error uploading the file.";
         StateHasChanged();
      }

      /// <summary>
      /// Clears out the audio file information.
      /// </summary>
      private void ClearAudioFile()
      {
         mStickerData.Audio = String.Empty;
         mUploadAudioProgress = 0;
         StateHasChanged();
      }

      /// <summary>
      /// Handle the volume slider changing.
      /// </summary>
      private void OnVolumeChanged(dynamic value)
      {
         if (value is ChangeEventArgs changeEventArgs)
         {
            mStickerData.Volume = (double)((double)(Int32.Parse(changeEventArgs.Value.ToString())) / 100);
            StateHasChanged();
         }
      }

      /// <summary>
      /// Tests the audio level.
      /// </summary>
      private async Task HandleTestAudioVolume()
      {
         await JSRuntime.InvokeVoidAsync("PlaySound", $"media/{mStickerData.Audio}", mStickerData.Volume, 0);
      }

      private void OnCancel()
      {
         NavigationManager.NavigateTo("/");
      }

      private async Task OnSaveChanges()
      {
         await HandleDisplayTestModeChanged(false, null);
         await mHubConnection.InvokeAsync("UpdateStickerData", JsonConvert.SerializeObject(mStickerData));
         await GetStickerDataFromHub();
         NotificationService.Notify(NotificationSeverity.Success, "Save Successful", "The sticker data has been saved.", 5000);
         await InvokeAsync(() => { StateHasChanged(); });
      }

      private async Task HandleUserGroupSelected(string groupName)
      {
         if (!String.IsNullOrEmpty(groupName))
         {
            Guid groupId = await mHubConnection.InvokeAsync<Guid>("GetGroupIdByName", groupName);
            mStickerData.RestrictedToUserGroup = groupId;
            mSelectedUserGroupName = groupName;
            StateHasChanged();
         }
      }

      /// <summary>
      /// Handle the access level changing.
      /// </summary>
      private void OnAccessLevelChange(int? value)
      {
         if (value.HasValue)
         {
            mAccessSelection = value.Value;
            mStickerData.Access = (GIFBot.Shared.AnimationEnums.AccessType)value.Value;
         }
      }

      #region Sticker Categories

      private void HandleAddCategoryRequest(TreeListCommandEventArgs args)
      {
         mIsCreateCategoryDialogVisible = true;
         mTempCategory = new StickerCategory();
         StateHasChanged();
      }

      private void HandleCancelAddCategory()
      {
         mIsCreateCategoryDialogVisible = false;
         mTempCategory = new StickerCategory();
         StateHasChanged();
      }

      private async Task HandleConfirmAddCategory()
      {
         bool result = await mHubConnection.InvokeAsync<bool>("AddStickerCategory", mTempCategory.Name);
         if (result)
         {
            NotificationService.Notify(NotificationSeverity.Success, "Success", "The category has been added.", 5000);
            mIsCreateCategoryDialogVisible = false;
            await GetStickerDataFromHub();
            await InvokeAsync(() => { StateHasChanged(); });
         }
         else
         {
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"The category could not be added. Either there was no text or the name is in use by another category.", 5000);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private void HandleCancelEditCategory()
      {
         mIsEditCategoryDialogVisible = false;
         mTempCategory = new StickerCategory();
         StateHasChanged();
      }

      private async Task HandleConfirmEditCategory()
      {
         bool result = await mHubConnection.InvokeAsync<bool>("UpdateStickerCategory", mTempCategory.Id, mTempCategory.Name);
         if (result)
         {
            NotificationService.Notify(NotificationSeverity.Success, "Success", "The category has been updated.", 5000);
            mIsEditCategoryDialogVisible = false;
            await GetStickerDataFromHub();
            await InvokeAsync(() => { StateHasChanged(); });
         }
         else
         {
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"The category could not be updated. Either there was no text or the name is in use by another category.", 5000);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private async Task HandleMoveSelectedRequest()
      {
         if (mSelectedTreeItems.Where(t => t.Type == StickerTreeListItem.ItemType.Entry).Any())
         {
            mIsMoveCategoryDialogVisible = true;
            mTempStickerCategoryId = Guid.Empty;
            StateHasChanged();
         }

         await Task.CompletedTask;
      }

      private async Task HandleDeleteSelectedRequest()
      {
         bool confirmed = await Dialogs.ConfirmAsync($"Are you sure you want to delete the selected stickers?", "Delete?");
         if (confirmed)
         {
            List<StickerTreeListItem> selectedStickers = mSelectedTreeItems.Where(t => t.Type == StickerTreeListItem.ItemType.Entry).ToList();
            if (selectedStickers.Any())
            {
               List<Guid> stickersToDelete = new List<Guid>();
               foreach (var sticker in selectedStickers)
               {
                  stickersToDelete.Add(sticker.Id);
               }

               bool result = await mHubConnection.InvokeAsync<bool>("DeleteStickers", JsonConvert.SerializeObject(stickersToDelete));
               if (result)
               {
                  NotificationService.Notify(NotificationSeverity.Success, "Success", "The stickers have been deleted.", 5000);
                  await GetStickerDataFromHub();
                  await InvokeAsync(() => { StateHasChanged(); });
               }
               else
               {
                  NotificationService.Notify(NotificationSeverity.Error, "Error", $"The stickers could not be deleted.", 5000);
                  await InvokeAsync(() => { StateHasChanged(); });
               }
            }
         }
      }

      private async Task HandleEnableSelectedRequest()
      {
         List<StickerTreeListItem> selectedStickers = mSelectedTreeItems.Where(t => t.Type == StickerTreeListItem.ItemType.Entry).ToList();
         if (selectedStickers.Any())
         {
            List<Guid> stickersToModify = new List<Guid>();
            foreach (var sticker in selectedStickers)
            {
               stickersToModify.Add(sticker.Id);
            }

            bool result = await mHubConnection.InvokeAsync<bool>("EnableStickers", JsonConvert.SerializeObject(stickersToModify));
            if (result)
            {
               NotificationService.Notify(NotificationSeverity.Success, "Success", "The stickers have been enabled.", 5000);
               await GetStickerDataFromHub();
               await InvokeAsync(() => { StateHasChanged(); });
            }
            else
            {
               NotificationService.Notify(NotificationSeverity.Error, "Error", $"The stickers could not be enabled.", 5000);
               await InvokeAsync(() => { StateHasChanged(); });
            }
         }
      }

      private async Task HandleDisableSelectedRequest()
      {
         List<StickerTreeListItem> selectedStickers = mSelectedTreeItems.Where(t => t.Type == StickerTreeListItem.ItemType.Entry).ToList();
         if (selectedStickers.Any())
         {
            List<Guid> stickersToModify = new List<Guid>();
            foreach (var sticker in selectedStickers)
            {
               stickersToModify.Add(sticker.Id);
            }

            bool result = await mHubConnection.InvokeAsync<bool>("DisableStickers", JsonConvert.SerializeObject(stickersToModify));
            if (result)
            {
               NotificationService.Notify(NotificationSeverity.Success, "Success", "The stickers have been disabled.", 5000);
               await GetStickerDataFromHub();
               await InvokeAsync(() => { StateHasChanged(); });
            }
            else
            {
               NotificationService.Notify(NotificationSeverity.Error, "Error", $"The stickers could not be disabled.", 5000);
               await InvokeAsync(() => { StateHasChanged(); });
            }
         }
      }

      private void HandleCancelMoveCategory()
      {
         mIsMoveCategoryDialogVisible = false;
         mTempStickerCategoryId = Guid.Empty;
         StateHasChanged();
      }

      private async Task HandleConfirmMoveCategory()
      {
         List<Guid> stickersToEdit = new List<Guid>();
         foreach (var entry in mSelectedTreeItems.Where(t => t.Type == StickerTreeListItem.ItemType.Entry))
         {
            stickersToEdit.Add(entry.Id);
         }

         bool result = await mHubConnection.InvokeAsync<bool>("MoveStickerCategory", JsonConvert.SerializeObject(stickersToEdit), mTempStickerCategoryId);
         if (result)
         {
            NotificationService.Notify(NotificationSeverity.Success, "Success", "The stickers have been moved to the new category.", 5000);
            mIsMoveCategoryDialogVisible = false;
            mTempStickerCategoryId = Guid.Empty;
            await GetStickerDataFromHub();
            await InvokeAsync(() => { StateHasChanged(); });
         }
         else
         {
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"The stickers could not be moved to the new category.", 5000);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      #endregion

      #region Sticker Files

      /// <summary>
      /// Handles when a new Sticker is requested for add.
      /// </summary>
      private async Task HandleAddNewSticker()
      {
         bool result = await mHubConnection.InvokeAsync<bool>("AddStickerEntry", JsonConvert.SerializeObject(mTempStickerToAdd), mTempStickerCategoryId);
         if (result)
         {
            NotificationService.Notify(NotificationSeverity.Success, "Success", "The sticker has been added.", 5000);
            await GetStickerDataFromHub();

            Guid newStickerId = mTempStickerToAdd.Id;

            mTempStickerToAdd = new StickerEntryData();
            mUploadStickerVisualProgress = 0;

            CurrentlyEditedSticker = null;
            foreach (var category in mStickerData.Categories)
            {
               StickerEntryData found = category.Entries.FirstOrDefault(s => s.Id == newStickerId);
               if (found != null)
               {
                  CurrentlyEditedSticker = found;
                  break;
               }
            }

            if (CurrentlyEditedSticker != null)
            {
               // Set the initial canvas size to the primary canvas settings.
               SetPlacementComponentCanvasSize(CurrentlyEditedSticker);
               CurrentlyEditedStickerLayer = 0;

               await HandleDisplayTestModeChanged(true, CurrentlyEditedSticker);
               mIsPlacementBeingEdited = true;
            }

            await InvokeAsync(() => { StateHasChanged(); });
         }
         else
         {
            NotificationService.Notify(NotificationSeverity.Error, "Error", $"The sticker could not be added. It is too large for your canvas. Resize the image before uploading.", 5000);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      /// <summary>
      /// Resets the add new sticker form.
      /// </summary>
      private void HandleCancelNewSticker()
      {
         mTempStickerToAdd = new StickerEntryData();
         mTempStickerCategoryId = Guid.Empty;
         mUploadStickerVisualProgress = 0;
         ActiveStickerTabIndex = 0;
         StateHasChanged();
      }

      private async Task HandlePlaceSticker(StickerTreeListItem treeItem)
      {
         if (treeItem != null && treeItem.Type == StickerTreeListItem.ItemType.Entry)
         {
            await mHubConnection.InvokeAsync("PlaceSticker", treeItem.Id);
         }
      }

      /// <summary>
      /// Handles when the display test mode changes on the placement component.
      /// </summary>
      private async Task HandleDisplayTestModeChanged(bool isDisplayTestModeOn, StickerEntryData stickerToTest)
      {
         mIsDisplayTestMode = isDisplayTestModeOn;

         Guid stickerId = Guid.Empty;
         GIFBot.Shared.AnimationEnums.AnimationLayer layer = GIFBot.Shared.AnimationEnums.AnimationLayer.Primary;
         if (stickerToTest != null)
         {
            stickerId = stickerToTest.Id;
            layer = stickerToTest.Layer;
         }

         StateHasChanged();

         await mHubConnection.InvokeAsync("SetStickerDisplayTestMode", isDisplayTestModeOn, stickerId, layer);
      }

      private async Task NavigationManager_LocationChanged(object sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
      {
         // If the user leaves the editor, toggle off the display test mode.
         await HandleDisplayTestModeChanged(false, null);
      }

      /// <summary>
      /// Handles when the width changes on the placement component.
      /// </summary>
      private async Task HandleNotifyDimensionsChanged(Tuple<int, int> dimensions, StickerEntryData stickerToTest)
      {
         if (stickerToTest != null)
         {
            stickerToTest.Placement.Width = dimensions.Item1;
            stickerToTest.Placement.Height = dimensions.Item2;

            await mHubConnection.InvokeAsync("UpdateStickerDisplayTestDimensions", dimensions.Item1, dimensions.Item2);
         }
      }

      private void UpdateStickerPosition(int top, int left)
      {
         if (CurrentlyEditedSticker != null)
         {
            CurrentlyEditedSticker.Placement.Top = top;
            CurrentlyEditedSticker.Placement.Left = left;

            StateHasChanged();
         }
      }

      private async Task OnLayerChange(int? value)
      {
         if (value.HasValue)
         {
            CurrentlyEditedStickerLayer = value.Value;
            CurrentlyEditedSticker.Layer = (GIFBot.Shared.AnimationEnums.AnimationLayer)value.Value;

            SetPlacementComponentCanvasSize(CurrentlyEditedSticker);

            if (mIsDisplayTestMode)
            {
               // If we're in display mode, go ahead and toggle off then back on so it adjusts and places
               // the sticker visual on the correct layer.
               await HandleDisplayTestModeChanged(false, null);
               await HandleDisplayTestModeChanged(true, CurrentlyEditedSticker);
            }
         }
      }

      private void SetPlacementComponentCanvasSize(StickerEntryData sticker)
      {
         if (sticker != null)
         {
            if (sticker.Layer == GIFBot.Shared.AnimationEnums.AnimationLayer.Primary)
            {
               mWorkingCanvasWidth = mStickerData.CanvasWidth;
               mWorkingCanvasHeight = mStickerData.CanvasHeight;
            }
            else
            {
               mWorkingCanvasWidth = mStickerData.SecondaryCanvasWidth;
               mWorkingCanvasHeight = mStickerData.SecondaryCanvasHeight;
            }
         }
      }

      private void HandleAddStickerRequest(Guid id)
      {
         mTempStickerToAdd = new StickerEntryData();
         mTempStickerCategoryId = id;
         ActiveStickerTabIndex = 1;
         StateHasChanged();
      }

      private async Task HandleEditStickerRequest(StickerTreeListItem treeItem)
      {
         if (treeItem != null)
         {
            if (treeItem.Type == StickerTreeListItem.ItemType.Entry)
            {
               StickerEntryData sticker = null;
               foreach (var category in mStickerData.Categories)
               {
                  sticker = category.Entries.FirstOrDefault(s => s.Id == treeItem.Id);
                  if (sticker != null)
                  {
                     break;
                  }
               }

               if (sticker != null)
               {
                  CurrentlyEditedSticker = sticker;
                  CurrentlyEditedStickerLayer = (int)sticker.Layer;
                  SetPlacementComponentCanvasSize(sticker);

                  await HandleDisplayTestModeChanged(true, CurrentlyEditedSticker);
                  mIsPlacementBeingEdited = true;
                  StateHasChanged();
               }
            }
            else if (treeItem.Type == StickerTreeListItem.ItemType.Category)
            {
               StickerCategory found = mStickerData.Categories.FirstOrDefault(c => c.Id == treeItem.Id);
               if (found != null)
               {
                  mIsEditCategoryDialogVisible = true;
                  mTempCategory = found;
                  StateHasChanged();
               }
            }
         }
      }

      private async Task HandleCancelStickerUpdate()
      {
         await HandleDisplayTestModeChanged(false, null);
         await GetStickerDataFromHub();
         mIsPlacementBeingEdited = false;
         StateHasChanged();
      }

      private async Task HandleConfirmStickerUpdate()
      {
         await HandleDisplayTestModeChanged(false, null);
         await UpdateStickerOnServer(CurrentlyEditedSticker);
         mIsPlacementBeingEdited = false;
         StateHasChanged();
      }

      /// <summary>
      /// Handles the deletion of a Sticker.
      /// </summary>
      private async Task HandleDeleteSticker(StickerTreeListItem treeItem)
      {
         bool confirmed = await Dialogs.ConfirmAsync($"Are you sure you want to delete the sticker?", "Delete?");
         if (confirmed)
         {
            await HandleDisplayTestModeChanged(false, null);

            if (treeItem != null)
            {
               if (treeItem.Type == StickerTreeListItem.ItemType.Entry)
               {
                  StickerEntryData sticker = null;
                  foreach (var category in mStickerData.Categories)
                  {
                     sticker = category.Entries.FirstOrDefault(s => s.Id == treeItem.Id);
                     if (sticker != null)
                     {
                        break;
                     }
                  }

                  if (sticker != null)
                  {
                     bool result = await mHubConnection.InvokeAsync<bool>("DeleteStickerEntry", sticker.Id);
                     if (result)
                     {
                        NotificationService.Notify(NotificationSeverity.Success, "Success", "The sticker has been deleted.", 5000);
                        await GetStickerDataFromHub();
                        await InvokeAsync(() => { StateHasChanged(); });
                     }
                     else
                     {
                        NotificationService.Notify(NotificationSeverity.Error, "Error", $"The sticker could not be deleted.", 5000);
                        await InvokeAsync(() => { StateHasChanged(); });
                     }
                  }
               }
               else if (treeItem.Type == StickerTreeListItem.ItemType.Category)
               {
                  bool result = await mHubConnection.InvokeAsync<bool>("DeleteStickerCategory", treeItem.Id);
                  if (result)
                  {
                     NotificationService.Notify(NotificationSeverity.Success, "Success", "The category has been deleted.", 5000);
                     await GetStickerDataFromHub();
                     await InvokeAsync(() => { StateHasChanged(); });
                  }
                  else
                  {
                     NotificationService.Notify(NotificationSeverity.Error, "Error", $"The category could not be deleted. Does it have stickers in it? Move them first!", 5000);
                     await InvokeAsync(() => { StateHasChanged(); });
                  }
               }
            }
         }
      }

      private void StickerPageChanged(int page)
      {
         mStickerPage = page;
      }

      private void StickersListSelectionChanged(IEnumerable<StickerTreeListItem> items)
      {
         mSelectedTreeItems = items;
         StateHasChanged();
      }

      private async Task UpdateStickerOnServer(StickerEntryData sticker)
      {
         if (sticker != null)
         {
            mUploadStickerVisualProgress = 0;

            await HandleDisplayTestModeChanged(false, null);

            bool result = await mHubConnection.InvokeAsync<bool>("UpdateStickerEntry", JsonConvert.SerializeObject(sticker));
            if (result)
            {
               NotificationService.Notify(NotificationSeverity.Success, "Success", "The sticker was updated.", 5000);
               await GetStickerDataFromHub();
               await InvokeAsync(() => { StateHasChanged(); });
            }
            else
            {
               NotificationService.Notify(NotificationSeverity.Error, "Error", $"The sticker could not be updated.", 5000);
               await InvokeAsync(() => { StateHasChanged(); });
            }
         }
      }

      private void OnImportStickerVisualFileProgress(UploadProgressArgs e)
      {
         mUploadStickerVisualProgress = e.Progress;
         StateHasChanged();
      }

      private async Task OnImportStickerVisualFileComplete(UploadCompleteEventArgs e)
      {
         // Upload completed.
         StickerEntryData dataToEdit = mTempStickerToAdd;
         if (mIsPlacementBeingEdited)
         {
            dataToEdit = CurrentlyEditedSticker;
         }

         dataToEdit.Visual = e.RawResponse;
         mUploadStickerVisualProgress = 100;

         string result = await mHubConnection.InvokeAsync<string>("GetStickerFileDimensions", dataToEdit.Visual);
         if (!String.IsNullOrEmpty(result))
         {
            Tuple<int, int> dimensions = JsonConvert.DeserializeObject<Tuple<int, int>>(result);
            dataToEdit.Placement.Width = dimensions.Item1;
            dataToEdit.Placement.Height = dimensions.Item2;

            if (mIsPlacementBeingEdited)
            {
               await mHubConnection.InvokeAsync("UpdateStickerDisplayTestDimensions", dimensions.Item1, dimensions.Item2);
            }
         }

         StateHasChanged();
      }

      private void OnImportStickerVisualFileError(Radzen.UploadErrorEventArgs e)
      {
         mUploadStickerVisualErrorMessage = $"There was an error uploading the file.";
         StateHasChanged();
      }

      private async Task ClearAllStickers()
      {
         await mHubConnection.InvokeAsync("ClearAllStickers");
      }

      private async Task DisableAllStickers()
      {
         await mHubConnection.InvokeAsync("SetStickerEnabledFlags", false);
         await GetStickerDataFromHub();
         StateHasChanged();
      }

      private async Task EnableAllStickers()
      {
         await mHubConnection.InvokeAsync("SetStickerEnabledFlags", true);
         await GetStickerDataFromHub();
         StateHasChanged();
      }

      #endregion

      private HubConnection mHubConnection;

      private StickerData mStickerData = new StickerData();
      private ObservableCollection<StickerTreeListItem> mStickerTreeListData = new ObservableCollection<StickerTreeListItem>();
      private string mURL = String.Empty;
      private string mSecondaryURL = String.Empty;
      private string mSelectedUserGroupName = String.Empty;
      private List<string> mUserGroupNames = new List<string>();
      private int mAccessSelection = 0;
      private string mUserGroupName = String.Empty;

      // Audio Upload Variables
      private string mUploadAudioErrorMessage = String.Empty;
      private int mUploadAudioProgress = 0;
      private int mFormVolume = 50;

      // Sticker Upload Variables
      private StickerEntryData mTempStickerToAdd = new StickerEntryData();
      private Guid mTempStickerCategoryId = Guid.Empty;
      private string mUploadStickerVisualErrorMessage = String.Empty;
      private int mUploadStickerVisualProgress = 0;
      private int mStickerPage = 1;

      // Sticker Placement Editing (popup)
      private bool mIsPlacementBeingEdited = false;
      private bool mIsDisplayTestMode = true;
      private int mWorkingCanvasWidth = 1920;
      private int mWorkingCanvasHeight = 1080;

      // Add Category Variables
      private StickerCategory mTempCategory = new StickerCategory();
      private bool mIsCreateCategoryDialogVisible = false;
      private bool mIsEditCategoryDialogVisible = false;
      private bool mIsMoveCategoryDialogVisible = false;

      // Selection
      private IEnumerable<StickerTreeListItem> mSelectedTreeItems = Enumerable.Empty<StickerTreeListItem>();
   }
}
