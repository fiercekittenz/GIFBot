using GIFBot.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;

namespace GIFBot.Server.Controllers
{
   [Route("remotecontrol")]
   public class RemoteController : Controller
   {
      public RemoteController(GIFBot.GIFBot bot)
      {
         Bot = bot;
      }

      public IActionResult Index()
      {
         return View();
      }

      [HttpGet]
      [HttpOptions]
      [Route("play")]
      public ActionResult Play([FromQuery] string command)
      {
         if (Bot != null && Bot.AnimationManager != null && !String.IsNullOrEmpty(command))
         {
            AnimationData animation = Bot.AnimationManager.GetAnimationByCommand(command);
            if (animation != null)
            {
               Bot.AnimationManager.PriorityQueueAnimation(animation);
            }
         }

         return StatusCode(StatusCodes.Status200OK, "Success");
      }

      public GIFBot.GIFBot Bot { get; private set; }
   }
}
