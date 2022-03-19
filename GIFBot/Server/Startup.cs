using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;
using GIFBot.Server.GIFBot;
using GIFBot.Server.Hubs;
using System.Diagnostics;
using Microsoft.Net.Http.Headers;
using GIFBot.Server.Features.Regurgitator;
using System;
using System.Net;

namespace GIFBot.Server
{
   public class Startup
   {
      public Startup(IConfiguration configuration)
      {
         Configuration = configuration;
      }

      public IConfiguration Configuration { get; }

      public void ConfigureServices(IServiceCollection services)
      {
         services.AddCors(options =>
         {
            options.AddPolicy(name: "AllowedOrigins",
            builder =>
            {
               builder
               .AllowAnyOrigin()
               .WithMethods("GET")
               .AllowAnyHeader();
            });
         });

         services.AddSingleton<GIFBot.GIFBot>();
         services.AddHostedService<GIFBotService>();
         services.AddControllersWithViews();
         services.AddRazorPages().AddRazorRuntimeCompilation();
         services.AddSignalR();

         services.AddResponseCompression(opts =>
         {
            opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
                   new[] { "application/octet-stream", "application/json" });
         });
      }

      public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
      {
         if (env.IsDevelopment())
         {
            app.UseDeveloperExceptionPage();
            app.UseWebAssemblyDebugging();
         }
         else
         {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
         }

         //app.UseHttpsRedirection(); // Turn off SSL for now.
         app.UseBlazorFrameworkFiles();
         app.UseStaticFiles(new StaticFileOptions
         {
            OnPrepareResponse = context =>
            {
               context.Context.Response.Headers.Add("Cache-Control", "no-cache, no-store, must-revalidate, max-age=0");
               context.Context.Response.Headers.Add("Expires", "-1");
            }
         });

         app.UseRouting();

         app.UseCors("AllowedOrigins");

         app.UseEndpoints(endpoints =>
         {
            endpoints.MapRazorPages();
            endpoints.MapControllers();
            endpoints.MapFallbackToFile("index.html");
            endpoints.MapHub<GIFBotHub>("/gifbothub");
         });

         if (!env.IsDevelopment())
         {
            // This is coming from a non-dev user. Open up the browser to the site.
            // Make sure to append a random value so users don't have to shift+refresh. It tricks the browser.
            var ps = new ProcessStartInfo($"http://localhost:5000?{System.DateTimeOffset.UtcNow.ToUnixTimeSeconds()}")
            {
               UseShellExecute = true,
               Verb = "open"
            };
            Process.Start(ps);
         }
      }

      public string GetIPAddress()
      {
         IPHostEntry host = default(IPHostEntry);
         string hostname = null;
         hostname = System.Environment.MachineName;
         host = Dns.GetHostEntry(hostname);
         foreach (IPAddress ip in host.AddressList)
         {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
               return Convert.ToString(ip);
            }
         }

         return String.Empty;
      }
   }
}
