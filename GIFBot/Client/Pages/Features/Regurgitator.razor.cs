using GIFBot.Shared;
using GIFBot.Shared.Models.Visualization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json;
using Radzen;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Telerik.Blazor;
using Telerik.Blazor.Components;

namespace GIFBot.Client.Pages.Features
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
      /// Dialog factory for Telerik prompts.
      /// </summary>
      [CascadingParameter]
      public DialogFactory Dialogs { get; set; }

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
            mUserGroupNames = JsonSerializer.Deserialize<List<string>>(userGroupsRaw);
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
            List<RegurgitatorPackageBase> packages = JsonSerializer.Deserialize<List<RegurgitatorPackageBase>>(rawData);

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
            mLastKnownGridReadEventArgs = null;
            await FetchRegurgitatorSettings(packageId);

            StateHasChanged();
         }
      }

      private async Task AddNewPackage()
      {
         string packageName = await Dialogs.PromptAsync("Package Name:", "Add New Package");
         if (!string.IsNullOrEmpty(packageName))
         {
            Guid createdPackageId = await mHubConnection.InvokeAsync<Guid>("AddRegurgitatorPackage", packageName);
            if (createdPackageId != Guid.Empty)
            {
               await FetchRegurgitatorPackages();
               CurrentPackage = createdPackageId;
               await PackageSelectionChanged(createdPackageId);
               StateHasChanged();
            }
         }
      }

      private async Task DeletePackage()
      {
         if (CurrentPackage != Guid.Empty)
         { 
            bool confirmed = await Dialogs.ConfirmAsync($"Are you user you want to delete the selected package?", "Delete Package?");
            if (confirmed)
            {
               await mHubConnection.InvokeAsync("DeleteRegurgitatorPackage", CurrentPackage);
               await FetchRegurgitatorPackages();
               CurrentPackage = Guid.Empty;
               StateHasChanged();
            }
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

      private async Task ReadEntries(GridReadEventArgs args)
      {
         if (CurrentPackage != Guid.Empty && args != null)
         {
            mLastKnownGridReadEventArgs = args;

            DataEnvelope<RegurgitatorEntry> dataSourceResult = await mHubConnection.InvokeAsync<DataEnvelope<RegurgitatorEntry>>("GetRegurgitatorEntries", CurrentPackage, args.Request);

            CurrentEntries = dataSourceResult.CurrentPageData;
            TotalEntries = dataSourceResult.TotalItemCount;

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
               NotificationService.Notify(NotificationSeverity.Success, "Success", "The entry was added.", 5000);
               await ReadEntries(mLastKnownGridReadEventArgs);
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
            NotificationService.Notify(NotificationSeverity.Success, "Success", "The entry was removed.", 5000);
            await ReadEntries(mLastKnownGridReadEventArgs);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private async Task OnClearList()
      {
         if (CurrentPackage != Guid.Empty)
         { 
            await mHubConnection.InvokeAsync("ClearRegurgitatorEntries", CurrentPackage);
            await ReadEntries(mLastKnownGridReadEventArgs);
            StateHasChanged();
         }
      }

      private void OnImportTextFileProgress(UploadProgressArgs e)
      {
         if (CurrentPackage != Guid.Empty)
         { 
            mUploadProgress = e.Progress;
         }
      }

      private async Task OnImportTextFileComplete(UploadCompleteEventArgs e)
      {
         // Upload completed. Redownload the data and reset upload info.
         mUploadProgress = 0;
         mUploadErrorMessage = String.Empty;
         await ReadEntries(mLastKnownGridReadEventArgs);
         StateHasChanged();
      }

      private void OnImportTextFileError(Radzen.UploadErrorEventArgs e)
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
            NotificationService.Notify(NotificationSeverity.Success, "Save Successful", "The regurgitator data has been saved.", 5000);
            await InvokeAsync(() => { StateHasChanged(); });
         }
      }

      private HubConnection mHubConnection;
      private RegurgitatorSettings mRegurgitatorSettings = new RegurgitatorSettings();
      private GridReadEventArgs mLastKnownGridReadEventArgs = null;
      private Guid mSelectedValue = Guid.Empty;
      private string mNewEntryText = String.Empty;
      private int mAccessSelection = 0;
      private int mUploadProgress = 0;
      private int mFormVolume = 50;
      private string mSelectedUserGroupName = String.Empty;
      private string mUploadErrorMessage = String.Empty;
      private List<string> mUserGroupNames = new List<string>();
   }
}
