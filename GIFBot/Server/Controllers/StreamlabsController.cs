using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GIFBot.Server.Controllers
{
   [Route("streamlabs")]
   public class StreamlabsController : Controller
   {
      public StreamlabsController(GIFBot.GIFBot bot)
      {
         Bot = bot;
      }

      public IActionResult Index()
      {
         return View();
      }

      [HttpGet]
      [Route("maxconnectionattempts")]
      public ActionResult HandleMaxConnectionAttempts()
      {
         if (!String.IsNullOrEmpty(Bot.BotSettings.StreamlabsOauthToken))
         {
            _ = Bot.SendLogMessage("Streamlabs max connection attempts made. Disconnected.");
         }

         return StatusCode(StatusCodes.Status200OK);
      }

      [HttpGet]
      [Route("connected")]
      public ActionResult HandleConnected()
      {
         if (!String.IsNullOrEmpty(Bot.BotSettings.StreamlabsOauthToken))
         {
            _ = Bot.SendLogMessage("Streamlabs connected.");
         }

         return StatusCode(StatusCodes.Status200OK);
      }

      [HttpGet]
      [Route("disconnected")]
      public ActionResult HandleDisconnected()
      {
         if (!String.IsNullOrEmpty(Bot.BotSettings.StreamlabsOauthToken))
         {
            _ = Bot.SendLogMessage("Streamlabs disconnected.");
         }

         return StatusCode(StatusCodes.Status200OK);
      }

      [HttpGet]
      [Route("connectionerror")]
      public ActionResult HandleConnectionError()
      {
         if (!String.IsNullOrEmpty(Bot.BotSettings.StreamlabsOauthToken))
         {
            _ = Bot.SendLogMessage("Streamlabs connection error. Authorization failed.");
         }

         return StatusCode(StatusCodes.Status200OK);
      }

      [HttpGet]
      [Route("tip")]
      public ActionResult HandleTip()
      {
         if (HttpContext.Request.QueryString.HasValue)
         {
            Bot.HandleStreamlabsTip(HttpContext.Request.QueryString.Value);
         }

         return StatusCode(StatusCodes.Status200OK);
      }

      #region Properties

      public GIFBot.GIFBot Bot { get; private set; } = null;

      #endregion
   }
}