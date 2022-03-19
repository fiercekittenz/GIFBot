using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GIFBot.Shared
{
   /// <summary>
   /// The housing of objects related to animations as they are configured for the bot.
   /// </summary>
   public class AnimationLibrary
   {
      /// <summary>
      /// Constructor
      /// </summary>
      public AnimationLibrary()
      {
         Directory.CreateDirectory(AnimationLibrary.GetMediaRootPath());
      }

      /// <summary>
      /// Returns a simplified version of the animation library data for display and transport in blazor.
      /// </summary>
      public IEnumerable<AnimationTreeItem> GetTreeData()
      {
         List<AnimationTreeItem> treeData = new List<AnimationTreeItem>();

         foreach (var category in Categories)
         {
            AnimationTreeItem parent = new AnimationTreeItem(category.Id, category.Title, null, AnimationTreeTier.Category);
            treeData.Add(parent);

            foreach (var animation in category.Animations)
            {
               AnimationTreeItem child = new AnimationTreeItem(animation.Id, 
                                                               animation.Command, 
                                                               category.Id, 
                                                               AnimationTreeTier.Animation);
               child.Disabled = animation.Disabled;

               // Add to the parent as well to the flat tree. The parent animations list is needed for
               // treeview components whereas the flat tree data is needed for a TreeList.
               parent.Animations.Add(child);
               treeData.Add(child);
            }
         }

         return treeData;
      }

      /// <summary>
      /// Determines if a bump in versions is required. Handles converting data as upgrades are made.
      /// </summary>
      public bool BumpVersion()
      {
         // Bit requirements were changed to be flagged as being alerts, not just assuming 0 meant no alert.
         const int kBitAlertSpecification = 2;

         if (Version < kBitAlertSpecification)
         {
            foreach (var category in Categories)
            {
               foreach (var animation in category.Animations)
               {
                  if (animation.BitRequirement > 0)
                  {
                     animation.IsBitAlert = true;
                  }
               }
            }

            Version = BotSettings.skCurrentBotSettingsVersion;
            return true;
         }

         return false;
      }

      /// <summary>
      /// Returns the type of file the animation will be playing. Determines what HTML element needs to be used
      /// for the presentation.
      /// </summary>
      public static AnimationEnums.FileType GetFileTypeOfAnimation(string visualFileName)
      {
         if (!String.IsNullOrEmpty(visualFileName))
         {
            if (visualFileName.EndsWith(".gif") ||
                visualFileName.EndsWith(".png") ||
                visualFileName.EndsWith(".jpg") ||
                visualFileName.EndsWith(".jpeg"))
            {
               return AnimationEnums.FileType.Image;
            }
            else if (visualFileName.EndsWith(".mp4") ||
                     visualFileName.EndsWith(".mov") ||
                     visualFileName.EndsWith(".webm"))
            {
               return AnimationEnums.FileType.Video;
            }
         }

         return AnimationEnums.FileType.Unknown;
      }

      /// <summary>
      /// Super hacky way of getting the wwwroot folder where media is stored. The server thinks its
      /// working directory is itself, without knowledge of where wwwroot is.
      /// </summary>
      /// <returns></returns>
      public static string GetMediaRootPath()
      {
         string currentDirectory = System.Environment.CurrentDirectory.Replace("Server", "Client");
         return Path.Combine(currentDirectory, "wwwroot", AnimationLibrary.kMediaDirectoryName);
      }

      /// <summary>
      /// Determines what the visual file's dimensions should be.
      /// </summary>
      public static Tuple<int, int> GetVisualFileDimensions(string visualFileName)
      {
         string visualFilePath = Path.Combine(AnimationLibrary.GetMediaRootPath(), visualFileName);

         switch (AnimationLibrary.GetFileTypeOfAnimation(visualFileName))
         {
            case AnimationEnums.FileType.Image:
               {
                  System.Drawing.Bitmap image = new System.Drawing.Bitmap(visualFilePath);
                  if (image != null)
                  {
                     int width = image.Width;
                     int height = image.Height;
                     image.Dispose();
                     return new Tuple<int, int>(width, height);
                  }
               }
               break;

            case AnimationEnums.FileType.Video:
               {
                  return new Tuple<int, int>(1920, 1080);               
               }
         }

         return new Tuple<int, int>(0, 0);
      }

      /// <summary>
      /// Denotes the history of changes to the animation library format in case we need to 
      /// perform conversions when the data is loaded.
      /// 
      ///   Version 1: Launch
      ///   Version 2: Added a flag to denote an animation as a bit alert, not just assuming 0 meant no. Allows users to have an "alert" for bits.
      /// </summary>
      public static int skCurrentBotSettingsVersion = 2;

      /// <summary>
      /// The version of this library's data. Used for advancing and converting data when loaded.
      /// </summary>
      public int Version { get; set; } = 1;

      /// <summary>
      /// A list of animations, organized by category.
      /// </summary>
      public List<AnimationCategory> Categories = new List<AnimationCategory>();

      /// <summary>
      /// The name of the folder where media will be uploaded and stored.
      /// </summary>
      private const string kMediaDirectoryName = "media";
   }
}
