using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GIFBot.Server.Controllers
{
   [Route("ping")]
   public class PingController : Controller
   {
      public IActionResult Index()
      {
         return View();
      }

      [HttpGet]
      [HttpOptions]
      [EnableCors("AllowedOrigins")]
      [Route("pong")]
      public ActionResult Pong()
      {
         return StatusCode(StatusCodes.Status200OK, "PONG");
      }
   }
}