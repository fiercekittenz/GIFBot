using System;
using System.IO;
using System.Linq;
using GIFBot.Shared;
using GIFBot.Shared.Models.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace GIFBot.Server.Controllers
{
   [Route("upload")]
   public class UploadController : Controller
   {
      public UploadController(GIFBot.GIFBot bot)
      {
         Bot = bot;
      }

      public IActionResult Index()
      {
         return View();
      }

      [HttpPost]
      [Route("regurgitator/{id:Guid}")]
      [DisableRequestSizeLimit]
      public ActionResult RegurgitatorImport(Guid id, IFormFile file)
      {
         try
         {
            if (id != Guid.Empty && Bot.RegurgitatorManager != null && Bot.RegurgitatorManager.Data != null)
            {
               RegurgitatorPackage package = Bot.RegurgitatorManager.Data.Packages.FirstOrDefault(p => p.Id == id);
               if (package != null)
               { 
                  using (var reader = new StreamReader(file.OpenReadStream()))
                  {
                     while (reader.Peek() >= 0)
                     {
                        package.Entries.Add(new Shared.RegurgitatorEntry(reader.ReadLine()));
                     }
                  }

                  Bot.RegurgitatorManager.SaveData();
               }
            }

            return StatusCode(StatusCodes.Status200OK);
         }
         catch (Exception ex)
         {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
         }
      }

      [HttpPost]
      [Route("greeter/{id:Guid}")]
      [DisableRequestSizeLimit]
      public ActionResult GreeterImport(Guid id, IFormFile file)
      {
         try
         {
            if (Bot.GreeterManager != null && Bot.GreeterManager.Data != null)
            {
               using (var reader = new StreamReader(file.OpenReadStream()))
               {
                  GreeterEntry entry = Bot.GreeterManager.Data.Entries.FirstOrDefault(e => e.Id == id);
                  if (entry != null)
                  { 
                     while (reader.Peek() >= 0)
                     {
                        entry.Recipients.Add(new GreetedPersonality(reader.ReadLine()));
                     }
                  }
               }

               Bot.GreeterManager.SaveData();
            }

            return StatusCode(StatusCodes.Status200OK);
         }
         catch (Exception ex)
         {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
         }
      }

      [HttpPost]
      [Route("animation")]
      [DisableRequestSizeLimit]
      public ActionResult AnimationDataImport(IFormFile file)
      {
         try
         {
            string animationFilePath = Path.Combine(AnimationLibrary.GetMediaRootPath(), file.FileName);
            if (System.IO.File.Exists(animationFilePath))
            {
               // Remove the read-only attribute, if it exists.
               FileAttributes attributes = System.IO.File.GetAttributes(animationFilePath);
               if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
               {
                  attributes = (attributes & ~FileAttributes.ReadOnly);
                  System.IO.File.SetAttributes(animationFilePath, attributes);
               }
            }

            using (FileStream fs = System.IO.File.Open(animationFilePath, FileMode.OpenOrCreate))
            {
               file.CopyTo(fs);
            }
            
            return StatusCode(StatusCodes.Status200OK, file.FileName);
         }
         catch (Exception ex)
         {
            return StatusCode(StatusCodes.Status500InternalServerError, ex.Message);
         }
      }

      public GIFBot.GIFBot Bot { get; private set; }
   }
}