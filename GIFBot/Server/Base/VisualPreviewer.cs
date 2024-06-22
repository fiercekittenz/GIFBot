using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GIFBot.Server.GIFBot;
using GIFBot.Shared;
using GIFBot.Shared.Models.Animation;
using GIFBot.Shared.Models.Base;
using GIFBot.Shared.Models.Visualization;
using Microsoft.AspNetCore.SignalR;
using System.Text.Json;

namespace GIFBot.Server.Base
{
   public class VisualPreviewer
   {
      /// <summary>
      /// Sets the display test mode.
      /// </summary>
      public async Task SetDisplayTestMode(bool isDisplayTestModeOn, VisualBase visual, AnimationEnums.AnimationLayer layer, IHubClients clients)
      {
         if (mIsDisplayTestModeOn != isDisplayTestModeOn)
         {
            mIsDisplayTestModeOn = isDisplayTestModeOn;
            mLayer = layer;

            if (mIsDisplayTestModeOn)
            {
               // Get the visual and set it up on the front-end. Always use the primary animations hub, as it's required.
               mDisplayTestVisual = visual;
               mDisplayTestVisualPlacement = new AnimationPlacement(mDisplayTestVisual.Placement);
               await clients.All.SendAsync("UpdateTestVisual", JsonSerializer.Serialize(new TestVisualRequest(mDisplayTestVisual.Visual, visual.IsMirrored, mDisplayTestVisualPlacement, layer)));
            }
            else
            {
               // Null out the visual and signal to the front-end to get rid of the test visual display.
               mDisplayTestVisual = null;
               await clients.All.SendAsync("StopTestVisual");
            }
         }
      }

      /// <summary>
      /// Updates the live test display location in the scene.
      /// </summary>
      public async Task UpdateDisplayTestLocation(int top, int left, AnimationEnums.AnimationLayer layer, IHubClients clients)
      {
         if (mIsDisplayTestModeOn && mDisplayTestVisual != null)
         {
            mDisplayTestVisualPlacement.Top = top;
            mDisplayTestVisualPlacement.Left = left;

            await clients.All.SendAsync("UpdateTestVisual", JsonSerializer.Serialize(new TestVisualRequest(mDisplayTestVisual.Visual, mDisplayTestVisual.IsMirrored, mDisplayTestVisualPlacement, layer)));
         }
      }

      /// <summary>
      /// Updates the live test display width and height.
      /// </summary>
      public async Task UpdateDisplayTestDimensions(int width, int height, AnimationEnums.AnimationLayer layer, IHubClients clients)
      {
         if (mIsDisplayTestModeOn && mDisplayTestVisual != null)
         {
            mDisplayTestVisualPlacement.Width = width;
            mDisplayTestVisualPlacement.Height = height;

            await clients.All.SendAsync("UpdateTestVisual", JsonSerializer.Serialize(new TestVisualRequest(mDisplayTestVisual.Visual, mDisplayTestVisual.IsMirrored, mDisplayTestVisualPlacement, layer)));
         }
      }

      #region Properties

      /// <summary>
      /// Returns if the test display mode is on.
      /// </summary>
      public bool IsTestDisplayModeOn { get { return mIsDisplayTestModeOn; } }

      /// <summary>
      /// Returns the current working layer for the test visual, if applicable.
      /// </summary>
      public AnimationEnums.AnimationLayer CurrentTestLayer
      {
         get { return mLayer; }
      }

      #endregion

      #region Protected Members

      /// <summary>
      /// Test display mode members.
      /// </summary>
      protected VisualBase mDisplayTestVisual = null;
      protected AnimationPlacement mDisplayTestVisualPlacement = new AnimationPlacement();
      protected AnimationEnums.AnimationLayer mLayer = AnimationEnums.AnimationLayer.Primary;
      protected bool mIsDisplayTestModeOn = false;
      protected Guid mTestAnimationId = Guid.Empty;

      #endregion
   }
}