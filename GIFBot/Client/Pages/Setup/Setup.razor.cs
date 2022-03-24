using GIFBot.Client.Pages.Models;
using GIFBot.Client.Utility;
using GIFBot.Shared;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;

namespace GIFBot.Client.Pages.Setup
{
   public partial class Setup : IAsyncDisposable
   {

      protected override async Task OnInitializedAsync()
      {
         // Get any error strings.
         NavigationManager.TryGetQueryString<string>("err", out string mErrValue);

         // Build the connection to the main bot hub.
         mHubConnection = new HubConnectionBuilder()
            .WithUrl(NavigationManager.ToAbsoluteUri("/gifbothub"), HttpTransportType.LongPolling)
            .WithAutomaticReconnect()
            .Build();

         // Start the connection.
         await mHubConnection.StartAsync();

         // Get the latest settings from the server.
         string botSettingsJson = await mHubConnection.InvokeAsync<string>("GetBotSettings");
         if (!String.IsNullOrEmpty(botSettingsJson))
         {
            mBotSettings = Newtonsoft.Json.JsonConvert.DeserializeObject<BotSettings>(botSettingsJson);
            if (mBotSettings != null)
            {
               BotNameModel.Value = mBotSettings.BotName.Trim();
               ChannelNameModel.Value = mBotSettings.ChannelName.Trim();
            }
         }

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

      #region Form Submission Handlers

      private async Task HandleStartSetup()
      {
         mBotSettings.CurrentSetupStep = SetupStep.BotOauth;
         await mHubConnection.InvokeAsync("UpdateBotSettings", Newtonsoft.Json.JsonConvert.SerializeObject(mBotSettings), false);
         StateHasChanged();
      }

      private async Task HandleSubmitTwitchBotAuth()
      {
         // This next step will take us off site, which means we need to persist any form information collected so far and retrieve it
         // from the server when we circle back.
         await mHubConnection.InvokeAsync("UpdateBotSettings", Newtonsoft.Json.JsonConvert.SerializeObject(mBotSettings), false);

         // Forcibly forward the user to the correct URL for authentication. This is necessary,
         // because Twitch will redirect them and it will be a CORS error otherwise.
         string botAuthUrl = $"https://id.twitch.tv/oauth2/authorize?client_id={Common.skTwitchClientId}&redirect_uri=https://gifbot.azurewebsites.net/twitchoauth&response_type=code&force_verify=true&scope=chat_login chat:edit chat:read whispers:read whispers:edit channel_subscriptions channel:read:redemptions channel:read:hype_train channel:manage:redemptions";
         NavigationManager.NavigateTo(botAuthUrl);
      }

      private async Task HandleSkipTwitchStreamerAuth()
      {
         mBotSettings.CurrentSetupStep = SetupStep.Finished;
         await mHubConnection.InvokeAsync("UpdateBotSettings", Newtonsoft.Json.JsonConvert.SerializeObject(mBotSettings), false);
         StateHasChanged();
      }

      private async Task HandleSubmitTwitchStreamerAuth()
      {
         // This next step will take us off site, which means we need to persist any form information collected so far and retrieve it
         // from the server when we circle back.
         await mHubConnection.InvokeAsync("UpdateBotSettings", Newtonsoft.Json.JsonConvert.SerializeObject(mBotSettings), false);

         // Forcibly forward the user to the correct URL for authentication. This is necessary,
         // because Twitch will redirect them and it will be a CORS error otherwise.
         string streamerAuthUrl = $"https://id.twitch.tv/oauth2/authorize?client_id={Common.skTwitchClientId}&redirect_uri=https://gifbot.azurewebsites.net/twitchoauth&response_type=code&force_verify=true&scope=chat_login chat:edit chat:read whispers:read whispers:edit channel_subscriptions channel:read:redemptions channel:read:hype_train channel:manage:redemptions";
         NavigationManager.NavigateTo(streamerAuthUrl);
      }

      private async Task HandleSkipStreamlabsAuth()
      {
         mBotSettings.CurrentSetupStep = SetupStep.Finished;
         await mHubConnection.InvokeAsync("UpdateBotSettings", Newtonsoft.Json.JsonConvert.SerializeObject(mBotSettings), true);
         StateHasChanged();
      }

      private async Task HandleFinishSettings()
      {
         await SetReauthenticationVersion();
         NavigationManager.NavigateTo("/settings/");
      }

      private async Task HandleAddAnimations()
      {
         await SetReauthenticationVersion();
         NavigationManager.NavigateTo("/animationseditor/");
      }

      private async Task HandleFinishDashboard()
      {
         await SetReauthenticationVersion();
         NavigationManager.NavigateTo("/");
      }

      private async Task SetReauthenticationVersion()
      {
         mBotSettings.BotAuthenticationVersion = BotSettings.skCurrentAuthenticationVersion;
         await mHubConnection.InvokeAsync("UpdateBotSettings", Newtonsoft.Json.JsonConvert.SerializeObject(mBotSettings), true);
         StateHasChanged();
      }

      #endregion

      #region Properties and Private Members

      private HubConnection mHubConnection;

      private BotSettings mBotSettings;
      private StringFormModel BotNameModel = new StringFormModel();
      private StringFormModel ChannelNameModel = new StringFormModel();
      private string mErrValue = String.Empty;

      #endregion
   }
}
