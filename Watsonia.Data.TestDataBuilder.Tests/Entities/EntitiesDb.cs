using System;
using System.Collections.Generic;
using System.Text;

namespace Watsonia.Data.TestDataBuilder.Tests.Entities
{
	public class EntitiesDb : Database
	{
		public EntitiesDb(IDataAccessProvider provider, string connectionString, string entityNamespace)
			: base(provider, connectionString, entityNamespace)
		{
		}
	}
}
