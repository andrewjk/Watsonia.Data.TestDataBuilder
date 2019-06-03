using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;
using System.Threading.Tasks;
using Watsonia.Data.SQLite;
using Watsonia.Data.TestDataBuilder.Tests.Entities;

namespace Watsonia.Data.TestDataBuilder.Tests
{
	[TestClass]
	public class CreateData
	{
		[TestMethod]
		public async Task TestCreateData()
		{
			var provider = new SQLiteDataAccessProvider();
			var connectionString = @"Data Source=Data\DB\Entities.sqlite";
			var db = new EntitiesDb(provider, connectionString, "Watsonia.Data.TestDataBuilder.Tests.Entities");
			await TestData.Import(db);

			// Make sure everything's been created
			Assert.AreEqual(2, db.Query<Organisation>().Count());
			Assert.AreEqual(4, db.Query<Employee>().Count());

			// Make sure that employees have the right organisations
			Assert.AreEqual("ABC", db.Query<Employee>().First(e => e.Name == "Ron").Organisation.Code);
			Assert.AreEqual("ABC", db.Query<Employee>().First(e => e.Name == "Darren").Organisation.Code);
			Assert.AreEqual("XYZ", db.Query<Employee>().First(e => e.Name == "Lisa").Organisation.Code);
			Assert.AreEqual("XYZ", db.Query<Employee>().First(e => e.Name == "Marie").Organisation.Code);
		}
	}
}
