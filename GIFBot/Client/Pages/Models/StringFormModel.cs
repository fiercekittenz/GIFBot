using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace GIFBot.Client.Pages.Models
{
   public class StringFormModel
   {
      [Required]
      [StringLength(20, ErrorMessage = "Value is too long.")]
      public string Value { get; set; }
   }
}
