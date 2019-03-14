using System;
using System.Collections.Generic;
using System.Text;

namespace Watsonia.Data.TestDataBuilder.Tests.Entities
{
	public class Employee
	{
		public virtual string Name { get; set; }
		
		public virtual Organisation Organisation { get; set; }
	}
}
