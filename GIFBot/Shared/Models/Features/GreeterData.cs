﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GIFBot.Shared.Models.Features
{
   public class GreeterData
   {
      // For S.T.J.
      public GreeterData() { }

      public List<GreeterEntry> Entries { get; set; } = new List<GreeterEntry>();
   }
}
