using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GIFBot.Client.Utility
{
   public class ClientAppData
   {
      #region Setup Data Models

      public string BotOauthToken { get; set; } = String.Empty;
      public string BotRefreshToken { get; set; } = String.Empty;
      public string StreamerOauthToken { get; set; } = String.Empty;
      public string StreamerRefreshToken { get; set; } = String.Empty;

      #endregion
   }
}
