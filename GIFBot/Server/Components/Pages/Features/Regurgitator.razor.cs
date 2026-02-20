using global::GIFBot.Shared;
using global::GIFBot.Shared.Models.Visualization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using MudBlazor;
namespace GIFBot.Server.Components.Pages.Features
{
   public partial class Regurgitator : IAsyncDisposable
   {
      /// <summary>
      /// Keeps track of which tab is selected.
      /// </summary>
      public int ActiveTabIndex { get; set; } = 0;

      /// <summary>
      /// Number of total entries in the Regurgitator Data.
      /// </summary>
      public int TotalEntries { get; set; } = 0;

      /// <summary>
      /// Dialog factory (migrated from Telerik).
      /// </summary>
      // TODO: Replace DialogFactory with IDialogService
      // [CascadingParameter]
      // public DialogFactory Dialogs { get; set; }

      /// <summary>
      /// The currently selected package.
      /// </summary>
      public Guid CurrentPackage { get; set; } = Guid.Empty;

      /// <summary>
      /// Information on available packages.
      /// </summary>
      public ObservableCollection<RegurgitatorPackageBase> AvailablePackages { get; set; } = new ObservableCollection<RegurgitatorPackageBase>();

      /// <summary>
      /// Working list of entries.
      /// </summary>
      public List<RegurgitatorEntry> CurrentEntries { get; set; } = new List<RegurgitatorEntry>();

      /// <summary>
      /// List of Azure-capable TTS voices.
      /// </summary>
      /// https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/rest-text-to-speech#get-a-list-of-voices
      public List<string> TTSVoiceOptions { get; set; } = new List<string>()
      {
         "en-AU-Catherine",
         "en-AU-HayleyRUS",
         "en-CA-Linda",
         "en-CA-HeatherRUS",
         "en-GB-Susan-Apollo",
         "en-GB-HazelRUS",
         "en-GB-George-Apollo",
         "en-IE-Sean",
         "en-US-ZiraRUS",
         "en-US-AriaRUS",
         "en-US-BenjaminRUS",
         "en-US-Guy24kRUS",
      };

      /// <summary>
      /// List of Azure-capable TTS speeds.
      /// </summary>
      /// https://docs.microsoft.com/en-us/azure/cognitive-services/speech-service/speech-synthesis-markup?tabs=csharp#supported-ssml-elements
      public List<string> TTSSpeedOptions { get; set; } = new List<string>()
      {
         "x-slow",
         "slow",
         "medium",
         "fast",
         "x-fast",
         "default",
      };

      protected override async Task OnInitializedAsync()
      {
         // Build the connection to the main bot hub.
         mHubConnection = new HubConnectionBuilder()
            .WithUrl(NavigationManager.ToAbsoluteUri("/gifbothub"), HttpTransportType.LongPolling) 
            .WithAutomaticReconnect()
            .Build();

         // Start the connection.
         await mHubConnection.StartAsync();

         // Get a string list of user groups.
         string userGroupsRaw = await mHubConnection.InvokeAsync<string>("GetUserGroupList");
         if (!String.IsNullOrEmpty(userGroupsRaw))
         {
            mUserGroupNames = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(userGroupsRaw);
            mUserGroupNames.Sort();
         }

         // Get the regurgitator packages.
         await FetchRegurgitatorPackages();

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

      private async Task FetchRegurgitatorPackages()
      {
         AvailablePackages.Clear();

         string rawData = await mHubConnection.InvokeAsync<string>("GetRegurgitatorPackages");
         if (!string.IsNullOrEmpty(rawData))
         {
            List<RegurgitatorPackageBase> packages = JsonConvert.DeserializeObject<List<RegurgitatorPackageBase>>(rawData);

            foreach (var package in packages)
            {
               AvailablePackages.Add(package);
            }

            StateHasChanged();
         }
      }

      private async Task PackageSelectionChanged(object selected)
      {
         if (selected is Guid packageId)
         {
            CurrentPackage = packageId;
            mLastKnownArgs = null;
            await FetchRegurgitatorSettings(packageId);

            StateHasChanged();
         }
      }

      private async Task OnPackageSelected(Guid packageId)
      {
         CurrentPackage = packageId;
         mLastKnownArgs = null;
         if (packageId != Guid.Empty)
         {
            await FetchRegurgitatorSettings(packageId);
            await ReadEntries();
         }
         StateHasChanged();
      }

      private string GetPackageName(Guid id)
      {
         if (id == Guid.Empty) return string.Empty;
         var pkg = AvailablePackages?.FirstOrDefault(p => p.Id == id);
         return pkg?.Name ?? string.Empty;
      }

      private async Task AddNewPackage()
      {
         HandleOpenAddPackageDialog();
      }

      private void HandleOpenAddPackageDialog()
      {
         mNewPackageName = string.Empty;
         mIsAddPackageDialogVisible = true;
      }

      private async Task HandleConfirmAddPackage()
      {
         mIsAddPackageDialogVisible = false;
         string packageName = mNewPackageName?.Trim();
         mNewPackageName = string.Empty;

         if (!string.IsNullOrEmpty(packageName))
         {
            Guid createdPackageId = await mHubConnection.InvokeAsync<Guid>("AddRegurgitatorPackage", packageName);
            if (createdPackageId != Guid.Empty)
            {
               await FetchRegurgitatorPackages();
               CurrentPackage = createdPackageId;
               await FetchRegurgitatorSettings(createdPackageId);
               await ReadEntries();
               StateHasChanged();
            }
         }
      }

      private async Task DeletePackage()
      {
         if (CurrentPackage != Guid.Empty)
         { 
            // Find package name for confirmation dialog
            var package = AvailablePackages.FirstOrDefault(p => p.Id == CurrentPackage);
            if (package != null)
            {
               mPendingDeletePackageName = package.Name;
               mIsDeletePackageDialogVisible = true;
            }
         }
      }

      private async Task HandleConfirmDeletePackage()
      {
         mIsDeletePackageDialogVisible = false;
         if (CurrentPackage != Guid.Empty)
         {
            await mHubConnection.InvokeAsync("DeleteRegurgitatorPackage", CurrentPackage);
            await FetchRegurgitatorPackages();
            CurrentPackage = Guid.Empty;
            mPendingDeletePackageName = string.Empty;
            StateHasChanged();
         }
      }

      private async Task FetchRegurgitatorSettings(Guid packageId)
      {
         RegurgitatorSettings regurgitatorData = await mHubConnection.InvokeAsync<RegurgitatorSettings>("GetRegurgitatorSettings", packageId);
         if (regurgitatorData != null)
         {
            mRegurgitatorSettings = regurgitatorData;

            mAccessSelection = (int)regurgitatorData.Access;
            mFormVolume = (int)(regurgitatorData.TTSVolumeSvavaBlount * 100);

            // Map the access user group to the name.
            if (regurgitatorData.Access == AnimationEnums.AccessType.UserGroup)
            {
               mSelectedUserGroupName = await mHubConnection.InvokeAsync<string>("GetUserGroupNameById", regurgitatorData.RestrictedToUserGroup);
            }
         }
      }

      private async Task ReadEntries()
      {
         if (CurrentPackage != Guid.Empty)
         {
            var request = new PagedRequest { Page = 1, PageSize = 1000 };
            var envelope = await mHubConnection.InvokeAsync<DataEnvelope<RegurgitatorEntry>>("GetRegurgitatorEntries", CurrentPackage, request);

            CurrentEntries = envelope.CurrentPageData;
            TotalEntries = envelope.TotalItemCount;

            Console.WriteLine($"ReadEntries(): TotalEntries = {TotalEntries}");

            StateHasChanged();
         }
      }

      /// <summary>
      /// Handle the volume slider changing.
      /// </summary>
      private void OnVolumeChanged(dynamic value)
      {
         if (value is ChangeEventArgs changeEventArgs)
         {
            mRegurgitatorSettings.TTSVolumeSvavaBlount = (double)((double)(Int32.Parse(changeEventArgs.Value.ToString())) / 100);
            StateHasChanged();
         }
      }

      private void OnAccessLevelChange(int? value)
      {
         if (value.HasValue)
         {
            mAccessSelection = value.Value;
            mRegurgitatorSettings.Access = (AnimationEnums.AccessType)value.Value;
         }
      }

      private async Task HandleUserGroupSelected(string groupName)
      {
         if (!String.IsNullOrEmpty(groupName))
         {
            Guid groupId = await mHubConnection.InvokeAsync<Guid>("GetGroupIdByName", groupName);
            mRegurgitatorSettings.RestrictedToUserGroup = groupId;
            mSelectedUserGroupName = groupName;
            StateHasChanged();
         }
      }

      private async Task HandleTestTTSVolume()
      {
         await mHubConnection.InvokeAsync("TestTTSVoice", mRegurgitatorSettings.TTSAzureVoice, mRegurgitatorSettings.TTSVolumeSvavaBlount);
      }

      private async Task OnAddNewEntry()
      {
         if (CurrentPackage != Guid.Empty && !String.IsNullOrEmpty(mNewEntryText))
         {
            // Add this to the server, but instead of requesting all of the data back,
            // just add it to the local copy.
            RegurgitatorEntry newEntry = await mHubConnection.InvokeAsync<RegurgitatorEntry>("AddRegurgitatorEntry", CurrentPackage, mNewEntryText);
            if (newEntry != null)
            {
               mNewEntryText = String.Empty;
               Snackbar.Add("Success - The entry was added.", Severity.Success);
               await ReadEntries();
               await InvokeAsync(() => { StateHasChanged(); });
            }
         }
      }

      private async Task OnDeleteEntry(RegurgitatorEntry entry)
      {
         if (CurrentPackage != Guid.Empty && entry != null && entry.Id != Guid.Empty)
         {
            // Remove this from the server, but instead of requesting all of the data back,
            // just remove it from the local copy.
            await mHubConnection.InvokeAsync("RemoveRegurgitatorEntry", CurrentPackage, entry.Id);
            Snackbar.Add("Success - The entry was removed.", Severity.Success);
            await ReadEntries();
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private async Task OnClearList()
      {
         if (CurrentPackage != Guid.Empty)
         { 
            mIsClearListDialogVisible = true;
         }
      }

      private async Task HandleConfirmClearList()
      {
         mIsClearListDialogVisible = false;
         if (CurrentPackage != Guid.Empty)
         {
            await mHubConnection.InvokeAsync("ClearRegurgitatorEntries", CurrentPackage);
            await ReadEntries();
            StateHasChanged();
         }
      }

      private void OnImportTextFileProgress(EventArgs e)
      {
         if (CurrentPackage != Guid.Empty)
         { 
            // Upload progress tracking not available with InputFile
         }
      }

      private async Task OnImportTextFileComplete(InputFileChangeEventArgs e)
      {
         // Upload completed. Redownload the data and reset upload info.
         mUploadProgress = 0;
         mUploadErrorMessage = String.Empty;
         await ReadEntries();
         StateHasChanged();
      }

      private void OnImportTextFileError(EventArgs e)
      {
         mUploadErrorMessage = $"There was an error uploading the file.";
      }

      private void OnCancel()
      {
         NavigationManager.NavigateTo("/");
      }

      private async Task OnSaveChanges()
      {
         if (CurrentPackage != Guid.Empty)
         { 
            await mHubConnection.InvokeAsync("SetRegurgitatorSettings", CurrentPackage, mRegurgitatorSettings);
            Snackbar.Add("Save Successful - The regurgitator data has been saved.", Severity.Success);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private HubConnection mHubConnection;
      private RegurgitatorSettings mRegurgitatorSettings = new RegurgitatorSettings();
      private object /* was EventArgs */ mLastKnownArgs = null;
      private Guid mSelectedValue = Guid.Empty;
      private string mNewEntryText = String.Empty;
      private int mAccessSelection = 0;
      private int mUploadProgress = 0;
      private int mFormVolume = 50;
      private string mSelectedUserGroupName = String.Empty;
      private string mUploadErrorMessage = String.Empty;
      private List<string> mUserGroupNames = new List<string>();
      private bool mIsDeletePackageDialogVisible = false;
      private string mPendingDeletePackageName = string.Empty;
      private bool mIsClearListDialogVisible = false;
      private bool mIsAddPackageDialogVisible = false;
      private string mNewPackageName = string.Empty;

      private bool IsAddPackageDisabled => string.IsNullOrWhiteSpace(mNewPackageName) ||
         AvailablePackages.Any(p => p.Name.Equals(mNewPackageName.Trim(), StringComparison.OrdinalIgnoreCase));
   }
}
