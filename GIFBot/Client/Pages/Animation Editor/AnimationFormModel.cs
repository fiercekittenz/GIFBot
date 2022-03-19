using GIFBot.Shared;
using GIFBot.Shared.Models.Animation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace GIFBot.Client.Pages.Animation_Editor
{
   /// <summary>
   /// Large scale form model used for when editing an animation in its entirety.
   /// </summary>
   public class AnimationFormModel
   {
      // Primarily used by the tutorial, where we don't add the category and get back a Guid.
      public string CategoryTitle { get; set; } = String.Empty;

      [Required]
      [StringLength(20, ErrorMessage = "Command is too long.")]
      public string Command { get; set; } = String.Empty;

      public bool Disabled { get; set; } = true;

      public bool HideFromChatOutput { get; set; } = false;

      public string Visual { get; set; } = String.Empty;

      public string Audio { get; set; } = String.Empty;

      public double Volume { get; set; } = 0.5;

      public AnimationPlacement AnimationPlacement { get; set; } = new AnimationPlacement();

      public int DurationMilliseconds { get; set; } = 2000;

      public int MainCooldownMinutes { get; set; } = 5;

      public AnimationEnums.AccessType Access { get; set; } = AnimationEnums.AccessType.Anyone;
   }
}
