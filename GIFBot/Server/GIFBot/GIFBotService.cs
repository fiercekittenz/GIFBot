using GIFBot.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GIFBot.Server.GIFBot
{
   public class GIFBotService : IHostedService
   {
      #region Public Methods

      public GIFBotService(IConfiguration configuration, IHubContext<GIFBotHub> hub, GIFBot bot)
      {
         Configuration = configuration;
         Hub = hub;
         Bot = bot;
      }

      public Task StartAsync(CancellationToken cancellationToken)
      {
         return Task.CompletedTask;
      }

      public Task StopAsync(CancellationToken cancellationToken)
      {
         return Task.CompletedTask;
      }

      #endregion

      #region Properties

      public IConfiguration Configuration { get; private set; }

      public IHubContext<GIFBotHub> Hub { get; private set; }

      public GIFBot Bot { get; private set; }

      #endregion
   }
}
