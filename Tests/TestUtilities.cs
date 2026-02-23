using System;
using System.IO;

namespace LocalSaveSystem.Tests
{
	internal static class TestUtilities
	{
		public static string CreateTempPath()
		{
			var path = Path.Combine(Path.GetTempPath(), "LocalSaveSystemTests", Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(path);
			return path;
		}

		public static void Cleanup(string path)
		{
			if (string.IsNullOrWhiteSpace(path))
			{
				return;
			}

			if (Directory.Exists(path))
			{
				Directory.Delete(path, true);
			}
		}
	}
}
