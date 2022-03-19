using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using GIFBot.Client.Utility;
using Radzen;

namespace GIFBot.Client
{
   public class Program
   {
      public static async Task Main(string[] args)
      {
         var builder = WebAssemblyHostBuilder.CreateDefault(args);
         builder.RootComponents.Add<App>("app");

         builder.Services.AddTransient(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
         builder.Services.AddSingleton<ClientAppData>();

         // Register Scoped Services for Radzen
         builder.Services.AddScoped<NotificationService>();
         builder.Services.AddScoped<DialogService>();

         // Register the Telerik Services
         builder.Services.AddTelerikBlazor();

         await builder.Build().RunAsync();
      }
   }
}
