using GIFBot.Shared.Models.Visualization;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GIFBot.Shared.Models.Features
{
   public class GoalBarData
   {
      public GoalBarSettings Settings { get; set; } = new GoalBarSettings();
      public List<GoalData> Goals { get; set; } = new List<GoalData>();
   }

   public class GoalBarSettings
   {
      public bool Enabled { get; set; } = false;

      public bool IncludeCheers { get; set; } = false;
      public double BitPoints { get; set; } = 0.01;

      public bool IncludeTips { get; set; } = false;
      public double TipPoints { get; set; } = 1;

      public bool IncludeSubs { get; set; } = false;
      public double SubTier1Points { get; set; } = 2.5;
      public double SubTier2Points { get; set; } = 5.0;
      public double SubTier3Points { get; set; } = 10.0;

      public CaptionData Caption { get; set; } = new CaptionData();

      public string BarForecolor { get; set; } = "#FFFFFF";

      public string BarBackcolor { get; set; } = "#333333";

      public bool HideGoalText { get; set; } = false;

      public bool ShowAmountAsPercentage { get; set; } = true;

      public string CurrencySymbol { get; set; } = "$";

      public int Width { get; set; } = 1080;
   }

   public class GoalData : ICloneable
   {
      public GoalData()
      {
         Id = Guid.NewGuid();
      }

      public object Clone()
      {
         return MemberwiseClone();
      }

      public Guid Id { get; set; }

      public bool IsActive { get; set; } = false;

      [Required(ErrorMessage = "A goal must have a title.")]
      public string Title { get; set; } = String.Empty;

      [Range(1, 1000000)]
      public double GoalAmount { get; set; } = 100.0;

      [Range(0, 1000000)]
      public double CurrentAmount { get; set; } = 0.0;

      public List<GoalMilestone> Milestones { get; set; } = new List<GoalMilestone>();

      public bool ResetWhenComplete { get; set; } = false;
   }

   public class GoalMilestone
   {
      [Required]
      [Range(1, 100)]
      public int PercentageTriggerPoint { get; set; } = 100;

      [Required(ErrorMessage = "You must provide an animation for the milestone.")]
      public string AnimationCommand { get; set; } = String.Empty;

      public bool HasTriggered { get; set; } = false;
   }
}
