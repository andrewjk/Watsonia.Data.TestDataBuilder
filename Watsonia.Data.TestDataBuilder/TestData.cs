using Watsonia.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Watsonia.Data.TestDataBuilder
{
	public class TestData
	{
		private readonly Database _db;
		private readonly TestDataConfiguration _configuration;
		private readonly List<TestDataFile> _dataFiles = new List<TestDataFile>();

		/// <summary>
		/// Initializes a new instance of the <see cref="TestData"/> class.
		/// </summary>
		/// <param name="db">The database context.</param>
		/// <param name="configuration">The optional configuration to use.</param>t
		private TestData(Database db, TestDataConfiguration configuration)
		{
			_db = db;
			_configuration = configuration;

			// Default to looking in the Data folder of the calling assembly
			if (string.IsNullOrEmpty(_configuration.DataFolder))
			{
				var assemblyFolder = Path.GetDirectoryName(Assembly.GetCallingAssembly().Location);
				_configuration.DataFolder = Path.Combine(assemblyFolder, "Data");
			}
		}

		private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
		private static bool _haveCreated = false;
		private static bool _haveErrored = false;
		private static string _errorMessage = "";

		/// <summary>
		/// Creates data in the supplied database context.
		/// </summary>
		/// <param name="db">The database context.</param>
		/// <param name="dataFolder">The data folder.</param>
		public static async Task Import(Database db, TestDataConfiguration configuration = null)
		{
			await _semaphore.WaitAsync();
			try
			{
				if (_haveCreated)
				{
					// Throw an exception if there has previously been an error so that it shows up against all the user's tests
					if (_haveErrored)
					{
						throw new InvalidOperationException(_errorMessage);
					}
				}
				else
				{
					_haveCreated = true;
					try
					{
						var config = configuration ?? new TestDataConfiguration();

						var creator = new TestData(db, config);

						await config.OnBeforeImportAsync(db);
						config.OnBeforeImport(db);

						creator.LoadDataFiles();
						await creator.CreateDatabaseAsync();
						await creator.CreateDataAsync();

						await config.OnAfterImportAsync(db);
						config.OnAfterImport(db);
					}
					catch (Exception ex)
					{
						_haveErrored = true;
						_errorMessage = ex.ToString();
						throw;
					}
				}
			}
			finally
			{
				_semaphore.Release();
			}
		}

		private void LoadDataFiles()
		{
			var dataFiles = new List<TestDataFile>();

			// Load the data
			foreach (var file in Directory.GetFiles(_configuration.DataFolder, "*.txt"))
			{
				var dataFile = new TestDataFile(file);
				dataFile.EntityName = _configuration.GetEntityName(Path.GetFileNameWithoutExtension(file));
				dataFiles.Add(dataFile);
			}

			// Load the dependent tables for each file (i.e. what must be imported before they can be imported)
			foreach (var file in dataFiles)
			{
				foreach (var part in file.PartNames)
				{
					// TODO: Allow mapping table and column names
					var typeName = "";
					if (part.Contains("."))
					{
						typeName = part.Split('.')[0].TrimStart('-');
					}
					else if (part.EndsWith("ID") || part.EndsWith("Id"))
					{
						var fieldName = _configuration.GetFieldName(file.EntityName, part);
						typeName = _configuration.GetEntityNameForField(file.EntityName, fieldName);
						if (typeName == fieldName)
						{
							typeName = fieldName.Substring(0, fieldName.Length - 2);
						}
						typeName = typeName.TrimStart('-');
					}
					if (!string.IsNullOrEmpty(typeName))
					{
						var dep = dataFiles.FirstOrDefault(f => f.EntityName == typeName);
						if (dep != null)
						{
							file.Dependencies.Add(dep);
						}
					}
				}
			}

			// Sort so that dependent files are processed first
			_dataFiles.AddRange(TopologicalSort(dataFiles, f => f.Dependencies));
		}

		private async Task CreateDatabaseAsync()
		{
			_db.EnsureDatabaseDeleted();
			_db.EnsureDatabaseCreated();

			// NOTE: This could potentially be an option:
			//var tables = new List<string>();
			//tables.AddRange(_tablesToDelete);
			//// TODO: Need to be able to map file name to table name
			//tables.AddRange(_dataFiles.Select(t => t.TypeName).Reverse());

			//foreach (var table in tables)
			//{
			//	var delete = $"DELETE FROM {table}; DBCC CHECKIDENT({table}, reseed, -1);";
			//	_db.Database.ExecuteSqlCommand(delete);
			//}

			await _configuration.OnDatabaseCreatedAsync(_db);
			_configuration.OnDatabaseCreated(_db);
		}

		private async Task CreateDataAsync()
		{
			foreach (var dataFile in _dataFiles)
			{
				await ImportDataAsync(dataFile);
			}
		}

		// From https://stackoverflow.com/questions/4106862/how-to-sort-depended-objects-by-dependency
		private IEnumerable<T> TopologicalSort<T>(IEnumerable<T> nodes, Func<T, IEnumerable<T>> connected)
		{
			var elems = nodes.ToDictionary(node => node, node => new HashSet<T>(connected(node)));
			while (elems.Count > 0)
			{
				var elem = elems.FirstOrDefault(x => x.Value.Count == 0);
				if (elem.Key == null)
				{
					throw new ArgumentException("Cyclic connections are not allowed");
				}
				elems.Remove(elem.Key);
				foreach (var selem in elems)
				{
					selem.Value.Remove(elem.Key);
				}
				yield return elem.Key;
			}
		}

		private async Task ImportDataAsync(TestDataFile dataFile)
		{
			var entityType = GetEntityType(dataFile.EntityName);
			var entities = new List<object>();

			foreach (var line in dataFile.Lines)
			{
				var createMethod = typeof(Database).GetMethod("Create", Type.EmptyTypes).MakeGenericMethod(entityType);
				var entity = createMethod.Invoke(_db, null);

				foreach (var propName in line.Parts.Keys)
				{
					// TODO: Allow specifying primary key name
					// Skip ID because it gets set automatically by the database
					// Howevever, we might like to specify it in the data file so that we can easily refer to what entity has what ID
					if (propName.Equals("ID", StringComparison.InvariantCultureIgnoreCase))
					{
						continue;
					}

					if (!_configuration.ShouldMapField(dataFile.EntityName, propName))
					{
						continue;
					}

					var value = line.Parts[propName].Replace("\\n", Environment.NewLine);

					if (propName.Contains("."))
					{
						// It's a link to another entity, so we have to find that entity
						var entityPropName = propName.Split('.')[0];
						var otherPropName = propName.Split('.')[1];
						SetEntityValue(entityType, entity, entityPropName, otherPropName, value);
					}
					// TODO: Need configuration options:
					else if (propName.EndsWith("ID") || propName.EndsWith("Id"))
					{
						var fieldName = _configuration.GetFieldName(dataFile.EntityName, propName);
						var entityPropName = fieldName.Substring(0, fieldName.Length - 2);
						var otherPropName = fieldName.Substring(fieldName.Length - 2);
						SetEntityValue(entityType, entity, entityPropName, otherPropName, value);
					}
					else
					{
						// It's a property value, so set it
						var fieldName = _configuration.GetFieldName(dataFile.EntityName, propName);
						SetPropertyValue(entityType, entity, fieldName, value);
					}
				}
				entities.Add(entity);

				// Save changes after each entity so that we the IDs are inserted in the same order as the data file
				// That means we can refer to things by their IDs in the data files
				try
				{
					await _db.SaveAsync(entity);
				}
				catch (Exception ex)
				{
					throw new Exception($"Error adding {entityType}", ex);
				}

				await _configuration.OnEntityAddedAsync(_db, dataFile.EntityName, entity, line);
				_configuration.OnEntityAdded(_db, dataFile.EntityName, entity, line);

				// Save changes again, just in case the user did something in _configuration.OnEntityAdded, above
				try
				{
					await _db.SaveAsync(entity);
				}
				catch (Exception ex)
				{
					throw new Exception($"Error saving {entityType}", ex);
				}
			}

			await _configuration.OnEntitiesSavedAsync(_db, dataFile.EntityName, entities, dataFile);
			_configuration.OnEntitiesSaved(_db, dataFile.EntityName, entities, dataFile);
		}

		private Type GetEntityType(string name)
		{
			var typeName = _db.Configuration.EntityNamespace + "." + name;
			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				Type[] types;
				try
				{
					types = assembly.GetTypes();
				}
				catch (ReflectionTypeLoadException ex)
				{
					types = ex.Types;
				}

				var type = types.FirstOrDefault(t => t != null && t.FullName == typeName);
				if (type != null)
				{
					return type;
				}
			}

			throw new InvalidOperationException($"Entity type not found: {name}");
		}

		private void SetEntityValue(Type entityType, object entity, string entityPropName, string otherPropName, string value)
		{
			var prop = entityType.GetProperty(entityPropName);
			if (prop == null)
			{
				throw new InvalidOperationException($"Property not found: {entityType.Name}.{entityPropName}");
			}

			if (value.Equals("null", StringComparison.InvariantCultureIgnoreCase))
			{
				// Easy one, just set to null
				prop.SetValue(entity, null);
				return;
			}

			var otherProp = prop.PropertyType.GetProperty(otherPropName);
			if (otherProp == null)
			{
				throw new InvalidOperationException($"Property not found: {prop.PropertyType.Name}.{otherPropName}");
			}

			var propValue = ConvertValue(otherProp, value);

			// NOTE: We should probably change this to storing everything that's added in a dictionary of entities,
			// along with the values for each. That way you could add e.g. -ID to your data files and it won't get
			// set in the database, but you would be able to use it in other data files to search for that item...

			// Get the IQueryable<entityType> by calling _db.Query<entityType>()
			// E.g. var query = _db.Query<Customer>()
			var queryMethod = _db.GetType().GetMethod("Query").MakeGenericMethod(prop.PropertyType);
			var query = queryMethod.Invoke(_db, null);

			// Create a condition to run against the IQueryable<entityType> with SingleOrDefault
			// E.g. (c => c.Code == "ABC")
			var param = Expression.Parameter(prop.PropertyType);
			var condition =
				Expression.Lambda(
					Expression.Equal(
						Expression.Property(param, otherPropName),
						Expression.Constant(propValue, otherProp.PropertyType)
					),
					param
				).Compile();

			// Get the other entity by calling running SingleOrDefault with the condition we created above
			// E.g. var otherEntity = query.SingleOrDefault(c => c.Code == "ABC")
			var singleMethod = typeof(Enumerable)
				.GetMethods(BindingFlags.Static | BindingFlags.Public)
				.First(mi =>
					mi.Name == "SingleOrDefault" &&
					mi.GetParameters().Count() == 2 &&
					mi.GetParameters()[1].ParameterType.GetGenericArguments().Count() == 2)
				.MakeGenericMethod(prop.PropertyType);

			var otherEntity = singleMethod.Invoke(query, new object[] { query, condition });
			if (otherEntity == null)
			{
				throw new InvalidOperationException($"Entity not found: {prop.PropertyType.Name}.{otherPropName} = {value}");
			}

			prop.SetValue(entity, otherEntity);
		}

		private void SetPropertyValue(Type entityType, object entity, string propName, string value)
		{
			var prop = entityType.GetProperty(propName);
			if (prop == null)
			{
				throw new InvalidOperationException($"Property not found: {entityType.Name}.{propName}");
			}

			try
			{
				var propValue = ConvertValue(prop, value);
				prop.SetValue(entity, propValue);
			}
			catch
			{
				throw new InvalidOperationException($"Could not convert value for {entityType.Name}.{propName}: {value}");
			}
		}

		private object ConvertValue(PropertyInfo prop, string value)
		{
			// NOTE: Probably other conversions required
			if (value.Equals("null", StringComparison.InvariantCultureIgnoreCase))
			{
				return null;
			}
			else if (prop.PropertyType.IsEnum)
			{
				return Enum.Parse(prop.PropertyType, value);
			}
			else if (prop.PropertyType == typeof(Guid))
			{
				return Guid.Parse(value);
			}
			else
			{
				// Handle nullable types
				var propType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
				return value.Equals("null", StringComparison.InvariantCultureIgnoreCase) ? null : Convert.ChangeType(value, propType);
			}
		}
	}
}
