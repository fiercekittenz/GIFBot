using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GIFBot.Shared;
using GIFBot.Shared.Models.Animation;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using static GIFBot.Shared.AnimationEnums;

namespace GIFBot.Server.Controllers
{
   [Route("utility")]
   public class UtilityController : Controller
   {
      public UtilityController(GIFBot.GIFBot bot)
      {
         Bot = bot;
      }

      public IActionResult Index()
      {
         return View();
      }

      [HttpGet]
      [HttpOptions]
      [EnableCors("AllowedOrigins")]
      [Route("bitcollisioncheck")]
      public ActionResult BitCollisionCheck([FromQuery] string animationid, [FromQuery] string bits)
      {
         if (Bot != null && Guid.TryParse(animationid, out Guid id))
         {
            if (Int32.TryParse(bits, out int bitamount))
            {
               var allAnimations = Bot.AnimationManager.GetAllAnimations();
               foreach (var animation in allAnimations)
               {
                  if (animation.Id != id && animation.BitRequirement != 0 && animation.BitRequirement == bitamount)
                  {
                     return StatusCode(StatusCodes.Status200OK, "true");
                  }
               }

               return StatusCode(StatusCodes.Status200OK, "false");
            }
         }

         // By default, return that there is a collision.
         return StatusCode(StatusCodes.Status200OK, "true");
      }

      [HttpGet]
      [HttpOptions]
      [EnableCors("AllowedOrigins")]
      [Route("channelpointcollisioncheck")]
      public ActionResult ChannelPointCollisionCheck([FromQuery] string animationid, [FromQuery] string channelpoints)
      {
         if (Bot != null && Guid.TryParse(animationid, out Guid id))
         {
            if (Int32.TryParse(channelpoints, out int channelpointamount))
            {
               var allAnimations = Bot.AnimationManager.GetAllAnimations();
               foreach (var animation in allAnimations)
               {
                  if (animation.Id != id && 
                      animation.ChannelPointRedemptionType == AnimationEnums.ChannelPointRedemptionTriggerType.PointsUsed && 
                      animation.ChannelPointsRequired != 0 && 
                      animation.ChannelPointsRequired == channelpointamount)
                  {
                     return StatusCode(StatusCodes.Status200OK, "true");
                  }
               }

               return StatusCode(StatusCodes.Status200OK, "false");
            }
         }

         // By default, return that there is a collision.
         return StatusCode(StatusCodes.Status200OK, "true");
      }

      [HttpGet]
      [HttpOptions]
      [EnableCors("AllowedOrigins")]
      [Route("updatevisual")]
      public async Task<ActionResult> UpdateVisual([FromQuery] int visualType, [FromQuery] string top, [FromQuery] string left, [FromQuery] double scalar, [FromQuery] bool updateclient)
      {
         if (Bot != null && scalar > 0)
         {
            int topValue = 0;
            double topNum = 0;
            if (!String.IsNullOrEmpty(top))
            {
               if (Double.TryParse(top.Replace("px", String.Empty), out topNum))
               {
                  topValue = (int)(topNum / scalar);
               }
            }

            int leftValue = 0;
            double leftNum = 0;
            if (!String.IsNullOrEmpty(left))
            {
               if (Double.TryParse(left.Replace("px", String.Empty), out leftNum))
               {
                  leftValue = (int)(leftNum / scalar);
               }
            }

            UpdateVisualType targetSystem = (UpdateVisualType)visualType;
            switch (targetSystem)
            {
            case UpdateVisualType.Animation:
               {
                  if (Bot.AnimationManager != null && Bot.AnimationManager.IsTestDisplayModeOn)
                  {
                     // TODO: Support a secondary layer for animations.
                     await Bot.AnimationManager.UpdateDisplayTestLocation(topValue, leftValue, AnimationLayer.Primary, Bot.GIFBotHub.Clients);
                  }
               }
               break;

            case UpdateVisualType.Sticker:
               {
                  if (Bot.StickersManager != null && Bot.StickersManager.IsTestDisplayModeOn)
                  {
                     IHubClients clients = Bot.GIFBotHub.Clients;
                     await Bot.StickersManager.UpdateDisplayTestLocation(topValue, leftValue, Bot.StickersManager.CurrentTestLayer, clients);
                  }
               }
               break;
            }

            if (updateclient)
            {
               await Bot.GIFBotHub.Clients.All.SendAsync("UpdatePosition", topValue, leftValue);
            }
         }

         // By default, return that there is a collision.
         return StatusCode(StatusCodes.Status200OK);
      }

      public GIFBot.GIFBot Bot { get; private set; }
   }
}
