using StreamDeckLib;
using StreamDeckLib.Messages;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace GIFBotStreamDeckPlugin
{
   [ActionUuid(Uuid = "com.fiercekittenz.gifbot.playanimation")]
   public class PlayAnimationAction : BaseStreamDeckActionWithSettingsModel<Models.PlayAnimationSettingsModel>
   {
      public override async Task OnKeyUp(StreamDeckEventPayload args)
      {
         string url = $"http://localhost:5000/remotecontrol/play?command={SettingsModel.AnimationCommand}";

         try
         {
            HttpWebRequest outboundRequest = (HttpWebRequest)WebRequest.Create(url);
            if (outboundRequest != null)
            {
               outboundRequest.Method = "GET";
               outboundRequest.Timeout = 12000;
               outboundRequest.ContentType = "application/json";

               using (HttpWebResponse inboundResponse = (HttpWebResponse)outboundRequest.GetResponse())
               {
                  if (inboundResponse.StatusCode == HttpStatusCode.OK)
                  {
                     await Task.CompletedTask;
                  }
               }
            }
         }
         catch (Exception /*e*/)
         {
            // This exception occurs even on a valid response with a 404. It's the 404 we are looking for here.
            // Do not log, because it is misleading.
         }
      }

      public override async Task OnDidReceiveSettings(StreamDeckEventPayload args)
      {
         await base.OnDidReceiveSettings(args);
      }

      public override async Task OnWillAppear(StreamDeckEventPayload args)
      {
         await base.OnWillAppear(args);
      }
   }
}
