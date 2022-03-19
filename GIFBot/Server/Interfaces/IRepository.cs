using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GIFBot.Server.Interfaces
{
   public interface IRepository<T>
   {
		IQueryable<T> GetQueryableDataSource();

      T GetQueryableEntryById(Guid id);
   }
}
