using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Watsonia.Data.TestDataBuilder
{
	public sealed class TestDataFileLine
	{
		public Dictionary<string, string> Parts { get; } = new Dictionary<string, string>();

		public TestDataFileLine(string line, List<string> partNames, List<int> partPositions)
		{
			Load(line, partNames, partPositions);
		}

		private void Load(string line, List<string> partNames, List<int> partPositions)
		{
			for (var i = 0; i < partNames.Count; i++)
			{
				var part = partNames[i];
				var startPosition = partPositions[i];
				var endPosition = (i + 1 < partPositions.Count) ? partPositions[i + 1] : line.Length;
				this.Parts.Add(part, SafeSubstring(line, startPosition, endPosition - startPosition).Trim());
			}
		}

		private string SafeSubstring(string text, int start, int length)
		{
			return text.Length <= start ? ""
				: text.Length - start <= length ? text.Substring(start)
				: text.Substring(start, length);
		}
	}
}
