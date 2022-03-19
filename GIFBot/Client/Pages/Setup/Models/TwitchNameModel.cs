using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace GIFBot.Client.Pages.Setup.Models
{
   public class TwitchNameModel
   {
      [Required]
      [StringLength(20, ErrorMessage = "Twitch channel name is too long.")]
      public string Name { get; set; }
   }
}
