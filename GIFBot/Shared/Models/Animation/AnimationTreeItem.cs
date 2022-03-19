using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace GIFBot.Shared
{
   /// <summary>
   /// Represents an Animation's tree view item.
   /// </summary>
   public class AnimationTreeItem
   {
      public AnimationTreeItem(Guid id, string title, Guid? parentTreeId, AnimationTreeTier tier)
      {
         Id = id;
         Title = title;
         ParentTreeId = parentTreeId;
         Tier = tier;
      }

      public Guid Id { get; set; } = Guid.Empty;

      public Guid? ParentTreeId { get; set; } = null;

      public string Title { get; set; } = String.Empty;

      public AnimationTreeTier Tier { get; set; } = AnimationTreeTier.Category;

      // Only used for a filtered treeview. This is not used by the Telerik TreeList component.
      public ObservableCollection<AnimationTreeItem> Animations { get; set; } = new ObservableCollection<AnimationTreeItem>();

      #region Animation Specific View Model Properties

      public bool Disabled { get; set; } = false;

      #endregion
   }

   public enum AnimationTreeTier
   {
      Category,
      Animation
   }
}
