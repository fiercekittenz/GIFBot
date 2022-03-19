using System;
using System.Collections.Generic;
using System.Text;

namespace GIFBot.Shared.Models.Features
{
   public class StickerTreeListItem
   {
      public Guid Id { get; set; } = Guid.Empty;
      public int TreeId { get; set; } = 0;
      public int? ParentTreeId { get; set; } = null;
      public string Name { get; set; } = String.Empty;
      public string Visual { get; set; } = String.Empty;
      public bool Enabled { get; set; } = false;

      public enum ItemType
      {
         Category,
         Entry
      }

      public ItemType Type { get; set; } = StickerTreeListItem.ItemType.Category;
   }
}
