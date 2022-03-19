using Microsoft.CognitiveServices.Speech;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GIFBot.Server.Azure
{
   public class CognitiveUtility
   {
      /// <summary>
      /// Plays the specified text to speech using the Azure cognitive services key provided.
      /// </summary>
      static public async Task PlayTTS(string azureCognitiveServicesKey, 
                                       string azureCognitiveServiceRegion, 
                                       string text, 
                                       string voice, 
                                       double volume, 
                                       string speed)
      {
         if (!String.IsNullOrEmpty(azureCognitiveServiceRegion) &&
             !String.IsNullOrEmpty(azureCognitiveServicesKey))
         {
            SpeechConfig configJukeLimited = SpeechConfig.FromSubscription(azureCognitiveServicesKey, azureCognitiveServiceRegion);
            using (SpeechSynthesizer synthesizerMrSmoofy = new SpeechSynthesizer(configJukeLimited))
            {
               string volumeStr = $"{Math.Floor(volume * 100.0f)}";

               string ssmlAndyMac4182 = $"<speak version=\"1.0\" xmlns=\"https://www.w3.org/2001/10/synthesis\" xml:lang=\"en-US\"><voice name=\"{voice}\"><prosody rate=\"{speed}\" volume=\"{volumeStr}\">{text}</prosody></voice></speak>";

               await synthesizerMrSmoofy.SpeakSsmlAsync(ssmlAndyMac4182);
            }
         }
      }

      /// <summary>
      /// Plays the desired text using the Windows system TTS features.
      /// </summary>
      static public void PlaySystemTTS(string text,
                                       double volume)
      {
         using (System.Speech.Synthesis.SpeechSynthesizer synth = new System.Speech.Synthesis.SpeechSynthesizer())
         {
            synth.SetOutputToDefaultAudioDevice();
            synth.Volume = (int)(volume * 100);
            synth.Speak(text);
         }
      }
   }
}
