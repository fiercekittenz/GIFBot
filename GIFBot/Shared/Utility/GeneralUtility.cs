using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace GIFBot.Shared.Utility
{
   public class GeneralUtility
   {
      public static string GetAssemblyPath()
      {
         string assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;
         return Path.GetDirectoryName(assemblyLocation);
      }
   }
}
