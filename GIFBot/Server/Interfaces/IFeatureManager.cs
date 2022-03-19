using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

namespace GIFBot.Server.Interfaces
{
   /// <summary>
   /// Interface that defines the functionality of a GIFBot feature.
   /// </summary>
   public interface IFeatureManager : IBasicManager
   {
      /// <summary>
      /// Loads the feature's data.
      /// </summary>
      void LoadData();

      /// <summary>
      /// Saves the feature's data.
      /// </summary>
      void SaveData();

      /// <summary>
      /// Returns if the feature manager can process the provided twitch message or not.
      /// </summary>
      /// <returns></returns>
      bool CanHandleTwitchMessage(string message, bool isBroadcaster = false);

      /// <summary>
      /// Handles the provided twitch message.
      /// </summary>
      void HandleTwitchMessage(OnMessageReceivedArgs message);
   }
}
