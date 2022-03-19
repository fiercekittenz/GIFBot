using System.Threading.Tasks;

namespace GIFBot.Server.Interfaces
{
   public interface IBasicManager
   {
      /// <summary>
      /// Starts the manager's background task. Any features that have background threads will implement this with their threads.
      /// </summary>
      Task Start();

      /// <summary>
      /// Stops the feature background threads.
      /// </summary>
      void Stop();
   }
}
