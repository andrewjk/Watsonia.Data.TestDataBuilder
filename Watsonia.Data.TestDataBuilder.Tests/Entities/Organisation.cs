using System;
using System.Collections.Generic;
using System.Text;

namespace Watsonia.Data.TestDataBuilder.Tests.Entities
{
	public class Organisation
	{
		public virtual string Code { get; set; }

		public virtual string Name { get; set; }

		public virtual ICollection<Employee> Employees { get; } = new List<Employee>();
	}
}
