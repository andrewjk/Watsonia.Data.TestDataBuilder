using Watsonia.Data;
using System;
using System.Collections.Generic;
using System.Text;

namespace Watsonia.Data.TestDataBuilder
{
	public class TestDataConfiguration
	{
		/// <summary>
		/// Gets the folder that contains the data files to process.
		/// </summary>
		/// <value>
		/// The data folder.
		/// </value>
		public virtual string DataFolder { get; set; }

		/// <summary>
		/// Gets whether to map a field in a data file.
		/// </summary>
		/// <param name="entityName">Name of the entity.</param>
		/// <param name="fieldName">Name of the field.</param>
		/// <returns></returns>
		public virtual bool ShouldMapField(string entityName, string fieldName)
		{
			return !fieldName.StartsWith("-");
		}

		/// <summary>
		/// Gets the name of the entity from the name of the file.
		/// </summary>
		/// <param name="fileName">Name of the file.</param>
		/// <returns></returns>
		public virtual string GetEntityName(string fileName)
		{
			return fileName;
		}

		/// <summary>
		/// Gets the name of the field from the file header.
		/// </summary>
		/// <param name="entityName">Name of the entity.</param>
		/// <param name="fieldName">Name of the field.</param>
		/// <returns></returns>
		public virtual string GetFieldName(string entityName, string fieldName)
		{
			return fieldName;
		}

		/// <summary>
		/// Gets the name of the entity.
		/// </summary>
		/// <param name="entityName">Name of the entity.</param>
		/// <param name="fieldName">Name of the field.</param>
		/// <returns></returns>
		public virtual string GetEntityNameForField(string entityName, string fieldName)
		{
			return fieldName;
		}

		/// <summary>
		/// Called before the data import process has started.
		/// </summary>
		/// <param name="db">The database.</param>
		public virtual void OnBeforeImport(Database db)
		{
		}

		/// <summary>
		/// Called when the database has been created.
		/// </summary>
		/// <param name="db">The database.</param>
		public virtual void OnDatabaseCreated(Database db)
		{
		}

		/// <summary>
		/// Called when an entity has been created (but not saved to the database).
		/// </summary>
		/// <param name="db">The database.</param>
		/// <param name="entityName">Name of the entity.</param>
		/// <param name="entity">The entity.</param>
		/// <param name="line">The line.</param>
		public virtual void OnEntityAdded(Database db, string entityName, object entity, TestDataFileLine line)
		{
		}

		/// <summary>
		/// Called when a set of entities has been saved to the database.
		/// </summary>
		/// <param name="db">The database.</param>
		/// <param name="entityName">Name of the entity.</param>
		/// <param name="entities">The entities.</param>
		/// <param name="file">The file.</param>
		public virtual void OnEntitiesSaved(Database db, string entityName, List<object> entities, TestDataFile file)
		{
		}

		/// <summary>
		/// Called after the data import process has finished.
		/// </summary>
		/// <param name="db">The database.</param>
		public virtual void OnAfterImport(Database db)
		{
		}
	}
}
