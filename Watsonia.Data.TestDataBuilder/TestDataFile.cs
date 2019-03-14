using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Watsonia.Data.TestDataBuilder
{
	public sealed class TestDataFile
	{
		public string EntityName { get; set; }
		public List<TestDataFile> Dependencies { get; } = new List<TestDataFile>();

		public List<string> PartNames { get; } = new List<string>();
		public List<int> PartPositions { get; } = new List<int>();

		public ICollection<TestDataFileLine> Lines { get; } = new List<TestDataFileLine>();

		public TestDataFile(string dataFile)
		{
			Load(dataFile);
		}

		private void Load(string dataFile)
		{
			using (var reader = new StreamReader(dataFile))
			{
				// Read the header line and split on word boundaries to get part names and positions
				var header = reader.ReadLine();
				var matches = Regex.Matches(header, @"([\w.-]+)(?![\w.-])");
				foreach (Match m in matches)
				{
					this.PartNames.Add(m.Value);
					this.PartPositions.Add(m.Index);
				}

				// Read each line and split it up
				while (!reader.EndOfStream)
				{
					var dataLine = reader.ReadLine();

					// Ignore whitespace and comments
					if (string.IsNullOrWhiteSpace(dataLine) || dataLine.StartsWith("--"))
					{
						continue;
					}

					if (dataLine.Contains("\t"))
					{
						throw new InvalidOperationException($"Tabs found in {Path.GetFileName(dataFile)}");
					}

					this.Lines.Add(new TestDataFileLine(dataLine, this.PartNames, this.PartPositions));
				}
			}
		}

		public override string ToString()
		{
			return this.EntityName;
		}
	}
}
