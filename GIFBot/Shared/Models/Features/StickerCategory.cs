using System;
using System.Collections.Generic;
using System.Text;

namespace GIFBot.Shared.Models.Features
{
   public class StickerCategory
   {
      /// <summary>
      /// Default Constructor
      /// </summary>
      public StickerCategory()
         : this(String.Empty)
      {
      }

      /// <summary>
      /// Constructor
      /// </summary>
      public StickerCategory(string name)
      {
         Id = Guid.NewGuid();
         Name = name;
      }

      /// <summary>
      /// Unique ID
      /// </summary>
      public Guid Id { get; set; }

      /// <summary>
      /// Name of the category for this sticker set.
      /// </summary>
      public string Name { get; set; } = string.Empty;

      /// <summary>
      /// List of sticker entry data tied to this category.
      /// </summary>
      public List<StickerEntryData> Entries { get; set; } = new List<StickerEntryData>();
   }
}
