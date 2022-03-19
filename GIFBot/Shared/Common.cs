using System;
using System.Collections.Generic;
using System.Text;

namespace GIFBot.Shared
{
   /// <summary>
   /// Class with statically accessible variables that are commonly used throughout the code.
   /// </summary>
   public class Common
   {
      public const string skTwitchClientId = "4nobmh8xn5ufrkjgmozi0hvg1gv5kx";

      // A random number generator.
      public static Random sRandom = new Random(System.Environment.TickCount);
   }
}