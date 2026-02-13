using System;

namespace GIFBot.Shared.Models.Visualization
{
   public class PagedRequest
   {
      public int Page { get; set; } = 1;
      public int PageSize { get; set; } = 20;
   }
}
