using System;
using System.Security.Cryptography;
using System.Text;

namespace LocalSaveSystem
{
internal static class SaveFileNaming
{
	public static string GetFileName(string key, string extension)
	{
		var hash = ComputeHash(key);
		return string.Concat(hash, extension);
	}

	private static string ComputeHash(string input)
	{
		using var sha = SHA256.Create();
		var bytes = Encoding.UTF8.GetBytes(input ?? string.Empty);
		var hashBytes = sha.ComputeHash(bytes);
		var builder = new StringBuilder(hashBytes.Length * 2);
		for (var i = 0; i < hashBytes.Length; i++)
		{
			builder.Append(hashBytes[i].ToString("x2"));
		}

		return builder.ToString();
	}
}
}
