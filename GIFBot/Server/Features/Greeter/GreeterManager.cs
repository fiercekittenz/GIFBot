using GIFBot.Server.Interfaces;
using GIFBot.Shared;
using GIFBot.Shared.Models.Features;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TwitchLib.Client.Events;

namespace GIFBot.Server.Features.Greeter
{
   public class GreeterManager : IFeatureManager
   {
      #region Public Methods

      /// <summary>
      /// Constructor
      /// </summary>
      public GreeterManager(GIFBot.GIFBot bot, string dataFilePath)
      {
         Bot = bot;
         DataFilePath = dataFilePath;
      }

      #endregion

      #region Properties

      public GIFBot.GIFBot Bot { get; private set; }

      public string DataFilePath { get; private set; }

      public GreeterData Data
      {
         get { return mData; }
         set { mData = value; }
      }

      #endregion

      #region IFeatureManager Implementation 

      public bool CanHandleTwitchMessage(string message, bool isBroadcaster = false)
      {
         return true;
      }

      public void HandleTwitchMessage(OnMessageReceivedArgs message)
      {
         bool needsSave = false;

         var validEntries = mData.Entries.Where(e => e.Recipients.Any(r => r.Name.Equals(message.ChatMessage.DisplayName, StringComparison.OrdinalIgnoreCase)));
         foreach (var entry in validEntries)
         {
            AnimationData animation = Bot.AnimationManager.GetAnimationById(entry.AnimationId);
            GreetedPersonality person = entry.Recipients.Where(r => r.Name.Equals(message.ChatMessage.DisplayName)).FirstOrDefault();
            if (entry.Enabled &&
                animation != null &&
                person != null &&
                DateTime.Now.Subtract(person.LastGreeted).TotalSeconds > skTimeBetweenGreets)
            {
               if (!String.IsNullOrEmpty(entry.ChatMessage))
               {
                  Bot.SendChatMessage(entry.ChatMessage.Replace("$user", message.ChatMessage.DisplayName));
               }

               Bot.AnimationManager.ForceQueueAnimation(animation, message.ChatMessage.DisplayName, String.Empty);
               person.LastGreeted = DateTime.Now;
               needsSave = true;
            }
         }

         if (needsSave)
         {
            SaveData();
         }
      }

      public void LoadData()
      {
         if (!String.IsNullOrEmpty(DataFilePath) && File.Exists(DataFilePath))
         {
            string fileData = File.ReadAllText(DataFilePath);
            mData = JsonConvert.DeserializeObject<GreeterData>(fileData);

            _ = Bot?.SendLogMessage("Greeter data loaded and enabled.");
         }
      }

      public void SaveData()
      {
         if (mData != null)
         {
            Directory.CreateDirectory(Path.GetDirectoryName(DataFilePath));

            var jsonData = JsonConvert.SerializeObject(mData);
            File.WriteAllText(DataFilePath, jsonData);

            _ = Bot?.SendLogMessage("Greeter data saved.");
         }
      }

      public Task Start()
      {
         return Task.CompletedTask;
      }

      public void Stop()
      {
      }

      #endregion

      #region Private Data

      public const string kFileName = "gifbot_greeter.json";

      private static int skTimeBetweenGreets = 7200; // seconds (2 hours)

      private GreeterData mData = new GreeterData();

      #endregion
   }
}
