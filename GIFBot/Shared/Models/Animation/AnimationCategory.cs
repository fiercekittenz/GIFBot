using System;
using System.Collections.Generic;
using System.Text;

namespace GIFBot.Shared
{
   public class AnimationCategory
   {
      /// <summary>
      /// Default Constructor
      /// </summary>
      public AnimationCategory()
         : this(String.Empty)
      {
      }

      /// <summary>
      /// Constructor
      /// </summary>
      /// <param name="title"></param>
      public AnimationCategory(string title)
      {
         Id = Guid.NewGuid();
         Title = title;
      }

      public Guid Id { get; set; }

      /// <summary>
      /// Title of the category for this Animation set.
      /// </summary>
      public string Title { get; set; } = string.Empty;

      /// <summary>
      /// List of animation data tied to this category.
      /// </summary>
      public List<AnimationData> Animations { get; set; } = new List<AnimationData>();
   }
}
